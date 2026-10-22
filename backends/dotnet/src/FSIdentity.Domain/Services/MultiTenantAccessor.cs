using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using FSIdentity.Domain.Interfaces.MultiTenant;
using FSIdentity.Domain.Security;

namespace FSIdentity.Domain.Services;

public class MultiTenantAccessor : IMultiTenantAccessor
{
    private readonly IHttpContextAccessor _http;

    private bool _initialized;
    private long? _userId;
    private long? _tenantId;
    private long? _companyId;
    private bool _bypass;
    private bool _bypassCompany;
    private bool _bypassTenant;
    private bool _bypassSoftDelete;

    public MultiTenantAccessor(IHttpContextAccessor http)
    {
        _http = http;
    }

    public long? UserId { get { EnsureInitialized(); return _userId; } }
    public long? TenantId { get { EnsureInitialized(); return _tenantId; } }
    public long? CompanyId { get { EnsureInitialized(); return _companyId; } }

    public bool Bypass
    {
        get { EnsureInitialized(); return _bypass; }
        set { EnsureInitialized(); _bypass = value; }
    }

    public bool BypassCompany
    {
        get { EnsureInitialized(); return _bypassCompany; }
        set { EnsureInitialized(); _bypassCompany = value; }
    }

    public bool BypassTenant
    {
        get { EnsureInitialized(); return _bypassTenant; }
        set { EnsureInitialized(); _bypassTenant = value; }
    }

    public bool BypassSoftDelete
    {
        get => _bypassSoftDelete;
        set => _bypassSoftDelete = value;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        var user = _http.HttpContext?.User;
        if (user is null) return;

        _userId = ReadLong(user, ClaimNames.UserId);
        _tenantId = ReadLong(user, ClaimNames.TenantId);
        _companyId = ReadLong(user, ClaimNames.ActiveCompanyId);

        _bypass = bool.TryParse(user.FindFirst(ClaimNames.IsSuperAdmin)?.Value, out var isSuperAdmin)
                  && isSuperAdmin;
    }

    private static long? ReadLong(ClaimsPrincipal user, string claimType)
        => long.TryParse(user.FindFirst(claimType)?.Value, out var value) ? value : null;
}