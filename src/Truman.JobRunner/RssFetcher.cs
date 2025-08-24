using CodeHollow.FeedReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.JobRunner;

public class RssFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<RssFetcher> logger,
    IDbContextFactory<TrumanDbContext> contextFactory)
{
    public async Task RunAsync()
    {
        logger.LogInformation("Starting RSS fetch job...");

        var feeds = new[]
        {
            "https://www.rnz.co.nz/rss/business.xml",
            "https://www.rnz.co.nz/rss/media-technology.xml",
            "https://www.rnz.co.nz/rss/world.xml",
            // "https://another.com/feed",
        };

        var client = httpClientFactory.CreateClient();

        // Create a new context for this operation
        await using var dbContext = await contextFactory.CreateDbContextAsync();
        var existingArticleCount = await dbContext.RssItems.CountAsync();
        var newArticleCount = 0;
        
        foreach (var feedUrl in feeds)
        {
            try
            {
                var response = await client.GetStringAsync(feedUrl);
                var feed = FeedReader.ReadFromString(response);

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

                    // Create new article
                    var rssItem = new RssItem
                    {
                        Link = link,
                        PubDate = pubDate,
                        TimeAnalysed = null // This will be set by the analysis job
                    };

                    dbContext.RssItems.Add(rssItem);
                    await dbContext.SaveChangesAsync();

                    logger.LogInformation("New article found: {Title} ({Link})", item.Title, link);
                    newArticleCount++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch or parse feed: {Url}", feedUrl);
            }
        }

        logger.LogInformation(
            "RSS fetch job completed with {NewCount} new articles and {ExistingCount} existing articles", 
            newArticleCount,
            existingArticleCount);
    }
}