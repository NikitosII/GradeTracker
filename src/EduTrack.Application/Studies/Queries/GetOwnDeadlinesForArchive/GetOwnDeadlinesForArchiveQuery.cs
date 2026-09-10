using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies.Archive;

namespace EduTrack.Application.Studies.Queries.GetOwnDeadlinesForArchive;

public sealed record GetOwnDeadlinesForArchiveQuery(long TelegramUserId, bool Archived, int Page = 1, int PageSize = 8)
    : IQuery<ArchivePageDto>;
