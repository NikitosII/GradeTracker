using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Localization;
using EduTrack.Application.Notifications;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Common;
using EduTrack.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Admin.Commands.ChangeUserRole;

internal sealed class ChangeUserRoleCommandHandler : ICommandHandler<ChangeUserRoleCommand, AdminUserDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ITranslator _translator;

    public ChangeUserRoleCommandHandler(IApplicationDbContext db, IDateTimeProvider clock, ITranslator translator)
    {
        _db = db;
        _clock = clock;
        _translator = translator;
    }

    public async Task<Result<AdminUserDto>> Handle(ChangeUserRoleCommand request, CancellationToken cancellationToken)
    {
        var gate = await AdminGuard.RequireAdminAsync(_db, request.CallerTelegramUserId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<AdminUserDto>(gate.Error);
        }

        if (gate.Value.Id == request.TargetUserId)
        {
            return Result.Failure<AdminUserDto>(AdminErrors.CannotDemoteSelf);
        }

        var target = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (target is null)
        {
            return Result.Failure<AdminUserDto>(AdminErrors.UserNotFound);
        }

        var previousRole = target.Role;
        var now = _clock.UtcNow;

        if (previousRole != request.NewRole)
        {
            target.ChangeRole(request.NewRole, now);

            _db.AuditLogs.Add(AuditLog.Create(
                gate.Value.Id,
                AuditActions.UserRoleChanged,
                AuditEntities.User,
                target.Id.ToString(),
                oldValue: previousRole.ToString(),
                newValue: request.NewRole.ToString(),
                now));

            var roleLabel = _translator.Find(target.Language, TextKeys.Role(request.NewRole.ToString())) ?? request.NewRole.ToString();
            var title = _translator.Find(target.Language, TextKeys.NotifyRoleChangedTitle) ?? "Your account was updated";
            var body = _translator.Find(target.Language, TextKeys.NotifyRoleChangedBody, roleLabel)
                ?? $"An administrator changed your role to {request.NewRole}.";

            OutboxWriter.Enqueue(_db, new UserNotificationRequested(
                Guid.NewGuid(),
                target.Id,
                NotificationType.AdminDataChange,
                title,
                body,
                Important: true), now);

            await _db.SaveChangesAsync(cancellationToken);
        }

        var fullName = string.Join(' ', new[] { target.FirstName, target.LastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
        var dto = new AdminUserDto(
            target.Id,
            target.TelegramUserId,
            target.Username,
            string.IsNullOrWhiteSpace(fullName) ? null : fullName,
            target.Role.ToString(),
            target.CreatedAt);

        return Result.Success(dto);
    }
}
