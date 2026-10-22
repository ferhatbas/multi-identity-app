using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Data;

public class PostCreateHookTests : IDisposable
{
    private readonly TestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed class GiveAClinic(IUnitOfWork uow) : IPostCreateHook<Patient>
    {
        public Task AfterCreateAsync(Patient entity, CancellationToken cancellationToken)
        {
            uow.Repository<Clinic>().AddAsync(new Clinic { Name = $"clinic-of-{entity.Id}" }, cancellationToken);
            return Task.CompletedTask;
        }
    }

    private (CrudService<Patient, PatientRead, PatientCreate, PatientUpdate> Service, TestDb Db) Open(
        Func<IUnitOfWork, IPostCreateHook<Patient>[]> hooks)
    {
        var db = _factory.Create(new FakeMtx { CompanyId = 1 });
        var uow = new UnitOfWork(db);
        return (new CrudService<Patient, PatientRead, PatientCreate, PatientUpdate>(uow, [], null, hooks(uow)), db);
    }

    [Fact]
    public async Task A_hook_sees_the_saved_record_and_what_it_adds_is_saved_with_it()
    {
        var (service, db) = Open(uow => [new GiveAClinic(uow)]);
        using var _ = db;

        var created = await service.CreateAsync(new PatientCreate("Ada"));

        using var read = _factory.Create(new FakeMtx { CompanyId = 1 });
        Assert.Equal($"clinic-of-{created.Id}", read.Clinics.IgnoreQueryFilters().Single().Name);
    }

    [Fact]
    public async Task A_hook_that_fails_takes_the_new_record_with_it()
    {
        var (service, db) = Open(_ => [new GiveAClinicThatThenFails()]);
        using var _ = db;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new PatientCreate("Ada")));

        using var read = _factory.Create(new FakeMtx { CompanyId = 1 });
        Assert.Empty(read.Patients.IgnoreQueryFilters());
        Assert.Empty(read.Clinics.IgnoreQueryFilters());
    }

    private sealed class GiveAClinicThatThenFails : IPostCreateHook<Patient>
    {
        public Task AfterCreateAsync(Patient entity, CancellationToken cancellationToken)
            => throw new InvalidOperationException("the hook failed");
    }

    [Fact]
    public async Task Without_hooks_a_create_behaves_as_before()
    {
        var (service, db) = Open(_ => []);
        using var _ = db;

        var created = await service.CreateAsync(new PatientCreate("Ada"));

        Assert.True(created.Id > 0);
        Assert.Empty(db.Clinics.IgnoreQueryFilters());
    }

    [Fact]
    public void AddCrud_registers_the_hooks_it_finds_in_an_assembly()
    {
        var services = new ServiceCollection();

        services.AddCrud(typeof(PostCreateHookTests).Assembly);

        Assert.Contains(services, s => s.ServiceType == typeof(IPostCreateHook<Patient>) && s.ImplementationType == typeof(GiveAClinic));
    }
}
