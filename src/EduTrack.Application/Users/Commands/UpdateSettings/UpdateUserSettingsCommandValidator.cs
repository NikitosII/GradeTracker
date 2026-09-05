using FluentValidation;

namespace EduTrack.Application.Users.Commands.UpdateSettings;

public sealed class UpdateUserSettingsCommandValidator : AbstractValidator<UpdateUserSettingsCommand>
{
    private static readonly string[] SupportedLanguages = { "ru", "en" };

    public UpdateUserSettingsCommandValidator()
    {
        RuleFor(x => x.TelegramUserId)
            .GreaterThan(0);

        RuleFor(x => x.TimeZone)
            .NotEmpty()
            .MaximumLength(64)
            .Must(BeAValidTimeZone)
            .WithMessage("Unknown time zone.");

        RuleFor(x => x.Language)
            .Must(lang => SupportedLanguages.Contains(lang))
            .WithMessage("Language must be one of: ru, en.");

        RuleFor(x => x.QuietHoursStart)
            .InclusiveBetween(0, 23)
            .When(x => x.QuietHoursStart is not null);

        RuleFor(x => x.QuietHoursEnd)
            .InclusiveBetween(0, 23)
            .When(x => x.QuietHoursEnd is not null);

        RuleFor(x => x)
            .Must(x => x.QuietHoursStart is null == (x.QuietHoursEnd is null))
            .WithMessage("Quiet hours start and end must both be set or both be cleared.");
    }

    private static bool BeAValidTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }
}
