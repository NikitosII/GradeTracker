using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Admin;
using EduTrack.Application.Studies.History;
using EduTrack.Application.Studies.Queries.GetOwnHistory;
using EduTrack.Bot.Web.Localization;
using EduTrack.Bot.Web.Telegram;
using EduTrack.Domain.Common;
using EduTrack.Integration.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EduTrack.Integration.Tests;

public class HistoryModuleTests
{
    private const long ChatId = 800;
    private const long UserId = 800;
    private static readonly DateTime When = new(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);

    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ITelegramSender _telegram = Substitute.For<ITelegramSender>();

    private HistoryModule CreateSut(IUiText? text = null) =>
        new(_sender, _telegram, text ?? new TestUiText(), NullLogger<HistoryModule>.Instance);

    private static StudentHistoryPageDto Sample() => new(
        new[] { new StudentHistoryEntryDto(AuditActions.GradeAdded, AuditEntities.Grade, "Math: 5", When) },
        Page: 1, PageSize: 8, TotalCount: 1);

    [Fact]
    public async Task Renders_history_in_english()
    {
        _sender.Send(Arg.Any<GetOwnHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Sample()));

        await CreateSut().ShowHistoryAsync(ChatId, UserId, 1, CancellationToken.None);

        await _telegram.Received(1).SendKeyboardAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("Your history") && s.Contains("Added grade") && s.Contains("Math: 5")),
            Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renders_empty_state()
    {
        _sender.Send(Arg.Any<GetOwnHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StudentHistoryPageDto(Array.Empty<StudentHistoryEntryDto>(), 1, 8, 0)));

        await CreateSut().ShowHistoryAsync(ChatId, UserId, 1, CancellationToken.None);

        await _telegram.Received(1).SendKeyboardAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("No changes recorded yet")),
            Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>>(),
            Arg.Any<CancellationToken>());
    }
}
