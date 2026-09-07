using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Admin;
using EduTrack.Application.Admin.Commands.CreateInviteCode;
using EduTrack.Application.Admin.Commands.CreateSubject;
using EduTrack.Application.Admin.Queries.GetAllSubjects;
using EduTrack.Application.Admin.Queries.GetInviteCodes;
using EduTrack.Application.Studies;
using EduTrack.Application.Users;
using EduTrack.Application.Users.Queries.GetUserProfile;
using EduTrack.Bot.Web.Conversations;
using EduTrack.Bot.Web.Telegram;
using EduTrack.Domain.Common;
using EduTrack.Domain.Users;
using EduTrack.Integration.Tests.TestSupport;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EduTrack.Integration.Tests;

public class AdminModuleTests
{
    private const long ChatId = 900;
    private const long UserId = 900;
    private const int MessageId = 42;

    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ITelegramSender _telegram = Substitute.For<ITelegramSender>();
    private readonly InMemoryConversationStore _store = new();

    private AdminModule CreateSut() =>
        new(_sender, _telegram, _store, new TestUiText(), NullLogger<AdminModule>.Instance);

    private void StubProfile(string role) =>
        _sender.Send(Arg.Any<GetUserProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new UserProfileDto(
                Guid.NewGuid(), UserId, "root", "Root", role, "UTC", "ru", true, DateTime.UtcNow)));

    [Fact]
    public async Task Menu_is_blocked_for_non_admin()
    {
        StubProfile(nameof(UserRole.Student));

        await CreateSut().ShowMenuAsync(ChatId, UserId, CancellationToken.None);

        await _telegram.Received(1).SendTextAsync(ChatId, AdminErrors.NotAdmin.Message, Arg.Any<CancellationToken>());
        await _telegram.DidNotReceive().SendKeyboardAsync(
            ChatId, Arg.Any<string>(), Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Menu_is_shown_for_admin()
    {
        StubProfile(nameof(UserRole.Admin));

        await CreateSut().ShowMenuAsync(ChatId, UserId, CancellationToken.None);

        await _telegram.Received(1).SendKeyboardAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("Admin menu")),
            Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Menu_is_shown_in_russian_for_a_russian_admin()
    {
        StubProfile(nameof(UserRole.Admin));

        var russian = new AdminModule(
            _sender, _telegram, _store,
            new TestUiText(new TestLanguageContext { Language = "ru" }),
            NullLogger<AdminModule>.Instance);

        await russian.ShowMenuAsync(ChatId, UserId, CancellationToken.None);

        await _telegram.Received(1).SendKeyboardAsync(
            ChatId,
            Arg.Is<string>(s => s.Contains("Меню администратора")),
            Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Choosing_expiry_sends_create_invite_command()
    {
        StubProfile(nameof(UserRole.Admin));
        _sender.Send(Arg.Any<CreateInviteCodeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new InviteCodeDto(
                Guid.NewGuid(), "ABCD2345", nameof(UserRole.Student), null, false, DateTime.UtcNow)));
        _sender.Send(Arg.Any<GetInviteCodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<InviteCodeDto>>(new List<InviteCodeDto>()));

        await CreateSut().HandleCallbackAsync(
            ChatId, UserId, "c1", CallbackData.AdminCodeExpiry((int)UserRole.Student, 7), MessageId, CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<CreateInviteCodeCommand>(c =>
                c.CallerTelegramUserId == UserId &&
                c.Role == UserRole.Student &&
                c.ExpiresInDays == 7),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Subject_add_wizard_walks_steps_and_creates()
    {
        StubProfile(nameof(UserRole.Admin));
        _sender.Send(Arg.Any<CreateSubjectCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new SubjectDto(Guid.NewGuid(), "Chemistry", true)));
        _sender.Send(Arg.Any<GetAllSubjectsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<SubjectDto>>(new List<SubjectDto>()));

        var sut = CreateSut();

        await sut.HandleCallbackAsync(ChatId, UserId, "c1", CallbackData.AdminNewSubject, MessageId, CancellationToken.None);
        _store.Peek(ChatId)!.Step.Should().Be(AdminStep.SubjectName);

        (await sut.TryHandleTextAsync(ChatId, UserId, "Chemistry", CancellationToken.None)).Should().BeTrue();
        _store.Peek(ChatId)!.Step.Should().Be(AdminStep.SubjectDescription);

        await sut.HandleCallbackAsync(ChatId, UserId, "c2", CallbackData.AdminSubjectSkip, MessageId, CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<CreateSubjectCommand>(c => c.Name == "Chemistry" && c.Description == null),
            Arg.Any<CancellationToken>());
        _store.Peek(ChatId).Should().BeNull();
    }

    [Fact]
    public async Task Text_without_active_conversation_is_not_consumed()
    {
        var consumed = await CreateSut().TryHandleTextAsync(ChatId, UserId, "hello", CancellationToken.None);
        consumed.Should().BeFalse();
    }
}
