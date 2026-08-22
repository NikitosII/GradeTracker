using EduTrack.Domain.Studies;
using FluentValidation;

namespace EduTrack.Application.Studies.Commands.CreateOwnAssignment;

public sealed class CreateOwnAssignmentCommandValidator : AbstractValidator<CreateOwnAssignmentCommand>
{
    public CreateOwnAssignmentCommandValidator()
    {
        RuleFor(x => x.TelegramUserId).GreaterThan(0);
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(Assignment.MaxTitleLength);
        RuleFor(x => x.Description)
            .MaximumLength(Assignment.MaxDescriptionLength)
            .When(x => x.Description is not null);
    }
}
