using FSIdentity.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Domain.Data;

public class Repository<T>(FSDbContext db) : IRepository<T> where T : class, IBaseEntityRoot
{
    private readonly DbSet<T> _set = db.Set<T>();

    public IQueryable<T> Query() => _set;

    public IQueryable<T> QueryIncludingDeleted()
        => _set.IgnoreQueryFilters([FSDbContext.SoftDeleteFilter]);

    public IQueryable<T> QueryAcrossScopes()
        => _set.IgnoreQueryFilters([
            FSDbContext.CompanyFilter, FSDbContext.TenantFilter, FSDbContext.CompanySharedFilter]);

    public Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => _set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await _set.AddAsync(entity, cancellationToken);

    public void Remove(T entity) => _set.Remove(entity);
}