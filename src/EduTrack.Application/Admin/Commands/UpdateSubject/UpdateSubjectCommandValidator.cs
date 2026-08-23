using EduTrack.Domain.Studies;
using FluentValidation;

namespace EduTrack.Application.Admin.Commands.UpdateSubject;

public sealed class UpdateSubjectCommandValidator : AbstractValidator<UpdateSubjectCommand>
{
    public UpdateSubjectCommandValidator()
    {
        RuleFor(x => x.CallerTelegramUserId).GreaterThan(0);
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Subject.MaxNameLength);
        RuleFor(x => x.Description)
            .MaximumLength(Subject.MaxDescriptionLength)
            .When(x => x.Description is not null);
    }
}
