using EduTrack.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTrack.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .HasMaxLength(OutboxMessage.MaxTypeLength)
            .IsRequired();

        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.OccurredAtUtc).IsRequired();
        builder.Property(m => m.ProcessedAtUtc);
        builder.Property(m => m.Error);
        builder.HasIndex(m => m.ProcessedAtUtc);
    }
}
