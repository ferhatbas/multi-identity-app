using System.Security.Claims;
using FSIdentity.Domain.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace FSIdentity.Tests.Api;

internal sealed class TestIdentityStartupFilter : IStartupFilter
{
    public const string UserIdHeader = "X-Test-UserId";
    public const string TenantIdHeader = "X-Test-TenantId";
    public const string CompanyIdHeader = "X-Test-CompanyId";
    public const string ApplicationTypeIdHeader = "X-Test-ApplicationTypeId";
    public const string SuperAdminHeader = "X-Test-SuperAdmin";

    public const string PermissionsHeader = "X-Test-Permissions";

    public const string PermissionsClaim = "TestPermissions";

    public const string RankHeader = "X-Test-Rank";

    public const string RankClaim = "TestRank";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            var claims = new List<Claim>();
            AddClaim(context, claims, UserIdHeader, ClaimNames.UserId);
            AddClaim(context, claims, TenantIdHeader, ClaimNames.TenantId);
            AddClaim(context, claims, CompanyIdHeader, ClaimNames.ActiveCompanyId);
            AddClaim(context, claims, ApplicationTypeIdHeader, ClaimNames.ActiveApplicationTypeId);
            AddClaim(context, claims, SuperAdminHeader, ClaimNames.IsSuperAdmin);

            if (claims.Count > 0)
            {
                claims.Add(new Claim(PermissionsClaim,
                    context.Request.Headers.TryGetValue(PermissionsHeader, out var permissions) ? permissions.ToString() : "*"));
                claims.Add(new Claim(RankClaim,
                    context.Request.Headers.TryGetValue(RankHeader, out var rank) ? rank.ToString() : "3"));
            }

            if (claims.Count > 0)
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

            await nextMiddleware();
        });

        next(app);
    };

    private static void AddClaim(HttpContext context, List<Claim> claims, string header, string claimType)
    {
        if (context.Request.Headers.TryGetValue(header, out var value) && !string.IsNullOrEmpty(value))
            claims.Add(new Claim(claimType, value.ToString()));
    }
}
