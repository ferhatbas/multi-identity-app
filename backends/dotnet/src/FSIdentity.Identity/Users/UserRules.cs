using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Interfaces.MultiTenant;
using FSIdentity.Identity.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Users;

public class UserRules(
    IUserProvisioning provisioning, IUnitOfWork uow, IMultiTenantAccessor caller, IUserAdministrationGuard guard, TimeProvider clock)
    : CrudRules<User, UserCreate, UserUpdate>
{
    public override async Task OnCreateAsync(UserCreate create, User entity, CancellationToken cancellationToken)
    {
        var tenantId = await provisioning.ResolveTenantAsync(create.TenantId, cancellationToken);
        var created = await provisioning.NewUserAsync(create, tenantId, create.Password, create.ShouldChangePasswordOnNextLogin, cancellationToken);

        entity.IdentityTenantId = created.IdentityTenantId;
        entity.PasswordHash = created.PasswordHash;
        entity.LastPasswordChangeAt = created.LastPasswordChangeAt;
    }

    public override async Task OnUpdateAsync(User entity, UserUpdate update, CancellationToken cancellationToken)
    {
        await guard.EnsureCanAdministerAsync(entity.Id, allowSelf: true, cancellationToken);

        if (update.ActiveCompanyId is { } activeCompany
            && !await uow.Repository<UserCompany>().Query().AnyAsync(m => m.UserId == entity.Id && m.CompanyId == activeCompany && m.IsActive, cancellationToken))
            throw new BadRequestException("The active company must be one the user belongs to");

        if (update.IsActive == false && entity.IsActive)
        {
            if (entity.Id == caller.UserId)
                throw new ConflictException("You cannot deactivate your own account");

            await EndSessionsAsync(entity.Id, "user deactivated", cancellationToken);
        }
    }

    public override Task OnRestoreAsync(User entity, CancellationToken cancellationToken)
        => guard.EnsureCanAdministerAsync(entity.Id, allowSelf: false, cancellationToken);

    public override async Task OnDeleteAsync(User entity, CancellationToken cancellationToken)
    {
        await guard.EnsureCanAdministerAsync(entity.Id, allowSelf: true, cancellationToken);

        if (entity.Id == caller.UserId)
            throw new ConflictException("You cannot delete your own account");

        await EndSessionsAsync(entity.Id, "user deleted", cancellationToken);
    }

    private async Task EndSessionsAsync(long userId, string reason, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var open = await uow.Repository<UserSession>().Query()
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in open)
        {
            session.RevokedAt = now;
            session.RevokedReason = reason;
        }
    }
}
