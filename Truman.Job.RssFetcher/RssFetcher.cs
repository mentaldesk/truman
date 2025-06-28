using CodeHollow.FeedReader;
using Microsoft.Extensions.Logging;

namespace Truman.Job.RssFetcher;

public class RssFetcher : IRssFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RssFetcher> _logger;

    public RssFetcher(IHttpClientFactory httpClientFactory, ILogger<RssFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Starting RSS fetch job...");

        var feeds = new[]
        {
            "https://www.rnz.co.nz/rss/business.xml",
            "https://www.rnz.co.nz/rss/media-technology.xml",
            "https://www.rnz.co.nz/rss/world.xml",
            // "https://another.com/feed",
        };

        var client = _httpClientFactory.CreateClient();

        var existingArticleCount = 0; // This should be replaced with a call to your database to get the count of existing articles
        var newArticleCount = 0;
        foreach (var feedUrl in feeds)
        {
            try
            {
                var response = await client.GetStringAsync(feedUrl);
                var feed = FeedReader.ReadFromString(response);

                foreach (var item in feed.Items)
                {
                    var title = item.Title;
                    var link = item.Link;

                    // Insert to DB here
                    _logger.LogInformation("New article found: {Title} ({Link})", title, link);
                    newArticleCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch or parse feed: {Url}", feedUrl);
            }
        }

        _logger.LogInformation("RSS fetch job completed - {0} new articles found", newArticleCount);
    }
}