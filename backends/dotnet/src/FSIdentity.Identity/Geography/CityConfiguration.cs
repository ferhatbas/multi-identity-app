using FSIdentity.Domain.Entities.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Identity.Geography;

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("Cities");

        builder.Property(c => c.Code).IsRequired().HasMaxLength(10);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(256);
        builder.Property(c => c.Description).HasMaxLength(4000);

        builder.HasIndex(c => c.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(c => c.Name);

        builder.HasOne<State>()
            .WithMany()
            .HasForeignKey(c => c.StateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}