using FluentValidation;

namespace EduTrack.Application.Admin.Commands.ChangeUserRole;

public sealed class ChangeUserRoleCommandValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleCommandValidator()
    {
        RuleFor(x => x.CallerTelegramUserId).GreaterThan(0);
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.NewRole).IsInEnum();
    }
}
