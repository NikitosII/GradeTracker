using EduTrack.Domain.Studies;
using FluentValidation;

namespace EduTrack.Application.Studies.Commands.UpdateOwnGrade;

public sealed class UpdateOwnGradeCommandValidator : AbstractValidator<UpdateOwnGradeCommand>
{
    public UpdateOwnGradeCommandValidator()
    {
        RuleFor(x => x.TelegramUserId).GreaterThan(0);
        RuleFor(x => x.GradeId).NotEmpty();
        RuleFor(x => x.Value).InclusiveBetween(Grade.MinValue, Grade.MaxValue);
        RuleFor(x => x.Weight).GreaterThan(0m);
        RuleFor(x => x.Comment)
            .MaximumLength(Grade.MaxCommentLength)
            .When(x => x.Comment is not null);
    }
}
