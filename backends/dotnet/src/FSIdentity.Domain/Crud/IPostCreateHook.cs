namespace FSIdentity.Domain.Crud;

public interface IPostCreateHook<in TEntity> where TEntity : class
{
    Task AfterCreateAsync(TEntity entity, CancellationToken cancellationToken);
}
