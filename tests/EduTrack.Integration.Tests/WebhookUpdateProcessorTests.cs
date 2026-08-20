using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Bot.Web.Telegram;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Telegram.Bot.Types;

namespace EduTrack.Integration.Tests;

public class WebhookUpdateProcessorTests
{
    private readonly ITelegramSender _sender = Substitute.For<ITelegramSender>();

    private WebhookUpdateProcessor CreateSut() =>
        new(_sender, NullLogger<WebhookUpdateProcessor>.Instance);

    private static Update MessageUpdate(long chatId, string text) => new()
    {
        Id = 1,
        Message = new Message
        {
            Chat = new Chat { Id = chatId },
            Text = text,
        },
    };

    [Fact]
    public async Task Start_command_sends_welcome_message()
    {
        await CreateSut().ProcessAsync(MessageUpdate(42, "/start"), CancellationToken.None);

        await _sender.Received(1).SendTextAsync(
            42,
            Arg.Is<string>(s => s.Contains("Welcome to GradeTracker")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_text_is_echoed_back()
    {
        await CreateSut().ProcessAsync(MessageUpdate(7, "hello there"), CancellationToken.None);

        await _sender.Received(1).SendTextAsync(
            7,
            "You said: hello there",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Empty_text_is_ignored()
    {
        await CreateSut().ProcessAsync(MessageUpdate(7, "   "), CancellationToken.None);

        await _sender.DidNotReceiveWithAnyArgs().SendTextAsync(default, default!, default);
    }
}
