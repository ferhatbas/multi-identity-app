using FSIdentity.Domain.Entities.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Identity.Geography;

public class StateConfiguration : IEntityTypeConfiguration<State>
{
    public void Configure(EntityTypeBuilder<State> builder)
    {
        builder.ToTable("States");

        builder.Property(s => s.Code).IsRequired().HasMaxLength(10);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(256);
        builder.Property(s => s.Description).HasMaxLength(4000);

        builder.HasIndex(s => s.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(s => s.Name);

        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(s => s.CountryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}