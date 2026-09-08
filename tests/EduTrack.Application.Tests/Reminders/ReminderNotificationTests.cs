using EduTrack.Application.Localization;
using EduTrack.Application.Reminders;
using EduTrack.Domain.Notifications;
using EduTrack.Domain.Reminders;
using FluentAssertions;

namespace EduTrack.Application.Tests.Reminders;

public class ReminderNotificationTests
{
    private static readonly ITranslator Translator = new ResxTranslator();
    private static readonly DateTime Due = new(2026, 9, 20, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Builds_english_24h_reminder()
    {
        var (type, title, body) = ReminderNotification.Build(
            Translator, ReminderKind.Ahead24h, "Physics", "Lab #3", Due, "UTC", "en");

        type.Should().Be(NotificationType.AssignmentReminder24h);
        title.Should().Be("Deadline in 24 hours");
        body.Should().Contain("Physics");
        body.Should().Contain("Lab #3");
        body.Should().Contain("Due");
    }

    [Fact]
    public void Builds_russian_24h_reminder()
    {
        var (_, title, body) = ReminderNotification.Build(
            Translator, ReminderKind.Ahead24h, "Physics", "Lab #3", Due, "UTC", "ru");

        title.Should().Be("Дедлайн через 24 часа");
        body.Should().Contain("Срок:");
    }

    [Fact]
    public void Builds_overdue_reminder()
    {
        var (type, title, _) = ReminderNotification.Build(
            Translator, ReminderKind.Overdue, "Physics", "Lab #3", Due, "UTC", "en");

        type.Should().Be(NotificationType.AssignmentOverdue);
        title.Should().Be("Deadline passed");
    }
}
