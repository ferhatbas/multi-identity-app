using FSIdentity.Identity.Applications;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class ApplicationSeederTests : ApiTestBase
{
    private async Task<T> InScopeAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task SeedApplicationsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationSeeder>().SeedAsync();
    }

    [Fact]
    public async Task Seeding_creates_management_two_modules_and_one_grant()
    {
        await SeedApplicationsAsync();

        Assert.Equal(["MANAGEMENT"], await InScopeAsync(db => db.ApplicationTypes.Select(a => a.Code).ToListAsync()));
        Assert.Equal(["ADMIN", "CORE"], await InScopeAsync(db => db.AppModules.OrderBy(m => m.Code).Select(m => m.Code).ToListAsync()));
        Assert.Equal(1, await InScopeAsync(db => db.ApplicationTypeModules.CountAsync()));
        Assert.True(await InScopeAsync(db => db.AppModules.Where(m => m.Code == "CORE").Select(m => m.IsCore).SingleAsync()));
    }

    [Fact]
    public async Task Seeding_twice_adds_nothing()
    {
        await SeedApplicationsAsync();
        await SeedApplicationsAsync();

        Assert.Equal(1, await InScopeAsync(db => db.ApplicationTypes.CountAsync()));
        Assert.Equal(2, await InScopeAsync(db => db.AppModules.CountAsync()));
        Assert.Equal(1, await InScopeAsync(db => db.ApplicationTypeModules.CountAsync()));
    }

    [Fact]
    public async Task Seeding_corrects_a_changed_row_in_place()
    {
        await SeedApplicationsAsync();
        await InScopeAsync(async db =>
        {
            var management = await db.ApplicationTypes.SingleAsync();
            management.Name = "Renamed";
            await db.SaveChangesAsync();
            return 0;
        });

        await SeedApplicationsAsync();

        Assert.Equal("Management", await InScopeAsync(db => db.ApplicationTypes.Select(a => a.Name).SingleAsync()));
    }
}