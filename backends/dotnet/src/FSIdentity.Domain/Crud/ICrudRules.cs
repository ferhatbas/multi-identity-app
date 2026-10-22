using FSIdentity.Domain.Interfaces;

namespace FSIdentity.Domain.Crud;

public interface ICrudRules<TEntity, in TCreate, in TUpdate> where TEntity : class, IBaseEntityRoot
{
    Task OnCreateAsync(TCreate create, TEntity entity, CancellationToken cancellationToken);

    Task OnUpdateAsync(TEntity entity, TUpdate update, CancellationToken cancellationToken);

    Task OnDeleteAsync(TEntity entity, CancellationToken cancellationToken);

    Task OnRestoreAsync(TEntity entity, CancellationToken cancellationToken);
}

public abstract class CrudRules<TEntity, TCreate, TUpdate> : ICrudRules<TEntity, TCreate, TUpdate>
    where TEntity : class, IBaseEntityRoot
{
    public virtual Task OnCreateAsync(TCreate create, TEntity entity, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public virtual Task OnUpdateAsync(TEntity entity, TUpdate update, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public virtual Task OnDeleteAsync(TEntity entity, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public virtual Task OnRestoreAsync(TEntity entity, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
