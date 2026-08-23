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

    // --- Admin --- //

    public const string AdminViewNamespace = "av";
    public const string AdminWizardNamespace = "aw";

    public const string AdminMenu = "av:menu";
    public const string AdminInvites = "av:invites";
    public const string AdminSubjects = "av:subjects";
    public const string AdminStatus = "av:status";

    public static string AdminUsers(int page) => $"av:users:{page}";

    public static string AdminUser(Guid userId) => $"av:user:{userId}";

    public static string AdminAudit(int page) => $"av:audit:{page}";

    public static string AdminSubject(Guid subjectId) => $"av:subject:{subjectId}";

    public static string AdminSetRole(Guid userId, int role) => $"aw:role:{userId}:{role}";

    public const string AdminNewCode = "aw:newcode";

    public static string AdminCodeRole(int role) => $"aw:coderole:{role}";

    public static string AdminCodeExpiry(int role, int days) => $"aw:codeexp:{role}:{days}";

    public const string AdminNewSubject = "aw:newsubject";

    public static string AdminSubjectRename(Guid subjectId) => $"aw:subrename:{subjectId}";

    public static string AdminSubjectToggle(Guid subjectId) => $"aw:subtoggle:{subjectId}";

    public const string AdminSubjectSkip = "aw:subskip";
    public const string AdminCancel = "aw:x";

    public static string[] Parts(string data) => data.Split(':');
}
