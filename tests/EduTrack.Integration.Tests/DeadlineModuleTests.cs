using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Queries.GetOwnAssignments;
using EduTrack.Application.Studies.Queries.GetSubjects;
using EduTrack.Application.Users;
using EduTrack.Application.Users.Queries.GetUserProfile;
using EduTrack.Bot.Web.Conversations;
using EduTrack.Bot.Web.Localization;
using EduTrack.Bot.Web.Telegram;
using EduTrack.Domain.Common;
using EduTrack.Integration.Tests.TestSupport;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EduTrack.Integration.Tests;

public class DeadlineModuleTests
{
    private const long ChatId = 900;
    private const long UserId = 900;
    private static readonly DateTime NowUtc = new(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc); // Wednesday
    private static readonly Guid PhysicsId = Guid.NewGuid();

    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly RecordingTelegramSender _telegram = new();
    private readonly InMemoryConversationStore _conversations = new();

    private DeadlineModule CreateSut(IUiText? text = null) =>
        new(_sender, _telegram, _conversations, new FixedClock(NowUtc), text ?? new TestUiText(), NullLogger<DeadlineModule>.Instance);

    private void SeedProfileAndSubjects(string timeZone = "UTC", string language = "en")
    {
        _sender.Send(Arg.Any<GetUserProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new UserProfileDto(
                Guid.NewGuid(), UserId, "nick", "Ada", "Student", timeZone, language, true, NowUtc)));

        _sender.Send(Arg.Any<GetSubjectsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<SubjectDto>>(new[]
            {
                new SubjectDto(PhysicsId, "Physics", true),
            }));
    }

    [Fact]
    public async Task Quick_add_jumps_to_confirmation_with_parsed_values()
    {
        SeedProfileAndSubjects();

        await CreateSut().QuickAddAsync(ChatId, UserId, "Physics homework tomorrow 18:00", CancellationToken.None);

        _telegram.Sent("Physics").Should().BeTrue();
        _telegram.Sent("homework").Should().BeTrue();
        _telegram.Sent("2026-09-10 18:00").Should().BeTrue();

        var state = await _conversations.GetAsync(ChatId, CancellationToken.None);
        state.Should().NotBeNull();
        state!.Step.Should().Be(DeadlineStep.Confirm);
        state.SubjectId.Should().Be(PhysicsId);
        state.DueAtUtc.Should().Be(new DateTime(2026, 9, 10, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Quick_add_without_argument_shows_usage()
    {
        SeedProfileAndSubjects();

        await CreateSut().QuickAddAsync(ChatId, UserId, "   ", CancellationToken.None);

        _telegram.Sent("/quick").Should().BeTrue();
        (await _conversations.GetAsync(ChatId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Quick_add_falls_back_when_subject_or_date_missing()
    {
        SeedProfileAndSubjects();

        // No recognizable due date -> guidance to the guided wizard, no state stored.
        await CreateSut().QuickAddAsync(ChatId, UserId, "Physics homework", CancellationToken.None);

        _telegram.Sent("/deadline_add").Should().BeTrue();
        (await _conversations.GetAsync(ChatId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Quick_add_with_unknown_subject_offers_a_subject_picker()
    {
        SeedProfileAndSubjects();

        // Date parses, but "Biology" matches no existing subject -> pick, don't dead-end.
        await CreateSut().QuickAddAsync(ChatId, UserId, "Biology homework tomorrow", CancellationToken.None);

        var state = await _conversations.GetAsync(ChatId, CancellationToken.None);
        state.Should().NotBeNull();
        state!.Step.Should().Be(DeadlineStep.QuickSubject);
        state.SubjectId.Should().BeNull();
        state.DueAtUtc.Should().Be(new DateTime(2026, 9, 10, 23, 59, 0, DateTimeKind.Utc));
        _telegram.HasButton(CallbackData.DeadlineWizardQuickSubject(PhysicsId)).Should().BeTrue();
    }

    [Fact]
    public async Task Quick_subject_pick_jumps_to_confirmation()
    {
        SeedProfileAndSubjects();
        await CreateSut().QuickAddAsync(ChatId, UserId, "Biology homework tomorrow", CancellationToken.None);

        await CreateSut().HandleCallbackAsync(
            ChatId, UserId, "cbq", CallbackData.DeadlineWizardQuickSubject(PhysicsId), CancellationToken.None);

        var state = await _conversations.GetAsync(ChatId, CancellationToken.None);
        state!.Step.Should().Be(DeadlineStep.Confirm);
        state.SubjectId.Should().Be(PhysicsId);
        state.SubjectName.Should().Be("Physics");
    }

    [Fact]
    public async Task Deadlines_view_shows_hub_action_buttons()
    {
        _sender.Send(Arg.Any<GetOwnAssignmentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AssignmentsPageDto(Array.Empty<AssignmentDto>(), 1, 5, 0)));

        await CreateSut().ShowDeadlinesAsync(ChatId, UserId, DeadlineModule.ScopeAll, 1, CancellationToken.None);

        _telegram.HasButton(CallbackData.DeadlineView(DeadlineModule.ScopeToday, 1)).Should().BeTrue();
        _telegram.HasButton(CallbackData.DeadlineView(DeadlineModule.ScopeWeek, 1)).Should().BeTrue();
        _telegram.HasButton(CallbackData.DeadlineView(DeadlineModule.ScopeNext, 1)).Should().BeTrue();
        _telegram.HasButton(CallbackData.DeadlineQuick).Should().BeTrue();
        _telegram.HasButton(CallbackData.DeadlineEdit).Should().BeTrue();
    }

    [Fact]
    public async Task Hub_quick_button_shows_usage_hint()
    {
        await CreateSut().HandleCallbackAsync(ChatId, UserId, "cbq", CallbackData.DeadlineQuick, CancellationToken.None);

        _telegram.Sent("/quick").Should().BeTrue();
    }

    [Fact]
    public async Task Hub_edit_button_starts_edit_wizard()
    {
        SeedProfileAndSubjects();

        await CreateSut().HandleCallbackAsync(ChatId, UserId, "cbq", CallbackData.DeadlineEdit, CancellationToken.None);

        var state = await _conversations.GetAsync(ChatId, CancellationToken.None);
        state.Should().NotBeNull();
        state!.Flow.Should().Be(ConversationFlow.DeadlineEdit);
        state.Step.Should().Be(DeadlineStep.Subject);
    }
}
