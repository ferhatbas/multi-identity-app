using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Entities.Applications;
using FSIdentity.Domain.Entities.Geography;
using FSIdentity.Domain.Entities.Menus;
using FSIdentity.Domain.Entities.Workspace;
using FSIdentity.Domain.Entities.Localization;
using FSIdentity.Domain.Entities.Notifications;
using FSIdentity.Domain.Entities.Organization;
using FSIdentity.Domain.Interfaces.MultiTenant;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Data;

public class AppDbContext(
    DbContextOptions<AppDbContext> options, IMultiTenantAccessor mtx, TimeProvider? clock = null, IAuditContext? auditContext = null)
    : FSDbContext(options, mtx, clock, auditContext)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyType> CompanyTypes => Set<CompanyType>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<State> States => Set<State>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<ApplicationType> ApplicationTypes => Set<ApplicationType>();
    public DbSet<AppModule> AppModules => Set<AppModule>();
    public DbSet<CompanyTypeApplicationType> CompanyTypeApplicationTypes => Set<CompanyTypeApplicationType>();
    public DbSet<ApplicationTypeModule> ApplicationTypeModules => Set<ApplicationTypeModule>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();
    public DbSet<UserCompanyRole> UserCompanyRoles => Set<UserCompanyRole>();
    public DbSet<UserCompanyPermission> UserCompanyPermissions => Set<UserCompanyPermission>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<UserLockout> UserLockouts => Set<UserLockout>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<EmailConfirmation> EmailConfirmations => Set<EmailConfirmation>();
    public DbSet<UserRecoveryCode> UserRecoveryCodes => Set<UserRecoveryCode>();
    public DbSet<SecurityPolicy> SecurityPolicies => Set<SecurityPolicy>();
    public DbSet<SoDRule> SoDRules => Set<SoDRule>();
    public DbSet<MenuType> MenuTypes => Set<MenuType>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuLocalization> MenuLocalizations => Set<MenuLocalization>();
    public DbSet<MenuCompanyType> MenuCompanyTypes => Set<MenuCompanyType>();
    public DbSet<MenuPermissionOverride> MenuPermissionOverrides => Set<MenuPermissionOverride>();
    public DbSet<MenuFeatureToggle> MenuFeatureToggles => Set<MenuFeatureToggle>();
    public DbSet<MenuApplicationTypeLayout> MenuApplicationTypeLayouts => Set<MenuApplicationTypeLayout>();
    public DbSet<MenuCompanyTypeLayout> MenuCompanyTypeLayouts => Set<MenuCompanyTypeLayout>();
    public DbSet<UnitType> UnitTypes => Set<UnitType>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<UserUnit> UserUnits => Set<UserUnit>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<CompanySetting> CompanySettings => Set<CompanySetting>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<ComponentType> ComponentTypes => Set<ComponentType>();
    public DbSet<DashboardWidget> DashboardWidgets => Set<DashboardWidget>();
    public DbSet<DashboardLayout> DashboardLayouts => Set<DashboardLayout>();
    public DbSet<UserPushToken> UserPushTokens => Set<UserPushToken>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();
    public DbSet<PushTicket> PushTickets => Set<PushTicket>();
    public const string SqlServerCollation = "Latin1_General_100_CI_AI";

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        if (Database.IsSqlServer())
            UseCollationOnEveryStringColumn(modelBuilder);
    }

    private static void UseCollationOnEveryStringColumn(ModelBuilder modelBuilder)
    {
        var stringProperties = modelBuilder.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties())
            .Where(property => property.ClrType == typeof(string));

        foreach (var property in stringProperties)
            property.SetCollation(SqlServerCollation);
    }
}