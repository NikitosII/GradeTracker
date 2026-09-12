namespace EduTrack.Bot.Web.Conversations;

/// <summary>Wizard identifiers.</summary>
public static class ConversationFlow
{
    public const string GradeAdd = "grade_add";
    public const string GradeEdit = "grade_edit";
    public const string DeadlineAdd = "deadline_add";
    public const string DeadlineEdit = "deadline_edit";
    public const string AdminSubjectAdd = "admin_subject_add";
    public const string AdminSubjectRename = "admin_subject_rename";
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

/// <summary>Steps shared by the add/edit deadline wizards.</summary>
public static class DeadlineStep
{
    public const string Subject = "d_subject";
    public const string QuickSubject = "d_quick_subject";
    public const string Item = "d_item";
    public const string Type = "d_type";
    public const string Title = "d_title";
    public const string Description = "d_description";
    public const string Due = "d_due";
    public const string Repeat = "d_repeat";
    public const string Count = "d_count";
    public const string Confirm = "d_confirm";
}

/// <summary>Steps for the administrator subject wizards.</summary>
public static class AdminStep
{
    public const string SubjectName = "a_subject_name";
    public const string SubjectDescription = "a_subject_desc";
    public const string SubjectNewName = "a_subject_newname";
}

/// <summary>
/// Mutable, JSON-serializable snapshot of an in-progress wizard
/// </summary>
public sealed class ConversationState
{
    public string Flow { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;

    /// <summary>
    /// Telegram id of the single message this wizard renders into. 
    /// </summary>
    public int? WizardMessageId { get; set; }
    public Guid? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public Guid? GradeId { get; set; }
    public int? Value { get; set; }
    public decimal? Weight { get; set; }
    public string? Comment { get; set; }
    public DateTime? OccurredAt { get; set; }

    // Deadline wizard fields.
    public Guid? AssignmentId { get; set; }
    public int? AssignmentType { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? DueAtUtc { get; set; }

    // Recurrence (deadline add wizard).
    public int? RecurrenceFrequency { get; set; }
    public int? RecurrenceCount { get; set; }
}
