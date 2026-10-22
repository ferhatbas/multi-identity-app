using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FSIdentity.Domain.Security;

namespace FSIdentity.Tests.Api;

public class MenuApiTests : ApiTestBase
{
    private long _tenantId;
    private long _companyId;
    private long _typeId;

    private async Task SetUpAsync()
    {
        _tenantId = await CreateTenantAsync();
        _typeId = await CreateCompanyTypeAsync("CLINIC");
        _companyId = await PostIdAsync("/api/Company", new { name = "HQ", code = "HQ", tenantId = _tenantId, companyTypeId = _typeId });
    }

    private Task<long> MenuAsync(
        string code, string title, long? parentId = null, string? path = "/x", int order = 0,
        long? permissionId = null, string? module = null, string? feature = null, long? applicationTypeId = null)
        => PostIdAsync("/api/Menu", new
        {
            code, title, parentId, path, displayOrder = order, permissionId, moduleCode = module, featureCode = feature, applicationTypeId,
        });

    private async Task<HttpClient> MemberWithAsync(string userName = "ada", params string[] permissionCodes)
    {
        var userId = await CreateSignInUserAsync(userName, _tenantId, _companyId);
        if (permissionCodes.Length > 0)
        {
            var roleId = await CreateRoleAsync($"{userName}-role");
            foreach (var code in permissionCodes)
                await PostIdAsync("/api/RolePermission", new { roleId, permissionId = await PermissionIdAsync(code) });
            await PostIdAsync("/api/UserCompanyRole", new { userId, companyId = _companyId, roleId });
        }

        return Bearer((await SignInAsync(userName)).GetProperty("accessToken").GetString()!);
    }

    private async Task<long> PermissionIdAsync(string code)
    {
        var all = (await Read(await Client.GetAsync("/api/Permission"))).GetProperty("data").EnumerateArray();
        var existing = all.FirstOrDefault(p => p.GetProperty("code").GetString() == code);
        return existing.ValueKind == JsonValueKind.Undefined ? await CreatePermissionAsync(code) : existing.GetProperty("id").GetInt64();
    }

    private static string[] Codes(JsonElement tree)
        => tree.EnumerateArray().SelectMany(Flatten).ToArray();

    private static IEnumerable<string> Flatten(JsonElement node)
        => new[] { node.GetProperty("code").GetString()! }.Concat(node.GetProperty("children").EnumerateArray().SelectMany(Flatten));

