using System.Text.Json;

namespace FSIdentity.Identity.Seeding;

public class DemoSeedOptions
{
    public const string SectionName = "DemoSeed";

    public string Password { get; set; } = "Demo@12345";

    public bool MustChangePassword { get; set; }

    public string EmailDomain { get; set; } = "example.test";

    public string LanguageCode { get; set; } = "en";

    public string? File { get; set; }
}

public class DemoDefinition
{
    public DemoTenant Management { get; set; } = new();

    public List<DemoApplication> Applications { get; set; } = [];

    public List<DemoTenant> Tenants { get; set; } = [];

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static DemoDefinition Load(string path)
        => JsonSerializer.Deserialize<DemoDefinition>(System.IO.File.ReadAllText(path), Json)
           ?? throw new InvalidOperationException($"{path} does not hold a demo definition");

    public void Validate()
    {
        var problems = new List<string>();
        var appCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Domain.Entities.Applications.ApplicationType.ManagementCode };
        foreach (var a in Applications)
        {
            if (string.IsNullOrWhiteSpace(a.Code) || string.IsNullOrWhiteSpace(a.Name)) problems.Add("Every application needs a code and a name");
            else if (!appCodes.Add(a.Code.Trim())) problems.Add($"Application '{a.Code}' is listed twice (MANAGEMENT is built in)");
        }

