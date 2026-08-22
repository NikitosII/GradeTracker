namespace EduTrack.Bot.Web.Telegram;

/// <summary>
/// Compact callback payloads for inline buttons.
/// </summary>
public static class CallbackData
{
    public const string ViewNamespace = "gv";
    public const string WizardNamespace = "gw";

    public static string ViewSubject(Guid subjectId, int page) => $"gv:sub:{subjectId}:{page}";

    public static string WizardSubject(Guid subjectId) => $"gw:sub:{subjectId}";

    public static string WizardGrade(Guid gradeId) => $"gw:grade:{gradeId}";

    public static string WizardValue(int value) => $"gw:val:{value}";

    public const string WizardSkip = "gw:skip";
    public const string WizardConfirm = "gw:ok";
    public const string WizardCancel = "gw:x";

    // --- Deadlines --- //

    public const string DeadlineViewNamespace = "dv";
    public const string DeadlineWizardNamespace = "dw";

    public static string DeadlineView(string scope, int page) => $"dv:{scope}:{page}";

    public static string DeadlineWizardSubject(Guid subjectId) => $"dw:sub:{subjectId}";

    public static string DeadlineWizardItem(Guid assignmentId) => $"dw:item:{assignmentId}";

    public static string DeadlineWizardType(int type) => $"dw:type:{type}";

    public const string DeadlineWizardSkip = "dw:skip";
    public const string DeadlineWizardConfirm = "dw:ok";
    public const string DeadlineWizardCancel = "dw:x";

    public static string[] Parts(string data) => data.Split(':');
}
