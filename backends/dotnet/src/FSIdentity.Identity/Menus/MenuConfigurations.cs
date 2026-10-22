using FSIdentity.Domain.Entities.Applications;
using FSIdentity.Domain.Entities.Menus;
using FSIdentity.Domain.Entities.Organization;
using FSIdentity.Domain.Entities.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Identity.Menus;

public class MenuTypeConfiguration : IEntityTypeConfiguration<MenuType>
{
    public void Configure(EntityTypeBuilder<MenuType> builder)
    {
        builder.ToTable("MenuTypes");
        builder.Property(t => t.Code).IsRequired().HasMaxLength(32);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(128);
        builder.HasIndex(t => t.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable("Menus");
        builder.Property(m => m.Code).IsRequired().HasMaxLength(64);
        builder.Property(m => m.Title).IsRequired().HasMaxLength(256);
        builder.Property(m => m.Path).HasMaxLength(256);
        builder.Property(m => m.Icon).HasMaxLength(64);
        builder.Property(m => m.ModuleCode).HasMaxLength(48);
        builder.Property(m => m.FeatureCode).HasMaxLength(64);

        builder.HasOne<Menu>().WithMany().HasForeignKey(m => m.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MenuType>().WithMany().HasForeignKey(m => m.MenuTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationType>().WithMany().HasForeignKey(m => m.ApplicationTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Permission>().WithMany().HasForeignKey(m => m.PermissionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(m => m.ParentId);
    }
}

public class MenuLocalizationConfiguration : IEntityTypeConfiguration<MenuLocalization>
{
    public void Configure(EntityTypeBuilder<MenuLocalization> builder)
    {
        builder.ToTable("MenuLocalizations");
        builder.Property(l => l.LanguageCode).IsRequired().HasMaxLength(8);
        builder.Property(l => l.Title).IsRequired().HasMaxLength(256);
        builder.HasOne<Menu>().WithMany().HasForeignKey(l => l.MenuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.MenuId, l.LanguageCode }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class MenuCompanyTypeConfiguration : IEntityTypeConfiguration<MenuCompanyType>
{
    public void Configure(EntityTypeBuilder<MenuCompanyType> builder)
    {
        builder.ToTable("MenuCompanyTypes");
        builder.HasOne<Menu>().WithMany().HasForeignKey(l => l.MenuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyType>().WithMany().HasForeignKey(l => l.CompanyTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.MenuId, l.CompanyTypeId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class MenuPermissionOverrideConfiguration : IEntityTypeConfiguration<MenuPermissionOverride>
{
    public void Configure(EntityTypeBuilder<MenuPermissionOverride> builder)
    {
        builder.ToTable("MenuPermissionOverrides");
        builder.HasOne<Menu>().WithMany().HasForeignKey(o => o.MenuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Permission>().WithMany().HasForeignKey(o => o.PermissionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.IdentityTenantId, o.MenuId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class MenuFeatureToggleConfiguration : IEntityTypeConfiguration<MenuFeatureToggle>
{
    public void Configure(EntityTypeBuilder<MenuFeatureToggle> builder)
    {
        builder.ToTable("MenuFeatureToggles");
        builder.Property(t => t.FeatureCode).IsRequired().HasMaxLength(64);
        builder.HasIndex(t => new { t.IdentityTenantId, t.FeatureCode }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class MenuApplicationTypeLayoutConfiguration : IEntityTypeConfiguration<MenuApplicationTypeLayout>
{
    public void Configure(EntityTypeBuilder<MenuApplicationTypeLayout> builder)
    {
        builder.ToTable("MenuApplicationTypeLayouts");
        builder.HasOne<ApplicationType>().WithMany().HasForeignKey(l => l.ApplicationTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Menu>().WithMany().HasForeignKey(l => l.MenuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.ApplicationTypeId, l.MenuId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class MenuCompanyTypeLayoutConfiguration : IEntityTypeConfiguration<MenuCompanyTypeLayout>
{
    public void Configure(EntityTypeBuilder<MenuCompanyTypeLayout> builder)
    {
        builder.ToTable("MenuCompanyTypeLayouts");
        builder.HasOne<CompanyType>().WithMany().HasForeignKey(l => l.CompanyTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Menu>().WithMany().HasForeignKey(l => l.MenuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.CompanyTypeId, l.MenuId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
