using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Organization;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Interfaces.MultiTenant;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Organization;

public class CompanyRules(IUnitOfWork uow, IMultiTenantAccessor mtx) : CrudRules<Company, CompanyCreate, CompanyUpdate>
{
    public override async Task OnCreateAsync(CompanyCreate create, Company entity, CancellationToken cancellationToken)
    {
        var activeTenant = mtx.TenantId;

        if (activeTenant is not null && create.TenantId is not null && create.TenantId != activeTenant)
            throw new ForbiddenException("A company can only be created inside your own tenant");

        var tenantId = activeTenant ?? create.TenantId
            ?? throw new BadRequestException("tenantId is required when no tenant is active");

        var tenantExists = await uow.Repository<Tenant>().Query()
            .AnyAsync(t => t.Id == tenantId, cancellationToken);
        if (!tenantExists)
            throw new BadRequestException($"Tenant {tenantId} does not exist");

        entity.IdentityTenantId = tenantId;
    }
}
