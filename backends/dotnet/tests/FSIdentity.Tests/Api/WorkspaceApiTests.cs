using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class WorkspaceApiTests : ApiTestBase
{
    private long _tenantId;
    private long _companyId;

    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task SetUpAsync()
    {
        _tenantId = await CreateTenantAsync();
        _companyId = await CreateCompanyAsync(_tenantId);
    }

    private HttpClient Admin(string permissions = "*", long userId = 500)
        => ClientFor(tenantId: _tenantId, companyId: _companyId, userId: userId, permissions: permissions);

    [Fact]
    public async Task A_new_tenant_and_a_new_company_start_with_their_default_settings()
    {
        await SetUpAsync();

        var tenantSettings = await InDbAsync(db => db.TenantSettings.IgnoreQueryFilters().Where(s => s.IdentityTenantId == _tenantId)
            .OrderBy(s => s.Key).Select(s => s.Key + "=" + s.Value).ToListAsync());
        var companySettings = await InDbAsync(db => db.CompanySettings.IgnoreQueryFilters().Where(s => s.IdentityCompanyId == _companyId)
            .Select(s => s.Key + "=" + s.Value).ToListAsync());

        Assert.Equal(["DATE_FORMAT=yyyy-MM-dd", "TIME_ZONE=UTC"], tenantSettings);
        Assert.Equal(["WEEK_START=Monday"], companySettings);
    }

    private Task<long> UnitTypeAsync(string code = "DEPT") => PostIdAsync("/api/UnitType", new { code, name = code });

    [Fact]
    public async Task A_unit_belongs_to_the_company_the_caller_works_in_and_has_a_unique_code_there()
    {
        await SetUpAsync();
        var typeId = await UnitTypeAsync();
        using var admin = Admin();

        var created = await admin.PostAsJsonAsync("/api/Unit", new { code = " ward-a ", name = "Ward A", unitTypeId = typeId });
        var data = (await Read(created)).GetProperty("data");
        var again = await admin.PostAsJsonAsync("/api/Unit", new { code = "WARD-A", name = "Other", unitTypeId = typeId });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("WARD-A", data.GetProperty("code").GetString());
        Assert.Equal(_companyId, data.GetProperty("companyId").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Another_company_may_use_the_same_unit_code_and_cannot_see_the_unit()
    {
        await SetUpAsync();
        var second = await CreateCompanyAsync(_tenantId, "BRANCH");
        var typeId = await UnitTypeAsync();
        using var admin = Admin();
        using var branch = ClientFor(tenantId: _tenantId, companyId: second, userId: 501);
        var unitId = (await Read(await admin.PostAsJsonAsync("/api/Unit", new { code = "OPS", name = "Ops", unitTypeId = typeId })))
            .GetProperty("data").GetProperty("id").GetInt64();

        var same = await branch.PostAsJsonAsync("/api/Unit", new { code = "OPS", name = "Ops", unitTypeId = typeId });

        Assert.Equal(HttpStatusCode.Created, same.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await branch.GetAsync($"/api/Unit/{unitId}")).StatusCode);
        Assert.Single((await Read(await branch.GetAsync("/api/Unit"))).GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task Without_a_company_there_is_no_unit_to_make()
    {
        await SetUpAsync();
        var typeId = await UnitTypeAsync();

        var response = await Client.PostAsJsonAsync("/api/Unit", new { code = "OPS", name = "Ops", unitTypeId = typeId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_organisation_list_has_every_unit_with_the_company_it_belongs_to()
    {
        await SetUpAsync();
        var typeId = await UnitTypeAsync();
        using var admin = Admin();
        await admin.PostAsJsonAsync("/api/Unit", new { code = "OPS", name = "Ops", unitTypeId = typeId });

        var list = (await Read(await admin.GetAsync("/api/Unit/organization"))).GetProperty("data").EnumerateArray().ToList();

        var unit = Assert.Single(list);
        Assert.Equal("OPS", unit.GetProperty("code").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, unit.GetProperty("companyId").ValueKind);
    }

    [Fact]
    public async Task Units_form_a_tree_that_cannot_loop_and_a_unit_with_children_cannot_be_deleted()
    {
        await SetUpAsync();
        var typeId = await UnitTypeAsync();
        using var admin = Admin();
        async Task<long> Make(string code, long? parentId = null)
            => (await Read(await admin.PostAsJsonAsync("/api/Unit", new { code, name = code, unitTypeId = typeId, parentId })))
                .GetProperty("data").GetProperty("id").GetInt64();
        var root = await Make("ROOT");
        var child = await Make("CHILD", root);
        var grand = await Make("GRAND", child);

        var self = await admin.PutAsJsonAsync($"/api/Unit/{child}", new { code = "CHILD", name = "CHILD", unitTypeId = typeId, parentId = child });
        var loop = await admin.PutAsJsonAsync($"/api/Unit/{root}", new { code = "ROOT", name = "ROOT", unitTypeId = typeId, parentId = grand });
        var tree = (await Read(await admin.GetAsync("/api/Unit/tree"))).GetProperty("data");

        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, loop.StatusCode);
        Assert.Equal("ROOT", tree[0].GetProperty("code").GetString());
        Assert.Equal("GRAND", tree[0].GetProperty("children")[0].GetProperty("children")[0].GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/Unit/{root}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/Unit/{grand}")).StatusCode);
    }

    [Fact]
    public async Task A_units_manager_must_be_a_member_of_the_company()
    {
        await SetUpAsync();
        var typeId = await UnitTypeAsync();
        var member = await CreateSignInUserAsync("ada", _tenantId, _companyId);
        var stranger = await CreateUserAsync("eve", _tenantId);
        using var admin = Admin();

        var good = await admin.PostAsJsonAsync("/api/Unit", new { code = "A", name = "A", unitTypeId = typeId, managerUserId = member });
        var bad = await admin.PostAsJsonAsync("/api/Unit", new { code = "B", name = "B", unitTypeId = typeId, managerUserId = stranger });

        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task A_person_joins_units_of_their_company_with_one_primary()
    {
        await SetUpAsync();
        var typeId = await UnitTypeAsync();
        var userId = await CreateSignInUserAsync("ada", _tenantId, _companyId);
        var stranger = await CreateUserAsync("eve", _tenantId);
        using var admin = Admin();
        async Task<long> Make(string code) => (await Read(await admin.PostAsJsonAsync("/api/Unit", new { code, name = code, unitTypeId = typeId })))
            .GetProperty("data").GetProperty("id").GetInt64();
        var first = await Make("ONE");
        var second = await Make("TWO");

        var a = (await Read(await admin.PostAsJsonAsync("/api/UserUnit", new { userId, unitId = first, isPrimary = true }))).GetProperty("data").GetProperty("id").GetInt64();
        var b = (await Read(await admin.PostAsJsonAsync("/api/UserUnit", new { userId, unitId = second, isPrimary = true }))).GetProperty("data").GetProperty("id").GetInt64();
        var notMember = await admin.PostAsJsonAsync("/api/UserUnit", new { userId = stranger, unitId = first });
        var twice = await admin.PostAsJsonAsync("/api/UserUnit", new { userId, unitId = first });

        async Task<bool> Primary(long id) => (await Read(await admin.GetAsync($"/api/UserUnit/{id}"))).GetProperty("data").GetProperty("isPrimary").GetBoolean();
        Assert.False(await Primary(a));
        Assert.True(await Primary(b));
        Assert.Equal(HttpStatusCode.BadRequest, notMember.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
    }

    [Fact]
    public async Task Updating_a_membership_to_primary_moves_the_flag_too()
    {
        await SetUpAsync();
        var typeId = await UnitTypeAsync();
        var userId = await CreateSignInUserAsync("ada", _tenantId, _companyId);
        using var admin = Admin();
        async Task<long> Make(string code) => (await Read(await admin.PostAsJsonAsync("/api/Unit", new { code, name = code, unitTypeId = typeId })))
            .GetProperty("data").GetProperty("id").GetInt64();
        var one = await Make("ONE");
        var two = await Make("TWO");
        var first = (await Read(await admin.PostAsJsonAsync("/api/UserUnit", new { userId, unitId = one, isPrimary = true }))).GetProperty("data").GetProperty("id").GetInt64();
        var second = (await Read(await admin.PostAsJsonAsync("/api/UserUnit", new { userId, unitId = two }))).GetProperty("data").GetProperty("id").GetInt64();

        var response = await admin.PutAsJsonAsync($"/api/UserUnit/{second}", new { userId, unitId = two, isPrimary = true });

        async Task<bool> Primary(long id) => (await Read(await admin.GetAsync($"/api/UserUnit/{id}"))).GetProperty("data").GetProperty("isPrimary").GetBoolean();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await Primary(first));
        Assert.True(await Primary(second));
    }

    [Fact]
    public async Task Units_are_written_with_the_unit_permission_and_a_unit_type_in_use_is_kept()
    {
        await SetUpAsync();
        var typeId = await UnitTypeAsync();
        using var nobody = Admin("none", 600);
        using var admin = Admin();
        await admin.PostAsJsonAsync("/api/Unit", new { code = "A", name = "A", unitTypeId = typeId });

        Assert.Equal(HttpStatusCode.OK, (await nobody.GetAsync("/api/Unit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.PostAsJsonAsync("/api/Unit", new { code = "B", name = "B", unitTypeId = typeId })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/UnitType/{typeId}")).StatusCode);
    }

    [Fact]
    public async Task A_setting_is_the_persons_else_the_companys_else_the_tenants()
    {
        await SetUpAsync();
        var userId = await CreateSignInUserAsync("ada", _tenantId, _companyId);
        using var admin = Admin();
        await admin.PostAsJsonAsync("/api/TenantSetting", new { key = "ui.theme", value = "tenant-blue" });
        await admin.PostAsJsonAsync("/api/TenantSetting", new { key = "ui.density", value = "compact" });
        await admin.PostAsJsonAsync("/api/CompanySetting", new { key = "ui.theme", value = "company-green" });
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        async Task<Dictionary<string, (string? Value, string Source)>> Effective(string keys) =>
            (await Read(await ada.GetAsync($"/api/Settings/effective?keys={keys}"))).GetProperty("data").EnumerateArray()
                .ToDictionary(s => s.GetProperty("key").GetString()!, s => (s.GetProperty("value").GetString(), s.GetProperty("source").GetString()!));

        var before = await Effective("ui.theme,ui.density,ui.missing");
        Assert.Equal(("company-green", "company"), before["ui.theme"]);
        Assert.Equal(("compact", "tenant"), before["ui.density"]);
        Assert.False(before.ContainsKey("ui.missing"));

        Assert.Equal(HttpStatusCode.OK, (await ada.PutAsJsonAsync("/api/Settings/mine/ui.theme", new { value = "dark" })).StatusCode);
        Assert.Equal(("dark", "user"), (await Effective("ui.theme"))["ui.theme"]);

        await ada.DeleteAsync("/api/Settings/mine/ui.theme");
        Assert.Equal(("company-green", "company"), (await Effective("ui.theme"))["ui.theme"]);
        _ = userId;
    }

    [Fact]
    public async Task One_tenants_and_one_companys_settings_do_not_reach_another()
    {
        await SetUpAsync();
        var otherTenant = await CreateTenantAsync("OTHER");
        var otherCompany = await CreateCompanyAsync(otherTenant, "OTHERHQ");
        await CreateSignInUserAsync("ada", _tenantId, _companyId);
        using var mine = Admin();
        using var theirs = ClientFor(tenantId: otherTenant, companyId: otherCompany, userId: 700);
        await mine.PostAsJsonAsync("/api/TenantSetting", new { key = "ui.theme", value = "mine" });
        await theirs.PostAsJsonAsync("/api/TenantSetting", new { key = "ui.theme", value = "theirs" });
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        var data = (await Read(await ada.GetAsync("/api/Settings/effective?keys=ui.theme"))).GetProperty("data");

        Assert.Equal("mine", data[0].GetProperty("value").GetString());
        Assert.Single((await Read(await mine.GetAsync("/api/TenantSetting"))).GetProperty("data").EnumerateArray().Where(s => s.GetProperty("key").GetString() == "ui.theme"));
    }

    [Fact]
    public async Task Setting_keys_are_checked_and_a_person_keeps_a_bounded_number()
    {
        await SetUpAsync();
        await CreateSignInUserAsync("ada", _tenantId, _companyId);
        using var admin = Admin();
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/TenantSetting", new { key = "1bad", value = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/TenantSetting", new { key = "has space", value = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/TenantSetting", new { key = "TIME_ZONE", value = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ada.PutAsJsonAsync("/api/Settings/mine/1bad", new { value = "x" })).StatusCode);

        var userId = (await Read(await ada.GetAsync("/api/Auth/me"))).GetProperty("data").GetProperty("user").GetProperty("id").GetInt64();
        await InDbAsync(async db =>
        {
            for (var i = 0; i < 200; i++)
                db.UserSettings.Add(new FSIdentity.Domain.Entities.Workspace.UserSetting { UserId = userId, Key = $"k{i}" });
            return await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await ada.PutAsJsonAsync("/api/Settings/mine/one-too-many", new { value = "x" })).StatusCode);
    }

    [Fact]
    public async Task Nobody_reads_another_persons_settings_and_tenant_settings_need_the_settings_permission()
    {
        await SetUpAsync();
        await CreateSignInUserAsync("ada", _tenantId, _companyId);
        await CreateSignInUserAsync("bob", _tenantId, _companyId);
        using var ada = Bearer((await SignInAsync("ada")).GetProperty("accessToken").GetString()!);
        using var bob = Bearer((await SignInAsync("bob")).GetProperty("accessToken").GetString()!);
        await ada.PutAsJsonAsync("/api/Settings/mine/private", new { value = "ada's" });

        var bobs = (await Read(await bob.GetAsync("/api/Settings/mine"))).GetProperty("data");
        var effective = (await Read(await bob.GetAsync("/api/Settings/effective?keys=private"))).GetProperty("data");

        Assert.Empty(bobs.EnumerateArray());
        Assert.Empty(effective.EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await ada.GetAsync("/api/TenantSetting")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ada.PostAsJsonAsync("/api/CompanySetting", new { key = "x", value = "y" })).StatusCode);
    }

    private async Task<long> WidgetAsync(string code, long? permissionId = null, string? module = null, int width = 4, int height = 3)
        => await PostIdAsync("/api/DashboardWidget", new
        {
            code, title = code, permissionId, moduleCode = module, defaultWidth = width, defaultHeight = height,
        });

    private static object Layout(string name, int scope, long scopeId, params (string Widget, int X, int Y, int W, int H)[] items) => new
    {
        name, scope, scopeId,
        layoutJson = JsonSerializer.Serialize(items.Select(i => new { widget = i.Widget, x = i.X, y = i.Y, w = i.W, h = i.H })),
    };

    private async Task<string[]> MineWidgetsAsync(HttpClient client)
    {
        var view = (await Read(await client.GetAsync("/api/Dashboard/mine"))).GetProperty("data");
        return view.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("widget").GetString()!).ToArray();
    }

    [Fact]
    public async Task With_no_layout_a_person_gets_every_widget_they_may_see_laid_out_left_to_right()
    {
        await SetUpAsync();
        var secret = await CreatePermissionAsync("SECRET_VIEW");
        await WidgetAsync("SALES");
        await WidgetAsync("STOCK");
        await WidgetAsync("PAYROLL", secret, width: 20);
        await CreateSignInUserAsync("ada", _tenantId, _companyId);
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        var view = (await Read(await ada.GetAsync("/api/Dashboard/mine"))).GetProperty("data");

        Assert.Equal("default", view.GetProperty("source").GetString());
        Assert.Equal(["SALES", "STOCK"], view.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("widget").GetString()));
        Assert.Equal(4, view.GetProperty("items")[1].GetProperty("x").GetInt32());
        using var platform = ClientFor(userId: 9, superAdmin: true);
        Assert.Equal(["PAYROLL", "SALES", "STOCK"], await MineWidgetsAsync(platform));
    }

    [Fact]
    public async Task The_most_specific_layout_wins()
    {
        await SetUpAsync();
        var typeId = await CreateCompanyTypeAsync("CLINIC");
        await Client.PutAsJsonAsync($"/api/Company/{_companyId}", new { name = "HQ", code = "HQ-ACME", companyTypeId = typeId });
        var application = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });
        await PostIdAsync("/api/CompanyTypeApplicationType", new { companyTypeId = typeId, applicationTypeId = application, isDefault = true });
        foreach (var code in new[] { "A", "B", "C", "D" })
            await WidgetAsync(code);
        var userId = await CreateSignInUserAsync("ada", _tenantId, _companyId);
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);
        using var admin = Admin("*");

        await Client.PostAsJsonAsync("/api/DashboardLayout", Layout("app", 0, application, ("A", 0, 0, 4, 3)));
        Assert.Equal(["A"], await MineWidgetsAsync(ada));

        await Client.PostAsJsonAsync("/api/DashboardLayout", Layout("type", 1, typeId, ("B", 0, 0, 4, 3)));
        Assert.Equal(["B"], await MineWidgetsAsync(ada));

        await admin.PostAsJsonAsync("/api/DashboardLayout", Layout("company", 2, _companyId, ("C", 0, 0, 4, 3)));
        Assert.Equal(["C"], await MineWidgetsAsync(ada));

        await admin.PostAsJsonAsync("/api/DashboardLayout", Layout("person", 3, userId, ("D", 0, 0, 4, 3)));
        var view = (await Read(await ada.GetAsync("/api/Dashboard/mine"))).GetProperty("data");
        Assert.Equal(["D"], view.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("widget").GetString()));
        Assert.Equal("user", view.GetProperty("source").GetString());
    }

    [Fact]
    public async Task Role_rank_and_tenant_layers_come_between_the_person_and_the_company_and_can_be_previewed()
    {
        await SetUpAsync();
        foreach (var code in new[] { "A", "B", "C", "D", "E" })
            await WidgetAsync(code);
        var application = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });
        using var admin = Admin("*");
        var role = (await Read(await admin.PostAsJsonAsync("/api/Role", new { name = "Nurse" }))).GetProperty("data").GetProperty("id").GetInt64();
        var userId = await CreateSignInUserAsync("ada", _tenantId, _companyId);
        await InDbAsync(db =>
        {
            db.UserCompanyRoles.Add(new FSIdentity.Domain.Entities.Accounts.UserCompanyRole { IdentityTenantId = _tenantId, UserId = userId, CompanyId = _companyId, RoleId = role });
            return db.SaveChangesAsync();
        });
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        await admin.PostAsJsonAsync("/api/DashboardLayout", Layout("tenant", 4, _tenantId, ("E", 0, 0, 4, 3)));
        Assert.Equal(["E"], await MineWidgetsAsync(ada));

        await admin.PostAsJsonAsync("/api/DashboardLayout", Layout("company", 2, _companyId, ("C", 0, 0, 4, 3)));
        Assert.Equal(["C"], await MineWidgetsAsync(ada));

        await admin.PostAsJsonAsync("/api/DashboardLayout", Layout("rank", 6, 1, ("A", 0, 0, 4, 3)));
        Assert.Equal(["A"], await MineWidgetsAsync(ada));

        var roleLayout = await admin.PostAsJsonAsync("/api/DashboardLayout", Layout("role", 5, role, ("B", 0, 0, 4, 3)));
        Assert.True(roleLayout.StatusCode == HttpStatusCode.Created, await roleLayout.Content.ReadAsStringAsync());
        Assert.Equal(["B"], await MineWidgetsAsync(ada));

        var inCompany = new { name = "role here", scope = 5, scopeId = role, companyId = _companyId, layoutJson = "[{\"widget\":\"D\",\"x\":0,\"y\":0,\"w\":4,\"h\":3}]" };
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/DashboardLayout", inCompany)).StatusCode);
        var view = (await Read(await ada.GetAsync("/api/Dashboard/mine"))).GetProperty("data");
        Assert.Equal(["D"], view.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("widget").GetString()));
        Assert.Equal("roleInCompany", view.GetProperty("source").GetString());

        var wrong = await admin.PostAsJsonAsync("/api/DashboardLayout", new { name = "x", scope = 2, scopeId = _companyId, companyId = _companyId, layoutJson = "[]" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var preview = (await Read(await Client.GetAsync($"/api/DashboardStudio/preview?applicationTypeId={application}&companyId={_companyId}&roleId={role}"))).GetProperty("data");
        Assert.Equal(["D"], preview.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("widget").GetString()));
        var rankOnly = (await Read(await Client.GetAsync($"/api/DashboardStudio/preview?applicationTypeId={application}&companyId={_companyId}&userType=1"))).GetProperty("data");
        Assert.Equal("userType", rankOnly.GetProperty("source").GetString());
    }

    [Fact]
    public async Task A_platform_administrator_working_in_another_tenant_saves_a_layout_for_the_tenant_it_is_about()
    {
        await SetUpAsync();
        var home = await CreateTenantAsync("HOME");
        await WidgetAsync("A");
        await CreateSignInUserAsync("ada", _tenantId, _companyId);
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);
        using var platform = ClientFor(tenantId: home, userId: 9, superAdmin: true);

        var forCompany = await platform.PostAsJsonAsync("/api/DashboardLayout", Layout("company", 2, _companyId, ("A", 0, 0, 4, 3)));
        var forApplication = await platform.PostAsJsonAsync("/api/DashboardLayout", Layout("application", 0, await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" }), ("A", 0, 0, 4, 3)));

        Assert.Equal(HttpStatusCode.Created, forCompany.StatusCode);
        Assert.Equal(_tenantId, (await Read(forCompany)).GetProperty("data").GetProperty("tenantId").GetInt64());
        Assert.Equal(["A"], await MineWidgetsAsync(ada));
        Assert.Equal(JsonValueKind.Null, (await Read(forApplication)).GetProperty("data").GetProperty("tenantId").ValueKind);
    }

    [Fact]
    public async Task A_tenants_own_layout_beats_the_platforms_for_the_same_scope_and_no_other_tenants_applies()
    {
        await SetUpAsync();
        var otherTenant = await CreateTenantAsync("OTHER");
        var typeId = await CreateCompanyTypeAsync("CLINIC");
        await Client.PutAsJsonAsync($"/api/Company/{_companyId}", new { name = "HQ", code = "HQ-ACME", companyTypeId = typeId });
        foreach (var code in new[] { "PLATFORM", "OURS", "THEIRS" })
            await WidgetAsync(code);
        await CreateSignInUserAsync("ada", _tenantId, _companyId);
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);
        using var theirAdmin = ClientFor(tenantId: otherTenant, userId: 800);
        using var ourAdmin = Admin("*");

        await Client.PostAsJsonAsync("/api/DashboardLayout", Layout("platform", 1, typeId, ("PLATFORM", 0, 0, 4, 3)));
        await theirAdmin.PostAsJsonAsync("/api/DashboardLayout", Layout("theirs", 1, typeId, ("THEIRS", 0, 0, 4, 3)));
        Assert.Equal(["PLATFORM"], await MineWidgetsAsync(ada));

        await ourAdmin.PostAsJsonAsync("/api/DashboardLayout", Layout("ours", 1, typeId, ("OURS", 0, 0, 4, 3)));
        Assert.Equal(["OURS"], await MineWidgetsAsync(ada));
    }

    [Fact]
    public async Task A_widget_the_person_may_not_see_is_left_out_of_even_their_own_layout()
    {
        await SetUpAsync();
        var typeId = await CreateCompanyTypeAsync("CLINIC");
        await Client.PutAsJsonAsync($"/api/Company/{_companyId}", new { name = "HQ", code = "HQ-ACME", companyTypeId = typeId });
        var application = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });
        await PostIdAsync("/api/CompanyTypeApplicationType", new { companyTypeId = typeId, applicationTypeId = application, isDefault = true });
        await PostIdAsync("/api/ApplicationTypeModule", new
        {
            applicationTypeId = application, appModuleId = await PostIdAsync("/api/AppModule", new { name = "Lab", code = "LAB" }),
        });
        var secret = await CreatePermissionAsync("SECRET_VIEW");
        await WidgetAsync("OPEN");
        await WidgetAsync("SECRET", secret);
        await WidgetAsync("BILLING_ONLY", module: "BILLING");
        await WidgetAsync("LAB_ONLY", module: "LAB");
        var userId = await CreateSignInUserAsync("ada", _tenantId, _companyId);
        await Client.PostAsJsonAsync("/api/DashboardLayout", Layout("person", 3, userId,
            ("OPEN", 0, 0, 4, 3), ("SECRET", 4, 0, 4, 3), ("BILLING_ONLY", 8, 0, 4, 3), ("LAB_ONLY", 12, 0, 4, 3)));
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        Assert.Equal(["OPEN", "LAB_ONLY"], await MineWidgetsAsync(ada));
    }

    [Fact]
    public async Task A_person_arranges_their_own_dashboard_and_can_go_back()
    {
        await SetUpAsync();
        await WidgetAsync("A");
        await WidgetAsync("B");
        await CreateSignInUserAsync("ada", _tenantId, _companyId);
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        var saved = await ada.PutAsJsonAsync("/api/Dashboard/mine", new { items = new[] { new { widget = "b", x = 0, y = 0, w = 12, h = 4 } } });
        var view = (await Read(saved)).GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("user", view.GetProperty("source").GetString());
        Assert.Equal(["B"], await MineWidgetsAsync(ada));

        await ada.PutAsJsonAsync("/api/Dashboard/mine", new { items = new[] { new { widget = "A", x = 0, y = 0, w = 6, h = 4 } } });
        Assert.Equal(["A"], await MineWidgetsAsync(ada));
        Assert.Equal(1, await InDbAsync(db => db.DashboardLayouts.IgnoreQueryFilters().CountAsync(l => l.Scope == FSIdentity.Domain.Entities.Workspace.DashboardScope.User)));

        await ada.DeleteAsync("/api/Dashboard/mine");
        Assert.Equal(["A", "B"], await MineWidgetsAsync(ada));
    }

    [Fact]
    public async Task A_layout_must_fit_the_grid_name_real_widgets_and_not_repeat_one()
    {
        await SetUpAsync();
        await WidgetAsync("A");
        await CreateSignInUserAsync("ada", _tenantId, _companyId);
        using var ada = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        async Task<HttpStatusCode> Save(object[] items) => (await ada.PutAsJsonAsync("/api/Dashboard/mine", new { items })).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await Save([new { widget = "A", x = 20, y = 0, w = 8, h = 2 }]));
        Assert.Equal(HttpStatusCode.BadRequest, await Save([new { widget = "A", x = -1, y = 0, w = 4, h = 2 }]));
        Assert.Equal(HttpStatusCode.BadRequest, await Save([new { widget = "A", x = 0, y = 0, w = 0, h = 2 }]));
        Assert.Equal(HttpStatusCode.BadRequest, await Save([new { widget = "NOPE", x = 0, y = 0, w = 4, h = 2 }]));
        Assert.Equal(HttpStatusCode.BadRequest, await Save([new { widget = "A", x = 0, y = 0, w = 4, h = 2 }, new { widget = "a", x = 4, y = 0, w = 4, h = 2 }]));
        Assert.Equal(HttpStatusCode.BadRequest, await Save(Enumerable.Range(0, 51).Select(i => (object)new { widget = $"W{i}", x = 0, y = i, w = 4, h = 1 }).ToArray()));
        Assert.Equal(HttpStatusCode.OK, await Save([new { widget = "A", x = 20, y = 0, w = 4, h = 2 }]));
    }

    [Fact]
    public async Task Layouts_made_by_administrators_are_checked_the_same_way_and_one_per_scope()
    {
        await SetUpAsync();
        await WidgetAsync("A");
        var userId = await CreateUserAsync("ada", _tenantId);

        var bad = await Client.PostAsJsonAsync("/api/DashboardLayout", new { name = "x", scope = 3, scopeId = userId, layoutJson = "not json" });
        var missingScope = await Client.PostAsJsonAsync("/api/DashboardLayout", Layout("x", 2, 999, ("A", 0, 0, 4, 3)));
        var good = await Client.PostAsJsonAsync("/api/DashboardLayout", Layout("x", 3, userId, ("A", 0, 0, 4, 3)));
        var again = await Client.PostAsJsonAsync("/api/DashboardLayout", Layout("y", 3, userId, ("A", 0, 0, 4, 3)));

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingScope.StatusCode);
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task The_widget_catalogue_and_its_layouts_need_the_dashboard_permission_and_a_component_type_in_use_is_kept()
    {
        using var nobody = ClientFor(tenantId: 1, userId: 5, permissions: "none");
        var typeId = await PostIdAsync("/api/ComponentType", new { code = "chart", name = "Chart" });
        await PostIdAsync("/api/DashboardWidget", new { code = "SALES", title = "Sales", componentTypeId = typeId });

        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.GetAsync("/api/DashboardWidget")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.PostAsJsonAsync("/api/DashboardWidget", new { code = "X", title = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/ComponentType/{typeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous.GetAsync("/api/Dashboard/mine")).StatusCode);
    }
}
