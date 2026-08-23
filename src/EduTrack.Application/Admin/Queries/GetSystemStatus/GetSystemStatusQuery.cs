using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Admin.Queries.GetSystemStatus;

/// <summary>Aggregate counts describing the current state of the system.</summary>
public sealed record GetSystemStatusQuery(long CallerTelegramUserId) : IQuery<SystemStatusDto>;
