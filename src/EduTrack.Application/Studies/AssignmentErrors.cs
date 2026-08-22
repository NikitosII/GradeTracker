using EduTrack.Domain.Common;

namespace EduTrack.Application.Studies;

public static class AssignmentErrors
{
    public static readonly Error SubjectNotFound = Error.NotFound("Deadlines.SubjectNotFound", "Subject not found or inactive.");

    public static readonly Error NotFound = Error.NotFound("Deadlines.NotFound", "Deadline not found.");

    public static readonly Error NotOwner = Error.AccessDenied("Deadlines.NotOwner", "You can only change your own deadlines.");
}
