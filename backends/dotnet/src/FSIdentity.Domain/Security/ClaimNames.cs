namespace FSIdentity.Domain.Security;

public static class ClaimNames
{
    public const string UserId = "UserId";
    public const string UserName = "UserName";
    public const string TenantId = "TenantId";
    public const string ActiveCompanyId = "ActiveCompanyId";
    public const string ActiveApplicationTypeId = "ActiveApplicationTypeId";
    public const string IsSuperAdmin = "IsSuperAdmin";

    public const string SessionId = "sid";

    public const string MustChangePassword = "MustChangePassword";

    public const string MfaSetupRequired = "MfaSetupRequired";

    public const string EmailConfirmationRequired = "EmailConfirmationRequired";

    public const string ImpersonatorId = "ImpersonatorId";  
}