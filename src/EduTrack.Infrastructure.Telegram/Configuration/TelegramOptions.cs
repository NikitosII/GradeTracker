using System.ComponentModel.DataAnnotations;

namespace EduTrack.Infrastructure.Telegram.Configuration;

/// <summary>
/// Telegram bot configuration.
/// </summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    [Required]
    public string BotToken { get; set; } = string.Empty;

    [Required]
    public string WebhookSecret { get; set; } = string.Empty;

    public string? WebhookUrl { get; set; }
}
