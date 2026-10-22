using FSIdentity.Domain.Common;
using FSIdentity.Domain.Controllers;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Interfaces.MultiTenant;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Security;

public class SecurityPolicyController(ICrudService<SecurityPolicy, SecurityPolicyRead, SecurityPolicyCreate, SecurityPolicyUpdate> service)
    : CrudController<SecurityPolicy, SecurityPolicyRead, SecurityPolicyCreate, SecurityPolicyUpdate>(
        service, "SecurityPolicy", "SecurityPolicies")
{
    protected override string WritePermission => Permissions.SecurityManage;

    protected override string? ReadPermission => Permissions.SecurityManage;
}

public class SoDRuleController(ICrudService<SoDRule, SoDRuleRead, SoDRuleCreate, SoDRuleUpdate> service)
    : CrudController<SoDRule, SoDRuleRead, SoDRuleCreate, SoDRuleUpdate>(service, "SoDRule", "SoDRules")
{
    protected override string WritePermission => Permissions.SecurityManage;

    protected override string? ReadPermission => Permissions.SecurityManage;
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SecurityController(ISecurityPolicyService policies) : ControllerBase
{
    [HttpGet("effective-policy")]
    [AccountSelfService]
    public async Task<ActionResult<ApiResponse<EffectivePolicy>>> EffectivePolicy(CancellationToken cancellationToken)
        => Ok(ApiResponse<EffectivePolicy>.Success(
            await policies.ForTenantAsync(User.TenantId(), cancellationToken), "Effective policy retrieved"));

    [HttpGet("status")]
    [AccountSelfService]
    public async Task<ActionResult<ApiResponse<SecurityStatus>>> Status(CancellationToken cancellationToken)
        => Ok(ApiResponse<SecurityStatus>.Success(new SecurityStatus
        {
            Policy = await policies.ForTenantAsync(User.TenantId(), cancellationToken),
            MustChangePassword = User.MustChangePassword(),
            MfaSetupRequired = User.MfaSetupRequired(),
            EmailConfirmationRequired = User.EmailConfirmationRequired(),
        }, "Security status retrieved"));
}

public class SecurityPolicyRules(ICallerAccess caller, IMultiTenantAccessor current, IUnitOfWork uow)
    : CrudRules<SecurityPolicy, SecurityPolicyCreate, SecurityPolicyUpdate>
{
    public override async Task OnCreateAsync(SecurityPolicyCreate create, SecurityPolicy entity, CancellationToken cancellationToken)
    {
        if (current.TenantId is null)
        {
            if (await caller.RankAsync(cancellationToken) != UserType.PlatformAdmin)
                throw new ForbiddenException("Only a platform administrator may set the platform's own policy");

            if (create.TenantId is { } tenantId)
            {
                var exists = await uow.Repository<FSIdentity.Domain.Entities.Organization.Tenant>().Query()
                    .AnyAsync(t => t.Id == tenantId, cancellationToken);
                if (!exists)
                    throw new BadRequestException($"Tenant {tenantId} does not exist");
                entity.IdentityTenantId = tenantId;
            }
        }
        else if (create.TenantId is not null && create.TenantId != current.TenantId)
        {
            throw new ForbiddenException("A policy can only be set for your own tenant");
        }

        Validate(create);
    }

    public override async Task OnUpdateAsync(SecurityPolicy entity, SecurityPolicyUpdate update, CancellationToken cancellationToken)
    {
        if (entity.IdentityTenantId is null && await caller.RankAsync(cancellationToken) != UserType.PlatformAdmin)
            throw new ForbiddenException("Only a platform administrator may change the platform's own policy");

        Validate(update);
    }

    public override async Task OnDeleteAsync(SecurityPolicy entity, CancellationToken cancellationToken)
    {
        if (entity.IdentityTenantId is null && await caller.RankAsync(cancellationToken) != UserType.PlatformAdmin)
            throw new ForbiddenException("Only a platform administrator may delete the platform's own policy");
    }

    private static void Validate(SecurityPolicyCreate policy)
    {
        if (policy.RequireMfaFromRank is { } rank && rank < UserType.Standard)
            throw new BadRequestException("requireMfaFromRank must be Standard or higher");
    }
}

public class SoDRuleRules(ISoDGuard guard) : CrudRules<SoDRule, SoDRuleCreate, SoDRuleUpdate>
{
    public override async Task OnCreateAsync(SoDRuleCreate create, SoDRule entity, CancellationToken cancellationToken)
    {
        Check(create);
        await guard.EnsureNoRoleBreaksAsync(create.PermissionAId, create.PermissionBId, cancellationToken);
    }

    public override async Task OnUpdateAsync(SoDRule entity, SoDRuleUpdate update, CancellationToken cancellationToken)
    {
        Check(update);
        await guard.EnsureNoRoleBreaksAsync(update.PermissionAId, update.PermissionBId, cancellationToken);
    }

    private static void Check(SoDRuleCreate rule)
    {
        if (rule.PermissionAId == rule.PermissionBId)
            throw new BadRequestException("A rule needs two different permissions");
    }
}
