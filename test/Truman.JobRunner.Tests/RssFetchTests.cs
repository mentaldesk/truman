using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Testing;
using Truman.Data.Entities;
using Truman.JobRunner.Tests.TestSupport;

namespace Truman.JobRunner.Tests;

[Collection(JobRunnerDatabaseCollection.Name)]
public class RssFetchTests(JobRunnerDatabaseFixture fixture) : IAsyncLifetime
{
    private const string FeedUrl = "https://example.invalid/feed/";

    private static string Rss(params string[] links) =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <rss version="2.0"><channel><title>Example</title>
         {string.Join("\n", links.Select(l => $"<item><title>A headline</title><link>{l}</link><description>Body text.</description></item>"))}
         </channel></rss>
         """;

    private readonly FakeLogger<RssFetcher> _logger = new();

    private string LoggedText => string.Join("\n", _logger.Collector.GetSnapshot().Select(r => r.Message));

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();

        await using var db = fixture.NewContext();
        await db.Database.ExecuteSqlRawAsync("""TRUNCATE "Feeds" RESTART IDENTITY CASCADE;""");
        db.Feeds.Add(new Feed { Name = "Example", Url = FeedUrl, IsEnabled = true });
        await db.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private RssFetcher Fetcher(ScriptedHttpHandler http) => new(http, _logger, fixture.ContextFactory);

    [Fact]
    public async Task A_feed_that_parses_is_stored()
    {
        var http = new ScriptedHttpHandler().Then(Rss("https://example.invalid/one"));

        await Fetcher(http).RunAsync();

        await using var db = fixture.NewContext();
        Assert.Equal("https://example.invalid/one", Assert.Single(await db.RssItems.ToListAsync()).Link);
        Assert.Equal(FeedUrl, Assert.Single(http.Requests).AbsoluteUri);
    }

    [Fact]
    public async Task A_body_that_is_not_xml_is_retried_past_the_cache()
    {
        var http = new ScriptedHttpHandler()
            .Then("Warning: feed plugin failed in /srv/wp/feed.php on line 12")
            .Then(Rss("https://example.invalid/one"));

        await Fetcher(http).RunAsync();

        await using var db = fixture.NewContext();
        Assert.Equal("https://example.invalid/one", Assert.Single(await db.RssItems.ToListAsync()).Link);

        Assert.Equal(2, http.Requests.Count);
        Assert.Equal(FeedUrl, http.Requests[0].AbsoluteUri);
        Assert.Contains($"{RssFetcher.CacheBusterKey}=", http.Requests[1].Query);
    }

    [Fact]
    public async Task A_body_that_never_parses_is_logged_with_the_start_of_the_body()
    {
        var http = new ScriptedHttpHandler()
            .Then("error code: 1020")
            .Then("error code: 1020");

        await Fetcher(http).RunAsync();

        await using var db = fixture.NewContext();
        Assert.Empty(await db.RssItems.ToListAsync());

        Assert.Equal(2, http.Requests.Count);
        Assert.Contains("body starts: error code: 1020", LoggedText);
        Assert.Contains("Failed to fetch or parse feed Example", LoggedText);
    }

    [Fact]
    public async Task A_feed_that_fails_does_not_stop_the_next_feed()
    {
        await using (var db = fixture.NewContext())
        {
            db.Feeds.Add(new Feed { Name = "Second", Url = "https://second.invalid/feed/", IsEnabled = true });
            await db.SaveChangesAsync();
        }

        var http = new ScriptedHttpHandler()
            .Then("error code: 1020")
            .Then("error code: 1020")
            .Then(Rss("https://second.invalid/one"));

        await Fetcher(http).RunAsync();

        await using var verify = fixture.NewContext();
        Assert.Equal("https://second.invalid/one", Assert.Single(await verify.RssItems.ToListAsync()).Link);
    }

    [Fact]
    public void A_body_preview_escapes_bytes_that_are_not_printable_ascii()
    {
        Assert.Equal(@"\x1f\x8b\x08rest", RssFetcher.DescribeBody([0x1f, 0x8b, 0x08, (byte)'r', (byte)'e', (byte)'s', (byte)'t']));
    }

    [Fact]
    public void A_body_preview_is_bounded()
    {
        var preview = RssFetcher.DescribeBody(new byte[RssFetcher.BodyPreviewBytes + 50]);

        Assert.Equal(string.Concat(Enumerable.Repeat(@"\x00", RssFetcher.BodyPreviewBytes)) + "...", preview);
    }

    [Theory]
    [InlineData("https://example.invalid/feed/")]
    [InlineData("https://example.invalid/feed?format=rss")]
    public void A_cache_buster_is_added_to_the_query(string url)
    {
        var busted = new Uri(RssFetcher.WithCacheBuster(url));

        Assert.Contains($"{RssFetcher.CacheBusterKey}=", busted.Query);
        Assert.Equal(new Uri(url).GetLeftPart(UriPartial.Path), busted.GetLeftPart(UriPartial.Path));
        Assert.All(new Uri(url).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries),
            original => Assert.Contains(original, busted.Query));
    }
}