        var permissionCodes = IdentitySeeder.Catalogue.Where(p => p.Rank is null).Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sharedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            IdentitySeeder.AdministratorRole, DemoSeeder.ManagerRole, IdentitySeeder.ViewerRole, DemoSeeder.StaffRole,
        };
        var appRoles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in Applications)
        {
            var names = appRoles[a.Code?.Trim() ?? ""] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in a.Roles)
            {
                if (string.IsNullOrWhiteSpace(r.Name)) problems.Add($"Application {a.Code}: a role needs a name");
                else if (!names.Add(r.Name.Trim())) problems.Add($"Application {a.Code}: role '{r.Name}' is listed twice");
                foreach (var code in r.Permissions.Where(c => !permissionCodes.Contains(c)))
                    problems.Add($"Application {a.Code}, role {r.Name}: '{code}' is not a permission that a role can hold");
            }
        }

        var tenantCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var companyCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var userNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Person(DemoPerson? p, string where)
        {
            if (p is null) return;
            if (string.IsNullOrWhiteSpace(p.UserName) || string.IsNullOrWhiteSpace(p.FirstName) || string.IsNullOrWhiteSpace(p.LastName))
                problems.Add($"{where}: a person needs a user name, a first name and a last name");
            else if (!userNames.Add(p.UserName.Trim()))
                problems.Add($"User name '{p.UserName}' is used twice");
        }

        void Tenant(DemoTenant t, string where, bool management)
        {
            if (string.IsNullOrWhiteSpace(t.Code) || string.IsNullOrWhiteSpace(t.Name)) problems.Add($"{where}: a tenant needs a code and a name");
            else if (!tenantCodes.Add(t.Code.Trim())) problems.Add($"Tenant '{t.Code}' is listed twice");
            if (t.TenantAdmin is null) problems.Add($"{where}: a tenant needs a tenant administrator");
            if (t.Companies.Count == 0) problems.Add($"{where}: a tenant needs at least one company");

            Person(t.TenantAdmin, where);
            foreach (var c in t.Companies)
            {
                var name = $"{where} / {c.Code}";
                if (string.IsNullOrWhiteSpace(c.Code) || string.IsNullOrWhiteSpace(c.Name)) problems.Add($"{where}: a company needs a code and a name");
                else if (!companyCodes.Add(c.Code.Trim())) problems.Add($"Company '{c.Code}' is listed twice (codes are unique across the demo)");

                var expected = management ? Domain.Entities.Applications.ApplicationType.ManagementCode : c.ApplicationCode;
                if (management && !string.Equals(c.ApplicationCode, expected, StringComparison.OrdinalIgnoreCase))
                    problems.Add($"{name}: the management company uses the MANAGEMENT application");
                else if (!management && !appCodes.Contains(c.ApplicationCode ?? string.Empty))
                    problems.Add($"{name}: unknown application '{c.ApplicationCode}'");
                else if (!management && string.Equals(c.ApplicationCode, Domain.Entities.Applications.ApplicationType.ManagementCode, StringComparison.OrdinalIgnoreCase))
                    problems.Add($"{name}: only the management company uses MANAGEMENT");

                Person(c.CompanyAdmin, name);
                foreach (var u in c.Users) Person(u, name);

                var known = sharedRoles.Concat(appRoles.GetValueOrDefault(c.ApplicationCode ?? string.Empty) ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var person in new[] { c.CompanyAdmin }.Concat(c.Users).Where(p => p is not null))
                    foreach (var role in person!.Roles.Where(r => !known.Contains(r)))
                        problems.Add($"{name}: {person.UserName} is given the role '{role}', which neither the application nor the tenant has");
            }
        }

        Tenant(Management, "Management", management: true);
        foreach (var t in Tenants) Tenant(t, $"Tenant {t.Code}", management: false);
        if (problems.Count > 0)
            throw new ArgumentException("The demo definition is not valid:\n - " + string.Join("\n - ", problems));
    }

    public static DemoDefinition Default() => new()
    {
        Management = new DemoTenant
        {
            Code = "MANAGEMENT", Name = "Management",
            TenantAdmin = new DemoPerson("admin", "System", "Administrator"),
            Companies = [new DemoCompany { Code = "MANAGEMENT", Name = "Management", ApplicationCode = "MANAGEMENT" }],
        },
        Applications =
        [
            new DemoApplication
            {
                Code = "RETAIL", Name = "Retail", Description = "Stores, products and orders",
                Roles = [new DemoRole("Cashier", "Takes payments at the till", "TENANT_VIEW", "USER_VIEW")],
            },
            new DemoApplication
            {
                Code = "HOSPITALITY", Name = "Hospitality", Description = "Hotels, rooms and bookings",
                Roles = [new DemoRole("Cashier", "Settles guest bills", "TENANT_VIEW"), new DemoRole("Receptionist", "Checks guests in and out", "TENANT_VIEW", "USER_VIEW")],
            },
            new DemoApplication
            {
                Code = "LOGISTICS", Name = "Logistics", Description = "Shipments, vehicles and routes",
                Roles = [new DemoRole("Dispatcher", "Plans routes and assigns vehicles", "TENANT_VIEW", "UNIT_MANAGE")],
            },
            new DemoApplication
            {
                Code = "EDUCATION", Name = "Education", Description = "Courses, classes and students",
                Roles = [new DemoRole("Teacher", "Runs classes", "TENANT_VIEW", "SETTINGS_MANAGE")],
            },
        ],
        Tenants =
        [
            new DemoTenant
            {
                Code = "ALPHA", Name = "Alpha Group",
                TenantAdmin = new DemoPerson("alpha.admin", "Alex", "Morgan"),
                Companies =
                [
                    new DemoCompany
                    {
                        Code = "ALPHA-RETAIL", Name = "Alpha Retail", ApplicationCode = "RETAIL",
                        CompanyAdmin = new DemoPerson("alpha-retail.admin", "Riley", "Stone"),
                        Users = [new DemoPerson("alpha-retail.user1", "Sam", "Carter") { Roles = ["Staff", "Cashier"] }, new DemoPerson("alpha-retail.user2", "Jamie", "Brooks")],
                    },
                    new DemoCompany
                    {
                        Code = "ALPHA-HOSPITALITY", Name = "Alpha Hospitality", ApplicationCode = "HOSPITALITY",
                        CompanyAdmin = new DemoPerson("alpha-hospitality.admin", "Harper", "Reed"),
                        Users = [new DemoPerson("alpha-hospitality.user1", "Drew", "Hayes") { Roles = ["Staff", "Cashier"] }, new DemoPerson("alpha-hospitality.user2", "Casey", "Lane")],
                    },
                ],
            },
            new DemoTenant
            {
                Code = "BETA", Name = "Beta Group",
                TenantAdmin = new DemoPerson("beta.admin", "Blake", "Turner"),
                Companies =
                [
                    new DemoCompany
                    {
                        Code = "BETA-LOGISTICS", Name = "Beta Logistics", ApplicationCode = "LOGISTICS",
                        CompanyAdmin = new DemoPerson("beta-logistics.admin", "Logan", "Fox"),
                        Users = [new DemoPerson("beta-logistics.user1", "Morgan", "Webb") { Roles = ["Staff", "Dispatcher"] }, new DemoPerson("beta-logistics.user2", "Quinn", "Ellis")],
                    },
                    new DemoCompany
                    {
                        Code = "BETA-EDUCATION", Name = "Beta Education", ApplicationCode = "EDUCATION",
                        CompanyAdmin = new DemoPerson("beta-education.admin", "Eden", "Price"),
                        Users = [new DemoPerson("beta-education.user1", "Skyler", "Moore") { Roles = ["Staff", "Teacher"] }, new DemoPerson("beta-education.user2", "Rowan", "Cole")],
                    },
                ],
            },
        ],
    };
}

public class DemoApplication
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public List<DemoRole> Roles { get; set; } = [];
}

public class DemoRole
{
    public DemoRole() { }

    public DemoRole(string name, string description, params string[] permissions)
    {
        Name = name;
        Description = description;
        Permissions = [.. permissions];
    }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public List<string> Permissions { get; set; } = [];
}

public class DemoTenant
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public DemoPerson? TenantAdmin { get; set; }

    public List<DemoCompany> Companies { get; set; } = [];
}

public class DemoCompany
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public string ApplicationCode { get; set; } = string.Empty;

    public DemoPerson? CompanyAdmin { get; set; }

    public List<DemoPerson> Users { get; set; } = [];
}

public class DemoPerson
{
    public DemoPerson() { }

    public DemoPerson(string userName, string firstName, string lastName)
    {
        UserName = userName;
        FirstName = firstName;
        LastName = lastName;
    }

    public string UserName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];
}
