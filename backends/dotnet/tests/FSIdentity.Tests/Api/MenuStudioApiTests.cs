using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FSIdentity.Tests.Api;

public class MenuStudioApiTests : ApiTestBase
{
    private long _tenantId;
    private long _companyId;
    private long _typeId;
    private long _applicationId;

    private async Task SetUpAsync()
    {
        _tenantId = await CreateTenantAsync();
        _typeId = await CreateCompanyTypeAsync("CLINIC");
        _companyId = await PostIdAsync("/api/Company", new { name = "HQ", code = "HQ", tenantId = _tenantId, companyTypeId = _typeId });
        _applicationId = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });
    }

    private Task<long> MenuAsync(string code, int order = 0, long? permissionId = null)
        => PostIdAsync("/api/Menu", new { code, title = code, path = $"/{code.ToLowerInvariant()}", displayOrder = order, permissionId });

    private async Task<string[]> MineCodesAsync(HttpClient client)
        => (await Read(await client.GetAsync("/api/Navigation/mine"))).GetProperty("data").EnumerateArray().Select(n => n.GetProperty("code").GetString()!).ToArray();

    private static object Draft(long applicationId, long? companyTypeId, params object[] changes) => new { applicationTypeId = applicationId, companyTypeId, note = "test", changes };

    [Fact]
    public async Task A_published_draft_changes_what_the_company_type_sees_and_a_bad_draft_changes_nothing()
    {
        await SetUpAsync();
        var a = await MenuAsync("A", 10);
        var b = await MenuAsync("B", 20);
        await CreateSignInUserAsync("ada", _tenantId, _companyId);
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);
        Assert.Equal(["A", "B"], await MineCodesAsync(ada));

        var impact = await Client.PostAsJsonAsync("/api/MenuStudio/impact", Draft(_applicationId, _typeId, new { kind = "hide", menuId = a }));
        Assert.Equal(HttpStatusCode.OK, impact.StatusCode);

        var published = await Client.PostAsJsonAsync("/api/MenuStudio/publish", Draft(_applicationId, _typeId,
            new { kind = "hide", menuId = a }, new { kind = "order", menuId = b, order = 5 }, new { kind = "title", menuId = b, languageCode = "tr", title = "Be" }));
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal(["B"], await MineCodesAsync(ada));

        var bad = await Client.PostAsJsonAsync("/api/MenuStudio/publish", Draft(_applicationId, _typeId,
            new { kind = "show", menuId = a }, new { kind = "hide", menuId = 99999 }));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(["B"], await MineCodesAsync(ada));

        var reset = await Client.PostAsJsonAsync("/api/MenuStudio/publish", Draft(_applicationId, _typeId, new { kind = "reset", menuId = a }, new { kind = "reset", menuId = b }));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(["A", "B"], await MineCodesAsync(ada));
    }

    [Fact]
    public async Task A_limit_in_a_draft_keeps_an_entry_to_its_company_types_and_the_publish_is_audited()
    {
        await SetUpAsync();
        var other = await CreateCompanyTypeAsync("OTHER");
        var a = await MenuAsync("A");
        await CreateSignInUserAsync("ada", _tenantId, _companyId);
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        await Client.PostAsJsonAsync("/api/MenuStudio/publish", Draft(_applicationId, null, new { kind = "limit", menuId = a, companyTypeId = other, on = true }));
        Assert.Empty(await MineCodesAsync(ada));

        await Client.PostAsJsonAsync("/api/MenuStudio/publish", Draft(_applicationId, null, new { kind = "limit", menuId = a, companyTypeId = _typeId, on = true }));
        Assert.Equal(["A"], await MineCodesAsync(ada));

        var audit = (await Read(await Client.GetAsync("/api/AuditLog/paged?entityName=MenuStudio"))).GetProperty("data").GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(2, audit.Count(r => r.GetProperty("action").GetString() == "Publish"));
    }

    [Fact]
    public async Task The_preview_through_a_role_gives_the_menu_that_role_really_gets()
    {
        await SetUpAsync();
        var secret = await CreatePermissionAsync("SECRET_VIEW");
        var seen = await CreatePermissionAsync("SEEN_VIEW");
        await MenuAsync("OPEN", 10);
        await MenuAsync("SECRET", 20, secret);
        await MenuAsync("SEEN", 30, seen);

        var userId = await CreateSignInUserAsync("ada", _tenantId, _companyId);
        var roleId = await CreateRoleAsync("ada-role");
        await PostIdAsync("/api/RolePermission", new { roleId, permissionId = seen });
        await PostIdAsync("/api/UserCompanyRole", new { userId, companyId = _companyId, roleId });
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        var verdicts = (await Read(await Client.GetAsync($"/api/MenuStudio/preview?applicationTypeId={_applicationId}&companyTypeId={_typeId}&roleId={roleId}"))).GetProperty("data").EnumerateArray().ToList();
        var menus = (await Read(await Client.GetAsync("/api/Menu"))).GetProperty("data").EnumerateArray().ToDictionary(m => m.GetProperty("id").GetInt64(), m => m.GetProperty("code").GetString()!);

        var visible = verdicts.Where(v => v.GetProperty("visible").GetBoolean()).Select(v => menus[v.GetProperty("menuId").GetInt64()]).Order().ToArray();
        Assert.Equal((await MineCodesAsync(ada)).Order().ToArray(), visible);
        var blocked = verdicts.Single(v => !v.GetProperty("visible").GetBoolean());
        Assert.Equal("SECRET", menus[blocked.GetProperty("menuId").GetInt64()]);
        Assert.Equal("permission", blocked.GetProperty("reason").GetString());

        var open = (await Read(await Client.GetAsync($"/api/MenuStudio/preview?applicationTypeId={_applicationId}"))).GetProperty("data").EnumerateArray();
        Assert.All(open, v => Assert.True(v.GetProperty("visible").GetBoolean()));
    }

    [Fact]
    public async Task A_new_item_brings_its_permission_its_role_grants_and_its_company_types_in_one_step()
    {
        await SetUpAsync();
        var userId = await CreateSignInUserAsync("ada", _tenantId, _companyId);
        var roleId = await CreateRoleAsync("ada-role");
        await PostIdAsync("/api/UserCompanyRole", new { userId, companyId = _companyId, roleId });
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);
        Assert.Empty(await MineCodesAsync(ada));

        var made = await Client.PostAsJsonAsync("/api/MenuStudio/items", new
        {
            titleEn = "Reports", titleTr = "Raporlar", route = "/admin/reports/:id", icon = "chart", permissionCode = "report_view",
            grantToRoleIds = new[] { roleId }, companyTypeIds = new[] { _typeId },
        });
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        var item = (await Read(made)).GetProperty("data");
        Assert.Equal("ADMIN_REPORTS", item.GetProperty("code").GetString());
        Assert.True(item.GetProperty("permissionCreated").GetBoolean());
        Assert.Equal(["ADMIN_REPORTS"], await MineCodesAsync(ada));

        var again = (await Read(await Client.PostAsJsonAsync("/api/MenuStudio/items", new { titleEn = "Reports 2", route = "/admin/reports", permissionCode = "REPORT_VIEW" }))).GetProperty("data");
        Assert.Equal("ADMIN_REPORTS_2", again.GetProperty("code").GetString());
        Assert.False(again.GetProperty("permissionCreated").GetBoolean());
    }

    [Fact]
    public async Task A_bad_new_item_writes_nothing()
    {
        await SetUpAsync();
        var before = (await Read(await Client.GetAsync("/api/Menu"))).GetProperty("data").GetArrayLength();

        var noSlash = await Client.PostAsJsonAsync("/api/MenuStudio/items", new { titleEn = "X", route = "admin/x" });
        var badRole = await Client.PostAsJsonAsync("/api/MenuStudio/items", new { titleEn = "X", route = "/x", permissionCode = "X_VIEW", grantToRoleIds = new[] { 99999 } });
        var rolesWithoutPermission = await Client.PostAsJsonAsync("/api/MenuStudio/items", new { titleEn = "X", route = "/x", grantToRoleIds = new[] { 1 } });

        Assert.Equal(HttpStatusCode.BadRequest, noSlash.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badRole.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, rolesWithoutPermission.StatusCode);
        Assert.Equal(before, (await Read(await Client.GetAsync("/api/Menu"))).GetProperty("data").GetArrayLength());
        Assert.DoesNotContain((await Read(await Client.GetAsync("/api/Permission"))).GetProperty("data").EnumerateArray(), p => p.GetProperty("code").GetString() == "X_VIEW");
    }

    [Fact]
    public async Task Only_a_menu_designer_may_use_the_studio()
    {
        await SetUpAsync();
        await CreateSignInUserAsync("nobody", _tenantId, _companyId);
        using var nobody = Bearer((await SignInAsync("nobody")).GetProperty("accessToken").GetString()!);

        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.GetAsync($"/api/MenuStudio/preview?applicationTypeId={_applicationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.PostAsJsonAsync("/api/MenuStudio/publish", Draft(_applicationId, null))).StatusCode);
    }
}
