using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EduTrack.Domain.Users;
using EduTrack.Infrastructure.Persistence;
using EduTrack.Integration.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgUser = Telegram.Bot.Types.User;

namespace EduTrack.Integration.Tests;

/// <summary>
/// Drives the live <c>POST /api/telegram/webhook</c> endpoint over HTTP against a
/// real Postgres container: secret-token enforcement, payload validation and a
/// full <c>/bind</c> round-trip that persists a user through EF Core.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class WebhookEndpointTests : IDisposable
{
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    private readonly EduTrackWebAppFactory _factory;

    public WebhookEndpointTests(PostgresFixture postgres)
        => _factory = new EduTrackWebAppFactory(postgres.ConnectionString);

    [Fact]
    public async Task Rejects_missing_secret_token_with_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/telegram/webhook",
            JsonContent(BuildMessageUpdate(10, 10, "/start")));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rejects_wrong_secret_token_with_401()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/telegram/webhook")
        {
            Content = JsonContent(BuildMessageUpdate(11, 11, "/start")),
        };
        request.Headers.Add(SecretHeader, "not-the-secret");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rejects_malformed_payload_with_400()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/telegram/webhook")
        {
            Content = new StringContent("{ this is not valid json", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(SecretHeader, EduTrackWebAppFactory.WebhookSecret);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_command_returns_200_and_replies_with_welcome()
    {
        var client = _factory.CreateClient();

        var response = await PostUpdateAsync(client, BuildMessageUpdate(20, 20, "/start"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.TelegramSender.Sent("Welcome to GradeTracker").Should().BeTrue();
    }

    [Fact]
    public async Task Bind_with_valid_code_persists_the_user()
    {
        const long telegramUserId = 424242;
        const string code = "WEBHOOK-BIND-OK";

        await _factory.WithDbContextAsync(async db =>
        {
            db.InviteCodes.Add(InviteCode.Create(code, UserRole.Student, DateTime.UtcNow));
            await db.SaveChangesAsync();
        });

        var client = _factory.CreateClient();

        var response = await PostUpdateAsync(client,
            BuildMessageUpdate(30, telegramUserId, $"/bind {code}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.TelegramSender.Sent("Account linked").Should().BeTrue();

        await _factory.WithDbContextAsync(async db =>
        {
            var exists = await db.Users.AnyAsync(u => u.TelegramUserId == telegramUserId);
            exists.Should().BeTrue();
        });
    }

    [Fact]
    public async Task Bind_with_unknown_code_does_not_create_a_user()
    {
        const long telegramUserId = 525252;
        var client = _factory.CreateClient();

        var response = await PostUpdateAsync(client,
            BuildMessageUpdate(31, telegramUserId, "/bind NO-SUCH-CODE"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.WithDbContextAsync(async db =>
        {
            var exists = await db.Users.AnyAsync(u => u.TelegramUserId == telegramUserId);
            exists.Should().BeFalse();
        });
    }

    private async Task<HttpResponseMessage> PostUpdateAsync(HttpClient client, Update update)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/telegram/webhook")
        {
            Content = JsonContent(update),
        };
        request.Headers.Add(SecretHeader, EduTrackWebAppFactory.WebhookSecret);
        return await client.SendAsync(request);
    }

    private static StringContent JsonContent(Update update)
    {
        var json = JsonSerializer.Serialize(update, JsonBotAPI.Options);
        var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static Update BuildMessageUpdate(int updateId, long userId, string text) =>
        new()
        {
            Id = updateId,
            Message = new Message
            {
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = userId, Type = ChatType.Private },
                From = new TgUser { Id = userId, FirstName = "Test", IsBot = false },
                Text = text,
            },
        };

    public void Dispose() => _factory.Dispose();
}
