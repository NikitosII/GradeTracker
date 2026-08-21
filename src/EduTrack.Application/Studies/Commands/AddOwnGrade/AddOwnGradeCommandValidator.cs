using EduTrack.Domain.Studies;
using FluentValidation;

namespace EduTrack.Application.Studies.Commands.AddOwnGrade;

public sealed class AddOwnGradeCommandValidator : AbstractValidator<AddOwnGradeCommand>
{
    public AddOwnGradeCommandValidator()
    {
        RuleFor(x => x.TelegramUserId).GreaterThan(0);
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.Value).InclusiveBetween(Grade.MinValue, Grade.MaxValue);
        RuleFor(x => x.Weight).GreaterThan(0m);
        RuleFor(x => x.Comment)
            .MaximumLength(Grade.MaxCommentLength)
            .When(x => x.Comment is not null);
    }
}
