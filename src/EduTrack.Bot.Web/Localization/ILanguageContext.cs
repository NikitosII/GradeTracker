namespace EduTrack.Bot.Web.Localization;

/// <summary>
/// Carries the language for the update currently being processed.
/// </summary>
public interface ILanguageContext
{
    string Language { get; set; }
}

internal sealed class LanguageContext : ILanguageContext
{
    public string Language { get; set; } = "ru";
}
