namespace EduTrack.Domain.Studies;

/// <summary>
/// A deadline (homework, test, exam, ...) owned by a student.
/// </summary>
public class Assignment
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 1024;

    private Assignment()
    {
    }

    private Assignment(
        Guid id,
        Guid ownerUserId,
        Guid subjectId,
        AssignmentType type,
        string title,
        string? description,
        DateTime dueAtUtc,
        Guid createdByUserId,
        DateTime nowUtc)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        SubjectId = subjectId;
        Type = type;
        Title = title;
        Description = description;
        DueAtUtc = dueAtUtc;
        CreatedByUserId = createdByUserId;
        UpdatedByUserId = createdByUserId;
        CreatedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>The student who owns the deadline.</summary>
    public Guid OwnerUserId { get; private set; }
    public Guid SubjectId { get; private set; }
    public AssignmentType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime DueAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    public static Assignment Create(
        Guid ownerUserId,
        Guid subjectId,
        AssignmentType type,
        string title,
        string? description,
        DateTime dueAtUtc,
        Guid createdByUserId,
        DateTime nowUtc)
        => new(Guid.NewGuid(), ownerUserId, subjectId, type, title, description, dueAtUtc, createdByUserId, nowUtc);

    public void Update(
        AssignmentType type,
        string title,
        string? description,
        DateTime dueAtUtc,
        Guid updatedByUserId,
        DateTime nowUtc)
    {
        Type = type;
        Title = title;
        Description = description;
        DueAtUtc = dueAtUtc;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = nowUtc;
    }

    public void Delete(Guid updatedByUserId, DateTime nowUtc)
    {
        IsDeleted = true;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = nowUtc;
    }
}
