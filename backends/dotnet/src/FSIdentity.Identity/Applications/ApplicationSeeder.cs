using FSIdentity.Domain.Entities.Applications;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Applications;

public class ApplicationSeeder(AppDbContext db)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var management = await EnsureApplicationAsync(
            ApplicationType.ManagementCode, "Management", "The vendor's back office", 0, cancellationToken);

        await EnsureModuleAsync(AppModuleCodes.Core, "Core", "Dashboard, account and notifications", 0, isCore: true, cancellationToken);
        var admin = await EnsureModuleAsync(AppModuleCodes.Admin, "Administration", "Users, roles and organisation", 1, isCore: false, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var granted = await db.Set<ApplicationTypeModule>()
            .AnyAsync(g => g.ApplicationTypeId == management.Id && g.AppModuleId == admin.Id, cancellationToken);
        if (!granted)
        {
            db.Add(new ApplicationTypeModule { ApplicationTypeId = management.Id, AppModuleId = admin.Id });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ApplicationType> EnsureApplicationAsync(
        string code, string name, string description, int displayOrder, CancellationToken cancellationToken)
    {
        var application = await db.Set<ApplicationType>().FirstOrDefaultAsync(a => a.Code == code, cancellationToken);
        if (application is null)
        {
            application = new ApplicationType { Code = code };
            db.Add(application);
        }

        application.Name = name;
        application.Description = description;
        application.DisplayOrder = displayOrder;
        return application;
    }

    private async Task<AppModule> EnsureModuleAsync(
        string code, string name, string description, int displayOrder, bool isCore, CancellationToken cancellationToken)
    {
        var module = await db.Set<AppModule>().FirstOrDefaultAsync(m => m.Code == code, cancellationToken);
        if (module is null)
        {
            module = new AppModule { Code = code };
            db.Add(module);
        }

        module.Name = name;
        module.Description = description;
        module.DisplayOrder = displayOrder;
        module.IsCore = isCore;
        return module;
    }
}