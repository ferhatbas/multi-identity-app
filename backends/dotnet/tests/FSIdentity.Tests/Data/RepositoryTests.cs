using FSIdentity.Domain.Data;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Tests.Data;

public class RepositoryTests : IDisposable
{
    private readonly TestDbFactory _factory = new();

    public RepositoryTests()
    {
        using var db = _factory.Create(new FakeMtx());
        db.Patients.AddRange(
            new Patient { Name = "Mine", IdentityCompanyId = 1 },
            new Patient { Name = "Theirs", IdentityCompanyId = 2 },
            new Patient { Name = "Gone", IdentityCompanyId = 1, IsDeleted = true });
        db.SaveChanges();
    }

    public void Dispose() => _factory.Dispose();

    private long IdOf(string name)
    {
        using var db = _factory.Create(new FakeMtx());
        return db.Patients.IgnoreQueryFilters().Single(p => p.Name == name).Id;
    }

    [Fact]
    public async Task GetById_returns_a_visible_row()
    {
        var id = IdOf("Mine");
        using var db = _factory.Create(new FakeMtx { CompanyId = 1 });

        var patient = await new Repository<Patient>(db).GetByIdAsync(id);

        Assert.Equal("Mine", patient!.Name);
    }

    [Fact]
    public async Task GetById_does_not_return_another_companys_row()
    {
        var id = IdOf("Theirs");
        using var db = _factory.Create(new FakeMtx { CompanyId = 1 });

        Assert.Null(await new Repository<Patient>(db).GetByIdAsync(id));
    }

    [Fact]
    public async Task GetById_does_not_return_a_soft_deleted_row()
    {
        var id = IdOf("Gone");
        using var db = _factory.Create(new FakeMtx { CompanyId = 1 });

        Assert.Null(await new Repository<Patient>(db).GetByIdAsync(id));
    }

    [Fact]
    public async Task GetById_does_not_use_the_change_tracker_to_dodge_filters()
    {
        var id = IdOf("Theirs");
        var mtx = new FakeMtx();
        using var db = _factory.Create(mtx);
        var repository = new Repository<Patient>(db);
        Assert.NotNull(await repository.GetByIdAsync(id));

        mtx.CompanyId = 1;

        Assert.Null(await repository.GetByIdAsync(id));
    }

    [Fact]
    public void QueryIncludingDeleted_shows_deleted_rows_but_keeps_the_scope()
    {
        using var db = _factory.Create(new FakeMtx { CompanyId = 1 });

        var names = new Repository<Patient>(db).QueryIncludingDeleted()
            .Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(new[] { "Gone", "Mine" }, names);
    }

    [Fact]
    public void QueryAcrossScopes_shows_other_companies_but_still_hides_deleted_rows()
    {
        using var db = _factory.Create(new FakeMtx { CompanyId = 1 });

        var names = new Repository<Patient>(db).QueryAcrossScopes()
            .Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(new[] { "Mine", "Theirs" }, names);
    }

    [Fact]
    public void Query_hides_deleted_and_other_companies()
    {
        using var db = _factory.Create(new FakeMtx { CompanyId = 1 });

        Assert.Equal(new[] { "Mine" }, new Repository<Patient>(db).Query().Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task Add_then_save_assigns_an_id_and_ownership()
    {
        using var db = _factory.Create(new FakeMtx { UserId = 9, CompanyId = 1 });
        var repository = new Repository<Patient>(db);
        var patient = new Patient { Name = "New" };

        await repository.AddAsync(patient);
        await db.SaveChangesAsync();

        Assert.True(patient.Id > 0);
        Assert.Equal(1, patient.IdentityCompanyId);
        Assert.Equal(9, patient.CreatedBy);
    }

    [Fact]
    public async Task Remove_is_a_soft_delete()
    {
        var id = IdOf("Mine");
        using (var db = _factory.Create(new FakeMtx { UserId = 9, CompanyId = 1 }))
        {
            var repository = new Repository<Patient>(db);
            repository.Remove((await repository.GetByIdAsync(id))!);
            await db.SaveChangesAsync();
        }

        using var check = _factory.Create(new FakeMtx { CompanyId = 1 });
        var row = new Repository<Patient>(check).QueryIncludingDeleted().Single(p => p.Id == id);
        Assert.True(row.IsDeleted);
        Assert.Equal(9, row.DeletedBy);
    }
}