using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Authorization;
using FSIdentity.Identity.Security;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Accounts;

public static class ReservedRoleNames
{
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "administrator", "admin", "superadmin", "superuser", "root", "system", "owner",
        "platformadmin", "platformadministrator", "tenantadmin", "tenantadministrator",
        "companyadmin", "companyadministrator", "reader", "managementuser",
    };

    public static bool IsReserved(string name)
        => Reserved.Contains(new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant());
}

public class PermissionRules : CrudRules<Permission, PermissionCreate, PermissionUpdate>
{
    public override Task OnCreateAsync(PermissionCreate create, Permission entity, CancellationToken cancellationToken)
        => Task.FromResult(Validate(create.IsManagement, create.ManagementRank));

    public override Task OnUpdateAsync(Permission entity, PermissionUpdate update, CancellationToken cancellationToken)
        => Task.FromResult(Validate(update.IsManagement, update.ManagementRank));

    private static bool Validate(bool isManagement, UserType? rank)
    {
        if (isManagement && rank is not (UserType.CompanyAdmin or UserType.TenantAdmin))
            throw new BadRequestException("A management permission needs managementRank CompanyAdmin or TenantAdmin");
        if (!isManagement && rank is not null)
            throw new BadRequestException("managementRank only belongs to a management permission");
        return true;
    }
}

public class RolePermissionRules(IUnitOfWork uow, ISoDGuard sod) : CrudRules<RolePermission, RolePermissionCreate, RolePermissionUpdate>
{
    public override async Task OnCreateAsync(RolePermissionCreate create, RolePermission entity, CancellationToken cancellationToken)
    {
        await ManagementGuard.EnsureNotManagementAsync(uow, create.PermissionId, "a role", cancellationToken);
        await sod.EnsureRoleMayGainAsync(create.RoleId, create.PermissionId, cancellationToken);
    }

    public override async Task OnUpdateAsync(RolePermission entity, RolePermissionUpdate update, CancellationToken cancellationToken)
    {
        await ManagementGuard.EnsureNotManagementAsync(uow, update.PermissionId, "a role", cancellationToken);
        await sod.EnsureRoleMayGainAsync(update.RoleId, update.PermissionId, cancellationToken);
    }

    public override async Task OnRestoreAsync(RolePermission entity, CancellationToken cancellationToken)
    {
        await ManagementGuard.EnsureNotManagementAsync(uow, entity.PermissionId, "a role", cancellationToken);
        await sod.EnsureRoleMayGainAsync(entity.RoleId, entity.PermissionId, cancellationToken);
    }
}

public class UserCompanyPermissionRules(IUnitOfWork uow, IUserAdministrationGuard guard, ISoDGuard sod)
    : CrudRules<UserCompanyPermission, UserCompanyPermissionCreate, UserCompanyPermissionUpdate>
{
    public override async Task OnCreateAsync(UserCompanyPermissionCreate create, UserCompanyPermission entity, CancellationToken cancellationToken)
    {
        await ManagementGuard.EnsureNotManagementAsync(uow, create.PermissionId, "a personal grant", cancellationToken);
        await guard.EnsureCanAdministerAsync(create.UserId, allowSelf: false, cancellationToken);
        if (create.IsGranted)
            await sod.EnsureUserMayGainAsync(create.UserId, create.CompanyId, [create.PermissionId], cancellationToken);
    }

    public override async Task OnUpdateAsync(UserCompanyPermission entity, UserCompanyPermissionUpdate update, CancellationToken cancellationToken)
    {
        await ManagementGuard.EnsureNotManagementAsync(uow, update.PermissionId, "a personal grant", cancellationToken);
        await guard.EnsureCanAdministerAsync(entity.UserId, allowSelf: false, cancellationToken);
        await guard.EnsureCanAdministerAsync(update.UserId, allowSelf: false, cancellationToken);
        if (update.IsGranted)
            await sod.EnsureUserMayGainAsync(update.UserId, update.CompanyId, [update.PermissionId], cancellationToken);
    }

    public override Task OnDeleteAsync(UserCompanyPermission entity, CancellationToken cancellationToken)
        => guard.EnsureCanAdministerAsync(entity.UserId, allowSelf: false, cancellationToken);

    public override async Task OnRestoreAsync(UserCompanyPermission entity, CancellationToken cancellationToken)
    {
        await guard.EnsureCanAdministerAsync(entity.UserId, allowSelf: false, cancellationToken);
        if (entity.IsGranted)
            await sod.EnsureUserMayGainAsync(entity.UserId, entity.CompanyId, [entity.PermissionId], cancellationToken);
    }
}

