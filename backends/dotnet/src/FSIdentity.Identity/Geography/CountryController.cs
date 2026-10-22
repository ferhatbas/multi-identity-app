using FSIdentity.Domain.Controllers;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Security;
using FSIdentity.Domain.Entities.Geography;

namespace FSIdentity.Identity.Geography;

public class CountryController(ICrudService<Country, CountryRead, CountryCreate, CountryUpdate> service)
    : CrudController<Country, CountryRead, CountryCreate, CountryUpdate>(service, "Country", "Countries")
{
    protected override string WritePermission => Permissions.GeographyManage;
}