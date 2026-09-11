using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Queries.GetSubjects;
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

public class GradeModuleTests
{
    private const long ChatId = 600;
    private const long UserId = 600;
    private static readonly DateTime NowUtc = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PhysicsId = Guid.NewGuid();

    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly RecordingTelegramSender _telegram = new();
    private readonly InMemoryConversationStore _conversations = new();

    private GradeModule CreateSut() =>
        new(_sender, _telegram, _conversations, new FixedClock(NowUtc), new TestUiText(), NullLogger<GradeModule>.Instance);

    private void SeedSubjects() =>
        _sender.Send(Arg.Any<GetSubjectsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<SubjectDto>>(new[] { new SubjectDto(PhysicsId, "Physics", true) }));

    [Fact]
    public async Task Grades_menu_shows_add_and_edit_buttons()
    {
        SeedSubjects();

        await CreateSut().ShowGradesMenuAsync(ChatId, UserId, CancellationToken.None);

        _telegram.HasButton(CallbackData.GradeAdd).Should().BeTrue();
        _telegram.HasButton(CallbackData.GradeEdit).Should().BeTrue();
    }

    [Fact]
    public async Task Hub_add_button_starts_add_wizard()
    {
        SeedSubjects();

        await CreateSut().HandleCallbackAsync(ChatId, UserId, "cbq", CallbackData.GradeAdd, CancellationToken.None);

        var state = await _conversations.GetAsync(ChatId, CancellationToken.None);
        state.Should().NotBeNull();
        state!.Flow.Should().Be(ConversationFlow.GradeAdd);
        state.Step.Should().Be(GradeStep.Subject);
    }

    [Fact]
    public async Task Hub_edit_button_starts_edit_wizard()
    {
        SeedSubjects();

        await CreateSut().HandleCallbackAsync(ChatId, UserId, "cbq", CallbackData.GradeEdit, CancellationToken.None);

        var state = await _conversations.GetAsync(ChatId, CancellationToken.None);
        state.Should().NotBeNull();
        state!.Flow.Should().Be(ConversationFlow.GradeEdit);
        state.Step.Should().Be(GradeStep.Subject);
    }
}
