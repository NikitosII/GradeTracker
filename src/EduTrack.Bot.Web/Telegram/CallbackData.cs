namespace EduTrack.Bot.Web.Telegram;

/// <summary>
/// Compact callback payloads for inline buttons.
/// </summary>
public static class CallbackData
{
    public const string ViewNamespace = "gv";
    public const string WizardNamespace = "gw";

    public static string ViewSubject(Guid subjectId, int page) => $"gv:sub:{subjectId}:{page}";

    public const string GradeAdd = "gv:add";
    public const string GradeEdit = "gv:edit";

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

    /// <summary>Hub action: open the guided edit wizard.</summary>
    public const string DeadlineEdit = "dv:edit";

    /// <summary>Hub action: explain the one-line /quick capture.</summary>
    public const string DeadlineQuick = "dv:quick";

    public static string DeadlineWizardSubject(Guid subjectId) => $"dw:sub:{subjectId}";

    public static string DeadlineWizardItem(Guid assignmentId) => $"dw:item:{assignmentId}";

    public static string DeadlineWizardType(int type) => $"dw:type:{type}";

    public const string DeadlineWizardSkip = "dw:skip";
    public const string DeadlineWizardConfirm = "dw:ok";
    public const string DeadlineWizardCancel = "dw:x";

    public const string DeadlineWizardRepeatOnce = "dw:rep:none";

    public static string DeadlineWizardRepeat(int frequency) => $"dw:rep:{frequency}";

    public static string DeadlineWizardCount(int count) => $"dw:cnt:{count}";

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

    // --- Settings --- //

    public const string SettingsNamespace = "st";

    public const string SettingsMenu = "st:menu";

    public static string SettingsToggle(string key) => $"st:toggle:{key}";

    public const string SettingsTimeZone = "st:tz";

    public static string SettingsSetTimeZone(string id) => $"st:tz:{id}";

    public const string SettingsLanguage = "st:lang";

    public static string SettingsSetLanguage(string language) => $"st:lang:{language}";

    public const string SettingsQuiet = "st:quiet";

    public const string SettingsQuietOff = "st:quiet:off";

    public static string SettingsSetQuiet(int start, int end) => $"st:quiet:{start}:{end}";

    // --- History --- //

    public const string HistoryNamespace = "hist";

    public static string HistoryPage(int page) => $"hist:{page}";

    // --- Archive --- //

    public const string ArchiveNamespace = "arch";

    // List kinds: da = deadlines archived, ga = grades archived, dl = deadlines live, gl = grades live.
    public const string ArchiveDeadlinesArchived = "da";
    public const string ArchiveGradesArchived = "ga";
    public const string ArchiveDeadlinesLive = "dl";
    public const string ArchiveGradesLive = "gl";

    // Entity codes for archive/restore actions: d = deadline, g = grade.
    public const string ArchiveEntityDeadline = "d";
    public const string ArchiveEntityGrade = "g";

    public const string ArchiveMenu = "arch:menu";

    public static string ArchiveList(string kind, int page) => $"arch:list:{kind}:{page}";

    public static string ArchiveDo(string entity, Guid id) => $"arch:ar:{entity}:{id}";

    public static string ArchiveRestore(string entity, Guid id) => $"arch:re:{entity}:{id}";

    public static string[] Parts(string data) => data.Split(':');
}
