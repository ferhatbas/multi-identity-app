using FSIdentity.Domain.Controllers;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Security;
using FSIdentity.Domain.Entities.Organization;

namespace FSIdentity.Identity.Organization;

public class CompanyTypeController(ICrudService<CompanyType, CompanyTypeRead, CompanyTypeCreate, CompanyTypeUpdate> service)
    : CrudController<CompanyType, CompanyTypeRead, CompanyTypeCreate, CompanyTypeUpdate>(service, "CompanyType", "CompanyTypes")
{
    protected override string WritePermission => Permissions.CompanyTypeManage;
}
