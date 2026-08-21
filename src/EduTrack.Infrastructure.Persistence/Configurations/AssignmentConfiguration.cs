using EduTrack.Domain.Studies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTrack.Infrastructure.Persistence.Configurations;

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.OwnerUserId).IsRequired();
        builder.Property(a => a.SubjectId).IsRequired();

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(a => a.Title)
            .HasMaxLength(Assignment.MaxTitleLength)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasMaxLength(Assignment.MaxDescriptionLength);

        builder.Property(a => a.DueAtUtc).IsRequired();
        builder.Property(a => a.CreatedByUserId).IsRequired();
        builder.Property(a => a.UpdatedByUserId).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();
        builder.Property(a => a.IsDeleted).IsRequired();

        builder.HasIndex(a => new { a.OwnerUserId, a.DueAtUtc });
        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
