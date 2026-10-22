using FSIdentity.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Identity.Organization;

public class CompanyTypeConfiguration : IEntityTypeConfiguration<CompanyType>
{
    public void Configure(EntityTypeBuilder<CompanyType> builder)
    {
        builder.ToTable("CompanyTypes");

        builder.Property(t => t.Name).IsRequired().HasMaxLength(256);
        builder.Property(t => t.Code).IsRequired().HasMaxLength(16);
        builder.Property(t => t.Description).HasMaxLength(512);

        builder.HasIndex(t => t.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
