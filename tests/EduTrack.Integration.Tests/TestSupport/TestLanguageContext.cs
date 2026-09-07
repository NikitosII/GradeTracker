using EduTrack.Bot.Web.Localization;

namespace EduTrack.Integration.Tests.TestSupport;

public sealed class TestLanguageContext : ILanguageContext
{
    public string Language { get; set; } = "en";
}
