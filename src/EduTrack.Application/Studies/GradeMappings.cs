using EduTrack.Domain.Studies;

namespace EduTrack.Application.Studies;

internal static class GradeMappings
{
    public static GradeDto ToGradeDto(this Grade grade, string subjectName) =>
        new(grade.Id, grade.SubjectId, subjectName, grade.Value, grade.Weight, grade.Comment, grade.OccurredAt);
}
