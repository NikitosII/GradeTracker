using FluentValidation;

namespace EduTrack.Application.Admin.Commands.SendAnnouncement;

public sealed class SendAnnouncementCommandValidator : AbstractValidator<SendAnnouncementCommand>
{
    public const int MaxLength = 2048;

    public SendAnnouncementCommandValidator()
    {
        RuleFor(x => x.CallerTelegramUserId).GreaterThan(0);
        RuleFor(x => x.Text)
            .NotEmpty()
            .MaximumLength(MaxLength);
    }
}
