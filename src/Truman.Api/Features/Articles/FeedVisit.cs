using Truman.Data.Entities;

namespace Truman.Api.Features.Articles;

public static class FeedVisit
{
    public static readonly TimeSpan Gap = TimeSpan.FromHours(1);

    public static void Record(UserProfile profile, DateTimeOffset now)
    {
        if (profile.FeedSeenAt is { } seen && now - seen > Gap)
            profile.NewSince = seen;
        profile.FeedSeenAt = now;
    }
}
