using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Nlp;
using EduTrack.Domain.Studies;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class DeadlineTextParserTests
{
    // 2026-09-09 is a Wednesday (2026-09-04 is a Friday, per RecurrenceScheduleTests).
    private static readonly DateTime NowUtc = new(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyList<SubjectDto> Subjects = new[]
    {
        new SubjectDto(Guid.NewGuid(), "Physics", true),
        new SubjectDto(Guid.NewGuid(), "Computer Science", true),
        new SubjectDto(Guid.NewGuid(), "Химия", true),
        new SubjectDto(Guid.NewGuid(), "History", false), // inactive: must never match
    };

    private static ParsedDeadline Parse(string text, string? tz = "UTC", string? language = "en")
        => DeadlineTextParser.Parse(text, Subjects, language, NowUtc, tz);

    [Fact]
    public void Parses_subject_type_date_and_time()
    {
        var result = Parse("Physics homework tomorrow at 18:00");

        result.SubjectName.Should().Be("Physics");
        result.Type.Should().Be(AssignmentType.Homework);
        result.DueAtUtc.Should().Be(new DateTime(2026, 9, 10, 18, 0, 0, DateTimeKind.Utc));
        result.Title.Should().Be("homework");
    }

    [Fact]
    public void Parses_russian_phrase()
    {
        var result = Parse("Химия домашка завтра 18:00", language: "ru");

        result.SubjectName.Should().Be("Химия");
        result.Type.Should().Be(AssignmentType.Homework);
        result.DueAtUtc.Should().Be(new DateTime(2026, 9, 10, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Defaults_to_end_of_day_when_no_time_given()
    {
        var result = Parse("Physics exam tomorrow");

        result.Type.Should().Be(AssignmentType.Exam);
        result.DueAtUtc.Should().Be(new DateTime(2026, 9, 10, 23, 59, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Resolves_next_weekday()
    {
        // Wednesday -> next Friday is two days later.
        var result = Parse("Physics lab friday");

        result.Type.Should().Be(AssignmentType.Lab);
        result.DueAtUtc.Should().Be(new DateTime(2026, 9, 11, 23, 59, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Resolves_russian_weekday()
    {
        var result = Parse("Химия контрольная в пятницу", language: "ru");

        result.Type.Should().Be(AssignmentType.Test);
        result.DueAtUtc.Should().Be(new DateTime(2026, 9, 11, 23, 59, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Resolves_relative_in_n_days()
    {
        Parse("Physics quiz in 3 days").DueAtUtc
            .Should().Be(new DateTime(2026, 9, 12, 23, 59, 0, DateTimeKind.Utc));

        Parse("Химия проект через 5 дней", language: "ru").DueAtUtc
            .Should().Be(new DateTime(2026, 9, 14, 23, 59, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Resolves_iso_date_with_ampm_time()
    {
        var result = Parse("Physics project 2026-12-01 6pm");

        result.DueAtUtc.Should().Be(new DateTime(2026, 12, 1, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Resolves_day_dot_month_date()
    {
        var result = Parse("Physics homework 25.12");

        result.DueAtUtc.Should().Be(new DateTime(2026, 12, 25, 23, 59, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Converts_local_time_to_utc_using_time_zone()
    {
        // Europe/Moscow is UTC+3 year-round; resolvable by IANA or Windows id on .NET 8.
        var result = Parse("Physics homework tomorrow at 09:00", tz: "Europe/Moscow");

        result.DueAtUtc.Should().Be(new DateTime(2026, 9, 10, 6, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Matches_longest_subject_name()
    {
        var result = Parse("Computer Science project tomorrow");

        result.SubjectName.Should().Be("Computer Science");
        result.Type.Should().Be(AssignmentType.Project);
    }

    [Fact]
    public void Ignores_inactive_subjects()
    {
        var result = Parse("History essay tomorrow");

        result.SubjectId.Should().BeNull();
        result.SubjectName.Should().BeNull();
    }

    [Fact]
    public void Leaves_due_null_when_no_date_recognized()
    {
        var result = Parse("Physics homework");

        result.DueAtUtc.Should().BeNull();
        result.SubjectName.Should().Be("Physics");
    }

    [Fact]
    public void Defaults_type_to_homework_when_no_keyword()
    {
        Parse("Physics tomorrow").Type.Should().Be(AssignmentType.Homework);
    }

    [Fact]
    public void Falls_back_to_original_text_when_title_would_be_empty()
    {
        // Nothing but a subject and a date; the leftover is blank, so keep the raw phrase.
        var result = Parse("Physics tomorrow");

        result.Title.Should().Be("Physics tomorrow");
    }

    [Fact]
    public void Title_strips_subject_date_time_and_filler()
    {
        var result = Parse("Physics read chapter 4 by friday");

        result.Title.Should().Be("read chapter 4");
    }
}
