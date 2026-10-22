using FSIdentity.Domain.Data;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Interfaces;
using FSIdentity.Domain.Interfaces.MultiTenant;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Domain.Crud;

public class CrudService<TEntity, TRead, TCreate, TUpdate>(
    IUnitOfWork uow,
    IEnumerable<ICrudRules<TEntity, TCreate, TUpdate>> rules,
    IMultiTenantAccessor? tenant = null,
    IEnumerable<IPostCreateHook<TEntity>>? hooks = null)
    : ICrudService<TEntity, TRead, TCreate, TUpdate>
    where TEntity : class, IBaseEntityRoot, new()
{
    public const int MaxPageSize = 500;

    private static readonly CrudMap<TEntity, TRead, TCreate, TUpdate> Map = CrudMap<TEntity, TRead, TCreate, TUpdate>.Instance;
    private static readonly string Label = CrudText.Label(typeof(TEntity));

    static CrudService()
    {
    }

    private readonly ICrudRules<TEntity, TCreate, TUpdate>[] _rules = rules.ToArray();
    private readonly IPostCreateHook<TEntity>[] _hooks = hooks?.ToArray() ?? [];

    protected IUnitOfWork Uow { get; } = uow;

    protected IRepository<TEntity> Repository { get; } = uow.Repository<TEntity>();

    public virtual async Task<IReadOnlyList<TRead>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Repository.Query().OrderBy(e => e.Id).Select(Map.ReadExpression).ToListAsync(cancellationToken);

    public virtual async Task<PaginatedResult<TRead>> GetPagedAsync(PageQuery page, CancellationToken cancellationToken = default)
    {
        if (page.PageNumber < 1)
            throw new ArgumentException("pageNumber must be >= 1");
        if (page.PageSize < 1 || page.PageSize > MaxPageSize)
            throw new ArgumentException($"pageSize must be between 1 and {MaxPageSize}");

        var skip = (long)(page.PageNumber - 1) * page.PageSize;
        if (skip > int.MaxValue)
            throw new ArgumentException("pageNumber is too large");

        var query = Repository.Query();
        if (!string.IsNullOrWhiteSpace(page.Search) && Map.BuildSearch(page.Search.Trim()) is { } search)
            query = query.Where(search);

        var total = await query.CountAsync(cancellationToken);
        var rows = await ApplyOrder(query, page.OrderBy, page.Desc)
            .Skip((int)skip)
            .Take(page.PageSize)
            .Select(Map.ReadExpression)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<TRead>(rows, page.PageNumber, page.PageSize, total);
    }

    public virtual async Task<TRead> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await Repository.Query().Where(e => e.Id == id).Select(Map.ReadExpression)
               .FirstOrDefaultAsync(cancellationToken)
           ?? throw NotFound(id);

    public virtual async Task<IReadOnlyList<TRead>> GetDeletedAsync(CancellationToken cancellationToken = default)
        => await Repository.QueryIncludingDeleted()
            .Where(e => e.IsDeleted)
            .OrderBy(e => e.Id)
            .Select(Map.ReadExpression)
            .ToListAsync(cancellationToken);

    public virtual async Task<TRead> CreateAsync(TCreate create, CancellationToken cancellationToken = default)
    {
        var entity = Map.ToEntity(create);

        if (entity is IBaseEntity owned)
            owned.IdentityTenantId ??= tenant?.TenantId;

        foreach (var rule in _rules)
            await rule.OnCreateAsync(create, entity, cancellationToken);

        await CrudIntegrity<TEntity>.EnsureParentsExistAsync(Uow, entity, cancellationToken);
        await CrudIntegrity<TEntity>.EnsureUniqueAsync(Uow, entity, cancellationToken);

        await Repository.AddAsync(entity, cancellationToken);

        if (_hooks.Length == 0)
        {
            await Uow.SaveChangesAsync(cancellationToken);
            return Map.ToRead(entity);
        }

        await Uow.InTransactionAsync(async () =>
        {
            await Uow.SaveChangesAsync(cancellationToken);
            foreach (var hook in _hooks)
                await hook.AfterCreateAsync(entity, cancellationToken);
            await Uow.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return Map.ToRead(entity);
    }

    public virtual async Task<TRead> UpdateAsync(long id, TUpdate update, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        foreach (var rule in _rules)
            await rule.OnUpdateAsync(entity, update, cancellationToken);

        Map.Apply(update, entity);

        await CrudIntegrity<TEntity>.EnsureParentsExistAsync(Uow, entity, cancellationToken);
        await CrudIntegrity<TEntity>.EnsureUniqueAsync(Uow, entity, cancellationToken);

        await Uow.SaveChangesAsync(cancellationToken);
        return Map.ToRead(entity);
    }

    public virtual async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await GetEntityAsync(id, cancellationToken);
        foreach (var rule in _rules)
            await rule.OnDeleteAsync(entity, cancellationToken);

        await CrudIntegrity<TEntity>.EnsureNoChildrenAsync(Uow, entity, cancellationToken);

        Repository.Remove(entity);
        await Uow.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task<TRead> RestoreAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await Repository.QueryIncludingDeleted()
                         .FirstOrDefaultAsync(e => e.Id == id && e.IsDeleted, cancellationToken)
                     ?? throw new NotFoundException($"Deleted {Label.ToLowerInvariant()} {id} not found");

        foreach (var rule in _rules)
            await rule.OnRestoreAsync(entity, cancellationToken);

        await CrudIntegrity<TEntity>.EnsureParentsAreLiveAsync(Uow, entity, cancellationToken);
        await CrudIntegrity<TEntity>.EnsureUniqueAsync(Uow, entity, cancellationToken);

        entity.IsDeleted = false;
        await Uow.SaveChangesAsync(cancellationToken);
        return Map.ToRead(entity);
    }

    protected async Task<TEntity> GetEntityAsync(long id, CancellationToken cancellationToken)
        => await Repository.GetByIdAsync(id, cancellationToken) ?? throw NotFound(id);

    private static NotFoundException NotFound(long id) => new($"{Label} {id} not found");

    private static IOrderedQueryable<TEntity> ApplyOrder(IQueryable<TEntity> query, string? orderBy, bool desc)
    {
        var key = orderBy is not null && Map.Sortable.TryGetValue(orderBy, out var column) ? column : null;

        if (key is null)
            return desc ? query.OrderByDescending(e => e.Id) : query.OrderBy(e => e.Id);

        var ordered = desc ? query.OrderByDescending(key) : query.OrderBy(key);
        return ordered.ThenBy(e => e.Id);
    }
}
