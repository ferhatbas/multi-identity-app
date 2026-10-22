using System.Text.Json;
using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Entities;
using FSIdentity.Domain.Interfaces.MultiTenant;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace FSIdentity.Tests.Data;

public class Vault : BaseEntity, IStrictCompanyScoped
{
    public string Name { get; set; } = "";

    [AuditIgnore]
    public string Secret { get; set; } = "";

    public string PasswordHash { get; set; } = "";
}

[NoAudit]
public class Scratch : BaseEntity, ICompanyScoped
{
    public string Name { get; set; } = "";
}

public class AuditLogTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
    private static readonly DateTime T0 = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FakeAuditContext : IAuditContext
    {
        public string? UserName => "ada";
        public long? ImpersonatorId => null;
        public string? IpAddress => "10.0.0.7";
        public string? UserAgent => "tests";
        public string? CorrelationId => "corr-1";
    }

    public AuditLogTests()
    {
        _conn.Open();
        using var db = Make(new FakeMtx());
        db.Database.EnsureCreated();
    }

    public void Dispose() => _conn.Dispose();

    private TestDb Make(FakeMtx mtx, IAuditContext? context = null)
        => new(new DbContextOptionsBuilder<TestDb>().UseSqlite(_conn).Options, mtx, _clock, context);

    private static FakeMtx Ada() => new() { UserId = 7, TenantId = 5, CompanyId = 1 };

    private List<AuditLog> Trail()
    {
        using var db = Make(new FakeMtx());
        return db.AuditLogs.AsNoTracking().OrderBy(a => a.Id).ToList();
    }

    private long Add(string name = "Ada")
    {
        using var db = Make(Ada());
        var patient = new Patient { Name = name };
        db.Patients.Add(patient);
        db.SaveChanges();
        return patient.Id;
    }

    [Fact]
    public void A_create_writes_one_row_with_the_new_key_values_and_who_did_it()
    {
        long id;
        using (var db = Make(Ada(), new FakeAuditContext()))
        {
            var patient = new Patient { Name = "Ada" };
            db.Patients.Add(patient);
            db.SaveChanges();
            id = patient.Id;
        }

        var row = Assert.Single(Trail());
        Assert.Equal("Patient", row.EntityName);
        Assert.Equal(id.ToString(), row.EntityId);
        Assert.Equal("Create", row.Action);
        Assert.Null(row.OldValues);
        Assert.Equal("Ada", JsonDocument.Parse(row.NewValues!).RootElement.GetProperty("Name").GetString());
        Assert.Equal(7, row.UserId);
        Assert.Equal(5, row.AuditTenantId);
        Assert.Equal(1, row.AuditCompanyId);
        Assert.Equal("ada", row.UserName);
        Assert.Equal("10.0.0.7", row.IpAddress);
        Assert.Equal("tests", row.UserAgent);
        Assert.Equal("corr-1", row.CorrelationId);
        Assert.Equal(T0, row.CreatedAt);
    }

    [Fact]
    public async Task The_async_save_writes_the_same_trail()
    {
        await using var db = Make(Ada());
        db.Patients.Add(new Patient { Name = "Ada" });

        await db.SaveChangesAsync();

        var row = Assert.Single(Trail());
        Assert.Equal("Create", row.Action);
        Assert.NotNull(row.EntityId);
    }

    [Fact]
    public void An_update_records_only_what_changed_and_not_the_server_stamps()
    {
        var id = Add("Ada");
        using (var db = Make(Ada()))
        {
            db.Patients.Single(p => p.Id == id).Name = "Grace";
            db.SaveChanges();
        }

        var row = Trail().Last();
        Assert.Equal("Update", row.Action);
        Assert.Equal("Name", row.ChangedFields);
        Assert.Equal("Ada", JsonDocument.Parse(row.OldValues!).RootElement.GetProperty("Name").GetString());
        Assert.Equal("Grace", JsonDocument.Parse(row.NewValues!).RootElement.GetProperty("Name").GetString());
        Assert.DoesNotContain("LastModified", row.ChangedFields);
    }

    [Fact]
    public void Saving_a_value_that_did_not_change_writes_nothing()
    {
        var id = Add("Ada");
        using (var db = Make(Ada()))
        {
            var patient = db.Patients.Single(p => p.Id == id);
            patient.Name = "Ada";
            db.Entry(patient).Property(p => p.Name).IsModified = true;
            db.SaveChanges();
        }

        Assert.Single(Trail());
    }

    [Fact]
    public void A_soft_delete_is_a_Delete_and_bringing_it_back_is_a_Restore()
    {
        var id = Add();
        using (var db = Make(Ada()))
        {
            db.Patients.Remove(db.Patients.Single(p => p.Id == id));
            db.SaveChanges();
        }
        using (var db = Make(Ada()))
        {
            db.Patients.IgnoreQueryFilters().Single(p => p.Id == id).IsDeleted = false;
            db.SaveChanges();
        }

        var rows = Trail();
        Assert.Equal(["Create", "Delete", "Restore"], rows.Select(r => r.Action));
        Assert.Equal("IsDeleted", rows[1].ChangedFields);
        Assert.Equal("IsDeleted", rows[2].ChangedFields);
    }

    [Fact]
    public void One_save_of_several_entities_writes_a_row_each()
    {
        using (var db = Make(Ada()))
        {
            db.Patients.Add(new Patient { Name = "A" });
            db.Clinics.Add(new Clinic { Name = "B" });
            db.SaveChanges();
        }

        Assert.Equal(["Clinic", "Patient"], Trail().Select(r => r.EntityName).Order());
    }

    [Fact]
    public void A_property_marked_AuditIgnore_and_known_secret_names_stay_out_of_the_values()
    {
        long id;
        using (var db = Make(Ada()))
        {
            var vault = new Vault { Name = "box", Secret = "s3cret", PasswordHash = "hash1" };
            db.Vaults.Add(vault);
            db.SaveChanges();
            id = vault.Id;
        }
        using (var db = Make(Ada()))
        {
            var vault = db.Vaults.Single(v => v.Id == id);
            vault.Secret = "other";
            vault.PasswordHash = "hash2";
            vault.Name = "renamed";
            db.SaveChanges();
        }

        foreach (var row in Trail())
        {
            var text = row.OldValues + row.NewValues + row.ChangedFields;
            Assert.DoesNotContain("s3cret", text);
            Assert.DoesNotContain("other", text);
            Assert.DoesNotContain("hash", text);
            Assert.DoesNotContain("Secret", text);
            Assert.DoesNotContain("PasswordHash", text);
        }
        Assert.Equal("Name", Trail().Last().ChangedFields);
    }

    [Fact]
    public void An_entity_marked_NoAudit_writes_nothing()
    {
        using (var db = Make(Ada()))
        {
            db.Scratches.Add(new Scratch { Name = "tmp" });
            db.SaveChanges();
        }

        Assert.Empty(Trail());
    }

    [Fact]
    public void Audit_rows_are_not_audited_themselves()
    {
        Add();

        Assert.Single(Trail());
    }

    [Fact]
    public void Inside_a_suppression_scope_nothing_is_written_and_afterwards_it_is_again()
    {
        using (AuditLogSuppression.Suppress())
            Add("quiet");

        Assert.Empty(Trail());

        Add("loud");
        Assert.Single(Trail());
    }

    [Fact]
    public void Suppression_nests_and_the_inner_scope_restores_the_outer_value()
    {
        using (AuditLogSuppression.Suppress())
        {
            using (AuditLogSuppression.Suppress())
                Assert.True(AuditLogSuppression.IsSuppressed);

            Assert.True(AuditLogSuppression.IsSuppressed);
        }

        Assert.False(AuditLogSuppression.IsSuppressed);
    }

    [Fact]
    public async Task Suppression_does_not_reach_a_flow_that_started_before_it()
    {
        var gate = new TaskCompletionSource();
        var concurrent = Task.Run(async () =>
        {
            await gate.Task;
            return AuditLogSuppression.IsSuppressed;
        });

        using (AuditLogSuppression.Suppress())
        {
            gate.SetResult();
            Assert.False(await concurrent);
        }
    }

    [Fact]
    public void A_note_adds_one_Publish_row_with_the_note_and_the_row_count()
    {
        using (AuditNote.Set("MenuLayout", "9", "moved together"))
        using (var db = Make(Ada(), new FakeAuditContext()))
        {
            db.Patients.AddRange(new Patient { Name = "A" }, new Patient { Name = "B" });
            db.SaveChanges();
        }

        var rows = Trail();
        var note = Assert.Single(rows, r => r.Action == "Publish");
        Assert.Equal("MenuLayout", note.EntityName);
        Assert.Equal("9", note.EntityId);
        var values = JsonDocument.Parse(note.NewValues!).RootElement;
        Assert.Equal("moved together", values.GetProperty("note").GetString());
        Assert.Equal(2, values.GetProperty("rows").GetInt32());
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal("corr-1", r.CorrelationId));
    }

    [Fact]
    public void Without_a_note_there_is_no_Publish_row()
    {
        Add();

        Assert.DoesNotContain(Trail(), r => r.Action == "Publish");
    }

    [Fact]
    public void A_rolled_back_transaction_takes_the_audit_rows_with_it()
    {
        using (var db = Make(Ada()))
        {
            using var transaction = db.Database.BeginTransaction();
            db.Patients.Add(new Patient { Name = "A" });
            db.SaveChanges();
            transaction.Rollback();
        }

        using var read = Make(Ada());
        Assert.Empty(read.Patients);
        Assert.Empty(Trail());
    }

    [Fact]
    public void When_the_audit_row_cannot_be_written_the_change_is_not_kept_either()
    {
        using (var setup = Make(new FakeMtx()))
            setup.Database.ExecuteSqlRaw(
                "CREATE TRIGGER refuse_audit BEFORE INSERT ON AuditLogs BEGIN SELECT RAISE(ABORT, 'refused'); END");

        using (var db = Make(Ada()))
        {
            db.Patients.Add(new Patient { Name = "A" });
            Assert.ThrowsAny<Exception>(() => db.SaveChanges());
        }

        using var read = Make(Ada());
        Assert.Empty(read.Patients);
    }

    [Fact]
    public void A_context_without_audit_information_still_writes_the_row()
    {
        Add();

        var row = Assert.Single(Trail());
        Assert.Null(row.UserName);
        Assert.Null(row.IpAddress);
        Assert.Equal(7, row.UserId);
    }
}
