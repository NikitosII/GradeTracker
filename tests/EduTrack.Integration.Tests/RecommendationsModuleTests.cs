using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Studies.Queries.GetStudentRecommendations;
using EduTrack.Application.Studies.Recommendations;
using EduTrack.Bot.Web.Localization;
using EduTrack.Bot.Web.Telegram;
using EduTrack.Domain.Common;
using EduTrack.Integration.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EduTrack.Integration.Tests;

public class RecommendationsModuleTests
{
    private const long ChatId = 970;
    private const long UserId = 970;

    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ITelegramSender _telegram = Substitute.For<ITelegramSender>();

    private RecommendationsModule CreateSut(IUiText? text = null) =>
        new(_sender, _telegram, text ?? new TestUiText(), NullLogger<RecommendationsModule>.Instance);

    private static StudentRecommendationsDto Sample() => new(new[]
    {
        new RecommendationDto(RecommendationKind.OverdueDeadlines, null, 2, null, null, null, null),
        new RecommendationDto(RecommendationKind.UrgentDeadline, "Physics", 0, null, null, "Midterm", new DateTime(2026, 9, 13, 15, 0, 0, DateTimeKind.Utc)),
    });

    [Fact]
    public async Task Renders_tips_in_english()
    {
        _sender.Send(Arg.Any<GetStudentRecommendationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Sample()));

        await CreateSut().ShowTipsAsync(ChatId, UserId, CancellationToken.None);

        await _telegram.Received(1).SendTextAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("Recommendations") && s.Contains("overdue") && s.Contains("Midterm")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renders_tips_in_russian()
    {
        _sender.Send(Arg.Any<GetStudentRecommendationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Sample()));

        var russian = CreateSut(new TestUiText(new TestLanguageContext { Language = "ru" }));
        await russian.ShowTipsAsync(ChatId, UserId, CancellationToken.None);

        await _telegram.Received(1).SendTextAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("Рекомендации") && s.Contains("просроченных")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renders_empty_state()
    {
        _sender.Send(Arg.Any<GetStudentRecommendationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StudentRecommendationsDto(Array.Empty<RecommendationDto>())));

        await CreateSut().ShowTipsAsync(ChatId, UserId, CancellationToken.None);

        await _telegram.Received(1).SendTextAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("caught up")),
            Arg.Any<CancellationToken>());
    }
}
