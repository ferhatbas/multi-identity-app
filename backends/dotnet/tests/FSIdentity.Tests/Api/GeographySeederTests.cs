using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class GeographySeederTests : ApiTestBase
{
    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<int[]> CountsAsync() => WithDbAsync(async db => new[]
    {
        await db.Languages.CountAsync(),
        await db.Countries.CountAsync(),
        await db.States.CountAsync(),
        await db.Cities.CountAsync(),
    });

    [Fact]
    public async Task Seeds_languages_countries_regions_and_provinces()
    {
        await SeedAsync();

        Assert.Equal(new[] { 4, 10, 9, 83 }, await CountsAsync());
    }

    [Fact]
    public async Task Seeding_twice_adds_nothing()
    {
        await SeedAsync();
        await SeedAsync();

        Assert.Equal(new[] { 4, 10, 9, 83 }, await CountsAsync());
    }

    [Fact]
    public async Task Province_codes_are_the_plate_numbers_01_to_81()
    {
        await SeedAsync();

        var codes = await WithDbAsync(db => db.Cities.Where(c => c.Code.StartsWith("TR-"))
            .Select(c => c.Code).ToListAsync());

        Assert.Equal(
            Enumerable.Range(1, 81).Select(n => $"TR-{n:00}").Order().ToArray(),
            codes.Order().ToArray());
    }

    [Fact]
    public async Task Every_province_sits_in_a_turkish_region()
    {
        await SeedAsync();

        var orphans = await WithDbAsync(db => db.Cities
            .Where(c => c.Code.StartsWith("TR-") && !db.States.Any(s => s.Id == c.StateId && s.Code.StartsWith("TR-")))
            .CountAsync());

        Assert.Equal(0, orphans);
    }

    [Fact]
    public async Task A_changed_row_is_corrected_in_place()
    {
        await SeedAsync();
        await WithDbAsync(async db =>
        {
            (await db.Cities.SingleAsync(c => c.Code == "TR-34")).Name = "Changed";
            return await db.SaveChangesAsync();
        });

        await SeedAsync();

        Assert.Equal("İstanbul", await WithDbAsync(db => db.Cities.Where(c => c.Code == "TR-34")
            .Select(c => c.Name).SingleAsync()));
        Assert.Equal(83, (await CountsAsync())[3]);
    }

    [Fact]
    public async Task A_soft_deleted_row_is_replaced_by_a_new_live_one()
    {
        await SeedAsync();
        await WithDbAsync(async db =>
        {
            db.Cities.Remove(await db.Cities.SingleAsync(c => c.Code == "TR-34"));
            return await db.SaveChangesAsync();
        });

        await SeedAsync();

        var (live, total) = await WithDbAsync(async db => (
            await db.Cities.CountAsync(c => c.Code == "TR-34"),
            await db.Cities.IgnoreQueryFilters().CountAsync(c => c.Code == "TR-34")));
        Assert.Equal(1, live);
        Assert.Equal(2, total);
    }

    [Fact]
    public async Task English_is_the_fallback_and_the_seed_never_moves_it()
    {
        await SeedAsync();
        Assert.Equal(new[] { "en" }, await WithDbAsync(db => db.Languages
            .Where(l => l.IsFallback).Select(l => l.Code).ToArrayAsync()));

        await WithDbAsync(async db =>
        {
            (await db.Languages.SingleAsync(l => l.Code == "en")).IsFallback = false;
            (await db.Languages.SingleAsync(l => l.Code == "tr")).IsFallback = true;
            return await db.SaveChangesAsync();
        });
        await SeedAsync();

        Assert.Equal(new[] { "tr" }, await WithDbAsync(db => db.Languages
            .Where(l => l.IsFallback).Select(l => l.Code).ToArrayAsync()));
    }

    [Fact]
    public async Task Seeded_countries_are_complete_enough_to_use()
    {
        await SeedAsync();

        var turkey = await WithDbAsync(db => db.Countries.SingleAsync(c => c.Code == "TR"));

        Assert.Equal("tr-TR", turkey.FormatCode);
        Assert.Equal("TRY", turkey.CurrencyCode);
        Assert.Equal("Europe/Istanbul", turkey.TimeZone);
    }

    [Fact]
    public async Task The_seeder_works_next_to_rows_an_admin_already_created()
    {
        await CreateCountryAsync("TR");

        await SeedAsync();

        Assert.Equal(1, await WithDbAsync(db => db.Countries.CountAsync(c => c.Code == "TR")));
        Assert.Equal(81, (await CountsAsync())[3] - 2);
    }
}