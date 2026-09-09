using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies.History;

namespace EduTrack.Application.Studies.Queries.GetOwnHistory;

public sealed record GetOwnHistoryQuery(long TelegramUserId, int Page = 1, int PageSize = 8) : IQuery<StudentHistoryPageDto>;
