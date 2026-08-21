namespace EduTrack.Domain.Studies;

/// <summary>
/// A subject reference entry managed by administrators and used by students
/// when recording grades and deadlines.
/// </summary>
public class Subject
{
    public const int MaxNameLength = 128;
    public const int MaxDescriptionLength = 512;

    private Subject()
    {
    }

    private Subject(Guid id, string name, string? description, DateTime nowUtc)
    {
        Id = id;
        Name = name;
        Description = description;
        IsActive = true;
        CreatedAt = nowUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Subject Create(string name, string? description, DateTime nowUtc)
        => new(Guid.NewGuid(), name, description, nowUtc);

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
