namespace EduTrack.Domain.Notifications;

/// <summary>Kinds of notification the system can deliver.</summary>
public enum NotificationType
{
    AssignmentReminder24h = 0,
    AssignmentReminder2h = 1,
    AssignmentOverdue = 2,
    MorningDigest = 3,
    WeeklyReport = 4,
    LowAverageScore = 5,
    AdminDataChange = 6,
    SystemAnnouncement = 7,
}
