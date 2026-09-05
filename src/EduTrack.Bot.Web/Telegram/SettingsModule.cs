using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Users;
using EduTrack.Application.Users.Commands.UpdateSettings;
using EduTrack.Application.Users.Queries.GetUserSettings;
using FluentValidation;
using MediatR;

namespace EduTrack.Bot.Web.Telegram;

/// <summary>
/// Handles the /settings menu
/// </summary>
public sealed class SettingsModule
{
    public const string Namespace = CallbackData.SettingsNamespace;

    private static readonly (string Id, string Label)[] TimeZones =
    {
        ("UTC", "UTC"),
        ("Europe/Kaliningrad", "Kaliningrad (UTC+2)"),
        ("Europe/Moscow", "Moscow (UTC+3)"),
        ("Asia/Yekaterinburg", "Yekaterinburg (UTC+5)"),
        ("Asia/Novosibirsk", "Novosibirsk (UTC+7)"),
        ("Asia/Vladivostok", "Vladivostok (UTC+10)"),
        ("Europe/London", "London (UTC+0/+1)"),
        ("America/New_York", "New York (UTC-5/-4)"),
    };

    private static readonly (int Start, int End)[] QuietPresets =
    {
        (22, 8),
        (23, 7),
        (0, 6),
    };

    private static readonly IReadOnlyList<IReadOnlyList<InlineButton>> NoKeyboard =
        Array.Empty<IReadOnlyList<InlineButton>>();

    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;
    private readonly ILogger<SettingsModule> _logger;

    public SettingsModule(ISender sender, ITelegramSender telegram, ILogger<SettingsModule> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _logger = logger;
    }

    /// <summary>Entry point for the /settings command: sends a fresh menu message.</summary>
    public async Task ShowSettingsAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var settings = await LoadAsync(chatId, telegramUserId, ct);
        if (settings is null)
        {
            return;
        }

