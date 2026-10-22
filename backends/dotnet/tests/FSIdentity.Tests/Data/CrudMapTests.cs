using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Data;

namespace FSIdentity.Tests.Data;

public class CrudMapTests : IDisposable
{
    private readonly TestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private string ShapeError(Type read, Type create, Type update)
    {
        using var db = _factory.Create(new FakeMtx());
        var closed = typeof(CrudService<,,,>).MakeGenericType(typeof(Patient), read, create, update);
        var rules = Array.CreateInstance(typeof(ICrudRules<,,>).MakeGenericType(typeof(Patient), create, update), 0);

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(
            () => Activator.CreateInstance(closed, new UnitOfWork(db), rules, null, null));

        return Assert.IsType<TypeInitializationException>(ex.InnerException).InnerException!.Message;
    }

    private string ShapeError<TRead, TCreate, TUpdate>() => ShapeError(typeof(TRead), typeof(TCreate), typeof(TUpdate));

    public record WithStrayField(string Name, string Nickname);

    [Fact]
    public void A_request_field_without_a_column_stops_the_resource()
    {
        var message = ShapeError<PatientRead, WithStrayField, PatientUpdate>();

        Assert.Contains("Nickname", message);
        Assert.Contains("no column", message);
    }

    public record WithIgnoredField(string Name, [property: MapIgnore] string Nickname);

    [Fact]
    public async Task A_field_marked_MapIgnore_is_left_to_a_rule()
    {
        using var db = _factory.Create(new FakeMtx { CompanyId = 1 });
        var service = new CrudService<Patient, PatientRead, WithIgnoredField, PatientUpdate>(new UnitOfWork(db), []);

        var created = await service.CreateAsync(new WithIgnoredField("Ada", "ignored"));

        Assert.Equal("Ada", created.Name);
    }

    public record WritesIsDeleted(string Name, bool IsDeleted);

    public record WritesTenant(string Name, long? IdentityTenantId);

    public record WritesCreatedAt(string Name, DateTime? CreatedAt);

    [Theory]
    [InlineData(typeof(WritesIsDeleted), "IsDeleted")]
    [InlineData(typeof(WritesTenant), "IdentityTenantId")]
    [InlineData(typeof(WritesCreatedAt), "CreatedAt")]
    public void A_request_cannot_write_a_server_owned_column(Type request, string column)
    {
        var message = ShapeError(typeof(PatientRead), request, typeof(PatientUpdate));

        Assert.Contains(column, message);
        Assert.Contains("only the server sets", message);
    }

    public record ReadsAMissingColumn(long Id, string Name, string Nickname);

    [Fact]
    public void A_read_field_without_a_source_stops_the_resource()
    {
        var message = ShapeError<ReadsAMissingColumn, PatientCreate, PatientUpdate>();

        Assert.Contains("Nickname", message);
        Assert.Contains("no source", message);
    }

    public record Spaced([property: Upper] string Name);

    [Fact]
    public async Task Text_is_trimmed_and_cased_as_the_request_field_asks()
    {
        using var db = _factory.Create(new FakeMtx { CompanyId = 1 });
        var service = new CrudService<Patient, PatientRead, Spaced, PatientUpdate>(new UnitOfWork(db), []);

        var created = await service.CreateAsync(new Spaced("  ada  "));

        Assert.Equal("ADA", created.Name);
    }

    private sealed class LoudPatientService(IUnitOfWork uow)
        : CrudService<Patient, PatientRead, PatientCreate, PatientUpdate>(uow, [])
    {
        public override async Task<PatientRead> CreateAsync(PatientCreate create, CancellationToken cancellationToken = default)
            => await base.CreateAsync(create with { Name = create.Name.ToUpperInvariant() }, cancellationToken);
    }

    [Fact]
    public async Task A_resource_can_override_one_operation_and_keep_the_rest()
    {
        using var db = _factory.Create(new FakeMtx { CompanyId = 1 });
        var service = new LoudPatientService(new UnitOfWork(db));

        var created = await service.CreateAsync(new PatientCreate("ada"));

        Assert.Equal("ADA", created.Name);
        Assert.Equal("ADA", (await service.GetByIdAsync(created.Id)).Name);
    }

    private sealed class RefuseTuring : CrudRules<Patient, PatientCreate, PatientUpdate>
    {
        public override Task OnCreateAsync(PatientCreate create, Patient entity, CancellationToken cancellationToken)
            => create.Name == "Turing"
                ? throw new FSIdentity.Domain.Exceptions.ConflictException("no Turing")
                : Task.CompletedTask;
    }

    [Fact]
    public async Task A_rules_object_can_refuse_a_create()
    {
        using var db = _factory.Create(new FakeMtx { CompanyId = 1 });
        var service = new CrudService<Patient, PatientRead, PatientCreate, PatientUpdate>(
            new UnitOfWork(db), [new RefuseTuring()]);

        await Assert.ThrowsAsync<FSIdentity.Domain.Exceptions.ConflictException>(
            () => service.CreateAsync(new PatientCreate("Turing")));
        Assert.Empty(db.Patients);
    }
}
