using FSIdentity.Domain.Controllers;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Security;
using FSIdentity.Domain.Entities.Localization;

namespace FSIdentity.Identity.Localization;

public class LanguageController(ICrudService<Language, LanguageRead, LanguageCreate, LanguageUpdate> service)
    : CrudController<Language, LanguageRead, LanguageCreate, LanguageUpdate>(service, "Language", "Languages")
{
    protected override string WritePermission => Permissions.LanguageManage;
}