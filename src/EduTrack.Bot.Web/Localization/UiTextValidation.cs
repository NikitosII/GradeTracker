using FluentValidation;

namespace EduTrack.Bot.Web.Localization;

/// <summary>
/// Renders FluentValidation failures as a localized bullet list.
/// </summary>
public static class UiTextValidation
{
    public static string ValidationDetails(this IUiText text, ValidationException ex) =>
        string.Join("\n", ex.Errors.Select(e => "- " + Resolve(text, e.ErrorCode, e.ErrorMessage)));

    private static string Resolve(IUiText text, string? errorCode, string fallback)
    {
        if (string.IsNullOrEmpty(errorCode) || !errorCode.StartsWith("valid.", StringComparison.Ordinal))
        {
            return fallback;
        }

        var localized = text.Get(errorCode);
        return localized == errorCode ? fallback : localized; // Get returns the key when undefined
    }
}
