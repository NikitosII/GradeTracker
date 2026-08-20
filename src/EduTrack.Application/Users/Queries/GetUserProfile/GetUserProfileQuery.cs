using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Users.Queries.GetUserProfile;

/// <summary>Returns the linked profile for a Telegram account, or NotFound.</summary>
public sealed record GetUserProfileQuery(long TelegramUserId) : IQuery<UserProfileDto>;
