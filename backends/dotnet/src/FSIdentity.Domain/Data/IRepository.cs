using FSIdentity.Domain.Interfaces;

namespace FSIdentity.Domain.Data;

public interface IRepository<T> where T : class, IBaseEntityRoot
{
    IQueryable<T> Query();

    IQueryable<T> QueryIncludingDeleted();

    IQueryable<T> QueryAcrossScopes();

    Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Remove(T entity);
}