using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Users.Queries.GetUserSettings;

public sealed record GetUserSettingsQuery(long TelegramUserId) : IQuery<UserSettingsDto>;
