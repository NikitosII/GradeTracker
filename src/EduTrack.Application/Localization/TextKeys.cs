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

    // Recurrence (deadline add wizard)
    public const string DeadlineRepeatPrompt = "deadline.repeat_prompt";
    public const string DeadlineRepeatOnce = "deadline.repeat_once";
    public const string DeadlineCountPrompt = "deadline.count_prompt";
    public const string DeadlineRepeatSummary = "deadline.repeat_summary";
    public const string DeadlineRecurringAdded = "deadline.recurring_added";
    public const string RecurrenceDaily = "recurrence.daily";
    public const string RecurrenceWeekly = "recurrence.weekly";
    public const string RecurrenceMonthly = "recurrence.monthly";

    // Time remaining
    public const string RemainingOverdue = "remaining.overdue";
    public const string RemainingDays = "remaining.days";
    public const string RemainingHours = "remaining.hours";
    public const string RemainingMinutes = "remaining.minutes";

    // Roles
    /// <summary>Resource key for a role label, e.g. role.admin.</summary>
    public static string Role(string roleName) => $"role.{roleName.ToLowerInvariant()}";

    // Admin
    public const string AdminMenuTitle = "admin.menu_title";
    public const string AdminBtnUsers = "admin.btn_users";
    public const string AdminBtnInvites = "admin.btn_invites";
    public const string AdminBtnSubjects = "admin.btn_subjects";
    public const string AdminBtnAudit = "admin.btn_audit";
    public const string AdminBtnStatus = "admin.btn_status";
    public const string AdminBtnMenu = "admin.btn_menu";
    public const string AdminBtnNewCode = "admin.btn_new_code";
    public const string AdminBtnNewSubject = "admin.btn_new_subject";
    public const string AdminBtnDeactivate = "admin.btn_deactivate";
    public const string AdminBtnActivate = "admin.btn_activate";
    public const string AdminAnnounceUsage = "admin.announce_usage";
    public const string AdminAnnounceQueued = "admin.announce_queued";
    public const string AdminRolePicker = "admin.role_picker";
    public const string AdminExpiry1Day = "admin.expiry_1day";
    public const string AdminExpiry7Days = "admin.expiry_7days";
    public const string AdminExpiry30Days = "admin.expiry_30days";
    public const string AdminExpiryNever = "admin.expiry_never";
    public const string AdminExpiryPicker = "admin.expiry_picker";
    public const string AdminCodeCreated = "admin.code_created";
    public const string AdminCodeExpires = "admin.code_expires";
    public const string AdminCodeNoExpiry = "admin.code_no_expiry";
    public const string AdminRoleUpdated = "admin.role_updated";
    public const string AdminNewSubjectPrompt = "admin.new_subject_prompt";
    public const string AdminSubjectDescPrompt = "admin.subject_desc_prompt";
    public const string AdminSubjectNameEmpty = "admin.subject_name_empty";
    public const string AdminSubjectNewNameEmpty = "admin.subject_newname_empty";
    public const string AdminSubjectAdded = "admin.subject_added";
    public const string AdminSubjectRenamePrompt = "admin.subject_rename_prompt";
    public const string AdminSubjectRenamed = "admin.subject_renamed";
    public const string AdminSubjectActivated = "admin.subject_activated";
    public const string AdminSubjectDeactivated = "admin.subject_deactivated";
    public const string AdminValidation = "admin.validation";
    public const string AdminNoName = "admin.no_name";
    public const string AdminUsersEmpty = "admin.users_empty";
    public const string AdminUsersTitle = "admin.users_title";
    public const string AdminUsersTap = "admin.users_tap";
    public const string AdminInvitesTitle = "admin.invites_title";
    public const string AdminInvitesEmpty = "admin.invites_empty";
    public const string AdminInviteUsed = "admin.invite_used";
    public const string AdminInviteAvailable = "admin.invite_available";
    public const string AdminInviteExpires = "admin.invite_expires";
    public const string AdminSubjectsTitle = "admin.subjects_title";
    public const string AdminSubjectsEmpty = "admin.subjects_empty";
    public const string AdminSubjectActive = "admin.subject_active";
    public const string AdminSubjectInactive = "admin.subject_inactive";
    public const string AdminAuditEmpty = "admin.audit_empty";
    public const string AdminAuditTitle = "admin.audit_title";
    public const string AdminAuditSystem = "admin.audit_system";
    public const string AdminStatusTitle = "admin.status_title";
    public const string AdminStatusUsers = "admin.status_users";
    public const string AdminStatusSubjects = "admin.status_subjects";
    public const string AdminStatusGrades = "admin.status_grades";
    public const string AdminStatusDeadlines = "admin.status_deadlines";
    public const string AdminStatusInvites = "admin.status_invites";
    public const string AdminStatusAudit = "admin.status_audit";
    public const string AdminStatusAsOf = "admin.status_asof";

    // Stats
    public const string StatsTitle = "stats.title";
    public const string StatsNoGrades = "stats.no_grades";
    public const string StatsGpa = "stats.gpa";
    public const string StatsWeek = "stats.week";
    public const string StatsMonth = "stats.month";
    public const string StatsBySubject = "stats.by_subject";
    public const string StatsSubjectLine = "stats.subject_line";
    public const string StatsNeedsAttention = "stats.needs_attention";
    public const string StatsUpcoming = "stats.upcoming";
    public const string StatsRecorded = "stats.recorded";

    // Validation
    public const string ValidTimeZone = "valid.timezone";
    public const string ValidLanguage = "valid.language";
    public const string ValidQuietHours = "valid.quiet_hours";
    public const string ValidBindCode = "valid.bind_code";

    // Notifications, reminders, digest
    public const string NotifyReminder24hTitle = "notify.reminder_24h_title";
    public const string NotifyReminder2hTitle = "notify.reminder_2h_title";
    public const string NotifyReminderOverdueTitle = "notify.reminder_overdue_title";
    public const string NotifyReminderTitle = "notify.reminder_title";
    public const string NotifyReminderDueBody = "notify.reminder_due_body";
    public const string NotifyReminderOverdueBody = "notify.reminder_overdue_body";
    public const string NotifyDigestTitle = "notify.digest_title";
    public const string NotifyRoleChangedTitle = "notify.role_changed_title";
    public const string NotifyRoleChangedBody = "notify.role_changed_body";
    public const string NotifyAnnouncementTitle = "notify.announcement_title";
    public const string DigestToday = "digest.today";
    public const string DigestNoDeadlines = "digest.no_deadlines";
    public const string DigestAverage = "digest.average";

    /// <summary>Resource key for an assignment type label, e.g. type.homework.</summary>
    public static string Type(string typeName) => $"type.{typeName.ToLowerInvariant()}";

    /// <summary>Builds the resource key for a Result error code, e.g. error.Users.NotBound.</summary>
    public static string Error(string code) => $"error.{code}";
}
