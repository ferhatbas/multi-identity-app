namespace FSIdentity.Domain.Security;

public static class Permissions
{
    public const string GeographyManage = "GEOGRAPHY_MANAGE";
    public const string LanguageManage = "LANGUAGE_MANAGE";
    public const string TenantView = "TENANT_VIEW";
    public const string TenantManage = "TENANT_MANAGE";
    public const string CompanyManage = "COMPANY_MANAGE";
    public const string CompanyTypeManage = "COMPANYTYPE_MANAGE";
    public const string ApplicationTypeManage = "APPTYPE_MANAGE";
    public const string UserView = "USER_VIEW";
    public const string UserManage = "USER_MANAGE";
    public const string RoleView = "ROLE_VIEW";
    public const string RoleManage = "ROLE_MANAGE";
    public const string PermissionManage = "PERMISSION_MANAGE";
    public const string AuditLogView = "AUDIT_LOG_VIEW";
    public const string SecurityManage = "SECURITY_MANAGE";
    public const string MenuDesign = "MENU_DESIGN";
    public const string MenuManage = "MENU_MANAGE";
    public const string DashboardManage = "DASHBOARD_MANAGE";
    public const string UnitManage = "UNIT_MANAGE";
    public const string SettingsManage = "SETTINGS_MANAGE";
    public const string SessionManage = "SESSION_MANAGE";
    public const string Impersonate = "IMPERSONATE";

    public static IReadOnlyList<string> All { get; } = typeof(Permissions)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f is { IsLiteral: true } && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();
}
