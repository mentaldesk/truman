using Microsoft.EntityFrameworkCore;
using Truman.JobRunner.Tests.TestSupport;

namespace Truman.JobRunner.Tests;

[Collection(JobRunnerDatabaseCollection.Name)]
public class ArticleAnalysisDeserialisationTests(JobRunnerDatabaseFixture fixture)
    : ArticleAnalyserTestBase(fixture)
{
    private const string Link = "https://example.invalid/story";
    private const string PresenterReply = """{"title":"T","tldr":"L","content":"C"}""";

    private async Task<AnalysisOutcome> RunWith(string analysisJson)
    {
        await Db.SeedPendingAsync(Link);
        await Db.SeedPresenterAsync();

        Chat.Then(analysisJson).Always(PresenterReply);

        return await Analyser().RunAsync();
    }

    [Fact]
    public async Task A_well_formed_payload_maps_every_field()
    {
        var outcome = await RunWith("""
            {
              "tags": ["economy", "rates"],
              "claim": "Rates will rise.",
              "support": ["a central bank figure"],
              "padding": ["a celebrity quote"],
              "headline_gap": "withholds",
              "source_depth": "summary"
            }
            """);

        Assert.Equal(new AnalysisOutcome(1, 1, 0), outcome);

        // tags land on the Article; the other five are only observable through the presenter spine.
        await using var db = Db.NewContext();
        var article = await db.Articles.SingleAsync();
        Assert.Equal(["economy", "rates"], article.Tags);

        var spine = Chat.Calls[1].UserText;
        Assert.Contains("- Claim: Rates will rise.", spine);
        Assert.Contains("- Support: a central bank figure", spine);
        Assert.Contains("- Padding, do not spend words on: a celebrity quote", spine);
        Assert.Contains("- Headline relation to claim: withholds", spine);
        Assert.Contains("- Source depth: summary", spine);
    }

    [Theory]
    [InlineData("""{"claim":"Rates will rise."}""")]
    [InlineData("""{"tags":null,"claim":"Rates will rise."}""")]
    public async Task A_payload_without_tags_is_recorded_as_a_failure(string analysisJson)
    {
        var outcome = await RunWith(analysisJson);

        Assert.Equal(new AnalysisOutcome(1, 0, 1), outcome);
        Assert.Contains("Deserialization returned null", LoggedText);

        // No presenter call: the run stopped at article-analysis.
        Assert.Single(Chat.Calls);
        await AssertWrittenOffWithoutAnArticle();
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"tags": ["economy",}""")]
    [InlineData("""{"tags": "a string, not an array"}""")]
    public async Task A_malformed_payload_is_recorded_as_a_failure_rather_than_thrown(string analysisJson)
    {
        var outcome = await RunWith(analysisJson);

        Assert.Equal(new AnalysisOutcome(1, 0, 1), outcome);
        Assert.Contains("JSON deserialization failed", LoggedText);

        Assert.Single(Chat.Calls);
        await AssertWrittenOffWithoutAnArticle();
    }

    [Fact]
    public async Task An_empty_response_is_recorded_as_a_failure()
    {
        var outcome = await RunWith("   ");

        Assert.Equal(new AnalysisOutcome(1, 0, 1), outcome);
        Assert.Contains("No content returned", LoggedText);

        Assert.Single(Chat.Calls);
        await AssertWrittenOffWithoutAnArticle();
    }

    private async Task AssertWrittenOffWithoutAnArticle()
    {
        await using var db = Db.NewContext();
        Assert.Empty(await db.Articles.ToListAsync());

        // RecordFailure writes the item off permanently rather than leaving it to be retried.
        Assert.NotNull((await db.RssItems.SingleAsync()).TimeAnalysed);
    }
}
