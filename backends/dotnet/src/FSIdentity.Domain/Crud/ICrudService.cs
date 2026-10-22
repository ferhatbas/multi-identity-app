using FSIdentity.Domain.Data;
using FSIdentity.Domain.Interfaces;

namespace FSIdentity.Domain.Crud;

public interface ICrudService<TEntity, TRead, TCreate, TUpdate>
    where TEntity : class, IBaseEntityRoot, new()
{
    Task<IReadOnlyList<TRead>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PaginatedResult<TRead>> GetPagedAsync(PageQuery page, CancellationToken cancellationToken = default);

    Task<TRead> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TRead>> GetDeletedAsync(CancellationToken cancellationToken = default);

    Task<TRead> CreateAsync(TCreate create, CancellationToken cancellationToken = default);

    Task<TRead> UpdateAsync(long id, TUpdate update, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task<TRead> RestoreAsync(long id, CancellationToken cancellationToken = default);
}
