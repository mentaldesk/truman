using System.Text;
using System.Xml;
using CodeHollow.FeedReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Truman.Data;
using Truman.Data.Entities;
using FeedSource = Truman.Data.Entities.Feed;
using ParsedFeed = CodeHollow.FeedReader.Feed;

namespace Truman.JobRunner;

public class RssFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<RssFetcher> logger,
    IDbContextFactory<TrumanDbContext> contextFactory)
{
    internal const int BodyPreviewBytes = 200;
    internal const string CacheBusterKey = "trumancb";
    private const int MaxAttempts = 2;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting RSS fetch job...");

        var client = httpClientFactory.CreateClient("RssClient");

        // Create a new context for this operation
        await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

        var feeds = await dbContext.Feeds
            .Where(f => f.IsEnabled)
            .ToListAsync(cancellationToken);

        if (feeds.Count == 0)
        {
            logger.LogWarning("No enabled feeds found - exiting cleanly");
            return;
        }

        var existingArticleCount = await dbContext.RssItems.CountAsync();
        var newArticleCount = 0;

        foreach (var feedSource in feeds)
        {
            try
            {
                var feed = await ReadFeedAsync(client, feedSource, cancellationToken);

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
                        TimeAnalysed = null // This will be set by the analysis job
                    };

                    dbContext.RssItems.Add(rssItem);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    logger.LogInformation("New article found from {Feed}: {Title} ({Link})", feedSource.Name, item.Title, link);
                    newArticleCount++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch or parse feed {Feed} ({Url})", feedSource.Name, feedSource.Url);
            }
        }

        logger.LogInformation(
            "RSS fetch job completed with {NewCount} new articles and {ExistingCount} existing articles",
            newArticleCount,
            existingArticleCount);
    }

    private async Task<ParsedFeed> ReadFeedAsync(
        HttpClient client,
        FeedSource feedSource,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var url = attempt == 1 ? feedSource.Url : WithCacheBuster(feedSource.Url);
            var body = await client.GetByteArrayAsync(url, cancellationToken);

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
