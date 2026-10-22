using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FSIdentity.Tests.Data;

public class CollationTests
{
    private static IModel BuildModel(Action<DbContextOptionsBuilder<AppDbContext>> useProvider)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        useProvider(options);

        using var db = new AppDbContext(options.Options, new FakeMtx());
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static List<IProperty> StringProperties(IModel model)
        => model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties())
            .Where(property => property.ClrType == typeof(string))
            .ToList();

    [Fact]
    public void Every_string_column_on_sql_server_uses_the_accent_insensitive_collation()
    {
        var model = BuildModel(o => o.UseSqlServer("Server=localhost;Database=Unused"));

        var strings = StringProperties(model);

        Assert.NotEmpty(strings);
        Assert.All(strings, property => Assert.Equal(AppDbContext.SqlServerCollation, property.GetCollation()));
    }

    [Fact]
    public void The_collation_is_the_one_that_matches_istanbul_with_Istanbul_and_ISPARTA_with_isparta()
    {
        Assert.Equal("Latin1_General_100_CI_AI", AppDbContext.SqlServerCollation);
    }

    [Fact]
    public void Sqlite_does_not_get_a_sql_server_collation()
    {
        var model = BuildModel(o => o.UseSqlite("DataSource=:memory:"));

        Assert.All(StringProperties(model), property => Assert.Null(property.GetCollation()));
    }

    [Fact]
    public void A_searched_column_is_covered()
    {
        var model = BuildModel(o => o.UseSqlServer("Server=localhost;Database=Unused"));

        var cityName = model.FindEntityType("FSIdentity.Domain.Entities.Geography.City")!.FindProperty("Name")!;

        Assert.Equal("Latin1_General_100_CI_AI", cityName.GetCollation());
    }
}
