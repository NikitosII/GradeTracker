using EduTrack.Domain.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTrack.Infrastructure.Persistence.Configurations;

public sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("reminders");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.AssignmentId).IsRequired();

        builder.Property(r => r.Kind)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(r => r.SendAtUtc).IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(r => r.RetryCount).IsRequired();

        builder.Property(r => r.LastError)
            .HasMaxLength(Reminder.MaxLastErrorLength);

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasIndex(r => new { r.Status, r.SendAtUtc });
        builder.HasIndex(r => r.AssignmentId);
    }
}
