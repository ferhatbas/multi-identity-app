using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Identity.Notifications;

public class UserPushTokenConfiguration : IEntityTypeConfiguration<UserPushToken>
{
    public void Configure(EntityTypeBuilder<UserPushToken> builder)
    {
        builder.ToTable("UserPushTokens");
        builder.Property(t => t.Token).IsRequired().HasMaxLength(1024);
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(t => t.Platform).IsRequired().HasMaxLength(16);
        builder.Property(t => t.DeviceName).HasMaxLength(128);

        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => t.TokenHash).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(t => t.UserId);
    }
}

public class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.ToTable("UserNotificationPreferences");
        builder.Property(p => p.Category).IsRequired().HasMaxLength(32);
        builder.Property(p => p.Channel).IsRequired().HasMaxLength(16);

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.UserId, p.Category, p.Channel }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PushTicketConfiguration : IEntityTypeConfiguration<PushTicket>
{
    public void Configure(EntityTypeBuilder<PushTicket> builder)
    {
        builder.ToTable("PushTickets");
        builder.Property(t => t.Category).IsRequired().HasMaxLength(32);
        builder.Property(t => t.Title).IsRequired().HasMaxLength(256);
        builder.Property(t => t.Body).IsRequired().HasMaxLength(2000);
        builder.Property(t => t.ProviderTicketId).HasMaxLength(256);
        builder.Property(t => t.Error).HasMaxLength(1000);

        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserPushToken>().WithMany().HasForeignKey(t => t.PushTokenId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.UserId, t.CreatedAt });
    }
}
