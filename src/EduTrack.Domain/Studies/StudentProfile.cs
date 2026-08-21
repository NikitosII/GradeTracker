namespace EduTrack.Domain.Studies;

/// <summary>
/// Academic profile attached to a student user.
/// </summary>
public class StudentProfile
{
    public const int MaxFullNameLength = 256;
    public const int MaxStudentCodeLength = 32;

    private StudentProfile()
    {
    }

    private StudentProfile(
        Guid id,
        Guid userId,
        string fullName,
        string? studentCode,
        Guid? groupId,
        DateTime nowUtc)
    {
        Id = id;
        UserId = userId;
        FullName = fullName;
        StudentCode = studentCode;
        GroupId = groupId;
        CreatedAt = nowUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string FullName { get; private set; } = null!;
    public string? StudentCode { get; private set; }
    public Guid? GroupId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static StudentProfile Create(
        Guid userId,
        string fullName,
        string? studentCode,
        Guid? groupId,
        DateTime nowUtc)
        => new(Guid.NewGuid(), userId, fullName, studentCode, groupId, nowUtc);

    public void Update(string fullName, string? studentCode, Guid? groupId)
    {
        FullName = fullName;
        StudentCode = studentCode;
        GroupId = groupId;
    }
}
