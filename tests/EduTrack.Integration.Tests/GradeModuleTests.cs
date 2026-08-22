using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.AddOwnGrade;
using EduTrack.Application.Studies.Queries.GetOwnGrades;
using EduTrack.Application.Studies.Queries.GetSubjects;
using EduTrack.Bot.Web.Conversations;
using EduTrack.Bot.Web.Telegram;
using EduTrack.Domain.Common;
using EduTrack.Integration.Tests.TestSupport;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EduTrack.Integration.Tests;

public class GradeModuleTests
{
    private const long ChatId = 500;
    private const long UserId = 500;
    private static readonly DateTime Now = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SubjectId = Guid.NewGuid();

    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ITelegramSender _telegram = Substitute.For<ITelegramSender>();
    private readonly InMemoryConversationStore _store = new();

    private GradeModule CreateSut() =>
        new(_sender, _telegram, _store, new FixedClock(Now), NullLogger<GradeModule>.Instance);

    private void StubSubjects() =>
        _sender.Send(Arg.Any<GetSubjectsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<SubjectDto>>(
                new List<SubjectDto> { new(SubjectId, "Mathematics", true) }));

    [Fact]
    public async Task Add_wizard_walks_steps_and_sends_command_on_confirm()
    {
        StubSubjects();
        _sender.Send(Arg.Any<AddOwnGradeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GradeDto(Guid.NewGuid(), SubjectId, "Mathematics", 5, 1m, null, Now)));

        var sut = CreateSut();

        await sut.StartAddAsync(ChatId, UserId, CancellationToken.None);
        _store.Peek(ChatId)!.Step.Should().Be(GradeStep.Subject);

        await sut.HandleCallbackAsync(ChatId, UserId, "c1", CallbackData.WizardSubject(SubjectId), CancellationToken.None);
        _store.Peek(ChatId)!.Step.Should().Be(GradeStep.Value);

        await sut.HandleCallbackAsync(ChatId, UserId, "c2", CallbackData.WizardValue(5), CancellationToken.None);
        _store.Peek(ChatId)!.Step.Should().Be(GradeStep.Comment);

        await sut.HandleCallbackAsync(ChatId, UserId, "c3", CallbackData.WizardSkip, CancellationToken.None); // comment
        _store.Peek(ChatId)!.Step.Should().Be(GradeStep.Date);

        await sut.HandleCallbackAsync(ChatId, UserId, "c4", CallbackData.WizardSkip, CancellationToken.None); // date
        _store.Peek(ChatId)!.Step.Should().Be(GradeStep.Weight);

        await sut.HandleCallbackAsync(ChatId, UserId, "c5", CallbackData.WizardSkip, CancellationToken.None); // weight
        _store.Peek(ChatId)!.Step.Should().Be(GradeStep.Confirm);

        await sut.HandleCallbackAsync(ChatId, UserId, "c6", CallbackData.WizardConfirm, CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<AddOwnGradeCommand>(c =>
                c.TelegramUserId == UserId &&
                c.SubjectId == SubjectId &&
                c.Value == 5 &&
                c.Weight == 1m &&
                c.Comment == null &&
                c.OccurredAt == Now),
            Arg.Any<CancellationToken>());

        _store.Peek(ChatId).Should().BeNull(); // conversation cleared after saving
    }

    [Fact]
    public async Task Cancel_callback_clears_conversation()
    {
        StubSubjects();
        var sut = CreateSut();

        await sut.StartAddAsync(ChatId, UserId, CancellationToken.None);
        await sut.HandleCallbackAsync(ChatId, UserId, "c1", CallbackData.WizardCancel, CancellationToken.None);

        _store.Peek(ChatId).Should().BeNull();
    }

    [Fact]
    public async Task Value_can_be_typed_instead_of_tapped()
    {
        StubSubjects();
        var sut = CreateSut();

        await sut.StartAddAsync(ChatId, UserId, CancellationToken.None);
        await sut.HandleCallbackAsync(ChatId, UserId, "c1", CallbackData.WizardSubject(SubjectId), CancellationToken.None);

        var consumed = await sut.TryHandleTextAsync(ChatId, UserId, "4", CancellationToken.None);

        consumed.Should().BeTrue();
        var state = _store.Peek(ChatId)!;
        state.Value.Should().Be(4);
        state.Step.Should().Be(GradeStep.Comment);
    }

    [Fact]
    public async Task View_callback_renders_average_and_answers_query()
    {
        var page = new GradesPageDto(
            SubjectId,
            new List<GradeDto> { new(Guid.NewGuid(), SubjectId, "Mathematics", 5, 1m, "great", Now) },
            Page: 1, PageSize: 5, TotalCount: 1, Average: 5.0);

        _sender.Send(Arg.Any<GetOwnGradesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(page));

        var sut = CreateSut();
        await sut.HandleCallbackAsync(ChatId, UserId, "v1", CallbackData.ViewSubject(SubjectId, 1), CancellationToken.None);

        await _telegram.Received(1).SendKeyboardAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("Average grade: 5") && s.Contains("Mathematics")),
            Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>>(),
            Arg.Any<CancellationToken>());
        await _telegram.Received(1).AnswerCallbackAsync("v1", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Text_without_active_conversation_is_not_consumed()
    {
        var consumed = await CreateSut().TryHandleTextAsync(ChatId, UserId, "hello", CancellationToken.None);
        consumed.Should().BeFalse();
    }
}
