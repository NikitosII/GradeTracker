using EduTrack.Domain.Studies;
using FluentAssertions;

namespace EduTrack.Domain.Tests.Studies;

public class GradeTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Student = Guid.NewGuid();
    private static readonly Guid Subject = Guid.NewGuid();

    [Fact]
    public void Add_sets_audit_fields_and_stamps_creator_as_updater()
    {
        var grade = Grade.Add(Student, Subject, 5, 1m, "well done", Now, Student, Now);

        grade.StudentUserId.Should().Be(Student);
        grade.Value.Should().Be(5);
        grade.CreatedByUserId.Should().Be(Student);
        grade.UpdatedByUserId.Should().Be(Student);
        grade.CreatedAt.Should().Be(Now);
        grade.UpdatedAt.Should().Be(Now);
        grade.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Add_rejects_value_out_of_range(int value)
    {
        var act = () => Grade.Add(Student, Subject, value, 1m, null, Now, Student, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Update_changes_value_and_tracks_editor()
    {
        var grade = Grade.Add(Student, Subject, 3, 1m, null, Now, Student, Now);
        var later = Now.AddHours(2);
        var editor = Guid.NewGuid();

        grade.Update(4, 2m, "fixed", later, editor, later);

        grade.Value.Should().Be(4);
        grade.Weight.Should().Be(2m);
        grade.Comment.Should().Be("fixed");
        grade.UpdatedByUserId.Should().Be(editor);
        grade.UpdatedAt.Should().Be(later);
        grade.CreatedByUserId.Should().Be(Student);
    }

    [Fact]
    public void Delete_marks_soft_deleted()
    {
        var grade = Grade.Add(Student, Subject, 3, 1m, null, Now, Student, Now);
        var later = Now.AddHours(1);

        grade.Delete(Student, later);

        grade.IsDeleted.Should().BeTrue();
        grade.UpdatedAt.Should().Be(later);
    }
}