    private async Task<JsonElement> MineAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/Navigation/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Read(response)).GetProperty("data");
    }

    [Fact]
    public async Task A_menu_code_is_upper_case_and_unique_and_needs_a_title()
    {
        var created = await Client.PostAsJsonAsync("/api/Menu", new { code = " dash ", title = "Dashboard", path = "/dashboard" });
        var again = await Client.PostAsJsonAsync("/api/Menu", new { code = "DASH", title = "Other" });
        var untitled = await Client.PostAsJsonAsync("/api/Menu", new { code = "X" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("DASH", (await Read(created)).GetProperty("data").GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, untitled.StatusCode);
    }

    [Fact]
    public async Task The_parent_must_exist_and_an_entry_cannot_be_its_own_ancestor()
    {
        var root = await MenuAsync("ROOT", "Root", path: null);
        var child = await MenuAsync("CHILD", "Child", root);
        var grandchild = await MenuAsync("GRAND", "Grand", child);

        var orphan = await Client.PostAsJsonAsync("/api/Menu", new { code = "O", title = "O", parentId = 999 });
        var self = await Client.PutAsJsonAsync($"/api/Menu/{child}", new { code = "CHILD", title = "Child", parentId = child });
        var cycle = await Client.PutAsJsonAsync($"/api/Menu/{root}", new { code = "ROOT", title = "Root", parentId = grandchild });
        var move = await Client.PutAsJsonAsync($"/api/Menu/{grandchild}", new { code = "GRAND", title = "Grand", parentId = root });

        Assert.Equal(HttpStatusCode.BadRequest, orphan.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
    }

    [Fact]
    public async Task An_entry_cannot_belong_to_a_different_application_than_its_parent()
    {
        var hospital = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });
        var lab = await PostIdAsync("/api/ApplicationType", new { name = "Lab", code = "LAB" });
        var root = await MenuAsync("ROOT", "Root", path: null, applicationTypeId: hospital);

        var contradiction = await Client.PostAsJsonAsync("/api/Menu", new { code = "C", title = "C", parentId = root, applicationTypeId = lab });
        var shared = await Client.PostAsJsonAsync("/api/Menu", new { code = "S", title = "S", parentId = root });
        var same = await Client.PostAsJsonAsync("/api/Menu", new { code = "H", title = "H", parentId = root, applicationTypeId = hospital });

        Assert.Equal(HttpStatusCode.BadRequest, contradiction.StatusCode);
        Assert.Equal(HttpStatusCode.Created, shared.StatusCode);
        Assert.Equal(HttpStatusCode.Created, same.StatusCode);
    }

    [Fact]
    public async Task An_entry_with_children_cannot_be_deleted_and_a_translation_per_language_is_unique()
    {
        var root = await MenuAsync("ROOT", "Root", path: null);
        var child = await MenuAsync("CHILD", "Child", root);
        await PostIdAsync("/api/MenuLocalization", new { menuId = child, languageCode = "TR", title = "Çocuk" });

        var again = await Client.PostAsJsonAsync("/api/MenuLocalization", new { menuId = child, languageCode = "tr", title = "Başka" });

        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/Menu/{root}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/Menu/{child}")).StatusCode);
    }

    [Fact]
    public async Task The_definition_is_the_products_and_a_tenant_may_only_switch_and_replace()
    {
        using var tenantAdmin = ClientFor(tenantId: 1, userId: 5, permissions: Permissions.MenuManage);
        using var designer = ClientFor(userId: 6, permissions: Permissions.MenuDesign);
        var menu = await MenuAsync("DASH", "Dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, (await tenantAdmin.PostAsJsonAsync("/api/Menu", new { code = "X", title = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenantAdmin.GetAsync("/api/Menu")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenantAdmin.GetAsync("/api/Navigation/tree")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await designer.PostAsJsonAsync("/api/Menu", new { code = "X", title = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tenantAdmin.GetAsync("/api/MenuFeatureToggle")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await designer.GetAsync("/api/MenuFeatureToggle")).StatusCode);
        _ = menu;
    }

    [Fact]
    public async Task The_menu_is_a_tree_in_order_and_needs_a_signed_in_user()
    {
        await SetUpAsync();
        var root = await MenuAsync("ROOT", "Root", path: null, order: 1);
        await MenuAsync("B", "Second", root, order: 20);
        await MenuAsync("A", "First", root, order: 10);
        await MenuAsync("TOP", "Top", order: 0);
        using var user = await MemberWithAsync();

        var tree = await MineAsync(user);

        Assert.Equal(["TOP", "ROOT", "A", "B"], Codes(tree));
        Assert.Equal("First", tree[1].GetProperty("children")[0].GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous.GetAsync("/api/Navigation/mine")).StatusCode);
    }

    [Fact]
    public async Task An_entry_needing_a_permission_shows_only_to_those_who_have_it()
    {
        await SetUpAsync();
        var permission = await PermissionIdAsync("REPORTS_VIEW");
        await MenuAsync("OPEN", "Open");
        await MenuAsync("REPORTS", "Reports", permissionId: permission);
        using var without = await MemberWithAsync("plain");
        using var with = await MemberWithAsync("analyst", "REPORTS_VIEW");
        using var platform = ClientFor(userId: 9, superAdmin: true);

        Assert.Equal(["OPEN"], Codes(await MineAsync(without)));
        Assert.Equal(["OPEN", "REPORTS"], Codes(await MineAsync(with)));
        Assert.Equal(["OPEN", "REPORTS"], Codes(await MineAsync(platform)));
    }

    [Fact]
    public async Task A_group_shows_only_while_something_under_it_does()
    {
        await SetUpAsync();
        var permission = await PermissionIdAsync("REPORTS_VIEW");
        var group = await MenuAsync("GROUP", "Group", path: null);
        await MenuAsync("HIDDEN", "Hidden", group, permissionId: permission);
        var emptyGroup = await MenuAsync("EMPTY", "Empty", path: null);
        var linkWithKids = await MenuAsync("LINK", "Link", path: "/link");
        await MenuAsync("KID", "Kid", linkWithKids, permissionId: permission);
        using var plain = await MemberWithAsync("plain");
        using var analyst = await MemberWithAsync("analyst", "REPORTS_VIEW");

        Assert.Equal(["LINK"], Codes(await MineAsync(plain)));
        Assert.Equal(["GROUP", "HIDDEN", "LINK", "KID"], Codes(await MineAsync(analyst)));
        _ = emptyGroup;
    }

    [Fact]
    public async Task Children_of_a_hidden_entry_are_hidden_with_it()
    {
        await SetUpAsync();
        var permission = await PermissionIdAsync("SECRET");
        var root = await MenuAsync("ROOT", "Root", permissionId: permission);
        await MenuAsync("KID", "Kid", root);
        await MenuAsync("OPEN", "Open");
        using var plain = await MemberWithAsync("plain");

        Assert.Equal(["OPEN"], Codes(await MineAsync(plain)));
    }

    [Fact]
    public async Task An_entry_of_another_application_is_not_shown_here()
    {
        await SetUpAsync();
        var hospital = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });
        var lab = await PostIdAsync("/api/ApplicationType", new { name = "Lab", code = "LAB" });
        await PostIdAsync("/api/CompanyTypeApplicationType", new { companyTypeId = _typeId, applicationTypeId = hospital, isDefault = true });
        await PostIdAsync("/api/CompanyTypeApplicationType", new { companyTypeId = _typeId, applicationTypeId = lab });
        await MenuAsync("SHARED", "Shared");
        await MenuAsync("HOSPITAL_ONLY", "Hospital", applicationTypeId: hospital);
        await MenuAsync("LAB_ONLY", "Lab", applicationTypeId: lab);
        using var user = await MemberWithAsync();

        Assert.Equal(["SHARED", "HOSPITAL_ONLY"], Codes(await MineAsync(user)));

        var switched = await user.PostAsJsonAsync("/api/Auth/change-application", new { applicationTypeId = lab });
        using var inLab = Bearer((await Read(switched)).GetProperty("data").GetProperty("accessToken").GetString()!);
        Assert.Equal(["SHARED", "LAB_ONLY"], Codes(await MineAsync(inLab)));
    }

    [Fact]
    public async Task An_entry_limited_to_company_types_shows_only_to_those()
    {
        await SetUpAsync();
        var other = await CreateCompanyTypeAsync("OTHER");
        var limited = await MenuAsync("LIMITED", "Limited");
        await MenuAsync("EVERYONE", "Everyone");
        await PostIdAsync("/api/MenuCompanyType", new { menuId = limited, companyTypeId = other });
        using var user = await MemberWithAsync();

        Assert.Equal(["EVERYONE"], Codes(await MineAsync(user)));

        await PostIdAsync("/api/MenuCompanyType", new { menuId = limited, companyTypeId = _typeId });
        Assert.Equal(["EVERYONE", "LIMITED"], Codes(await MineAsync(user)).Order().ToArray());
    }

    [Fact]
    public async Task Layouts_hide_and_reorder_and_the_company_types_layout_beats_the_applications()
    {
        await SetUpAsync();
        var hospital = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });
        await PostIdAsync("/api/CompanyTypeApplicationType", new { companyTypeId = _typeId, applicationTypeId = hospital, isDefault = true });
        var a = await MenuAsync("A", "A", order: 1);
        var b = await MenuAsync("B", "B", order: 2);
        var c = await MenuAsync("C", "C", order: 3);
        using var user = await MemberWithAsync();
        Assert.Equal(["A", "B", "C"], Codes(await MineAsync(user)));

        await PostIdAsync("/api/MenuApplicationTypeLayout", new { applicationTypeId = hospital, menuId = a, isHidden = true });
        await PostIdAsync("/api/MenuApplicationTypeLayout", new { applicationTypeId = hospital, menuId = c, displayOrder = 0 });
        Assert.Equal(["C", "B"], Codes(await MineAsync(user)));

        await PostIdAsync("/api/MenuCompanyTypeLayout", new { companyTypeId = _typeId, menuId = a, isHidden = false });
        await PostIdAsync("/api/MenuCompanyTypeLayout", new { companyTypeId = _typeId, menuId = b, displayOrder = -1 });
        Assert.Equal(["B", "C", "A"], Codes(await MineAsync(user)));
    }

    [Fact]
    public async Task A_feature_switch_hides_entries_for_a_tenant_and_the_tenants_row_beats_the_platforms()
    {
        await SetUpAsync();
        await MenuAsync("PLAIN", "Plain");
        await MenuAsync("BETA", "Beta", feature: "BETA");
        using var user = await MemberWithAsync();
        using var tenantAdmin = ClientFor(tenantId: _tenantId, userId: 5);
        Assert.Equal(["PLAIN", "BETA"], Codes(await MineAsync(user)));

        var platformOff = await PostIdAsync("/api/MenuFeatureToggle", new { featureCode = "beta", isEnabled = false });
        Assert.Equal(["PLAIN"], Codes(await MineAsync(user)));

        await tenantAdmin.PostAsJsonAsync("/api/MenuFeatureToggle", new { featureCode = "BETA", isEnabled = true });
        Assert.Equal(["PLAIN", "BETA"], Codes(await MineAsync(user)));

        await Client.PutAsJsonAsync($"/api/MenuFeatureToggle/{platformOff}", new { featureCode = "BETA", isEnabled = true });
        _ = platformOff;
    }

    [Fact]
    public async Task Another_tenants_switch_does_not_reach_this_one()
    {
        await SetUpAsync();
        var other = await CreateTenantAsync("OTHER");
        await MenuAsync("BETA", "Beta", feature: "BETA");
        using var user = await MemberWithAsync();
        using var otherAdmin = ClientFor(tenantId: other, userId: 8);

        await otherAdmin.PostAsJsonAsync("/api/MenuFeatureToggle", new { featureCode = "BETA", isEnabled = false });

        Assert.Equal(["BETA"], Codes(await MineAsync(user)));
    }

    [Fact]
    public async Task An_entry_needing_a_module_shows_when_the_application_grants_it()
    {
        await SetUpAsync();
        var hospital = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });
        await PostIdAsync("/api/CompanyTypeApplicationType", new { companyTypeId = _typeId, applicationTypeId = hospital, isDefault = true });
        var lab = await PostIdAsync("/api/AppModule", new { name = "Lab", code = "LAB" });
        await PostIdAsync("/api/AppModule", new { name = "Billing", code = "BILLING" });
        await PostIdAsync("/api/ApplicationTypeModule", new { applicationTypeId = hospital, appModuleId = lab });
        await MenuAsync("LAB", "Lab", module: "LAB");
        await MenuAsync("BILLING", "Billing", module: "BILLING");
        using var user = await MemberWithAsync();

        Assert.Equal(["LAB"], Codes(await MineAsync(user)));
    }

    [Fact]
    public async Task A_tenant_can_replace_the_permission_an_entry_needs()
    {
        await SetUpAsync();
        var reports = await PermissionIdAsync("REPORTS_VIEW");
        var audits = await PermissionIdAsync("AUDITS_VIEW");
        var menu = await MenuAsync("REPORTS", "Reports", permissionId: reports);
        using var analyst = await MemberWithAsync("analyst", "REPORTS_VIEW");
        using var auditor = await MemberWithAsync("auditor", "AUDITS_VIEW");
        using var tenantAdmin = ClientFor(tenantId: _tenantId, userId: 5);
        Assert.Equal(["REPORTS"], Codes(await MineAsync(analyst)));
        Assert.Empty(Codes(await MineAsync(auditor)));

        var overrideId = (await Read(await tenantAdmin.PostAsJsonAsync("/api/MenuPermissionOverride", new { menuId = menu, permissionId = audits })))
            .GetProperty("data").GetProperty("id").GetInt64();
        Assert.Empty(Codes(await MineAsync(analyst)));
        Assert.Equal(["REPORTS"], Codes(await MineAsync(auditor)));

        await tenantAdmin.PutAsJsonAsync($"/api/MenuPermissionOverride/{overrideId}", new { menuId = menu });
        Assert.Equal(["REPORTS"], Codes(await MineAsync(analyst)));
        Assert.Equal(["REPORTS"], Codes(await MineAsync(auditor)));
    }

    [Fact]
    public async Task Titles_come_in_the_persons_language_and_fall_back_to_the_declared_one()
    {
        await SetUpAsync();
        await PostIdAsync("/api/Language", new { code = "en", name = "English", isFallback = true });
        await PostIdAsync("/api/Language", new { code = "tr", name = "Turkish" });
        var home = await MenuAsync("HOME", "Home");
        await MenuAsync("PLAIN", "Plain");
        await PostIdAsync("/api/MenuLocalization", new { menuId = home, languageCode = "tr", title = "Ana sayfa" });
        using var user = await MemberWithAsync();

        var english = await MineAsync(user);
        Assert.Equal("Home", english[0].GetProperty("title").GetString());

        await Client.PutAsJsonAsync($"/api/Tenant/{_tenantId}", new { name = "ACME", code = "ACME", languageCode = "tr" });
        var turkish = await MineAsync(user);
        Assert.Equal("Ana sayfa", turkish[0].GetProperty("title").GetString());
        Assert.Equal("Plain", turkish[1].GetProperty("title").GetString());
    }

    [Fact]
    public async Task The_designer_sees_the_whole_tree_unfiltered()
    {
        var permission = await PermissionIdAsync("SECRET");
        var root = await MenuAsync("ROOT", "Root", path: null, permissionId: permission);
        await MenuAsync("KID", "Kid", root);
        using var designer = ClientFor(userId: 6, permissions: Permissions.MenuDesign);

        var tree = (await Read(await designer.GetAsync("/api/Navigation/tree"))).GetProperty("data");

        Assert.Equal(["ROOT", "KID"], Codes(tree));
    }
}