public class UserCompanyRoleRules(IUserAdministrationGuard guard, ISoDGuard sod)
    : CrudRules<UserCompanyRole, UserCompanyRoleCreate, UserCompanyRoleUpdate>
{
    public override async Task OnCreateAsync(UserCompanyRoleCreate create, UserCompanyRole entity, CancellationToken cancellationToken)
    {
        await guard.EnsureCanAdministerAsync(create.UserId, allowSelf: false, cancellationToken);
        await sod.EnsureUserMayGainAsync(create.UserId, create.CompanyId, await sod.PermissionsOfRoleAsync(create.RoleId, cancellationToken), cancellationToken);
    }

    public override async Task OnUpdateAsync(UserCompanyRole entity, UserCompanyRoleUpdate update, CancellationToken cancellationToken)
    {
        await guard.EnsureCanAdministerAsync(entity.UserId, allowSelf: false, cancellationToken);
        await guard.EnsureCanAdministerAsync(update.UserId, allowSelf: false, cancellationToken);
        await sod.EnsureUserMayGainAsync(update.UserId, update.CompanyId, await sod.PermissionsOfRoleAsync(update.RoleId, cancellationToken), cancellationToken);
    }

    public override Task OnDeleteAsync(UserCompanyRole entity, CancellationToken cancellationToken)
        => guard.EnsureCanAdministerAsync(entity.UserId, allowSelf: false, cancellationToken);

    public override async Task OnRestoreAsync(UserCompanyRole entity, CancellationToken cancellationToken)
    {
        await guard.EnsureCanAdministerAsync(entity.UserId, allowSelf: false, cancellationToken);
        await sod.EnsureUserMayGainAsync(entity.UserId, entity.CompanyId, await sod.PermissionsOfRoleAsync(entity.RoleId, cancellationToken), cancellationToken);
    }
}

public class UserCompanyRules(IUserAdministrationGuard guard) : CrudRules<UserCompany, UserCompanyCreate, UserCompanyUpdate>
{
    public override async Task OnCreateAsync(UserCompanyCreate create, UserCompany entity, CancellationToken cancellationToken)
    {
        await guard.EnsureCanGrantAsync(create.MembershipType, cancellationToken);
        await guard.EnsureCanAdministerAsync(create.UserId, allowSelf: false, cancellationToken);
    }

    public override async Task OnUpdateAsync(UserCompany entity, UserCompanyUpdate update, CancellationToken cancellationToken)
    {
        await guard.EnsureCanGrantAsync(update.MembershipType, cancellationToken);
        await guard.EnsureCanAdministerAsync(entity.UserId, allowSelf: false, cancellationToken);
        await guard.EnsureCanAdministerAsync(update.UserId, allowSelf: false, cancellationToken);
    }

    public override Task OnDeleteAsync(UserCompany entity, CancellationToken cancellationToken)
        => guard.EnsureCanAdministerAsync(entity.UserId, allowSelf: false, cancellationToken);

    public override async Task OnRestoreAsync(UserCompany entity, CancellationToken cancellationToken)
    {
        await guard.EnsureCanGrantAsync(entity.MembershipType, cancellationToken);
        await guard.EnsureCanAdministerAsync(entity.UserId, allowSelf: false, cancellationToken);
    }
}

public class RoleRules(ICallerAccess caller) : CrudRules<Role, RoleCreate, RoleUpdate>
{
    public override Task OnCreateAsync(RoleCreate create, Role entity, CancellationToken cancellationToken)
        => EnsureNameAllowedAsync(create.Name, cancellationToken);

    public override async Task OnUpdateAsync(Role entity, RoleUpdate update, CancellationToken cancellationToken)
    {
        if (!string.Equals(entity.Name, update.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            await EnsureNameAllowedAsync(update.Name, cancellationToken);
    }

    private async Task EnsureNameAllowedAsync(string name, CancellationToken cancellationToken)
    {
        if (ReservedRoleNames.IsReserved(name) && await caller.RankAsync(cancellationToken) != UserType.PlatformAdmin)
            throw new BadRequestException($"'{name.Trim()}' is a reserved role name");
    }
}

internal static class ManagementGuard
{
    public static async Task EnsureNotManagementAsync(IUnitOfWork uow, long permissionId, string by, CancellationToken cancellationToken)
    {
        var isManagement = await uow.Repository<Permission>().Query()
            .Where(p => p.Id == permissionId).Select(p => (bool?)p.IsManagement).FirstOrDefaultAsync(cancellationToken);

        if (isManagement == true)
            throw new ForbiddenException($"Management permissions come with the user type, never through {by}");
    }
}
