using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Reminders;
using EduTrack.Application.Reminders.Commands.SnoozeReminder;
using MediatR;

namespace EduTrack.Bot.Web.Telegram;

/// <summary>
/// Handles the snooze buttons attached to reminder notifications.
/// </summary>
public sealed class ReminderModule
{
    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;
    private readonly ILogger<ReminderModule> _logger;

    public ReminderModule(ISender sender, ITelegramSender telegram, ILogger<ReminderModule> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _logger = logger;
    }

    public async Task HandleCallbackAsync(long chatId, long telegramUserId, string callbackQueryId, string data, CancellationToken ct)
    {
        try
        {
            if (!ReminderCallback.TryParseSnooze(data, out var reminderId, out var option))
            {
                await _telegram.AnswerCallbackAsync(callbackQueryId, cancellationToken: ct);
                return;
            }

            var result = await _sender.Send(new SnoozeReminderCommand(telegramUserId, reminderId, option), ct);

            if (result.IsFailure)
            {
                await _telegram.AnswerCallbackAsync(callbackQueryId, result.Error.Message, ct);
                return;
            }

            await _telegram.AnswerCallbackAsync(callbackQueryId, "Snoozed", ct);
            await _telegram.SendTextAsync(chatId, $"⏰ I'll remind you again {Describe(option)}.", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle reminder callback {Data} from {TelegramUserId}", data, telegramUserId);
            await _telegram.AnswerCallbackAsync(callbackQueryId, cancellationToken: ct);
        }
    }

    private static string Describe(SnoozeOption option) => option switch
    {
        SnoozeOption.OneHour => "in 1 hour",
        SnoozeOption.ThreeHours => "in 3 hours",
        SnoozeOption.TomorrowMorning => "tomorrow morning",
        _ => "later",
    };
}
