using EduTrack.Application.Localization;
using EduTrack.Domain.Common;

namespace EduTrack.Bot.Web.Localization;

/// <summary>
/// Wrapper that resolves UI strings in the current update's language
/// </summary>
public interface IUiText
{
    string Get(string key, params object[] args);

    /// <summary>Localized message for a Result error, falling back to the error's own message.</summary>
    string Error(Error error);
}

internal sealed class UiText : IUiText
{
    private readonly ITranslator _translator;
    private readonly ILanguageContext _language;

    public UiText(ITranslator translator, ILanguageContext language)
    {
        _translator = translator;
        _language = language;
    }

    public string Get(string key, params object[] args)
        => _translator.Find(_language.Language, key, args) ?? key;

    public string Error(Error error)
        => _translator.Find(_language.Language, TextKeys.Error(error.Code)) ?? error.Message;
}
