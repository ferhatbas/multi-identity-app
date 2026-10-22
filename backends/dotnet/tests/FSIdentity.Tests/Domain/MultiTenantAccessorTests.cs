using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using FSIdentity.Domain.Security;
using FSIdentity.Domain.Services;

namespace FSIdentity.Tests.Domain;

public class MultiTenantAccessorTests
{
    private static MultiTenantAccessor Create(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "test");
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return new MultiTenantAccessor(http);
    }

    [Fact]
    public void Reads_Ids_From_Claims()
    {
        var accessor = Create(
            new Claim(ClaimNames.UserId, "10"),
            new Claim(ClaimNames.TenantId, "3"),
            new Claim(ClaimNames.ActiveCompanyId, "7"));

        Assert.Equal(10, accessor.UserId);
        Assert.Equal(3, accessor.TenantId);
        Assert.Equal(7, accessor.CompanyId);
    }

    [Fact]
    public void Missing_Claims_Give_Null()
    {
        var accessor = Create();

        Assert.Null(accessor.UserId);
        Assert.Null(accessor.TenantId);
        Assert.Null(accessor.CompanyId);
    }

    [Fact]
    public void Invalid_Number_Gives_Null()
    {
        var accessor = Create(new Claim(ClaimNames.ActiveCompanyId, "abc"));

        Assert.Null(accessor.CompanyId);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    public void SuperAdmin_Claim_Enables_Bypass(string value)
    {
        var accessor = Create(new Claim(ClaimNames.IsSuperAdmin, value));

        Assert.True(accessor.Bypass);
    }

    [Fact]
    public void Without_SuperAdmin_Claim_Bypass_Is_False()
    {
        var accessor = Create(new Claim(ClaimNames.TenantId, "1"));

        Assert.False(accessor.Bypass);
        Assert.False(accessor.BypassCompany);
        Assert.False(accessor.BypassTenant);
        Assert.False(accessor.BypassSoftDelete);
    }

    [Fact]
    public void No_HttpContext_Gives_Null_And_No_Bypass()
    {
        var accessor = new MultiTenantAccessor(new HttpContextAccessor());

        Assert.Null(accessor.TenantId);
        Assert.False(accessor.Bypass);
    }

    [Fact]
    public void Explicit_Bypass_Value_Is_Not_Overwritten_By_Token()
    {
        var accessor = Create(new Claim(ClaimNames.IsSuperAdmin, "true"));

        accessor.Bypass = false;

        Assert.False(accessor.Bypass);
    }
}