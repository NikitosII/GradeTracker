using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Admin.Queries.GetInviteCodes;

/// <summary>Lists the most recent invite codes for an administrator.</summary>
public sealed record GetInviteCodesQuery(long CallerTelegramUserId, int Take = 15) : IQuery<IReadOnlyList<InviteCodeDto>>;
