using EduTrack.Application.Studies.Recurrence;
using EduTrack.Domain.Studies;
using FluentValidation;

namespace EduTrack.Application.Studies.Commands.CreateRecurringAssignment;

public sealed class CreateRecurringAssignmentCommandValidator : AbstractValidator<CreateRecurringAssignmentCommand>
{
    public CreateRecurringAssignmentCommandValidator()
    {
        RuleFor(x => x.TelegramUserId).GreaterThan(0);
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.Interval).GreaterThan(0);
        RuleFor(x => x.Count).InclusiveBetween(2, RecurrenceSchedule.MaxCount);
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(Assignment.MaxTitleLength);
        RuleFor(x => x.Description)
            .MaximumLength(Assignment.MaxDescriptionLength)
            .When(x => x.Description is not null);
    }
}
