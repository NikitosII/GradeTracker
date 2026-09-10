using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Studies.Queries.GetStudentStats;
using EduTrack.Application.Studies.Queries.GetStudentTrends;
using EduTrack.Application.Studies.Stats;
using EduTrack.Bot.Web.Localization;
using EduTrack.Bot.Web.Telegram;
using EduTrack.Domain.Common;
using EduTrack.Integration.Tests.TestSupport;
using MediatR;
using NSubstitute;

namespace EduTrack.Integration.Tests;

public class StatsModuleTests
{
    private const long ChatId = 700;
    private const long UserId = 700;

    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ITelegramSender _telegram = Substitute.For<ITelegramSender>();

    private StatsModule CreateSut(IUiText? text = null) =>
        new(_sender, _telegram, text ?? new TestUiText());

    private static StudentStatsDto Sample() => new(
        OverallGpa: 4.25, WeekAverage: 4.5, WeekCount: 2, MonthAverage: 4.2, MonthCount: 8,
        Subjects: new[] { new SubjectStatDto("Math", 4.6, 5) },
        WorstSubject: "Math", WorstSubjectAverage: 4.6, UpcomingDeadlines: 1, TotalGrades: 8);

    private static StudentTrendsDto SampleTrends() => new(new[]
    {
        new SubjectTrendDto("Math", 4.6, 4.2, 0.4, 3),
    });

    private void SetupStats(StudentStatsDto stats) =>
        _sender.Send(Arg.Any<GetStudentStatsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(stats));

    private void SetupTrends(StudentTrendsDto trends) =>
        _sender.Send(Arg.Any<GetStudentTrendsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(trends));

    [Fact]
    public async Task Report_combines_stats_and_trends_in_english()
    {
        SetupStats(Sample());
        SetupTrends(SampleTrends());

        await CreateSut().ShowReportAsync(ChatId, UserId, CancellationToken.None);

        await _telegram.Received(1).SendTextAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("Report") && s.Contains("GPA (all time)")
                && s.Contains("Math") && s.Contains("4.60") && s.Contains("↑") && s.Contains("+0.40")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Report_renders_in_russian()
    {
        SetupStats(Sample());
        SetupTrends(SampleTrends());

        var russian = CreateSut(new TestUiText(new TestLanguageContext { Language = "ru" }));
        await russian.ShowReportAsync(ChatId, UserId, CancellationToken.None);

        await _telegram.Received(1).SendTextAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("Ваша статистика") && s.Contains("Средний балл (за всё время)")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Report_shows_empty_state_when_no_grades()
    {
        SetupStats(new StudentStatsDto(null, null, 0, null, 0, Array.Empty<SubjectStatDto>(), null, null, 0, 0));
        SetupTrends(new StudentTrendsDto(Array.Empty<SubjectTrendDto>()));

        await CreateSut().ShowReportAsync(ChatId, UserId, CancellationToken.None);

        await _telegram.Received(1).SendTextAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("No grades yet")),
            Arg.Any<CancellationToken>());
    }
}
