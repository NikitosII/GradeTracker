using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.CreateOwnAssignment;
using EduTrack.Application.Studies.Queries.GetOwnAssignments;
using EduTrack.Application.Studies.Queries.GetSubjects;
using EduTrack.Bot.Web.Conversations;
using EduTrack.Bot.Web.Telegram;
using EduTrack.Domain.Common;
using EduTrack.Domain.Studies;
using EduTrack.Integration.Tests.TestSupport;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EduTrack.Integration.Tests;

public class DeadlineModuleTests
{
    private const long ChatId = 700;
    private const long UserId = 700;
    private static readonly DateTime Now = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SubjectId = Guid.NewGuid();

    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ITelegramSender _telegram = Substitute.For<ITelegramSender>();
    private readonly InMemoryConversationStore _store = new();

    private DeadlineModule CreateSut() =>
        new(_sender, _telegram, _store, new FixedClock(Now), NullLogger<DeadlineModule>.Instance);

    private void StubSubjects() =>
        _sender.Send(Arg.Any<GetSubjectsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<SubjectDto>>(
                new List<SubjectDto> { new(SubjectId, "Physics", true) }));

    [Fact]
    public async Task Add_wizard_walks_steps_and_sends_command_on_confirm()
    {
        StubSubjects();
        _sender.Send(Arg.Any<CreateOwnAssignmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AssignmentDto(
                Guid.NewGuid(), SubjectId, "Physics", AssignmentType.Lab, "Lab #3", null, Now.AddDays(3))));

        var sut = CreateSut();

        await sut.StartAddAsync(ChatId, UserId, CancellationToken.None);
        _store.Peek(ChatId)!.Step.Should().Be(DeadlineStep.Subject);

        await sut.HandleCallbackAsync(ChatId, UserId, "c1", CallbackData.DeadlineWizardSubject(SubjectId), CancellationToken.None);
        _store.Peek(ChatId)!.Step.Should().Be(DeadlineStep.Type);

        await sut.HandleCallbackAsync(ChatId, UserId, "c2", CallbackData.DeadlineWizardType((int)AssignmentType.Lab), CancellationToken.None);
        _store.Peek(ChatId)!.Step.Should().Be(DeadlineStep.Title);

        (await sut.TryHandleTextAsync(ChatId, UserId, "Lab #3", CancellationToken.None)).Should().BeTrue();
        _store.Peek(ChatId)!.Step.Should().Be(DeadlineStep.Description);

        await sut.HandleCallbackAsync(ChatId, UserId, "c3", CallbackData.DeadlineWizardSkip, CancellationToken.None); // description
        _store.Peek(ChatId)!.Step.Should().Be(DeadlineStep.Due);

        (await sut.TryHandleTextAsync(ChatId, UserId, "2026-08-25 18:00", CancellationToken.None)).Should().BeTrue();
        _store.Peek(ChatId)!.Step.Should().Be(DeadlineStep.Confirm);

        await sut.HandleCallbackAsync(ChatId, UserId, "c4", CallbackData.DeadlineWizardConfirm, CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<CreateOwnAssignmentCommand>(c =>
                c.TelegramUserId == UserId &&
                c.SubjectId == SubjectId &&
                c.Type == AssignmentType.Lab &&
                c.Title == "Lab #3" &&
                c.Description == null &&
                c.DueAtUtc == new DateTime(2026, 8, 25, 18, 0, 0, DateTimeKind.Utc)),
            Arg.Any<CancellationToken>());

        _store.Peek(ChatId).Should().BeNull(); // conversation cleared after saving
    }

    [Fact]
    public async Task Cancel_callback_clears_conversation()
    {
        StubSubjects();
        var sut = CreateSut();

        await sut.StartAddAsync(ChatId, UserId, CancellationToken.None);
        await sut.HandleCallbackAsync(ChatId, UserId, "c1", CallbackData.DeadlineWizardCancel, CancellationToken.None);

        _store.Peek(ChatId).Should().BeNull();
    }

    [Fact]
    public async Task Show_deadlines_renders_list_and_answers_from_query()
    {
        var page = new AssignmentsPageDto(
            new List<AssignmentDto>
            {
                new(Guid.NewGuid(), SubjectId, "Physics", AssignmentType.Test, "Quiz 1", null, Now.AddHours(5)),
            },
            Page: 1, PageSize: 5, TotalCount: 1);

        _sender.Send(Arg.Any<GetOwnAssignmentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(page));

        var sut = CreateSut();
        await sut.ShowDeadlinesAsync(ChatId, UserId, DeadlineModule.ScopeAll, 1, CancellationToken.None);

        await _telegram.Received(1).SendKeyboardAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("Quiz 1") && s.Contains("Physics") && s.Contains("in 5h")),
            Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Next_view_requests_single_item_window()
    {
        _sender.Send(Arg.Any<GetOwnAssignmentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AssignmentsPageDto(new List<AssignmentDto>(), 1, 1, 0)));

        var sut = CreateSut();
        await sut.ShowDeadlinesAsync(ChatId, UserId, DeadlineModule.ScopeNext, 1, CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<GetOwnAssignmentsQuery>(q => q.PageSize == 1 && q.FromUtc == Now && q.ToUtc == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Text_without_active_conversation_is_not_consumed()
    {
        var consumed = await CreateSut().TryHandleTextAsync(ChatId, UserId, "hello", CancellationToken.None);
        consumed.Should().BeFalse();
    }
}
