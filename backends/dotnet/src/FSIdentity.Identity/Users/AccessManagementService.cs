using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Entities.Organization;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Identity.Accounts;
using FSIdentity.Identity.Applications;
using FSIdentity.Identity.Authorization;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Users;

public interface IAccessManagementService
{
    Task<EffectivePermissionsRead> EffectivePermissionsAsync(long userId, long? companyId, CancellationToken cancellationToken);

    Task<CopiedUser> CopyUserAsync(long sourceUserId, UserCopyRequest request, CancellationToken cancellationToken);

    Task<BulkUserImportResult> ImportUsersAsync(BulkUserImportRequest request, CancellationToken cancellationToken);

    Task<RoleRead> CopyRoleAsync(long sourceRoleId, RoleCopyRequest request, CancellationToken cancellationToken);
}

public class AccessManagementService(
    AppDbContext db,
    PermissionResolver resolver,
    IApplicationScopeService applications,
    IUserProvisioning provisioning,
    IUserAdministrationGuard guard,
    ICrudService<Role, RoleRead, RoleCreate, RoleUpdate> roles) : IAccessManagementService
{
    public async Task<EffectivePermissionsRead> EffectivePermissionsAsync(
        long userId, long? companyId, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw new NotFoundException($"User {userId} not found");

        var company = companyId ?? user.ActiveCompanyId;
        if (company is null)
            return new EffectivePermissionsRead { UserId = userId };

        var application = await applications.ResolveActiveAsync(company.Value, user.ActiveApplicationTypeId, cancellationToken);
        var effective = await resolver.ResolveAsync(userId, company.Value, application, cancellationToken);

        return new EffectivePermissionsRead
        {
            UserId = userId,
            CompanyId = company,
            ApplicationTypeId = application,
            Granted = effective.Granted,
            Denied = effective.Denied,
        };
    }

    public async Task<CopiedUser> CopyUserAsync(long sourceUserId, UserCopyRequest request, CancellationToken cancellationToken)
    {
        var source = await db.Users.FirstOrDefaultAsync(u => u.Id == sourceUserId, cancellationToken)
                     ?? throw new NotFoundException($"User {sourceUserId} not found");

        await guard.EnsureCanAdministerAsync(sourceUserId, allowSelf: false, cancellationToken);
        await EnsureNamesAreFreeAsync(request.UserName, request.Email, cancellationToken);

        var password = request.Password ?? provisioning.GenerateTemporaryPassword();
        var profile = new UserProfile
        {
            UserName = request.UserName,
            Email = request.Email,
            FirstName = request.FirstName ?? source.FirstName,
            LastName = request.LastName ?? source.LastName,
            PhoneNumber = null,
            LanguageCode = source.LanguageCode,
        };
        var copy = await provisioning.NewUserAsync(profile, source.IdentityTenantId ?? throw new BadRequestException("The source user has no tenant"),
            password, mustChangePassword: request.Password is null, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Users.Add(copy);
        await db.SaveChangesAsync(cancellationToken);

        if (request.CopyMemberships)
            db.UserCompanies.AddRange(await db.UserCompanies.Where(m => m.UserId == sourceUserId && m.IsActive).AsNoTracking()
                .Select(m => new UserCompany { UserId = copy.Id, CompanyId = m.CompanyId, MembershipType = m.MembershipType, IdentityTenantId = m.IdentityTenantId })
                .ToListAsync(cancellationToken));

        if (request.CopyRoles)
            db.UserCompanyRoles.AddRange(await db.UserCompanyRoles.Where(r => r.UserId == sourceUserId && r.IsActive).AsNoTracking()
                .Select(r => new UserCompanyRole { UserId = copy.Id, CompanyId = r.CompanyId, RoleId = r.RoleId, IdentityTenantId = r.IdentityTenantId })
                .ToListAsync(cancellationToken));

        if (request.CopyPermissions)
            db.UserCompanyPermissions.AddRange(await db.UserCompanyPermissions.Where(p => p.UserId == sourceUserId && p.IsActive).AsNoTracking()
                .Select(p => new UserCompanyPermission { UserId = copy.Id, CompanyId = p.CompanyId, PermissionId = p.PermissionId, IsGranted = p.IsGranted, IdentityTenantId = p.IdentityTenantId })
                .ToListAsync(cancellationToken));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CopiedUser(ToRead(copy), request.Password is null ? password : null);
    }

    public async Task<RoleRead> CopyRoleAsync(long sourceRoleId, RoleCopyRequest request, CancellationToken cancellationToken)
    {
        var source = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == sourceRoleId, cancellationToken)
                     ?? throw new NotFoundException($"Role {sourceRoleId} not found");

        var name = request.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Name == name && r.IdentityTenantId == source.IdentityTenantId, cancellationToken))
            throw new ConflictException($"Role name '{name}' already exists");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var copy = new Role
        {
            Name = name,
            Code = request.Code?.Trim().ToUpperInvariant(),
            Description = source.Description,
            ApplicationTypeId = source.ApplicationTypeId,
            IdentityTenantId = source.IdentityTenantId,
        };
        db.Roles.Add(copy);
        await db.SaveChangesAsync(cancellationToken);

        db.RolePermissions.AddRange(await db.RolePermissions.Where(g => g.RoleId == sourceRoleId && g.IsActive).AsNoTracking()
            .Select(g => new RolePermission { RoleId = copy.Id, PermissionId = g.PermissionId, IdentityTenantId = g.IdentityTenantId })
            .ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await roles.GetByIdAsync(copy.Id, cancellationToken);
    }

    public async Task<BulkUserImportResult> ImportUsersAsync(BulkUserImportRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await provisioning.ResolveTenantAsync(request.TenantId, cancellationToken);
        foreach (var membership in request.Users.Where(u => u.CompanyId is not null).Select(u => u.MembershipType).Distinct())
            await guard.EnsureCanGrantAsync(membership, cancellationToken);

        var taken = await db.Users.IgnoreQueryFilters([FSIdentity.Domain.Data.FSDbContext.TenantFilter])
            .Select(u => new { u.UserName, u.Email }).ToListAsync(cancellationToken);
        var usedNames = new HashSet<string>(taken.Select(t => t.UserName), StringComparer.OrdinalIgnoreCase);
        var usedEmails = new HashSet<string>(taken.Select(t => t.Email), StringComparer.OrdinalIgnoreCase);

        var companies = await db.Companies.Select(c => c.Id).ToListAsync(cancellationToken);
        var knownRoles = await db.Roles.Select(r => r.Id).ToListAsync(cancellationToken);
        var emailCheck = new EmailAddressAttribute();

        var failures = new List<ImportFailure>();
        var valid = new List<(int Row, BulkUserRow Source, string Password, bool Generated)>();

        for (var i = 0; i < request.Users.Count; i++)
        {
            var row = request.Users[i];
            var error = Check(row, emailCheck, companies, knownRoles);
            if (error is null && !usedNames.Add(row.UserName.Trim()))
                error = $"User name '{row.UserName}' is already taken";
            if (error is null && !usedEmails.Add(row.Email.Trim()))
                error = $"E-mail '{row.Email}' is already taken";

            if (error is not null)
            {
                failures.Add(new ImportFailure(i + 1, row.UserName, error));
                continue;
            }

            var generated = string.IsNullOrEmpty(row.Password);
            valid.Add((i + 1, row, generated ? provisioning.GenerateTemporaryPassword() : row.Password!, generated));
        }

        if (request.Atomic && failures.Count > 0)
            return new BulkUserImportResult(0, RolledBack: true, [], failures);

        var imported = new List<ImportedUser>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var (rowNumber, source, password, generated) in valid)
        {
            try
            {
                var profile = new UserProfile
                {
                    UserName = source.UserName, Email = source.Email, FirstName = source.FirstName, LastName = source.LastName,
                };
                var user = await provisioning.NewUserAsync(profile, tenantId, password, mustChangePassword: generated, cancellationToken);
                db.Users.Add(user);
                await db.SaveChangesAsync(cancellationToken);

                if (source.CompanyId is { } companyId)
                {
                    db.UserCompanies.Add(new UserCompany
                    {
                        UserId = user.Id, CompanyId = companyId, MembershipType = source.MembershipType, IdentityTenantId = tenantId,
                    });
                    foreach (var roleId in source.RoleIds.Distinct())
                        db.UserCompanyRoles.Add(new UserCompanyRole
                        {
                            UserId = user.Id, CompanyId = companyId, RoleId = roleId, IdentityTenantId = tenantId,
                        });
                    await db.SaveChangesAsync(cancellationToken);
                }

                imported.Add(new ImportedUser(user.Id, user.UserName, generated ? password : null));
            }
            catch (BadRequestException ex)
            {
                failures.Add(new ImportFailure(rowNumber, source.UserName, ex.Message));
            }
        }

        if (request.Atomic && failures.Count > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new BulkUserImportResult(0, RolledBack: true, [], failures);
        }

        await transaction.CommitAsync(cancellationToken);
        return new BulkUserImportResult(imported.Count, RolledBack: false, imported, failures.OrderBy(f => f.Row).ToList());
    }

    private static string? Check(BulkUserRow row, EmailAddressAttribute emailCheck, List<long> companies, List<long> knownRoles)
    {
        if (string.IsNullOrWhiteSpace(row.UserName))
            return "userName is required";
        if (row.UserName.Length > 256)
            return "userName is too long";
        if (string.IsNullOrWhiteSpace(row.Email) || !emailCheck.IsValid(row.Email))
            return "email is not valid";
        if (string.IsNullOrWhiteSpace(row.FirstName) || string.IsNullOrWhiteSpace(row.LastName))
            return "firstName and lastName are required";
        if (row.CompanyId is { } companyId && !companies.Contains(companyId))
            return $"Company {companyId} does not exist";
        if (row.CompanyId is null && row.RoleIds.Count > 0)
            return "roleIds need a companyId";
        if (row.RoleIds.Any(id => !knownRoles.Contains(id)))
            return "a role does not exist";
        return null;
    }

    private async Task EnsureNamesAreFreeAsync(string userName, string email, CancellationToken cancellationToken)
    {
        var name = userName.Trim().ToLowerInvariant();
        var mail = email.Trim().ToLowerInvariant();
        var users = db.Users.IgnoreQueryFilters([FSIdentity.Domain.Data.FSDbContext.TenantFilter]);

        if (await users.AnyAsync(u => u.UserName == name, cancellationToken))
            throw new ConflictException($"User user name '{name}' already exists");
        if (await users.AnyAsync(u => u.Email == mail, cancellationToken))
            throw new ConflictException($"User email '{mail}' already exists");
    }

    private static UserRead ToRead(User user) => new()
    {
        Id = user.Id,
        CreatedBy = user.CreatedBy,
        CreatedAt = user.CreatedAt,
        IsActive = user.IsActive,
        UserName = user.UserName,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        PhoneNumber = user.PhoneNumber,
        EmailConfirmed = user.EmailConfirmed,
        ShouldChangePasswordOnNextLogin = user.ShouldChangePasswordOnNextLogin,
        ActiveCompanyId = user.ActiveCompanyId,
        LanguageCode = user.LanguageCode,
        IsPlatformAdmin = user.IsPlatformAdmin,
        TwoFactorEnabled = user.TwoFactorEnabled,
        TenantId = user.IdentityTenantId,
    };
}
