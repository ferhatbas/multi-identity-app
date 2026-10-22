using FSIdentity.Domain.Entities.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Identity.Applications;

public class ApplicationTypeConfiguration : IEntityTypeConfiguration<ApplicationType>
{
    public void Configure(EntityTypeBuilder<ApplicationType> builder)
    {
        builder.ToTable("ApplicationTypes");

        builder.Property(a => a.Name).IsRequired().HasMaxLength(256);
        builder.Property(a => a.Code).IsRequired().HasMaxLength(32);
        builder.Property(a => a.Description).HasMaxLength(512);
        builder.Property(a => a.Icon).HasMaxLength(64);

        builder.HasIndex(a => a.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class AppModuleConfiguration : IEntityTypeConfiguration<AppModule>
{
    public void Configure(EntityTypeBuilder<AppModule> builder)
    {
        builder.ToTable("AppModules");

        builder.Property(m => m.Code).IsRequired().HasMaxLength(48);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(128);
        builder.Property(m => m.Description).HasMaxLength(512);

        builder.HasIndex(m => m.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class CompanyTypeApplicationTypeConfiguration : IEntityTypeConfiguration<CompanyTypeApplicationType>
{
    public void Configure(EntityTypeBuilder<CompanyTypeApplicationType> builder)
    {
        builder.ToTable("CompanyTypeApplicationTypes");

        builder.HasOne(m => m.CompanyType).WithMany().HasForeignKey(m => m.CompanyTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.ApplicationType).WithMany().HasForeignKey(m => m.ApplicationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.CompanyTypeId, m.ApplicationTypeId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ApplicationTypeModuleConfiguration : IEntityTypeConfiguration<ApplicationTypeModule>
{
    public void Configure(EntityTypeBuilder<ApplicationTypeModule> builder)
    {
        builder.ToTable("ApplicationTypeModules");

        builder.HasOne(m => m.ApplicationType).WithMany().HasForeignKey(m => m.ApplicationTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.AppModule).WithMany().HasForeignKey(m => m.AppModuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.ApplicationTypeId, m.AppModuleId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}