using EduTrack.Bot.Web.Localization;
using EduTrack.Integration.Tests.TestSupport;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;

namespace EduTrack.Integration.Tests;

public class UiTextValidationTests
{
    private static ValidationException Ex(params ValidationFailure[] failures) => new(failures);

    [Fact]
    public void Maps_custom_error_code_to_localized_russian_message()
    {
        var text = new TestUiText(new TestLanguageContext { Language = "ru" });
        var ex = Ex(new ValidationFailure("TimeZone", "Unknown time zone.") { ErrorCode = "valid.timezone" });

        text.ValidationDetails(ex).Should().Contain("Неизвестный часовой пояс");
    }

    [Fact]
    public void Falls_back_to_the_message_for_builtin_codes()
    {
        var text = new TestUiText(); // English
        var ex = Ex(new ValidationFailure("Value", "'Value' must be between 1 and 5.")
        {
            ErrorCode = "InclusiveBetweenValidator",
        });

        text.ValidationDetails(ex).Should().Contain("must be between 1 and 5");
    }

    [Fact]
    public void Prefixes_each_failure_with_a_bullet()
    {
        var text = new TestUiText();
        var ex = Ex(
            new ValidationFailure("A", "first") { ErrorCode = "NotEmptyValidator" },
            new ValidationFailure("B", "second") { ErrorCode = "NotEmptyValidator" });

        var details = text.ValidationDetails(ex);

        details.Should().Contain("- first");
        details.Should().Contain("- second");
    }
}
