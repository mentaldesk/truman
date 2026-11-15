using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Testing;
using Truman.Data;
using Truman.Data.Entities;
using Truman.JobRunner.Tests.TestSupport;

namespace Truman.JobRunner.Tests;

[Collection(JobRunnerDatabaseCollection.Name)]
public class SourceWordCountTests(JobRunnerDatabaseFixture fixture) : ArticleAnalyserTestBase(fixture)
{
    private const string FeedUrl = "https://example.invalid/feed/";
    private const string Link = "https://example.invalid/story";
    private const string GoodAnalysis = """{"tags":["economy"],"claim":"Rates will rise.","support":["a figure"]}""";
    private const string PresenterReply = """{"title":"T","tldr":"L","content":"A much longer rewrite than its source."}""";

    [Theory]
    [InlineData("Bees learn to count.", 4)]
    [InlineData("<p class=\"lede intro\">Bees <a href=\"https://example.invalid/bees\" title=\"more on bees\">learn</a> to count.</p>", 4)]
    [InlineData("Bees&nbsp;learn &amp; count &mdash; fast.", 4)]
    [InlineData("  Bees \n\n\t learn   to\r\ncount.  ", 4)]
    [InlineData("<p>Bees</p><p>learn</p><!-- an editor's note --><style>p { color: red }</style>", 2)]
    [InlineData("It's a 3.5% rise in well-known U.S. rates.", 8)]
    public void Counts_the_words_of_the_text_not_the_markup(string html, int expected)
    {
        Assert.Equal(expected, SourceText.CountWords(html));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_source_text_has_no_count(string? html)
    {
        Assert.Null(SourceText.CountWords(html));
    }

    [Fact]
    public async Task An_analysed_story_records_the_words_of_the_longer_feed_body()
    {
        await Db.SeedFeedAsync(FeedUrl);
        await Db.SeedPresenterAsync();
        var http = new ScriptedHttpHandler().Then($"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0" xmlns:content="http://purl.org/rss/1.0/modules/content/"><channel><title>Example</title>
            <item><title>A headline</title><link>{Link}</link>
            <description>A two-line teaser.</description>
            <content:encoded><![CDATA[<p>The <em>whole</em> article, in five paragraphs of seven words.</p>]]></content:encoded>
            </item></channel></rss>
            """);
        await new RssFetcher(http, new FakeLogger<RssFetcher>(), Db.ContextFactory).RunAsync();
        Chat.Then(GoodAnalysis).Then(PresenterReply);

        await Analyser().RunAsync();

        await using var db = Db.NewContext();
        var article = await db.Articles.SingleAsync(a => a.Link == Link);
        Assert.Equal(9, article.SourceWordCount);
    }

    [Fact]
    public async Task A_story_with_no_stored_source_text_has_no_count()
    {
        await Db.SeedPresenterAsync();
        await Db.SeedPendingAsync(Link, description: "");
        Chat.Then(GoodAnalysis).Then(PresenterReply);

        await Analyser().RunAsync();

        await using var db = Db.NewContext();
        var article = await db.Articles.SingleAsync(a => a.Link == Link);
        Assert.Null(article.SourceWordCount);
    }
}
