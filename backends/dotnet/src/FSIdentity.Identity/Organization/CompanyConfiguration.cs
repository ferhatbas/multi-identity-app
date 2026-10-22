using FSIdentity.Domain.Entities.Geography;
using FSIdentity.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Identity.Organization;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.Property(c => c.Name).IsRequired().HasMaxLength(256);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(32);
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.PhoneNumber).HasMaxLength(32);
        builder.Property(c => c.Website).HasMaxLength(256);
        builder.Property(c => c.Address).HasMaxLength(256);
        builder.Property(c => c.Address2).HasMaxLength(256);
        builder.Property(c => c.City).HasMaxLength(256);
        builder.Property(c => c.State).HasMaxLength(256);
        builder.Property(c => c.Zip).HasMaxLength(64);
        builder.Property(c => c.LanguageCode).HasMaxLength(8);

        builder.Ignore(c => c.IdentityCompanyId);

        builder.HasIndex(c => new { c.IdentityTenantId, c.Code }).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne<Tenant>().WithMany().HasForeignKey(c => c.IdentityTenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyType>().WithMany().HasForeignKey(c => c.CompanyTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Country>().WithMany().HasForeignKey(c => c.CountryId).OnDelete(DeleteBehavior.Restrict);
    }
}
