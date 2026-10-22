using FSIdentity.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Domain.Data;

public class UnitOfWork(FSDbContext db) : IUnitOfWork
{
    public IRepository<T> Repository<T>() where T : class, IBaseEntityRoot => new Repository<T>(db);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);

    public async Task<TResult> InTransactionAsync<TResult>(
        Func<Task<TResult>> work, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is not null)
            return await work();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await work();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task InTransactionAsync(Func<Task> work, CancellationToken cancellationToken = default)
        => InTransactionAsync<object?>(async () =>
        {
            await work();
            return null;
        }, cancellationToken);
}