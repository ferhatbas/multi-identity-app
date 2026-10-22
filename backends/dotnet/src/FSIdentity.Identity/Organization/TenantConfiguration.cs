using FSIdentity.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Identity.Organization;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.Property(t => t.Name).IsRequired().HasMaxLength(256);
        builder.Property(t => t.Code).IsRequired().HasMaxLength(32);
        builder.Property(t => t.LanguageCode).HasMaxLength(8);

        builder.HasIndex(t => t.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
