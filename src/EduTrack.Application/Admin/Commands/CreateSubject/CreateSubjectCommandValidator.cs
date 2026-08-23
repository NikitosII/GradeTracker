using EduTrack.Domain.Studies;
using FluentValidation;

namespace EduTrack.Application.Admin.Commands.CreateSubject;

public sealed class CreateSubjectCommandValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectCommandValidator()
    {
        RuleFor(x => x.CallerTelegramUserId).GreaterThan(0);
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Subject.MaxNameLength);
        RuleFor(x => x.Description)
            .MaximumLength(Subject.MaxDescriptionLength)
            .When(x => x.Description is not null);
    }
}
