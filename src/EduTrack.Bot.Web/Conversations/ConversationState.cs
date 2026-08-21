namespace EduTrack.Bot.Web.Conversations;

/// <summary>Wizard identifiers.</summary>
public static class ConversationFlow
{
    public const string GradeAdd = "grade_add";
    public const string GradeEdit = "grade_edit";
}

/// <summary>Steps shared by the add/edit grade wizards.</summary>
public static class GradeStep
{
    public const string Subject = "subject";
    public const string Grade = "grade";
    public const string Value = "value";
    public const string Comment = "comment";
    public const string Date = "date";
    public const string Weight = "weight";
    public const string Confirm = "confirm";
}

/// <summary>
/// Mutable, JSON-serializable snapshot of an in-progress wizard, stored per chat
/// in the distributed cache between webhook calls.
/// </summary>
public sealed class ConversationState
{
    public string Flow { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public Guid? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public Guid? GradeId { get; set; }
    public int? Value { get; set; }
    public decimal? Weight { get; set; }
    public string? Comment { get; set; }
    public DateTime? OccurredAt { get; set; }
}
