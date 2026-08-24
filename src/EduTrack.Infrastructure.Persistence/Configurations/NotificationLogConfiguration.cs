using EduTrack.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTrack.Infrastructure.Persistence.Configurations;

public sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("notification_logs");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId).IsRequired();

        builder.Property(n => n.Type)
            .HasConversion<string>()
            .HasMaxLength(48)
            .IsRequired();

        builder.Property(n => n.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(n => n.TelegramMessageId);
        builder.Property(n => n.Error);
        builder.Property(n => n.CreatedAtUtc).IsRequired();
        builder.HasIndex(n => n.UserId);
    }
}
