using EduTrack.Domain.Common;

namespace EduTrack.Application.Studies;

public static class GradeErrors
{
    public static readonly Error SubjectNotFound = Error.NotFound("Grades.SubjectNotFound", "Subject not found or inactive.");

    public static readonly Error NotFound = Error.NotFound("Grades.NotFound", "Grade not found.");

    public static readonly Error NotOwner = Error.AccessDenied("Grades.NotOwner", "You can only change your own grades.");
}
