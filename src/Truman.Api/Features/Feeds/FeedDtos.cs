namespace Truman.Api.Features.Feeds;

public record FeedDto(int Id, string Url, string Name, bool IsEnabled);

public record CreateFeedRequest(string Url, string Name, bool IsEnabled);

public record UpdateFeedRequest(string? Name, bool? IsEnabled);
