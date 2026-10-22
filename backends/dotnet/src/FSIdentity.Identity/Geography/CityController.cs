using FSIdentity.Domain.Controllers;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Security;
using FSIdentity.Domain.Entities.Geography;

namespace FSIdentity.Identity.Geography;

public class CityController(ICrudService<City, CityRead, CityCreate, CityUpdate> service)
    : CrudController<City, CityRead, CityCreate, CityUpdate>(service, "City", "Cities")
{
    protected override string WritePermission => Permissions.GeographyManage;
}