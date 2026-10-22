using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FSIdentity.Identity.Geography;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public abstract class ApiTestBase : IDisposable
{
    protected readonly AppFactory Factory = new();

    protected readonly HttpClient Client;

    protected CapturedEmails Emails => Factory.Services.GetRequiredService<CapturedEmails>();

    protected readonly HttpClient Anonymous;

    private readonly List<HttpClient> _clients = [];

    protected ApiTestBase()
    {
        Client = Factory.CreateClient();
        Client.DefaultRequestHeaders.Add(TestIdentityStartupFilter.SuperAdminHeader, "true");
        Anonymous = Factory.CreateClient();
    }

    public void Dispose()
    {
        foreach (var client in _clients)
            client.Dispose();

        Client.Dispose();
        Anonymous.Dispose();
        Factory.Dispose();
    }

    protected HttpClient ClientFor(long? tenantId = null, long? companyId = null, long? userId = null, long? applicationTypeId = null, bool superAdmin = false, string? permissions = null, int? rank = null)
    {
        var client = Factory.CreateClient();
        _clients.Add(client);

        void Set(string header, object? value)
        {
            if (value is not null)
                client.DefaultRequestHeaders.Add(header, value.ToString());
        }

        Set(TestIdentityStartupFilter.TenantIdHeader, tenantId);
        Set(TestIdentityStartupFilter.CompanyIdHeader, companyId);
        Set(TestIdentityStartupFilter.UserIdHeader, userId);
                Set(TestIdentityStartupFilter.ApplicationTypeIdHeader, applicationTypeId);
        Set(TestIdentityStartupFilter.SuperAdminHeader, superAdmin ? "true" : null);
        Set(TestIdentityStartupFilter.PermissionsHeader, permissions);
        Set(TestIdentityStartupFilter.RankHeader, rank);

        return client;
    }

    protected static async Task<JsonElement> Read(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    protected async Task<long> PostIdAsync(string path, object body)
    {
        var response = await Client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Read(response)).GetProperty("data").GetProperty("id").GetInt64();
    }

    protected Task<long> CreateTenantAsync(string code = "ACME")
        => PostIdAsync("/api/Tenant", new { name = $"{code} tenant", code });

    protected Task<long> CreateCompanyTypeAsync(string code = "CLINIC")
        => PostIdAsync("/api/CompanyType", new { name = $"{code} type", code });

    protected Task<long> CreateCompanyAsync(long tenantId, string code = "HQ")
        => PostIdAsync("/api/Company", new { name = $"{code} company", code, tenantId });

    protected Task<long> CreateCountryAsync(string code = "TR")
        => PostIdAsync("/api/Country", new { name = $"{code} land", code, formatCode = "x" });

    protected Task<long> CreateStateAsync(long countryId, string code = "TR-MAR")
        => PostIdAsync("/api/State", new { code, name = $"{code} state", countryId });

    protected Task<long> CreateCityAsync(long stateId, string code = "TR-34")
        => PostIdAsync("/api/City", new { code, name = $"{code} city", stateId });

    protected async Task<long> CreateUserAsync(string userName = "ada", long? tenantId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FSIdentity.Identity.Data.AppDbContext>();
        var user = new FSIdentity.Domain.Entities.Accounts.User
        {
            UserName = userName,
            Email = $"{userName}@example.test",
            FirstName = userName,
            LastName = "Tester",
            PasswordHash = "not-a-real-hash",
            IdentityTenantId = tenantId,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    public const string Password = "Passw0rd!";

    protected async Task<long> CreateSignInUserAsync(
        string userName = "ada", long? tenantId = null, long? companyId = null, bool platformAdmin = false,
        FSIdentity.Domain.Entities.Accounts.UserType membership = FSIdentity.Domain.Entities.Accounts.UserType.Standard)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FSIdentity.Identity.Data.AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<FSIdentity.Identity.Auth.IPasswordHasher>();
        var user = new FSIdentity.Domain.Entities.Accounts.User
        {
            UserName = userName,
            Email = $"{userName}@example.test",
            FirstName = userName,
            LastName = "Tester",
            PasswordHash = hasher.Hash(Password),
            IdentityTenantId = tenantId,
            IsPlatformAdmin = platformAdmin,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (companyId is { } company)
        {
            db.UserCompanies.Add(new FSIdentity.Domain.Entities.Accounts.UserCompany
            {
                UserId = user.Id, CompanyId = company, MembershipType = membership, IdentityTenantId = tenantId,
            });
            await db.SaveChangesAsync();
        }

        return user.Id;
    }

    protected async Task<System.Text.Json.JsonElement> SignInAsync(string userName = "ada", string? password = null)
    {
        var response = await Client.PostAsJsonAsync("/api/Auth/login", new { userName, password = password ?? Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Read(response)).GetProperty("data");
    }

    protected HttpClient Bearer(string accessToken)
    {
        var client = Factory.CreateClient();
        _clients.Add(client);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    protected Task<long> CreatePermissionAsync(string code = "USER_VIEW")
        => PostIdAsync("/api/Permission", new { name = $"{code} permission", code });

    protected Task<long> CreateRoleAsync(string name = "Nurse")
        => PostIdAsync("/api/Role", new { name });

    protected async Task SeedAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<GeographySeeder>().SeedAsync();
    }
}