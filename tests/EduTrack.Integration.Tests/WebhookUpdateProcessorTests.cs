using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Users;
using EduTrack.Application.Users.Commands.BindUser;
using EduTrack.Application.Users.Queries.GetUserProfile;
using EduTrack.Domain.Common;
using EduTrack.Bot.Web.Telegram;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace EduTrack.Integration.Tests;

public class WebhookUpdateProcessorTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ITelegramSender _telegram = Substitute.For<ITelegramSender>();

    private WebhookUpdateProcessor CreateSut() =>
        new(_sender, _telegram, NullLogger<WebhookUpdateProcessor>.Instance);

    private static Update MessageUpdate(long fromId, string text) => new()
    {
        Id = 1,
        Message = new Message
        {
            Chat = new Chat { Id = fromId, Type = ChatType.Private },
            From = new Telegram.Bot.Types.User { Id = fromId, FirstName = "Test", Username = "tester" },
            Text = text,
        },
    };

    private static UserProfileDto SampleProfile(long id) =>
        new(Guid.NewGuid(), id, "tester", "Test User", "Student", "UTC", "ru", true, DateTime.UtcNow);

    [Fact]
    public async Task Start_sends_welcome_and_does_not_call_mediator()
    {
        await CreateSut().ProcessAsync(MessageUpdate(42, "/start"), CancellationToken.None);

        await _telegram.Received(1).SendTextAsync(
            42,
            Arg.Is<string>(s => s.Contains("Welcome to GradeTracker")),
            Arg.Any<CancellationToken>());
        _sender.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Bind_without_code_sends_usage_and_does_not_call_mediator()
    {
        await CreateSut().ProcessAsync(MessageUpdate(42, "/bind"), CancellationToken.None);

        await _telegram.Received(1).SendTextAsync(
            42,
            Arg.Is<string>(s => s.Contains("Usage: /bind")),
            Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<BindUserCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bind_with_code_sends_command_and_reports_success()
    {
        _sender.Send(Arg.Any<BindUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(SampleProfile(42)));

        await CreateSut().ProcessAsync(MessageUpdate(42, "/bind DEV-ADMIN"), CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<BindUserCommand>(c => c.Code == "DEV-ADMIN" && c.TelegramUserId == 42),
            Arg.Any<CancellationToken>());
        await _telegram.Received(1).SendTextAsync(
            42,
            Arg.Is<string>(s => s.Contains("Account linked")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Profile_when_not_bound_sends_error_message()
    {
        _sender.Send(Arg.Any<GetUserProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UserProfileDto>(UserErrors.NotBound));

        await CreateSut().ProcessAsync(MessageUpdate(42, "/profile"), CancellationToken.None);

        await _telegram.Received(1).SendTextAsync(
            42,
            UserErrors.NotBound.Message,
            Arg.Any<CancellationToken>());
    }
}
