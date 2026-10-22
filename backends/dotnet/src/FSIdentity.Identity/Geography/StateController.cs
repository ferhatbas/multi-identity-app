using FSIdentity.Domain.Controllers;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Security;
using FSIdentity.Domain.Entities.Geography;

namespace FSIdentity.Identity.Geography;

public class StateController(ICrudService<State, StateRead, StateCreate, StateUpdate> service)
    : CrudController<State, StateRead, StateCreate, StateUpdate>(service, "State", "States")
{
    protected override string WritePermission => Permissions.GeographyManage;
}