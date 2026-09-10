using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Studies.Archive;
using EduTrack.Application.Studies.Commands.SetOwnAssignmentArchived;
using EduTrack.Application.Studies.Queries.GetOwnDeadlinesForArchive;
using EduTrack.Bot.Web.Localization;
using EduTrack.Bot.Web.Telegram;
using EduTrack.Domain.Common;
using EduTrack.Integration.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EduTrack.Integration.Tests;

public class ArchiveModuleTests
{
    private const long ChatId = 950;
    private const long UserId = 950;
    private static readonly Guid DeadlineId = Guid.NewGuid();

    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ITelegramSender _telegram = Substitute.For<ITelegramSender>();

    private ArchiveModule CreateSut(IUiText? text = null) =>
        new(_sender, _telegram, text ?? new TestUiText(), NullLogger<ArchiveModule>.Instance);

    private static ArchivePageDto OneArchived() => new(
        new[] { new ArchiveItemDto(DeadlineId, "Old exam", "2026-05-01 23:59 UTC · Physics") },
        Page: 1, PageSize: 8, TotalCount: 1);

    [Fact]
    public async Task Menu_shows_four_options()
    {
        await CreateSut().ShowMenuAsync(ChatId, CancellationToken.None);

        await _telegram.Received(1).SendKeyboardAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("Archive")),
            Arg.Is<IReadOnlyList<IReadOnlyList<InlineButton>>>(rows => rows.Count == 4),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Archived_deadlines_list_renders_restore_buttons()
    {
        _sender.Send(Arg.Any<GetOwnDeadlinesForArchiveQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(OneArchived()));

        await CreateSut().HandleCallbackAsync(
            ChatId, UserId, "cb1", CallbackData.ArchiveList(CallbackData.ArchiveDeadlinesArchived, 1), CancellationToken.None);

        await _telegram.Received().SendKeyboardAsync(
            ChatId,
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<IReadOnlyList<InlineButton>>>(rows =>
                rows.Any(r => r.Any(b => b.CallbackData == CallbackData.ArchiveRestore(CallbackData.ArchiveEntityDeadline, DeadlineId)))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Archiving_a_deadline_sends_command_and_confirms()
    {
        _sender.Send(Arg.Any<SetOwnAssignmentArchivedCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _sender.Send(Arg.Any<GetOwnDeadlinesForArchiveQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ArchivePageDto(Array.Empty<ArchiveItemDto>(), 1, 8, 0)));

        await CreateSut().HandleCallbackAsync(
            ChatId, UserId, "cb2", CallbackData.ArchiveDo(CallbackData.ArchiveEntityDeadline, DeadlineId), CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<SetOwnAssignmentArchivedCommand>(c => c.AssignmentId == DeadlineId && c.Archived),
            Arg.Any<CancellationToken>());
        await _telegram.Received().SendTextAsync(
            ChatId, Arg.Is<string>(s => s.Contains("Archived")), Arg.Any<CancellationToken>());
    }
}
