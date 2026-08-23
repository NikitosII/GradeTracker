using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Admin.Queries.GetUsers;

/// <summary>Lists all users for an administrator, newest first.</summary>
public sealed record GetUsersQuery(
    long CallerTelegramUserId,
    int Page = 1,
    int PageSize = 8) : IQuery<AdminUsersPageDto>;
