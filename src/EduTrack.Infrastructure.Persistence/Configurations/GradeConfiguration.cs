using EduTrack.Domain.Studies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTrack.Infrastructure.Persistence.Configurations;

public sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.ToTable("grades");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.StudentUserId).IsRequired();
        builder.Property(g => g.SubjectId).IsRequired();
        builder.Property(g => g.Value).IsRequired();

        builder.Property(g => g.Weight)
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(g => g.Comment)
            .HasMaxLength(Grade.MaxCommentLength);

        builder.Property(g => g.OccurredAt).IsRequired();
        builder.Property(g => g.CreatedByUserId).IsRequired();
        builder.Property(g => g.UpdatedByUserId).IsRequired();
        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.UpdatedAt).IsRequired();
        builder.Property(g => g.IsDeleted).IsRequired();
        builder.Property(g => g.IsArchived).IsRequired();
        builder.Property(g => g.ArchivedAt);

        builder.HasIndex(g => new { g.StudentUserId, g.SubjectId });
        builder.HasIndex(g => new { g.StudentUserId, g.IsArchived });
        builder.HasQueryFilter(g => !g.IsDeleted && !g.IsArchived);
    }
}
