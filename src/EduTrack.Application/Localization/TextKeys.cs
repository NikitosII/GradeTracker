namespace EduTrack.Application.Localization;

/// <summary>Stable keys for localized UI strings (see Strings.resx / Strings.ru.resx).</summary>
public static class TextKeys
{
    // General commands
    public const string Welcome = "welcome";
    public const string Help = "help";
    public const string Unknown = "unknown";

    public const string BindUsage = "bind.usage";
    public const string BindInvalid = "bind.invalid";
    public const string BindLinked = "bind.linked";

    public const string ProfileTitle = "profile.title";
    public const string ProfileName = "profile.name";
    public const string ProfileUsername = "profile.username";
    public const string ProfileRole = "profile.role";
    public const string ProfileTimeZone = "profile.timezone";
    public const string ProfileLanguage = "profile.language";

    // Settings
    public const string SettingsTitle = "settings.title";
    public const string SettingsNotifOffNote = "settings.notif_off_note";
    public const string SettingsNotifications = "settings.notifications";
    public const string SettingsDigest = "settings.digest";
    public const string SettingsReminder24h = "settings.r24";
    public const string SettingsReminder2h = "settings.r2";
    public const string SettingsTimeZone = "settings.timezone";
    public const string SettingsQuiet = "settings.quiet";
    public const string SettingsLanguage = "settings.language";
    public const string SettingsOn = "settings.on";
    public const string SettingsOff = "settings.off";
    public const string SettingsQuietOff = "settings.quiet_off";
    public const string SettingsBack = "settings.back";
    public const string SettingsChooseTimeZone = "settings.choose_tz";
    public const string SettingsChooseLanguage = "settings.choose_lang";
    public const string SettingsChooseQuiet = "settings.choose_quiet";
    public const string SettingsError = "settings.error";
    public const string SettingsInvalid = "settings.invalid";

    // Shared chrome
    public const string CommonCancel = "common.cancel";
    public const string CommonSkip = "common.skip";
    public const string CommonConfirm = "common.confirm";
    public const string CommonPrev = "common.prev";
    public const string CommonNext = "common.next";
    public const string CommonNone = "common.none";
    public const string CommonUseButtons = "common.use_buttons";
    public const string CommonSomethingWrong = "common.something_wrong";
    public const string CommonNoSubjects = "common.no_subjects";
    public const string CommonNoSubjectsAdmin = "common.no_subjects_admin";
    public const string CommonCancelled = "common.cancelled";
    public const string CommonSubjectGone = "common.subject_gone";
    public const string CommonConfirmHeader = "common.confirm_header";
    public const string CommonPage = "common.page";

    // Shared field labels
    public const string LabelSubject = "label.subject";
    public const string LabelGrade = "label.grade";
    public const string LabelWeight = "label.weight";
    public const string LabelComment = "label.comment";
    public const string LabelDate = "label.date";
    public const string LabelType = "label.type";
    public const string LabelTitle = "label.title";
    public const string LabelDescription = "label.description";
    public const string LabelDue = "label.due";

    // Grades
    public const string GradeChooseSubjectView = "grade.choose_subject_view";
    public const string GradeAddSelectSubject = "grade.add_select_subject";
    public const string GradeEditSelectSubject = "grade.edit_select_subject";
    public const string GradeWizardExpired = "grade.wizard_expired";
    public const string GradeSelectToEdit = "grade.select_to_edit";
    public const string GradeNoneInSubject = "grade.none_in_subject";
    public const string GradeChooseValue = "grade.choose_value";
    public const string GradeSendComment = "grade.send_comment";
    public const string GradeSendDate = "grade.send_date";
    public const string GradeBadDate = "grade.bad_date";
    public const string GradeSendWeight = "grade.send_weight";
    public const string GradeBadWeight = "grade.bad_weight";
    public const string GradeNothingToSkip = "grade.nothing_to_skip";
    public const string GradeAdded = "grade.added";
    public const string GradeUpdated = "grade.updated";
    public const string GradeCouldNotSave = "grade.could_not_save";
    public const string GradeNoneYet = "grade.none_yet";
    public const string GradeAverage = "grade.average";
    public const string GradeAllTitle = "grade.all_title";
    public const string GradeTitleFallback = "grade.title_fallback";

    // Deadlines
    public const string DeadlineNoExport = "deadline.no_export";
    public const string DeadlineExportCaption = "deadline.export_caption";
    public const string DeadlineAddSelectSubject = "deadline.add_select_subject";
    public const string DeadlineEditSelectSubject = "deadline.edit_select_subject";
    public const string DeadlineWizardExpired = "deadline.wizard_expired";
    public const string DeadlineSelectToEdit = "deadline.select_to_edit";
    public const string DeadlineNoneInSubject = "deadline.none_in_subject";
    public const string DeadlineChooseType = "deadline.choose_type";
    public const string DeadlineSendTitle = "deadline.send_title";
    public const string DeadlineBadTitle = "deadline.bad_title";
    public const string DeadlineSendDescription = "deadline.send_description";
    public const string DeadlineSendDue = "deadline.send_due";
    public const string DeadlineBadDue = "deadline.bad_due";
    public const string DeadlineAdded = "deadline.added";
    public const string DeadlineUpdated = "deadline.updated";
    public const string DeadlineCouldNotSave = "deadline.could_not_save";
    public const string DeadlineNoneUpcoming = "deadline.none_upcoming";
    public const string DeadlineScopeToday = "deadline.scope_today";
    public const string DeadlineScopeWeek = "deadline.scope_week";
    public const string DeadlineScopeNext = "deadline.scope_next";
    public const string DeadlineScopeUpcoming = "deadline.scope_upcoming";

    // Time remaining
    public const string RemainingOverdue = "remaining.overdue";
    public const string RemainingDays = "remaining.days";
    public const string RemainingHours = "remaining.hours";
    public const string RemainingMinutes = "remaining.minutes";

    /// <summary>Resource key for an assignment type label, e.g. type.homework.</summary>
    public static string Type(string typeName) => $"type.{typeName.ToLowerInvariant()}";

    /// <summary>Builds the resource key for a Result error code, e.g. error.Users.NotBound.</summary>
    public static string Error(string code) => $"error.{code}";
}
