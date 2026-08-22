using EduTrack.Domain.Studies;

namespace EduTrack.Application.Studies;

internal static class AssignmentMappings
{
    public static AssignmentDto ToAssignmentDto(this Assignment assignment, string subjectName) =>
        new(assignment.Id, assignment.SubjectId, subjectName, assignment.Type, assignment.Title, assignment.Description, assignment.DueAtUtc);
}
