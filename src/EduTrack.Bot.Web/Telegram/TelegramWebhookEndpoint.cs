using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduTrack.Infrastructure.Telegram.Configuration;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace EduTrack.Bot.Web.Telegram;

public static class TelegramWebhookEndpoint
{
    private const string SecretHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    public static IEndpointRouteBuilder MapTelegramWebhook(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/telegram/webhook", HandleAsync)
            .WithName("TelegramWebhook")
            .ExcludeFromDescription();

        return endpoints;
    }

    private static bool IsValidSecret(HttpContext context, string expectedSecret)
    {
        if (!context.Request.Headers.TryGetValue(SecretHeaderName, out var provided))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided.ToString());
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IOptions<TelegramOptions> options,
        WebhookUpdateProcessor processor,
        ILogger<WebhookUpdateProcessor> logger,
        CancellationToken cancellationToken)
    {
        if (!IsValidSecret(context, options.Value.WebhookSecret))
        {
            logger.LogWarning("Rejected Telegram webhook call with invalid secret token.");
            return Results.Unauthorized();
        }

        Update? update;
        try
        {
            update = await JsonSerializer.DeserializeAsync<Update>(context.Request.Body, JsonBotAPI.Options, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Received malformed Telegram update payload.");
            return Results.BadRequest();
        }

        if (update is null)
        {
            return Results.BadRequest();
        }

        try
        {
            await processor.ProcessAsync(update, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process Telegram update {UpdateId}", update.Id);
        }

        return Results.Ok();
    }
}
