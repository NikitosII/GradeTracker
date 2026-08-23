using System.Security.Cryptography;
using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Common;
using EduTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Admin.Commands.CreateInviteCode;

internal sealed class CreateInviteCodeCommandHandler : ICommandHandler<CreateInviteCodeCommand, InviteCodeDto>
{
    // Unambiguous alphabet (no 0/O, 1/I) for codes people may type by hand.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateInviteCodeCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<InviteCodeDto>> Handle(CreateInviteCodeCommand request, CancellationToken cancellationToken)
    {
        var gate = await AdminGuard.RequireAdminAsync(_db, request.CallerTelegramUserId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<InviteCodeDto>(gate.Error);
        }

        var now = _clock.UtcNow;
        var expiresAt = request.ExpiresInDays is { } days ? now.AddDays(days) : (DateTime?)null;

        var code = await GenerateUniqueCodeAsync(cancellationToken);
        var invite = InviteCode.Create(code, request.Role, now, expiresAt);
        _db.InviteCodes.Add(invite);

        _db.AuditLogs.Add(AuditLog.Create(
            gate.Value.Id,
            AuditActions.InviteCodeCreated,
            AuditEntities.InviteCode,
            invite.Id.ToString(),
            oldValue: null,
            newValue: $"{invite.Code} ({request.Role})",
            now));

        await _db.SaveChangesAsync(cancellationToken);

        var dto = new InviteCodeDto(invite.Id, invite.Code, invite.Role.ToString(), invite.ExpiresAt, invite.IsUsed, invite.CreatedAt);
        return Result.Success(dto);
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = GenerateCode();
            var exists = await _db.InviteCodes.AsNoTracking().AnyAsync(c => c.Code == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        // Extremely unlikely; fall back to a GUID-derived code.
        return "INV-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    }

    private static string GenerateCode()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
