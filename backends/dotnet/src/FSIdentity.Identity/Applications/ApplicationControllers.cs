using FSIdentity.Domain.Controllers;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Security;
using FSIdentity.Domain.Entities.Applications;

namespace FSIdentity.Identity.Applications;

public class ApplicationTypeController(
    ICrudService<ApplicationType, ApplicationTypeRead, ApplicationTypeCreate, ApplicationTypeUpdate> service)
    : CrudController<ApplicationType, ApplicationTypeRead, ApplicationTypeCreate, ApplicationTypeUpdate>(
        service, "ApplicationType", "ApplicationTypes")
{
    protected override string WritePermission => Permissions.ApplicationTypeManage;
}

public class AppModuleController(
    ICrudService<AppModule, AppModuleRead, AppModuleCreate, AppModuleUpdate> service)
    : CrudController<AppModule, AppModuleRead, AppModuleCreate, AppModuleUpdate>(
        service, "AppModule", "AppModules")
{
    protected override string WritePermission => Permissions.ApplicationTypeManage;
}

public class CompanyTypeApplicationTypeController(
    ICrudService<CompanyTypeApplicationType, CompanyTypeApplicationTypeRead,
        CompanyTypeApplicationTypeCreate, CompanyTypeApplicationTypeUpdate> service)
    : CrudController<CompanyTypeApplicationType, CompanyTypeApplicationTypeRead,
        CompanyTypeApplicationTypeCreate, CompanyTypeApplicationTypeUpdate>(
        service, "CompanyTypeApplicationType", "CompanyTypeApplicationTypes")
{
    protected override string WritePermission => Permissions.ApplicationTypeManage;
}

public class ApplicationTypeModuleController(
    ICrudService<ApplicationTypeModule, ApplicationTypeModuleRead,
        ApplicationTypeModuleCreate, ApplicationTypeModuleUpdate> service)
    : CrudController<ApplicationTypeModule, ApplicationTypeModuleRead,
        ApplicationTypeModuleCreate, ApplicationTypeModuleUpdate>(
        service, "ApplicationTypeModule", "ApplicationTypeModules")
{
    protected override string WritePermission => Permissions.ApplicationTypeManage;
}