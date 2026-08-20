using FluentValidation;

namespace EduTrack.Application.Users.Commands.BindUser;

public sealed class BindUserCommandValidator : AbstractValidator<BindUserCommand>
{
    public BindUserCommandValidator()
    {
        RuleFor(x => x.TelegramUserId)
            .GreaterThan(0);

        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(64)
            .Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("The code may contain only letters, digits, '-' and '_'.");
    }
}
