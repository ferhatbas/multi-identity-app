using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Entities.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Identity.Workspace;

public class UnitTypeConfiguration : IEntityTypeConfiguration<UnitType>
{
    public void Configure(EntityTypeBuilder<UnitType> builder)
    {
        builder.ToTable("UnitTypes");
        builder.Property(t => t.Code).IsRequired().HasMaxLength(32);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(128);
        builder.HasIndex(t => t.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");
        builder.Property(u => u.Code).IsRequired().HasMaxLength(32);
        builder.Property(u => u.Name).IsRequired().HasMaxLength(256);
        builder.Property(u => u.Description).HasMaxLength(512);

        builder.HasOne<Unit>().WithMany().HasForeignKey(u => u.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UnitType>().WithMany().HasForeignKey(u => u.UnitTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(u => u.ManagerUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => new { u.IdentityCompanyId, u.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(u => u.ParentId);
    }
}

public class UserUnitConfiguration : IEntityTypeConfiguration<UserUnit>
{
    public void Configure(EntityTypeBuilder<UserUnit> builder)
    {
        builder.ToTable("UserUnits");
        builder.HasOne<User>().WithMany().HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Unit>().WithMany().HasForeignKey(u => u.UnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => new { u.UserId, u.UnitId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSetting>
{
    public void Configure(EntityTypeBuilder<TenantSetting> builder)
    {
        builder.ToTable("TenantSettings");
        builder.Property(s => s.Key).IsRequired().HasMaxLength(64);
        builder.Property(s => s.Value).HasMaxLength(4000);
        builder.HasIndex(s => new { s.IdentityTenantId, s.Key }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class CompanySettingConfiguration : IEntityTypeConfiguration<CompanySetting>
{
    public void Configure(EntityTypeBuilder<CompanySetting> builder)
    {
        builder.ToTable("CompanySettings");
        builder.Property(s => s.Key).IsRequired().HasMaxLength(64);
        builder.Property(s => s.Value).HasMaxLength(4000);
        builder.HasIndex(s => new { s.IdentityCompanyId, s.Key }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class UserSettingConfiguration : IEntityTypeConfiguration<UserSetting>
{
    public void Configure(EntityTypeBuilder<UserSetting> builder)
    {
        builder.ToTable("UserSettings");
        builder.Property(s => s.Key).IsRequired().HasMaxLength(64);
        builder.Property(s => s.Value).HasMaxLength(4000);
        builder.HasIndex(s => new { s.UserId, s.Key }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ComponentTypeConfiguration : IEntityTypeConfiguration<ComponentType>
{
    public void Configure(EntityTypeBuilder<ComponentType> builder)
    {
        builder.ToTable("ComponentTypes");
        builder.Property(t => t.Code).IsRequired().HasMaxLength(32);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(128);
        builder.Property(t => t.Description).HasMaxLength(512);
        builder.HasIndex(t => t.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class DashboardWidgetConfiguration : IEntityTypeConfiguration<DashboardWidget>
{
    public void Configure(EntityTypeBuilder<DashboardWidget> builder)
    {
        builder.ToTable("DashboardWidgets");
        builder.Property(w => w.Code).IsRequired().HasMaxLength(48);
        builder.Property(w => w.Title).IsRequired().HasMaxLength(256);
        builder.Property(w => w.Description).HasMaxLength(512);
        builder.Property(w => w.ModuleCode).HasMaxLength(48);

        builder.HasOne<ComponentType>().WithMany().HasForeignKey(w => w.ComponentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Permission>().WithMany().HasForeignKey(w => w.PermissionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(w => w.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class DashboardLayoutConfiguration : IEntityTypeConfiguration<DashboardLayout>
{
    public void Configure(EntityTypeBuilder<DashboardLayout> builder)
    {
        builder.ToTable("DashboardLayouts");
        builder.Property(l => l.Name).IsRequired().HasMaxLength(256);
        builder.Property(l => l.LayoutJson).IsRequired();
        builder.HasIndex(l => new { l.IdentityTenantId, l.Scope, l.ScopeId, l.ApplicationTypeId, l.CompanyId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
