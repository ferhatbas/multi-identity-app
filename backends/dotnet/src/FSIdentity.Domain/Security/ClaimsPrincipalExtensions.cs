using System.Security.Claims;

namespace FSIdentity.Domain.Security;

public static class ClaimsPrincipalExtensions
{
    public static long? UserId(this ClaimsPrincipal user) => Number(user, ClaimNames.UserId);

    public static long? SessionId(this ClaimsPrincipal user) => Number(user, ClaimNames.SessionId);

    public static long? ImpersonatorId(this ClaimsPrincipal user) => Number(user, ClaimNames.ImpersonatorId);

    public static long? TenantId(this ClaimsPrincipal user) => Number(user, ClaimNames.TenantId);

    public static long? ActiveCompanyId(this ClaimsPrincipal user) => Number(user, ClaimNames.ActiveCompanyId);

    public static long? ActiveApplicationTypeId(this ClaimsPrincipal user) => Number(user, ClaimNames.ActiveApplicationTypeId);

    public static bool IsSuperAdmin(this ClaimsPrincipal user)
        => bool.TryParse(user.FindFirst(ClaimNames.IsSuperAdmin)?.Value, out var value) && value;

    public static bool MfaSetupRequired(this ClaimsPrincipal user)
        => bool.TryParse(user.FindFirst(ClaimNames.MfaSetupRequired)?.Value, out var value) && value;

    public static bool EmailConfirmationRequired(this ClaimsPrincipal user)
        => bool.TryParse(user.FindFirst(ClaimNames.EmailConfirmationRequired)?.Value, out var value) && value;

    public static bool MustChangePassword(this ClaimsPrincipal user)
        => bool.TryParse(user.FindFirst(ClaimNames.MustChangePassword)?.Value, out var value) && value;

    private static long? Number(ClaimsPrincipal user, string claimType)
        => long.TryParse(user.FindFirst(claimType)?.Value, out var value) ? value : null;
}
