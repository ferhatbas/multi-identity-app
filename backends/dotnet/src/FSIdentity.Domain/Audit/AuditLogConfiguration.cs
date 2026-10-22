using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSIdentity.Domain.Audit;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(a => a.EntityName).IsRequired().HasMaxLength(128);
        builder.Property(a => a.EntityId).HasMaxLength(64);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(32);
        builder.Property(a => a.UserName).HasMaxLength(128);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(512);
        builder.Property(a => a.CorrelationId).HasMaxLength(64);
        builder.Property(a => a.ChangedFields).HasMaxLength(2048);

        builder.HasIndex(a => new { a.AuditTenantId, a.CreatedAt });
        builder.HasIndex(a => new { a.UserId, a.CreatedAt });
        builder.HasIndex(a => new { a.EntityName, a.EntityId });

        builder.HasIndex(a => a.CreatedAt);
    }
}
