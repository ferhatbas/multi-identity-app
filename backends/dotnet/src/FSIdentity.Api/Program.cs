using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Crud;
using FSIdentity.Api.Startup;
using FSIdentity.Domain.Interfaces.MultiTenant;
using FSIdentity.Domain.Services;
using FSIdentity.Api.Middleware;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using FSIdentity.Domain.Data;
using FSIdentity.Identity.Geography;
using FSIdentity.Identity.Localization;
using FSIdentity.Identity.Organization;
using FSIdentity.Domain.Events;
using FSIdentity.Identity.Applications;
using FSIdentity.Identity.Auth;
using FSIdentity.Identity.Localization;
using FSIdentity.Identity.Menus;
using FSIdentity.Identity.Security;
using FSIdentity.Identity.Users;
using FSIdentity.Identity.Notifications;
using FSIdentity.Identity.Seeding;
using FSIdentity.Identity.Workspace;
using FSIdentity.Identity.Authorization;
using FSIdentity.Domain.Security;
using FSIdentity.Domain.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options => options.AddBearerScheme());

builder.Services.ConfigureHttpJsonOptions(options => ApiJson.Configure(options.SerializerOptions));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("AllowFrontend", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders(RequestIdMiddleware.HeaderName)));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<FSDbContext>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddCrud(typeof(CountryController).Assembly);
builder.Services.AddDomainEvents(typeof(CountryController).Assembly);
builder.Services.AddScoped<GeographySeeder>();

builder.Services.AddScoped<ApplicationSeeder>();
builder.Services.AddScoped<IdentitySeeder>();
builder.Services.AddScoped<DemoSeeder>();
builder.Services.AddMemoryCache();
builder.Services.Configure<ModuleScopeOptions>(builder.Configuration.GetSection(ModuleScopeOptions.SectionName));
builder.Services.AddScoped<IApplicationScopeService, ApplicationScopeService>();
builder.Services.AddScoped<IModuleScopeAccessor, ModuleScopeAccessor>();

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<TraceIdResultFilter>();
        options.Filters.Add<SessionRestrictionGate>();
        options.Filters.Add<ImpersonationGate>();
        options.Filters.Add<ReaderGuard>();
    })
    .AddApplicationPart(typeof(CountryController).Assembly)
    .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions))
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = ValidationResponse.Create;

        options.SuppressMapClientErrors = true;
    });
    
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IMultiTenantAccessor, MultiTenantAccessor>();
builder.Services.AddScoped<IAuditContext, HttpAuditContext>();
builder.Services.AddIdentityAuth(builder.Configuration);
builder.Services.AddScoped<PermissionResolver>();
builder.Services.AddScoped<ISecurityPolicyService, SecurityPolicyService>();
builder.Services.AddScoped<ISoDGuard, SoDGuard>();
builder.Services.AddScoped<ILanguageResolver, LanguageResolver>();
builder.Services.AddScoped<INavigationService, NavigationService>();
builder.Services.AddScoped<IMenuStudioService, MenuStudioService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IPushSender, LoggingPushSender>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
builder.Services.AddScoped<IPushNotifier>(sp => sp.GetRequiredService<NotificationService>());
builder.Services.AddScoped<IUserProvisioning, UserProvisioning>();
builder.Services.AddScoped<IAccessManagementService, AccessManagementService>();
builder.Services.AddScoped<ICallerAccess, CallerAccess>();
builder.Services.AddScoped<IUserAdministrationGuard, UserAdministrationGuard>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    Console.WriteLine("Database is up to date.");
    return;
}

if (args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope();
    using (AuditLogSuppression.Suppress())
    {
        await scope.ServiceProvider.GetRequiredService<GeographySeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<ApplicationSeeder>().SeedAsync();
    }
    return;
}

if (args.Contains("--seed-identity"))
{
    using var scope = app.Services.CreateScope();
    var options = builder.Configuration.GetSection(IdentitySeedOptions.SectionName).Get<IdentitySeedOptions>() ?? new IdentitySeedOptions();
    try
    {
        var result = await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(options);
        Console.WriteLine($"Seeded tenant {result.TenantId}, company {result.CompanyId}; administrator '{result.AdminUserName}' (must change the password at first sign-in).");
        if (result.GeneratedPassword is not null)
            Console.WriteLine($"Generated password (shown once): {result.GeneratedPassword}");
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return;
    }

    return;
}
if (args.Contains("--seed-demo"))
{
    using var scope = app.Services.CreateScope();
    var demo = builder.Configuration.GetSection(DemoSeedOptions.SectionName).Get<DemoSeedOptions>() ?? new DemoSeedOptions();
    try
    {
        var definition = string.IsNullOrWhiteSpace(demo.File) ? DemoDefinition.Default() : DemoDefinition.Load(demo.File);
        using (AuditLogSuppression.Suppress())
            await scope.ServiceProvider.GetRequiredService<GeographySeeder>().SeedAsync();
        var result = await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync(definition, demo);

        Console.WriteLine($"Demo data created. Everybody signs in with the password: {result.Password}");
        Console.WriteLine();
        Console.WriteLine($"{"USER NAME",-26}{"NAME",-22}{"TENANT",-12}{"COMPANY",-20}{"APPLICATION",-14}ROLE");
        foreach (var a in result.Accounts)
            Console.WriteLine($"{a.UserName,-26}{a.FullName,-22}{a.Tenant,-12}{a.Company,-20}{a.Application,-14}{a.Rank}");
    }
    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine(ex.Message);
        return;
    }

    return;
}
app.UseRequestId();
app.UseAccessLog();
app.UseExceptionHandler();
app.UseStatusCodePages(StatusCodeResponses.WriteAsync);
app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapSwaggerPage();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
