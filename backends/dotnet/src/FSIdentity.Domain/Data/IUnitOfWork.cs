using FSIdentity.Domain.Interfaces;

namespace FSIdentity.Domain.Data;

public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : class, IBaseEntityRoot;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<TResult> InTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken cancellationToken = default);

    Task InTransactionAsync(Func<Task> work, CancellationToken cancellationToken = default);
}