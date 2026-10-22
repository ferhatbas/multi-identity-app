using FSIdentity.Domain.Data;
using FSIdentity.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Tests.Data;

public class CrudServiceTests : IDisposable
{
    private readonly TestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private (PatientService Service, TestDb Db) Open(FakeMtx mtx)
    {
        var db = _factory.Create(mtx);
        return (new PatientService(new UnitOfWork(db)), db);
    }

    private void Seed(params (string Name, long Company)[] rows)
    {
        using var db = _factory.Create(new FakeMtx());
        foreach (var (name, company) in rows)
            db.Patients.Add(new Patient { Name = name, IdentityCompanyId = company });
        db.SaveChanges();
    }

    private void SeedMany(int count)
        => Seed(Enumerable.Range(1, count).Select(i => ($"P{i:00}", 1L)).ToArray());

    [Fact]
    public async Task Create_returns_the_new_row_with_id_and_audit()
    {
        var (service, db) = Open(new FakeMtx { UserId = 7, CompanyId = 1 });
        using var _ = db;

        var created = await service.CreateAsync(new PatientCreate("Ada"));

        Assert.True(created.Id > 0);
        Assert.Equal("Ada", created.Name);
        Assert.NotNull(created.CreatedAt);
        Assert.Equal(1, db.Patients.Single().IdentityCompanyId);
        Assert.Equal(7, db.Patients.Single().CreatedBy);
    }

    [Fact]
    public async Task GetById_returns_the_row()
    {
        Seed(("Ada", 1));
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;
        var id = (await service.GetAllAsync()).Single().Id;

        Assert.Equal("Ada", (await service.GetByIdAsync(id)).Name);
    }

