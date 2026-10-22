using FSIdentity.Domain.Data;
using FSIdentity.Domain.Crud;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FSIdentity.Tests.Data;

public sealed class TestDbFactory : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TestDbFactory()
    {
        _connection.Open();
        using var db = Create(new FakeMtx());
        db.Database.EnsureCreated();
    }

    public TestDb Create(FakeMtx mtx, Action<string>? sqlLog = null)
    {
        var options = new DbContextOptionsBuilder<TestDb>().UseSqlite(_connection);
        if (sqlLog is not null)
            options.LogTo(sqlLog, [RelationalEventId.CommandExecuted]);
        return new TestDb(options.Options, mtx);
    }

    public void Dispose() => _connection.Dispose();
}

public record PatientRead(
    long Id,
    [property: Sortable, Searchable] string Name,
    [property: Sortable] DateTime? CreatedAt);

public record PatientCreate(string Name);

public record PatientUpdate(string Name);

public class PatientService(IUnitOfWork uow)
    : CrudService<Patient, PatientRead, PatientCreate, PatientUpdate>(uow, []);
