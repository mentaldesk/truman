using Microsoft.EntityFrameworkCore;
using Truman.JobRunner.Tests.TestSupport;

namespace Truman.JobRunner.Tests;

[Collection(JobRunnerDatabaseCollection.Name)]
public class AnalysisRunOutcomeTests(JobRunnerDatabaseFixture fixture) : ArticleAnalyserTestBase(fixture)
{
    private const string GoodAnalysis =
        """{"tags":["economy"],"claim":"Rates will rise.","support":["a figure"]}""";

    private const string PresenterReply = """{"title":"T","tldr":"L","content":"C"}""";

    [Fact]
    public async Task No_pending_items_yields_an_empty_outcome_that_does_not_throw()
    {
        await Db.SeedPresenterAsync();

        var outcome = await Analyser().RunAsync();

        Assert.Equal(new AnalysisOutcome(0, 0, 0), outcome);
        outcome.ThrowIfNothingSucceeded();
        Assert.Empty(Chat.Calls);
    }

    [Fact]
    public async Task No_presenters_configured_yields_an_empty_outcome_that_does_not_throw()
    {
        await Db.SeedPendingAsync("https://example.invalid/one");

        var outcome = await Analyser().RunAsync();

        Assert.Equal(new AnalysisOutcome(0, 0, 0), outcome);
        outcome.ThrowIfNothingSucceeded();
        Assert.Empty(Chat.Calls);
    }

    [Fact]
    public async Task A_mixed_run_counts_each_article_once()
    {
        await Db.SeedPendingAsync("https://example.invalid/one", "First");
        await Db.SeedPendingAsync("https://example.invalid/two", "Second");
        await Db.SeedPendingAsync("https://example.invalid/three", "Third");
        await Db.SeedPresenterAsync();

        Chat.Then(GoodAnalysis).Then(PresenterReply)   // one: saved
            .Then("not json")                          // two: failed at article-analysis
            .Then(GoodAnalysis).Then(PresenterReply);  // three: saved

        var outcome = await Analyser().RunAsync();

        Assert.Equal(new AnalysisOutcome(3, 2, 1), outcome);
        outcome.ThrowIfNothingSucceeded();

        await using var db = Db.NewContext();
        Assert.Equal(2, await db.Articles.CountAsync());
        Assert.Equal(3, await db.RssItems.CountAsync(x => x.TimeAnalysed != null));
    }

    [Fact]
    public async Task A_run_where_every_article_failed_reports_no_successes_and_trips_the_guard()
    {
        await Db.SeedPendingAsync("https://example.invalid/one", "First");
        await Db.SeedPendingAsync("https://example.invalid/two", "Second");
        await Db.SeedPresenterAsync();

        Chat.Always("not json");

        var outcome = await Analyser().RunAsync();

        Assert.Equal(new AnalysisOutcome(2, 0, 2), outcome);
        Assert.Throws<InvalidOperationException>(outcome.ThrowIfNothingSucceeded);

        await using var db = Db.NewContext();
        Assert.Empty(await db.Articles.ToListAsync());
    }

    [Fact]
    public async Task A_presenter_that_returns_nothing_usable_fails_validation_rather_than_saving()
    {
        await Db.SeedPendingAsync("https://example.invalid/one");
        await Db.SeedPresenterAsync();

        Chat.Then(GoodAnalysis).Always("""{"title":"","tldr":"","content":""}""");

        var outcome = await Analyser().RunAsync();

        Assert.Equal(new AnalysisOutcome(1, 0, 1), outcome);
        Assert.Contains("Validation failed", LoggedText);
    }

    [Fact]
    public async Task The_limit_caps_how_many_pending_items_are_attempted()
    {
        await Db.SeedPendingAsync("https://example.invalid/one", "First");
        await Db.SeedPendingAsync("https://example.invalid/two", "Second");
        await Db.SeedPendingAsync("https://example.invalid/three", "Third");
        await Db.SeedPresenterAsync();

        Chat.Then(GoodAnalysis).Always(PresenterReply);

        var outcome = await Analyser().RunAsync(limit: 1);

        Assert.Equal(new AnalysisOutcome(1, 1, 0), outcome);

        await using var db = Db.NewContext();
        Assert.Equal(2, await db.RssItems.CountAsync(x => x.TimeAnalysed == null));
    }
}
