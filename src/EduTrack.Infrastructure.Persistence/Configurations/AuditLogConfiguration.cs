using EduTrack.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTrack.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .HasMaxLength(AuditLog.MaxActionLength)
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasMaxLength(AuditLog.MaxEntityTypeLength)
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasMaxLength(AuditLog.MaxEntityIdLength);

        builder.Property(a => a.OldValue);
        builder.Property(a => a.NewValue);
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UserId);
    }
}
