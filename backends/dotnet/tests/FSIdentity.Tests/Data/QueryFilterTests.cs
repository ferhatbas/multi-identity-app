using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities;
using FSIdentity.Domain.Interfaces.MultiTenant;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Tests.Data;

public class FakeMtx : IMultiTenantAccessor
{
    public long? UserId { get; set; }
    public long? TenantId { get; set; }
    public long? CompanyId { get; set; }
    public bool Bypass { get; set; }
    public bool BypassCompany { get; set; }
    public bool BypassTenant { get; set; }
    public bool BypassSoftDelete { get; set; }
}

public class Patient : BaseEntity, IStrictCompanyScoped { public string Name { get; set; } = ""; }
public class Clinic : BaseEntity, ICompanyScoped { public string Name { get; set; } = ""; }
public class Payer : BaseEntity, IStrictTenantScoped, ICompanySharedScoped { public string Name { get; set; } = ""; }
public class Country : BaseEntityRoot { public string Name { get; set; } = ""; }

public class TestDb : FSDbContext
{
    public TestDb(DbContextOptions<TestDb> options, IMultiTenantAccessor mtx, TimeProvider? clock = null, IAuditContext? audit = null)
        : base(options, mtx, clock, audit) { }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<Payer> Payers => Set<Payer>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Vault> Vaults => Set<Vault>();
    public DbSet<Scratch> Scratches => Set<Scratch>();
}

public class QueryFilterTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");

    public QueryFilterTests()
    {
        _conn.Open();
        using var db = Make(new FakeMtx());
        db.Database.EnsureCreated();

        db.Patients.AddRange(
            new Patient { Name = "A", IdentityCompanyId = 1, IdentityTenantId = 1 },
            new Patient { Name = "B", IdentityCompanyId = 2, IdentityTenantId = 1 },
            new Patient { Name = "Del", IdentityCompanyId = 1, IdentityTenantId = 1, IsDeleted = true });
        db.Clinics.AddRange(
            new Clinic { Name = "C1", IdentityCompanyId = 1 },
            new Clinic { Name = "C2", IdentityCompanyId = 2 });
        db.Payers.AddRange(
            new Payer { Name = "Shared", IdentityCompanyId = null, IdentityTenantId = 1 },
            new Payer { Name = "Mine", IdentityCompanyId = 1, IdentityTenantId = 1 },
            new Payer { Name = "Other", IdentityCompanyId = 2, IdentityTenantId = 1 },
            new Payer { Name = "Tenant2", IdentityCompanyId = null, IdentityTenantId = 2 });
        db.Countries.AddRange(
            new Country { Name = "TR" },
            new Country { Name = "DE", IsDeleted = true });
        db.SaveChanges();
    }

    private TestDb Make(FakeMtx mtx)
        => new(new DbContextOptionsBuilder<TestDb>().UseSqlite(_conn).Options, mtx);

    public void Dispose() => _conn.Dispose();

    [Fact]
    public void Company_filter_and_soft_delete_apply_together()
    {
        using var db = Make(new FakeMtx { CompanyId = 1, TenantId = 1 });

        Assert.Equal(new[] { "A" }, db.Patients.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Same_model_with_different_users_is_filtered_differently()
    {
        using var a = Make(new FakeMtx { CompanyId = 1 });
        using var b = Make(new FakeMtx { CompanyId = 2 });

        Assert.Equal("A", a.Patients.Single().Name);
        Assert.Equal("B", b.Patients.Single().Name);
    }

    [Fact]
    public void No_active_company_means_unfiltered_system_work()
    {
        using var db = Make(new FakeMtx());

        Assert.Equal(2, db.Patients.Count());
    }

    [Fact]
    public void SuperAdmin_bypasses_normal_company_scope()
    {
        using var db = Make(new FakeMtx { CompanyId = 1, Bypass = true });

        Assert.Equal(2, db.Clinics.Count());
    }

    [Fact]
    public void SuperAdmin_cannot_bypass_strict_scope()
    {
        using var db = Make(new FakeMtx { CompanyId = 1, Bypass = true });

        Assert.Equal(new[] { "A" }, db.Patients.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void SuperAdmin_does_not_lift_soft_delete()
    {
        using var db = Make(new FakeMtx { Bypass = true });

        Assert.Equal(new[] { "TR" }, db.Countries.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void BypassSoftDelete_lifts_soft_delete()
    {
        using var db = Make(new FakeMtx { BypassSoftDelete = true });

        Assert.Equal(2, db.Countries.Count());
    }

    [Fact]
    public void Shared_scope_and_strict_tenant_combine()
    {
        using var db = Make(new FakeMtx { CompanyId = 1, TenantId = 1 });

        var names = db.Payers.Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(new[] { "Mine", "Shared" }, names);
    }

    [Fact]
    public void Can_ignore_a_single_named_filter()
    {
        using var db = Make(new FakeMtx { CompanyId = 1 });

        var names = db.Patients
            .IgnoreQueryFilters([FSDbContext.SoftDeleteFilter])
            .Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(new[] { "A", "Del" }, names);
    }

    [Fact]
    public void IgnoreQueryFilters_without_names_ignores_all()
    {
        using var db = Make(new FakeMtx { CompanyId = 1 });

        Assert.Equal(3, db.Patients.IgnoreQueryFilters().Count());
    }

    [Fact]
    public void Changing_the_company_on_the_same_context_applies_to_the_next_query()
    {
        var mtx = new FakeMtx { CompanyId = 1 };
        using var db = Make(mtx);

        Assert.Equal("A", db.Patients.Single().Name);
        mtx.CompanyId = 2;
        Assert.Equal("B", db.Patients.Single().Name);
    }
}