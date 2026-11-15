using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Testing;
using Truman.Data.Entities;
using Truman.JobRunner.Tests.TestSupport;

namespace Truman.JobRunner.Tests;

[Collection(JobRunnerDatabaseCollection.Name)]
public class FeedPictureTests(JobRunnerDatabaseFixture fixture) : IAsyncLifetime
{
    private const string FeedUrl = "https://example.invalid/feed/";

    private static string Rss(string itemExtras) =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <rss version="2.0" xmlns:media="http://search.yahoo.com/mrss/"><channel><title>Example</title>
         <item><title>A headline</title><link>https://example.invalid/one</link><description>Body text.</description>{itemExtras}</item>
         </channel></rss>
         """;

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();

        await using var db = fixture.NewContext();
        db.Feeds.Add(new Feed { Name = "Example", Url = FeedUrl, IsEnabled = true });
        await db.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<string?> FetchedPicture(string itemExtras)
    {
        var http = new ScriptedHttpHandler().Then(Rss(itemExtras));
        await new RssFetcher(http, new FakeLogger<RssFetcher>(), fixture.ContextFactory).RunAsync();

        await using var db = fixture.NewContext();
        return Assert.Single(await db.RssItems.ToListAsync()).ImageUrl;
    }

    [Theory]
    [InlineData("""<media:content url="https://img.invalid/a.jpg" medium="image" width="1200" height="800"/>""")]
    [InlineData("""<media:content url="https://img.invalid/a.jpg" type="image/jpeg"/>""")]
    [InlineData("""<media:thumbnail url="https://img.invalid/a.jpg"/>""")]
    [InlineData("""<enclosure url="https://img.invalid/a.jpg" type="image/jpeg" length="12345"/>""")]
    [InlineData("""<media:group><media:content url="https://img.invalid/a.jpg" medium="image"/></media:group>""")]
    public async Task Keeps_the_picture_the_feed_supplies(string extras)
    {
        Assert.Equal("https://img.invalid/a.jpg", await FetchedPicture(extras));
    }

    [Theory]
    [InlineData("""
        <media:thumbnail url="https://img.invalid/small.jpg" width="150" height="100"/>
        <media:content url="https://img.invalid/large.jpg" medium="image" width="1200" height="800"/>
        <media:content url="https://img.invalid/medium.jpg" medium="image" width="600" height="400"/>
        """)]
    [InlineData("""
        <media:thumbnail url="https://img.invalid/small.jpg" width="150" height="100"/>
        <media:content url="https://img.invalid/large.jpg" medium="image" width="1200"/>
        """)]
    [InlineData("""
        <media:thumbnail url="https://img.invalid/small.jpg" width="150" height="100"/>
        <media:content url="https://img.invalid/large.jpg" medium="image" height="800"/>
        """)]
    public async Task Prefers_the_largest_of_several(string extras)
    {
        Assert.Equal("https://img.invalid/large.jpg", await FetchedPicture(extras));
    }

    [Theory]
    [InlineData("")]
    [InlineData("""<media:content url="https://img.invalid/clip.mp4" medium="video"/>""")]
    [InlineData("""<enclosure url="https://img.invalid/episode.mp3" type="audio/mpeg" length="12345"/>""")]
    [InlineData("""<media:thumbnail url="/images/a.jpg"/>""")]
    [InlineData("""<media:thumbnail url="ftp://img.invalid/a.jpg"/>""")]
    [InlineData("""<media:thumbnail url="data:image/png;base64,AAAA"/>""")]
    [InlineData("""<media:thumbnail/>""")]
    public async Task Saves_nothing_without_an_absolute_web_picture(string extras)
    {
        Assert.Null(await FetchedPicture(extras));
    }

    [Fact]
    public async Task Skips_an_unusable_address_for_a_usable_one()
    {
        var picture = await FetchedPicture("""
            <media:content url="/relative/huge.jpg" medium="image" width="4000" height="3000"/>
            <media:thumbnail url="https://img.invalid/a.jpg" width="150" height="100"/>
            """);

        Assert.Equal("https://img.invalid/a.jpg", picture);
    }
}
