using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace FSIdentity.Tests.Data;

public class AuditTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
    private static readonly DateTime T0 = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    public AuditTests()
    {
        _conn.Open();
        using var db = Make(new FakeMtx());
        db.Database.EnsureCreated();
    }

    private TestDb Make(FakeMtx mtx)
        => new(new DbContextOptionsBuilder<TestDb>().UseSqlite(_conn).Options, mtx, _clock);

    public void Dispose() => _conn.Dispose();

    [Fact]
    public void Added_gets_created_fields_and_ownership()
    {
        using var db = Make(new FakeMtx { UserId = 10, CompanyId = 1, TenantId = 5 });
        var patient = new Patient { Name = "X" };

        db.Patients.Add(patient);
        db.SaveChanges();

        Assert.Equal(T0, patient.CreatedAt);
        Assert.Equal(10, patient.CreatedBy);
        Assert.Equal(1, patient.IdentityCompanyId);
        Assert.Equal(5, patient.IdentityTenantId);
        Assert.Null(patient.LastModifiedAt);
    }

    [Fact]
    public void Dates_read_back_from_the_database_are_marked_as_UTC()
    {
        long id;
        using (var db = Make(new FakeMtx { UserId = 10, CompanyId = 1 }))
        {
            var created = new Patient { Name = "X" };
            db.Patients.Add(created);
            db.SaveChanges();
            id = created.Id;
        }

        using var read = Make(new FakeMtx { CompanyId = 1 });
        var patient = read.Patients.Single(x => x.Id == id);

        Assert.Equal(DateTimeKind.Utc, patient.CreatedAt!.Value.Kind);
        Assert.Equal(T0, patient.CreatedAt);
    }

    [Fact]
    public void Explicit_ownership_is_not_overwritten()
    {
        using var db = Make(new FakeMtx { UserId = 10, CompanyId = 1, TenantId = 5 });
        var patient = new Patient { Name = "X", IdentityCompanyId = 9 };

        db.Patients.Add(patient);
        db.SaveChanges();

        Assert.Equal(9, patient.IdentityCompanyId);
    }

    [Fact]
    public async Task Async_save_also_audits()
    {
        using var db = Make(new FakeMtx { UserId = 10 });
        var country = new Country { Name = "X" };

        db.Countries.Add(country);
        await db.SaveChangesAsync();

        Assert.Equal(10, country.CreatedBy);
    }

    [Fact]
    public void Modified_gets_last_modified_and_keeps_created()
    {
        long id;
        using (var db = Make(new FakeMtx { UserId = 10, CompanyId = 1 }))
        {
            var patient = new Patient { Name = "X" };
            db.Patients.Add(patient);
            db.SaveChanges();
            id = patient.Id;
        }

        _clock.Advance(TimeSpan.FromHours(1));

        using (var db = Make(new FakeMtx { UserId = 20, CompanyId = 1 }))
        {
            var patient = db.Patients.Single(p => p.Id == id);
            patient.Name = "Y";
            db.SaveChanges();

            Assert.Equal(T0.AddHours(1), patient.LastModifiedAt);
            Assert.Equal(20, patient.LastModifiedBy);
            Assert.Equal(T0, patient.CreatedAt);
            Assert.Equal(10, patient.CreatedBy);
        }
    }

    [Fact]
    public void Remove_becomes_soft_delete()
    {
        long id;
        using (var db = Make(new FakeMtx { UserId = 10, CompanyId = 1 }))
        {
            var patient = new Patient { Name = "X" };
            db.Patients.Add(patient);
            db.SaveChanges();
            id = patient.Id;
        }

        _clock.Advance(TimeSpan.FromHours(2));

        using (var db = Make(new FakeMtx { UserId = 30, CompanyId = 1 }))
        {
            db.Patients.Remove(db.Patients.Single(p => p.Id == id));
            db.SaveChanges();
        }

        using (var db = Make(new FakeMtx { CompanyId = 1 }))
        {
            Assert.Empty(db.Patients);
            var row = db.Patients.IgnoreQueryFilters().Single(p => p.Id == id);
            Assert.True(row.IsDeleted);
            Assert.Equal(T0.AddHours(2), row.DeletedAt);
            Assert.Equal(30, row.DeletedBy);
        }
    }

    [Fact]
    public void Root_entities_are_soft_deleted_too()
    {
        long id;
        using (var db = Make(new FakeMtx { UserId = 1 }))
        {
            var country = new Country { Name = "X" };
            db.Countries.Add(country);
            db.SaveChanges();
            id = country.Id;
        }

        using (var db = Make(new FakeMtx { UserId = 1 }))
        {
            db.Countries.Remove(db.Countries.Single(c => c.Id == id));
            db.SaveChanges();
        }

        using (var db = Make(new FakeMtx { BypassSoftDelete = true }))
            Assert.True(db.Countries.Single(c => c.Id == id).IsDeleted);
    }

    [Fact]
    public void Restore_clears_the_deleted_fields()
    {
        long id;
        using (var db = Make(new FakeMtx { UserId = 1, CompanyId = 1 }))
        {
            var patient = new Patient { Name = "X" };
            db.Patients.Add(patient);
            db.SaveChanges();
            id = patient.Id;
            db.Patients.Remove(patient);
            db.SaveChanges();
        }

        using (var db = Make(new FakeMtx { UserId = 2, CompanyId = 1 }))
        {
            var patient = db.Patients.IgnoreQueryFilters().Single(p => p.Id == id);
            Assert.True(patient.IsDeleted);

            patient.IsDeleted = false;
            db.SaveChanges();

            Assert.Null(patient.DeletedAt);
            Assert.Null(patient.DeletedBy);
            Assert.Equal(2, patient.LastModifiedBy);
        }
    }
}