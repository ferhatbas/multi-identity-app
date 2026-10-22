using FSIdentity.Domain.Data;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Tests.Data;

public class UnitOfWorkTests : IDisposable
{
    private readonly TestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private int CountPatients()
    {
        using var db = _factory.Create(new FakeMtx());
        return db.Patients.IgnoreQueryFilters().Count();
    }

    [Fact]
    public async Task Transaction_commits_every_save_when_the_work_succeeds()
    {
        using var db = _factory.Create(new FakeMtx());
        var uow = new UnitOfWork(db);

        await uow.InTransactionAsync(async () =>
        {
            db.Patients.Add(new Patient { Name = "A" });
            await uow.SaveChangesAsync();
            db.Patients.Add(new Patient { Name = "B" });
            await uow.SaveChangesAsync();
        });

        Assert.Equal(2, CountPatients());
    }

    [Fact]
    public async Task Transaction_rolls_back_everything_when_the_work_throws()
    {
        using var db = _factory.Create(new FakeMtx());
        var uow = new UnitOfWork(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.InTransactionAsync(async () =>
        {
            db.Patients.Add(new Patient { Name = "A" });
            await uow.SaveChangesAsync();
            throw new InvalidOperationException("boom");
        }));

        Assert.Equal(0, CountPatients());
    }

    [Fact]
    public async Task Transaction_returns_the_value_of_the_work()
    {
        using var db = _factory.Create(new FakeMtx());

        var result = await new UnitOfWork(db).InTransactionAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Nested_transaction_does_not_commit_on_its_own()
    {
        using var db = _factory.Create(new FakeMtx());
        var uow = new UnitOfWork(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.InTransactionAsync(async () =>
        {
            await uow.InTransactionAsync(async () =>
            {
                db.Patients.Add(new Patient { Name = "inner" });
                await uow.SaveChangesAsync();
            });

            throw new InvalidOperationException("outer fails");
        }));

        Assert.Equal(0, CountPatients());
    }

    [Fact]
    public async Task Nested_transaction_reuses_the_outer_one()
    {
        using var db = _factory.Create(new FakeMtx());
        var uow = new UnitOfWork(db);

        await uow.InTransactionAsync(async () =>
        {
            var outer = db.Database.CurrentTransaction;
            await uow.InTransactionAsync(() =>
            {
                Assert.Same(outer, db.Database.CurrentTransaction);
                return Task.CompletedTask;
            });
        });
    }

    [Fact]
    public async Task No_transaction_is_left_open_afterwards()
    {
        using var db = _factory.Create(new FakeMtx());
        var uow = new UnitOfWork(db);

        await uow.InTransactionAsync(() => Task.CompletedTask);

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public void Repository_is_available_for_any_entity()
    {
        using var db = _factory.Create(new FakeMtx());

        Assert.NotNull(new UnitOfWork(db).Repository<Patient>());
        Assert.NotNull(new UnitOfWork(db).Repository<Country>());
    }
}