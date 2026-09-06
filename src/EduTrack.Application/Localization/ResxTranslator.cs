using System.Globalization;
using System.Resources;

namespace EduTrack.Application.Localization;

public sealed class ResxTranslator : ITranslator
{
    private static readonly ResourceManager Resources =
        new("EduTrack.Application.Localization.Strings", typeof(ResxTranslator).Assembly);

    public string? Find(string? language, string key, params object[] args)
    {
        var culture = ResolveCulture(language);
        var template = Resources.GetString(key, culture);

        if (template is null)
        {
            return null;
        }

        return args.Length == 0 ? template : string.Format(culture, template, args);
    }

    private static CultureInfo ResolveCulture(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return CultureInfo.InvariantCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
