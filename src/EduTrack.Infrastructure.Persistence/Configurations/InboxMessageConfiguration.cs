using EduTrack.Domain.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTrack.Infrastructure.Persistence.Configurations;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.UpdateId).IsRequired();
        builder.HasIndex(m => m.UpdateId).IsUnique();

        builder.Property(m => m.ReceivedAtUtc).IsRequired();
        builder.Property(m => m.ProcessedAtUtc);
        builder.Property(m => m.Error);
    }
}
