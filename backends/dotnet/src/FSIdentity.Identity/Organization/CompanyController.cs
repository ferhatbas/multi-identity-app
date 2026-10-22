using FSIdentity.Domain.Controllers;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Security;
using FSIdentity.Domain.Entities.Organization;

namespace FSIdentity.Identity.Organization;

public class CompanyController(ICrudService<Company, CompanyRead, CompanyCreate, CompanyUpdate> service)
    : CrudController<Company, CompanyRead, CompanyCreate, CompanyUpdate>(service, "Company", "Companies")
{
    protected override string WritePermission => Permissions.CompanyManage;
}