    [Fact]
    public async Task GetById_of_a_missing_row_is_not_found()
    {
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        var error = await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));

        Assert.Equal("Patient 999 not found", error.Message);
    }

    [Fact]
    public async Task GetById_of_another_companys_row_is_not_found_not_forbidden()
    {
        Seed(("Theirs", 2));
        long id;
        using (var any = _factory.Create(new FakeMtx()))
            id = any.Patients.Single().Id;
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetAll_returns_only_the_callers_rows_ordered_by_id()
    {
        Seed(("B", 1), ("Other", 2), ("A", 1));
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        var names = (await service.GetAllAsync()).Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "B", "A" }, names);
    }

    [Fact]
    public async Task Update_changes_the_fields_the_dto_carries()
    {
        Seed(("Ada", 1));
        var (service, db) = Open(new FakeMtx { UserId = 5, CompanyId = 1 });
        using var _ = db;
        var id = (await service.GetAllAsync()).Single().Id;

        var updated = await service.UpdateAsync(id, new PatientUpdate("Ada L."));

        Assert.Equal("Ada L.", updated.Name);
        var row = db.Patients.Single();
        Assert.Equal(5, row.LastModifiedBy);
        Assert.Equal(1, row.IdentityCompanyId);
    }

    [Fact]
    public async Task Update_of_another_companys_row_is_not_found()
    {
        Seed(("Theirs", 2));
        long id;
        using (var any = _factory.Create(new FakeMtx()))
            id = any.Patients.Single().Id;
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(id, new PatientUpdate("x")));
    }

    [Fact]
    public async Task Delete_hides_the_row_and_restore_brings_it_back()
    {
        Seed(("Ada", 1));
        var (service, db) = Open(new FakeMtx { UserId = 5, CompanyId = 1 });
        using var _ = db;
        var id = (await service.GetAllAsync()).Single().Id;

        await service.DeleteAsync(id);

        Assert.Empty(await service.GetAllAsync());
        Assert.Equal(id, Assert.Single(await service.GetDeletedAsync()).Id);

        var restored = await service.RestoreAsync(id);

        Assert.Equal("Ada", restored.Name);
        Assert.Single(await service.GetAllAsync());
        Assert.Empty(await service.GetDeletedAsync());
    }

    [Fact]
    public async Task Restore_of_a_live_row_is_not_found()
    {
        Seed(("Ada", 1));
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;
        var id = (await service.GetAllAsync()).Single().Id;

        await Assert.ThrowsAsync<NotFoundException>(() => service.RestoreAsync(id));
    }

    [Fact]
    public async Task Deleted_list_does_not_leak_other_companies()
    {
        using (var any = _factory.Create(new FakeMtx()))
        {
            any.Patients.Add(new Patient { Name = "Theirs", IdentityCompanyId = 2, IsDeleted = true });
            any.SaveChanges();
        }
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        Assert.Empty(await service.GetDeletedAsync());
    }

    [Fact]
    public async Task Paged_returns_the_requested_slice_and_totals()
    {
        SeedMany(25);
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        var page = await service.GetPagedAsync(new PageQuery(PageNumber: 3, PageSize: 10));

        Assert.Equal(5, page.Data.Count);
        Assert.Equal(25, page.TotalRecords);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(3, page.PageNumber);
    }

    [Fact]
    public async Task Paged_beyond_the_last_page_is_empty_not_an_error()
    {
        SeedMany(5);
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        var page = await service.GetPagedAsync(new PageQuery(PageNumber: 9, PageSize: 10));

        Assert.Empty(page.Data);
        Assert.Equal(5, page.TotalRecords);
    }

    [Fact]
    public async Task Paged_can_sort_by_an_allowed_column_in_both_directions()
    {
        Seed(("B", 1), ("C", 1), ("A", 1));
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        var asc = await service.GetPagedAsync(new PageQuery(OrderBy: "name"));
        var desc = await service.GetPagedAsync(new PageQuery(OrderBy: "NAME", Desc: true));

        Assert.Equal(new[] { "A", "B", "C" }, asc.Data.Select(p => p.Name).ToArray());
        Assert.Equal(new[] { "C", "B", "A" }, desc.Data.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task Paged_can_sort_by_a_date_column()
    {
        Seed(("B", 1), ("A", 1));
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        var page = await service.GetPagedAsync(new PageQuery(OrderBy: "createdAt"));

        Assert.Equal(2, page.Data.Count);
    }

    [Fact]
    public async Task Paged_falls_back_to_id_for_a_column_that_is_not_allowed()
    {
        Seed(("B", 1), ("A", 1));
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        var page = await service.GetPagedAsync(new PageQuery(OrderBy: "identityCompanyId; DROP TABLE x"));

        Assert.Equal(new[] { "B", "A" }, page.Data.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task Paged_sorts_by_the_chosen_column_and_then_by_id()
    {
        SeedMany(3);
        var sql = new List<string>();
        var db = _factory.Create(new FakeMtx { CompanyId = 1 }, sql.Add);
        using var _ = db;
        var service = new PatientService(new UnitOfWork(db));

        await service.GetPagedAsync(new PageQuery(OrderBy: "name"));

        var select = Assert.Single(sql, s => s.Contains("ORDER BY"));
        var orderBy = select[select.IndexOf("ORDER BY", StringComparison.Ordinal)..];
        Assert.Matches("ORDER BY .*\"Name\".*, .*\"Id\"", orderBy);
    }

    [Fact]
    public async Task Paged_search_filters_the_rows_and_the_total()
    {
        Seed(("Ada Lovelace", 1), ("Alan Turing", 1), ("Grace Hopper", 1));
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        var page = await service.GetPagedAsync(new PageQuery(Search: "  Al  "));

        Assert.Equal(1, page.TotalRecords);
        Assert.Equal("Alan Turing", page.Data.Single().Name);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 501)]
    [InlineData(int.MaxValue, 500)]
    public async Task Paged_rejects_invalid_arguments(int pageNumber, int pageSize)
    {
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetPagedAsync(new PageQuery(pageNumber, pageSize)));
    }

    [Fact]
    public async Task Paged_only_counts_the_callers_rows()
    {
        Seed(("Mine", 1), ("Theirs", 2));
        var (service, db) = Open(new FakeMtx { CompanyId = 1 });
        using var _ = db;

        var page = await service.GetPagedAsync(new PageQuery());

        Assert.Equal(1, page.TotalRecords);
    }
}