        await _telegram.SendKeyboardAsync(chatId, Render(settings), BuildMenu(settings), ct);
    }

    public async Task HandleCallbackAsync(long chatId, long telegramUserId, string callbackQueryId, string data, int messageId, CancellationToken ct)
    {
        try
        {
            var parts = CallbackData.Parts(data);
            var action = parts.Length > 1 ? parts[1] : string.Empty;

            switch (action)
            {
                case "menu":
                    await ShowMenuInPlaceAsync(chatId, telegramUserId, messageId, ct);
                    break;

                case "toggle" when parts.Length >= 3:
                    await ToggleAsync(chatId, telegramUserId, parts[2], messageId, ct);
                    break;

                case "tz" when parts.Length >= 3:
                    await ApplyAsync(chatId, telegramUserId, messageId, s => s with { TimeZone = parts[2] }, ct);
                    break;

                case "tz":
                    await ShowTimeZonePickerAsync(chatId, messageId, ct);
                    break;

                case "lang" when parts.Length >= 3:
                    await ApplyAsync(chatId, telegramUserId, messageId, s => s with { Language = parts[2] }, ct);
                    break;

                case "lang":
                    await ShowLanguagePickerAsync(chatId, messageId, ct);
                    break;

                case "quiet" when parts.Length >= 3 && parts[2] == "off":
                    await ApplyAsync(chatId, telegramUserId, messageId, s => s with { QuietHoursStart = null, QuietHoursEnd = null }, ct);
                    break;

                case "quiet" when parts.Length >= 4 && int.TryParse(parts[2], out var start) && int.TryParse(parts[3], out var end):
                    await ApplyAsync(chatId, telegramUserId, messageId, s => s with { QuietHoursStart = start, QuietHoursEnd = end }, ct);
                    break;

                case "quiet":
                    await ShowQuietPickerAsync(chatId, messageId, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle settings callback {Data} from {TelegramUserId}", data, telegramUserId);
            await _telegram.EditKeyboardAsync(chatId, messageId, "Something went wrong. Please try again.", NoKeyboard, ct);
        }
        finally
        {
            await _telegram.AnswerCallbackAsync(callbackQueryId, cancellationToken: ct);
        }
    }

    private Task ToggleAsync(long chatId, long telegramUserId, string key, int messageId, CancellationToken ct) =>
        ApplyAsync(chatId, telegramUserId, messageId, s => key switch
        {
            "notif" => s with { NotificationsEnabled = !s.NotificationsEnabled },
            "digest" => s with { MorningDigestEnabled = !s.MorningDigestEnabled },
            "r24" => s with { Reminder24hEnabled = !s.Reminder24hEnabled },
            "r2" => s with { Reminder2hEnabled = !s.Reminder2hEnabled },
            _ => s,
        }, ct);

    /// <summary>Loads the caller's settings, applies a change, and re-renders the menu in place.</summary>
    private async Task ApplyAsync(
        long chatId,
        long telegramUserId,
        int messageId,
        Func<UserSettingsDto, UserSettingsDto> change,
        CancellationToken ct)
    {
        var current = await LoadAsync(chatId, telegramUserId, ct);
        if (current is null)
        {
            return;
        }

        var desired = change(current);

        try
        {
            var result = await _sender.Send(ToCommand(telegramUserId, desired), ct);
            if (result.IsFailure)
            {
                await _telegram.EditKeyboardAsync(chatId, messageId, result.Error.Message, NoKeyboard, ct);
                return;
            }

            await _telegram.EditKeyboardAsync(chatId, messageId, Render(result.Value), BuildMenu(result.Value), ct);
        }
        catch (ValidationException ex)
        {
            await _telegram.EditKeyboardAsync(chatId, messageId, ValidationText(ex), NoKeyboard, ct);
        }
    }

    private async Task ShowMenuInPlaceAsync(long chatId, long telegramUserId, int messageId, CancellationToken ct)
    {
        var settings = await LoadAsync(chatId, telegramUserId, ct);
        if (settings is null)
        {
            return;
        }

        await _telegram.EditKeyboardAsync(chatId, messageId, Render(settings), BuildMenu(settings), ct);
    }

    private async Task ShowTimeZonePickerAsync(long chatId, int messageId, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<InlineButton>>();
        foreach (var (id, label) in TimeZones)
        {
            rows.Add(new[] { new InlineButton(label, CallbackData.SettingsSetTimeZone(id)) });
        }

        rows.Add(BackRow());
        await _telegram.EditKeyboardAsync(chatId, messageId, "Choose your time zone:", rows, ct);
    }

    private async Task ShowLanguagePickerAsync(long chatId, int messageId, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[]
            {
                new InlineButton("Русский", CallbackData.SettingsSetLanguage("ru")),
                new InlineButton("English", CallbackData.SettingsSetLanguage("en")),
            },
            BackRow(),
        };
        await _telegram.EditKeyboardAsync(chatId, messageId, "Choose your language:", rows, ct);
    }

    private async Task ShowQuietPickerAsync(long chatId, int messageId, CancellationToken ct)
    {
        var presets = QuietPresets
            .Select(p => new InlineButton(FormatQuiet(p.Start, p.End), CallbackData.SettingsSetQuiet(p.Start, p.End)))
            .ToArray();

        var rows = new List<IReadOnlyList<InlineButton>>
        {
            presets,
            new[] { new InlineButton("Off", CallbackData.SettingsQuietOff) },
            BackRow(),
        };
        await _telegram.EditKeyboardAsync(chatId, messageId, "Quiet hours — no notifications during this window:", rows, ct);
    }

    private async Task<UserSettingsDto?> LoadAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetUserSettingsQuery(telegramUserId), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
            return null;
        }

        return result.Value;
    }

    private static UpdateUserSettingsCommand ToCommand(long telegramUserId, UserSettingsDto s) => new(
        telegramUserId,
        s.TimeZone,
        s.Language,
        s.NotificationsEnabled,
        s.MorningDigestEnabled,
        s.Reminder24hEnabled,
        s.Reminder2hEnabled,
        s.QuietHoursStart,
        s.QuietHoursEnd);

    private static List<IReadOnlyList<InlineButton>> BuildMenu(UserSettingsDto s) => new()
    {
        new[] { new InlineButton($"Notifications: {OnOff(s.NotificationsEnabled)}", CallbackData.SettingsToggle("notif")) },
        new[] { new InlineButton($"Morning digest: {OnOff(s.MorningDigestEnabled)}", CallbackData.SettingsToggle("digest")) },
        new[] { new InlineButton($"24h reminders: {OnOff(s.Reminder24hEnabled)}", CallbackData.SettingsToggle("r24")) },
        new[] { new InlineButton($"2h reminders: {OnOff(s.Reminder2hEnabled)}", CallbackData.SettingsToggle("r2")) },
        new[] { new InlineButton($"Time zone: {s.TimeZone}", CallbackData.SettingsTimeZone) },
        new[] { new InlineButton($"Quiet hours: {QuietLabel(s)}", CallbackData.SettingsQuiet) },
        new[] { new InlineButton($"Language: {s.Language}", CallbackData.SettingsLanguage) },
    };

    private static string Render(UserSettingsDto s)
    {
        var master = s.NotificationsEnabled ? string.Empty : "\n\nNotifications are off — only critical alerts are delivered.";
        return "Settings\n\nTap an item to change it." + master;
    }

    private static IReadOnlyList<InlineButton> BackRow() => new[]
    {
        new InlineButton("⬅ Back", CallbackData.SettingsMenu),
    };

    private static string OnOff(bool enabled) => enabled ? "on ✅" : "off ⛔";

    private static string QuietLabel(UserSettingsDto s) =>
        s.QuietHoursStart is { } start && s.QuietHoursEnd is { } end ? FormatQuiet(start, end) : "off";

    private static string FormatQuiet(int start, int end) => $"{start:00}:00–{end:00}:00";

    private static string ValidationText(ValidationException ex) =>
        "Invalid input:\n" + string.Join("\n", ex.Errors.Select(e => "- " + e.ErrorMessage));
}
