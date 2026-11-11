using System.Text;
using System.Xml;
using CodeHollow.FeedReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sentry;
using Truman.Data;
using Truman.Data.Entities;
using FeedSource = Truman.Data.Entities.Feed;
using ParsedFeed = CodeHollow.FeedReader.Feed;

namespace Truman.JobRunner;

public readonly record struct FetchOutcome(int EnabledFeeds, int FailedFeeds);

public class RssFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<RssFetcher> logger,
    IDbContextFactory<TrumanDbContext> contextFactory)
{
    internal const int BodyPreviewBytes = 200;
    internal const string CacheBusterKey = "trumancb";
    private const int MaxAttempts = 2;

    public async Task<FetchOutcome> RunAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting RSS fetch job...");

        var stage = SentrySdk.GetSpan()?.StartChild("job.fetch", "fetch");
        try
        {
            var outcome = await FetchFeedsAsync(stage, cancellationToken);
            stage?.SetData("fetch.feeds", outcome.EnabledFeeds);
            stage?.SetData("fetch.failed_feeds", outcome.FailedFeeds);
            stage?.Finish(SpanStatus.Ok);
            return outcome;
        }
        catch (Exception ex)
        {
            stage?.Finish(ex);
            throw;
        }
    }

    private async Task<FetchOutcome> FetchFeedsAsync(ISpan? stage, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("RssClient");

        // Create a new context for this operation
        await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

        var feeds = await dbContext.Feeds
            .Where(f => f.IsEnabled)
            .ToListAsync(cancellationToken);

        if (feeds.Count == 0)
        {
            logger.LogWarning("No enabled feeds found; nothing to fetch");
            return new FetchOutcome(0, 0);
        }

        var existingArticleCount = await dbContext.RssItems.CountAsync();
        var newArticleCount = 0;
        var failedFeedCount = 0;

        foreach (var feedSource in feeds)
        {
            var span = stage?.StartChild("feed.fetch", feedSource.Name);
            span?.SetData("feed.url", feedSource.Url);
            ParsedFeed? feed = null;
            var newBodyLengths = new List<int>();
            try
            {
                feed = await ReadFeedAsync(client, feedSource, span, cancellationToken);

                foreach (var item in feed.Items)
                {
                    var link = item.Link;

                    // Check if we already have this article
                    var exists = await dbContext.RssItems.AnyAsync(a => a.Link == link);
                    if (exists)
                    {
                        existingArticleCount++;
                        continue;
                    }

                    // Try to parse the publication date
                    DateTimeOffset? pubDate = null;
                    if (item.PublishingDate.HasValue)
                    {
                        pubDate = item.PublishingDate.Value;
                    }

                    // Some feeds put the article body in <description>, others in <content:encoded>.
                    var body = (item.Content ?? string.Empty).Length > (item.Description ?? string.Empty).Length
                        ? item.Content
                        : item.Description;

                    // Create new article
                    var rssItem = new RssItem
                    {
                        Link = link,
                        Title = item.Title ?? string.Empty,
                        Description = body ?? string.Empty,
                        PubDate = pubDate,
                        FeedId = feedSource.Id,
                        TimeAnalysed = null // This will be set by the analysis job
                    };

                    dbContext.RssItems.Add(rssItem);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    logger.LogInformation("New article found from {Feed}: {Title} ({Link})", feedSource.Name, item.Title, link);
                    newArticleCount++;
                    newBodyLengths.Add(rssItem.Description.Length);
                }

                SetFeedCounts(span, feed, newBodyLengths);
                span?.Finish(SpanStatus.Ok);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch or parse feed {Feed} ({Url})", feedSource.Name, feedSource.Url);
                failedFeedCount++;
                SetFeedCounts(span, feed, newBodyLengths);
                span?.SetData("feed.failure", ex is XmlException ? "not XML" : ex.Message);
                span?.Finish(ex, SpanStatus.InternalError);
            }
        }

        logger.LogInformation(
            "RSS fetch job completed with {NewCount} new articles and {ExistingCount} existing articles",
            newArticleCount,
            existingArticleCount);

        return new FetchOutcome(feeds.Count, failedFeedCount);
    }

    private static void SetFeedCounts(ISpan? span, ParsedFeed? feed, List<int> newBodyLengths)
    {
        if (span is null || feed is null)
        {
            return;
        }

        span.SetData("feed.items", feed.Items.Count);
        span.SetData("feed.new", newBodyLengths.Count);
        if (Median(newBodyLengths) is { } median)
        {
            span.SetData("feed.body_median", median);
        }
    }

    private async Task<ParsedFeed> ReadFeedAsync(
        HttpClient client,
        FeedSource feedSource,
        ISpan? span,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            span?.SetData("feed.attempts", attempt);
            var url = attempt == 1 ? feedSource.Url : WithCacheBuster(feedSource.Url);
            var body = await client.GetByteArrayAsync(url, cancellationToken);
            span?.SetData("feed.response_bytes", body.Length);

            try
            {
                return FeedReader.ReadFromByteArray(body);
            }
            catch (XmlException)
            {
                logger.LogWarning(
                    "Feed {Feed} returned {ByteCount} bytes that are not XML on attempt {Attempt} of {MaxAttempts}; body starts: {BodyStart}",
                    feedSource.Name, body.Length, attempt, MaxAttempts, DescribeBody(body));

                if (attempt == MaxAttempts)
                {
                    throw;
                }
            }
        }
    }

    internal static int? Median(List<int> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
    }

    internal static string WithCacheBuster(string url)
    {
        var builder = new UriBuilder(url);
        var buster = $"{CacheBusterKey}={Guid.NewGuid():N}";
        var query = builder.Query.TrimStart('?');
        builder.Query = query.Length == 0 ? buster : $"{query}&{buster}";
        return builder.Uri.AbsoluteUri;
    }

    internal static string DescribeBody(byte[] body)
    {
        var description = new StringBuilder();

        foreach (var b in body.Take(BodyPreviewBytes))
        {
            if (b is >= 0x20 and < 0x7f)
            {
                description.Append((char)b);
            }
            else
            {
                description.Append($"\\x{b:x2}");
            }
        }

        if (body.Length > BodyPreviewBytes)
        {
            description.Append("...");
        }

        return description.ToString();
    }
}
