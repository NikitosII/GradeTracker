using EduTrack.Application.Localization;
using EduTrack.Bot.Web.Localization;
using EduTrack.Domain.Common;

namespace EduTrack.Integration.Tests.TestSupport;

public sealed class TestUiText : IUiText
{
    private static readonly ITranslator Translator = new ResxTranslator();
    private readonly ILanguageContext _language;

    public TestUiText(ILanguageContext? language = null)
        => _language = language ?? new TestLanguageContext();

    public string Get(string key, params object[] args) => Translator.Find(_language.Language, key, args) ?? key;

    public string Error(Error error) => Translator.Find(_language.Language, TextKeys.Error(error.Code)) ?? error.Message;
}
