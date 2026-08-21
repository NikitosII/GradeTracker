namespace EduTrack.Domain.Studies;

/// <summary>
/// A grade owned by a student. 
/// </summary>
public class Grade
{
    public const int MinValue = 1;
    public const int MaxValue = 5;
    public const int MaxCommentLength = 512;

    private Grade()
    {
    }

    private Grade(
        Guid id,
        Guid studentUserId,
        Guid subjectId,
        int value,
        decimal weight,
        string? comment,
        DateTime occurredAt,
        Guid createdByUserId,
        DateTime nowUtc)
    {
        Id = id;
        StudentUserId = studentUserId;
        SubjectId = subjectId;
        Value = value;
        Weight = weight;
        Comment = comment;
        OccurredAt = occurredAt;
        CreatedByUserId = createdByUserId;
        UpdatedByUserId = createdByUserId;
        CreatedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    private static void EnsureValueInRange(int value)
    {
        if (value is < MinValue or > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Grade value must be between {MinValue} and {MaxValue}.");
        }
    }

    public Guid Id { get; private set; }

    /// <summary>The student who owns the grade.</summary>
    public Guid StudentUserId { get; private set; }
    public Guid SubjectId { get; private set; }
    public int Value { get; private set; }
    public decimal Weight { get; private set; }
    public string? Comment { get; private set; }
    public DateTime OccurredAt { get; private set; }

    /// <summary>Who created the entry.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Who last modified the entry.</summary>
    public Guid UpdatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    public static Grade Add(
        Guid studentUserId,
        Guid subjectId,
        int value,
        decimal weight,
        string? comment,
        DateTime occurredAt,
        Guid createdByUserId,
        DateTime nowUtc)
    {
        EnsureValueInRange(value);
        return new Grade(Guid.NewGuid(), studentUserId, subjectId, value, weight, comment, occurredAt, createdByUserId, nowUtc);
    }

    public void Update(
        int value,
        decimal weight,
        string? comment,
        DateTime occurredAt,
        Guid updatedByUserId,
        DateTime nowUtc)
    {
        EnsureValueInRange(value);
        Value = value;
        Weight = weight;
        Comment = comment;
        OccurredAt = occurredAt;
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
