using EduTrack.Domain.Studies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTrack.Infrastructure.Persistence.Configurations;

public sealed class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> builder)
    {
        builder.ToTable("student_profiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId).IsRequired();

        builder.HasIndex(p => p.UserId)
            .IsUnique();

        builder.Property(p => p.FullName)
            .HasMaxLength(StudentProfile.MaxFullNameLength)
            .IsRequired();

        builder.Property(p => p.StudentCode)
            .HasMaxLength(StudentProfile.MaxStudentCodeLength);

        builder.Property(p => p.CreatedAt).IsRequired();
    }
}
