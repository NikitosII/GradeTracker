using FluentValidation;

namespace EduTrack.Application.Admin.Commands.CreateInviteCode;

public sealed class CreateInviteCodeCommandValidator : AbstractValidator<CreateInviteCodeCommand>
{
    public CreateInviteCodeCommandValidator()
    {
        RuleFor(x => x.CallerTelegramUserId).GreaterThan(0);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.ExpiresInDays)
            .InclusiveBetween(1, 365)
            .When(x => x.ExpiresInDays is not null);
    }
}
