using EduTrack.Application.Abstractions.Telegram;

namespace EduTrack.Bot.Web.Conversations;

/// <summary>
/// Renders a wizard into a single, reused Telegram message: the first step
/// sends it, every later step edits it in place, and completion turns it into
/// the final result. 
/// </summary>
internal static class WizardUi
{
    private static readonly IReadOnlyList<IReadOnlyList<InlineButton>> NoKeyboard =
        Array.Empty<IReadOnlyList<InlineButton>>();

    public static async Task ShowStepAsync(
        ITelegramSender telegram,
        IConversationStore conversations,
        long chatId,
        ConversationState state,
        string text,
        IReadOnlyList<IReadOnlyList<InlineButton>> rows,
        CancellationToken ct)
    {
        if (state.WizardMessageId is int id)
        {
            await telegram.EditKeyboardAsync(chatId, id, text, rows, ct);
        }
        else
        {
            state.WizardMessageId = await telegram.SendKeyboardAsync(chatId, text, rows, ct);
        }

        await conversations.SetAsync(chatId, state, ct);
    }

    public static async Task CompleteAsync(
        ITelegramSender telegram,
        IConversationStore conversations,
        long chatId,
        ConversationState? state,
        string finalText,
        CancellationToken ct)
    {
        if (state?.WizardMessageId is int id)
        {
            await telegram.EditKeyboardAsync(chatId, id, finalText, NoKeyboard, ct);
        }
        else
        {
            await telegram.SendTextAsync(chatId, finalText, ct);
        }

        await conversations.RemoveAsync(chatId, ct);
    }
}
