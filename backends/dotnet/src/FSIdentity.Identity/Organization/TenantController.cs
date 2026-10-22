using FSIdentity.Domain.Controllers;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Security;
using FSIdentity.Domain.Entities.Organization;

namespace FSIdentity.Identity.Organization;

public class TenantController(ICrudService<Tenant, TenantRead, TenantCreate, TenantUpdate> service)
    : CrudController<Tenant, TenantRead, TenantCreate, TenantUpdate>(service, "Tenant", "Tenants")
{
    protected override string WritePermission => Permissions.TenantManage;

    protected override string? ReadPermission => Permissions.TenantView;
}
