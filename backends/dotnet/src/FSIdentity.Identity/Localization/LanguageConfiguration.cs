using FSIdentity.Domain.Entities.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Identity.Localization;

public class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("Languages");

        builder.Property(l => l.Code).IsRequired().HasMaxLength(8);
        builder.Property(l => l.Name).IsRequired().HasMaxLength(64);
        builder.Property(l => l.NativeName).HasMaxLength(64);

        builder.HasIndex(l => l.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}