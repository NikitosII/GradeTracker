namespace EduTrack.Application.Localization;

/// <summary>
/// Resolves UI strings for a given language (e.g. "ru", "en"). 
/// </summary>
public interface ITranslator
{
    string? Find(string? language, string key, params object[] args);
}
