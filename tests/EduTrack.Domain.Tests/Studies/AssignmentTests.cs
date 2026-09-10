using EduTrack.Domain.Studies;
using FluentAssertions;

namespace EduTrack.Domain.Tests.Studies;

public class AssignmentTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Subject = Guid.NewGuid();

    [Fact]
    public void Create_sets_owner_and_audit_fields()
    {
        var due = Now.AddDays(3);

        var assignment = Assignment.Create(
            Owner, Subject, AssignmentType.Homework, "Essay", "1000 words", due, Owner, Now);

        assignment.OwnerUserId.Should().Be(Owner);
        assignment.Type.Should().Be(AssignmentType.Homework);
        assignment.Title.Should().Be("Essay");
        assignment.DueAtUtc.Should().Be(due);
        assignment.CreatedByUserId.Should().Be(Owner);
        assignment.UpdatedByUserId.Should().Be(Owner);
        assignment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Update_changes_fields_and_tracks_editor()
    {
        var assignment = Assignment.Create(
            Owner, Subject, AssignmentType.Homework, "Essay", null, Now.AddDays(3), Owner, Now);
        var later = Now.AddHours(5);
        var editor = Guid.NewGuid();

        assignment.Update(AssignmentType.Exam, "Final", "hall A", Now.AddDays(10), editor, later);

        assignment.Type.Should().Be(AssignmentType.Exam);
        assignment.Title.Should().Be("Final");
        assignment.UpdatedByUserId.Should().Be(editor);
        assignment.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void Delete_marks_soft_deleted()
    {
        var assignment = Assignment.Create(
            Owner, Subject, AssignmentType.Test, "Quiz", null, Now.AddDays(1), Owner, Now);

        assignment.Delete(Owner, Now.AddHours(1));

        assignment.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Archive_sets_flag_and_timestamp()
    {
        var assignment = Assignment.Create(
            Owner, Subject, AssignmentType.Test, "Quiz", null, Now.AddDays(1), Owner, Now);
        var editor = Guid.NewGuid();
        var when = Now.AddHours(2);

        assignment.Archive(editor, when);

        assignment.IsArchived.Should().BeTrue();
        assignment.ArchivedAt.Should().Be(when);
        assignment.UpdatedByUserId.Should().Be(editor);
        assignment.UpdatedAt.Should().Be(when);
    }

    [Fact]
    public void Unarchive_clears_flag_and_timestamp()
    {
        var assignment = Assignment.Create(
            Owner, Subject, AssignmentType.Test, "Quiz", null, Now.AddDays(1), Owner, Now);
        assignment.Archive(Owner, Now.AddHours(1));

        assignment.Unarchive(Owner, Now.AddHours(3));

        assignment.IsArchived.Should().BeFalse();
        assignment.ArchivedAt.Should().BeNull();
    }

    [Fact]
    public void Archive_is_idempotent_and_does_not_move_the_timestamp()
    {
        var assignment = Assignment.Create(
            Owner, Subject, AssignmentType.Test, "Quiz", null, Now.AddDays(1), Owner, Now);
        var first = Now.AddHours(1);
        assignment.Archive(Owner, first);

        assignment.Archive(Owner, Now.AddHours(5));

        assignment.ArchivedAt.Should().Be(first);
    }
}
