using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies.Archive;

namespace EduTrack.Application.Studies.Queries.GetOwnGradesForArchive;

public sealed record GetOwnGradesForArchiveQuery(long TelegramUserId, bool Archived, int Page = 1, int PageSize = 8)
    : IQuery<ArchivePageDto>;
