using FSIdentity.Domain.Entities.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Identity.Accounts;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.Property(u => u.UserName).IsRequired().HasMaxLength(256);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(256);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PhoneNumber).HasMaxLength(32);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
        builder.Property(u => u.LanguageCode).HasMaxLength(8);
        builder.Property(u => u.TwoFactorSecret).HasMaxLength(128);

        builder.HasIndex(u => u.UserName).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(u => u.Email).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.Property(r => r.Name).IsRequired().HasMaxLength(256);
        builder.Property(r => r.Code).HasMaxLength(32);
        builder.Property(r => r.Description).HasMaxLength(512);

        builder.HasIndex(r => new { r.IdentityTenantId, r.ApplicationTypeId, r.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.Property(p => p.Name).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Code).IsRequired().HasMaxLength(128);
        builder.Property(p => p.Description).HasMaxLength(512);

        builder.HasIndex(p => p.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class UserCompanyConfiguration : IEntityTypeConfiguration<UserCompany>
{
    public void Configure(EntityTypeBuilder<UserCompany> builder)
    {
        builder.ToTable("UserCompanies");

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FSIdentity.Domain.Entities.Organization.Company>().WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.CompanyId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class UserCompanyRoleConfiguration : IEntityTypeConfiguration<UserCompanyRole>
{
    public void Configure(EntityTypeBuilder<UserCompanyRole> builder)
    {
        builder.ToTable("UserCompanyRoles");

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FSIdentity.Domain.Entities.Organization.Company>().WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.CompanyId, x.RoleId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class UserCompanyPermissionConfiguration : IEntityTypeConfiguration<UserCompanyPermission>
{
    public void Configure(EntityTypeBuilder<UserCompanyPermission> builder)
    {
        builder.ToTable("UserCompanyPermissions");

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FSIdentity.Domain.Entities.Organization.Company>().WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.CompanyId, x.PermissionId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(EntityTypeBuilder<LoginAttempt> builder)
    {
        builder.ToTable("LoginAttempts");

        builder.Property(a => a.UserName).IsRequired().HasMaxLength(256);
        builder.Property(a => a.Reason).HasMaxLength(64);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(512);

        builder.HasIndex(a => new { a.UserId, a.CreatedAt });
        builder.HasIndex(a => new { a.UserName, a.CreatedAt });
    }
}

public class UserLockoutConfiguration : IEntityTypeConfiguration<UserLockout>
{
    public void Configure(EntityTypeBuilder<UserLockout> builder)
    {
        builder.ToTable("UserLockouts");

        builder.Property(l => l.Reason).HasMaxLength(256);

        builder.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.UserId, l.LockedUntil });
    }
}

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");

        builder.Property(s => s.IpAddress).HasMaxLength(64);
        builder.Property(s => s.UserAgent).HasMaxLength(512);
        builder.Property(s => s.RevokedReason).HasMaxLength(128);

        builder.HasIndex(s => new { s.UserId, s.RevokedAt });
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);

        builder.HasOne<UserSession>().WithMany().HasForeignKey(t => t.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.SessionId);
    }
}

public class PasswordResetConfiguration : IEntityTypeConfiguration<PasswordReset>
{
    public void Configure(EntityTypeBuilder<PasswordReset> builder)
    {
        builder.ToTable("PasswordResets");

        builder.Property(r => r.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(r => r.IpAddress).HasMaxLength(64);

        builder.HasIndex(r => r.TokenHash).IsUnique();
        builder.HasIndex(r => new { r.UserId, r.CreatedAt });
    }
}

public class EmailConfirmationConfiguration : IEntityTypeConfiguration<EmailConfirmation>
{
    public void Configure(EntityTypeBuilder<EmailConfirmation> builder)
    {
        builder.ToTable("EmailConfirmations");

        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.Property(c => c.TokenHash).IsRequired().HasMaxLength(64);

        builder.HasIndex(c => c.TokenHash).IsUnique();
        builder.HasIndex(c => c.UserId);
    }
}

public class UserRecoveryCodeConfiguration : IEntityTypeConfiguration<UserRecoveryCode>
{
    public void Configure(EntityTypeBuilder<UserRecoveryCode> builder)
    {
        builder.ToTable("UserRecoveryCodes");

        builder.Property(c => c.CodeHash).IsRequired().HasMaxLength(64);

        builder.HasIndex(c => new { c.UserId, c.CodeHash });
    }
}

public class SecurityPolicyConfiguration : IEntityTypeConfiguration<SecurityPolicy>
{
    public void Configure(EntityTypeBuilder<SecurityPolicy> builder)
    {
        builder.ToTable("SecurityPolicies");

        builder.Property(p => p.Name).HasMaxLength(256);

        builder.HasIndex(p => p.IdentityTenantId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class SoDRuleConfiguration : IEntityTypeConfiguration<SoDRule>
{
    public void Configure(EntityTypeBuilder<SoDRule> builder)
    {
        builder.ToTable("SoDRules");

        builder.Property(r => r.Name).IsRequired().HasMaxLength(256);
        builder.Property(r => r.Description).HasMaxLength(512);

        builder.HasOne<Permission>().WithMany().HasForeignKey(r => r.PermissionAId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Permission>().WithMany().HasForeignKey(r => r.PermissionBId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.IdentityTenantId, r.PermissionAId, r.PermissionBId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
