using FSIdentity.Domain.Entities.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Identity.Geography;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries");

        builder.Property(c => c.Name).IsRequired().HasMaxLength(256);
        builder.Property(c => c.NameGlobal).HasMaxLength(256);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(16);
        builder.Property(c => c.Region).HasMaxLength(256);
        builder.Property(c => c.FormatCode).IsRequired().HasMaxLength(8);
        builder.Property(c => c.LanguageCode).HasMaxLength(8);
        builder.Property(c => c.DateFormat).HasMaxLength(16);
        builder.Property(c => c.DateTimeFormat).HasMaxLength(50);
        builder.Property(c => c.CurrencyCode).HasMaxLength(8);
        builder.Property(c => c.CurrencySymbol).HasMaxLength(8);
        builder.Property(c => c.PriceFormat).HasMaxLength(32);
        builder.Property(c => c.TimeZone).HasMaxLength(64);

        builder.HasIndex(c => c.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}