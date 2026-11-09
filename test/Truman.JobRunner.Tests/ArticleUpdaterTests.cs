using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Testing;
using Truman.JobRunner.Tests.TestSupport;

namespace Truman.JobRunner.Tests;

[Collection(JobRunnerDatabaseCollection.Name)]
public class ArticleUpdaterTests(JobRunnerDatabaseFixture fixture) : ArticleAnalyserTestBase(fixture)
{
    private const string FirstFeed = "https://first.invalid/feed/";
    private const string SecondFeed = "https://second.invalid/feed/";
    private const string EmptyFeed = """<?xml version="1.0"?><rss version="2.0"><channel><title>Example</title></channel></rss>""";
    private const string NotAFeed = "error code: 1020";

    private const string GoodAnalysis = """{"tags":["economy"],"claim":"Rates will rise.","support":["a figure"]}""";
    private const string PresenterReply = """{"title":"T","tldr":"L","content":"C"}""";

    private const string NoFeeds = "No feeds are enabled";
    private const string AllFeedsFailed = "Every enabled feed failed to load";
    private const string NoPresenters = "No presenters are configured";
    private const string StaleStories = "No new story saved in the last 24 hours";

    private ScriptedHttpHandler Http { get; } = new();

    private ArticleUpdater Updater() =>
        new(new RssFetcher(Http, new FakeLogger<RssFetcher>(), Db.ContextFactory), Analyser(), Db.ContextFactory);

    private async Task SeedHealthyStateAsync()
    {
        await Db.SeedFeedAsync(FirstFeed);
        await Db.SeedPresenterAsync();
        await Db.SeedArticleAsync(DateTimeOffset.UtcNow.AddHours(-1));
    }

    private ScriptedHttpHandler FeedFails() => Http.Then(NotAFeed).Then(NotAFeed);

    private async Task SeedBatchAsync(int succeeding, int failing)
    {
        for (var i = 0; i < succeeding + failing; i++)
        {
            await Db.SeedPendingAsync($"https://example.invalid/{i}");
        }

        for (var i = 0; i < succeeding; i++)
        {
            Chat.Then(GoodAnalysis).Then(PresenterReply);
        }

        Chat.Always("not json");
    }

    private async Task<string> ScheduledRunFailureAsync()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Updater().RunScheduledAsync());
        return ex.Message;
    }

    [Fact]
    public async Task A_healthy_run_passes()
    {
        await SeedHealthyStateAsync();
        Http.Then(EmptyFeed);

        await Updater().RunScheduledAsync();
    }

    [Fact]
    public async Task Every_enabled_feed_failing_fails_the_run()
    {
        await SeedHealthyStateAsync();
        await Db.SeedFeedAsync(SecondFeed);
        FeedFails();
        FeedFails();

        var message = await ScheduledRunFailureAsync();

        Assert.Contains(AllFeedsFailed, message);
        Assert.DoesNotContain(NoFeeds, message);
        Assert.DoesNotContain(NoPresenters, message);
        Assert.DoesNotContain(StaleStories, message);
    }

    [Fact]
    public async Task One_feed_failing_among_healthy_ones_passes()
    {
        await SeedHealthyStateAsync();
        await Db.SeedFeedAsync(SecondFeed);
        FeedFails().Then(EmptyFeed);

        await Updater().RunScheduledAsync();
    }

    [Fact]
    public async Task No_enabled_feeds_fails_the_run()
    {
        await Db.SeedPresenterAsync();
        await Db.SeedArticleAsync(DateTimeOffset.UtcNow.AddHours(-1));
        await Db.SeedFeedAsync(FirstFeed, isEnabled: false);

        var message = await ScheduledRunFailureAsync();

        Assert.Contains(NoFeeds, message);
        Assert.DoesNotContain(AllFeedsFailed, message);
    }

    [Fact]
    public async Task No_presenters_with_an_empty_queue_fails_the_run()
    {
        await Db.SeedFeedAsync(FirstFeed);
        await Db.SeedArticleAsync(DateTimeOffset.UtcNow.AddHours(-1));
        Http.Then(EmptyFeed);

        var message = await ScheduledRunFailureAsync();

        Assert.Contains(NoPresenters, message);
        Assert.DoesNotContain("failed analysis", message);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 2)]
    [InlineData(6, 5)]
    public async Task A_batch_within_the_failure_limits_passes(int succeeding, int failing)
    {
        await SeedHealthyStateAsync();
        Http.Then(EmptyFeed);
        await SeedBatchAsync(succeeding, failing);

        await Updater().RunScheduledAsync();
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    [InlineData(0, 5)]
    public async Task At_least_five_attempted_with_half_or_more_failed_fails_the_run(int succeeding, int failing)
    {
        await SeedHealthyStateAsync();
        Http.Then(EmptyFeed);
        await SeedBatchAsync(succeeding, failing);

        var message = await ScheduledRunFailureAsync();

        Assert.Contains($"{failing} of {succeeding + failing} stories failed analysis", message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task A_batch_where_everything_failed_fails_below_five_attempted(int failing)
    {
        await SeedHealthyStateAsync();
        Http.Then(EmptyFeed);
        await SeedBatchAsync(0, failing);

        var message = await ScheduledRunFailureAsync();

        Assert.Contains($"{failing} of {failing} stories failed analysis", message);
    }

    [Fact]
    public async Task A_newest_story_just_inside_24_hours_passes()
    {
        await Db.SeedFeedAsync(FirstFeed);
        await Db.SeedPresenterAsync();
        await Db.SeedArticleAsync(DateTimeOffset.UtcNow.AddHours(-24).AddMinutes(1));
        Http.Then(EmptyFeed);

        await Updater().RunScheduledAsync();
    }

    [Fact]
    public async Task A_newest_story_just_outside_24_hours_fails_the_run_and_gives_its_time()
    {
        await Db.SeedFeedAsync(FirstFeed);
        await Db.SeedPresenterAsync();
        var newest = DateTimeOffset.UtcNow.AddHours(-24).AddMinutes(-1);
        await Db.SeedArticleAsync(newest.AddDays(-3));
        await Db.SeedArticleAsync(newest);
        Http.Then(EmptyFeed);

        var message = await ScheduledRunFailureAsync();

        Assert.Contains($"{StaleStories} (newest: {newest.UtcDateTime:d MMM yyyy HH:mm} UTC)", message);
    }

    [Fact]
    public async Task No_saved_story_at_all_fails_the_run()
    {
        await Db.SeedFeedAsync(FirstFeed);
        await Db.SeedPresenterAsync();
        Http.Then(EmptyFeed);

        Assert.Contains(StaleStories, await ScheduledRunFailureAsync());
    }

    [Fact]
    public async Task A_story_saved_by_this_run_counts_as_the_newest()
    {
        await Db.SeedFeedAsync(FirstFeed);
        await Db.SeedPresenterAsync();
        await Db.SeedArticleAsync(DateTimeOffset.UtcNow.AddDays(-2));
        Http.Then(EmptyFeed);
        await SeedBatchAsync(1, 0);

        await Updater().RunScheduledAsync();
    }

    [Fact]
    public async Task Several_conditions_at_once_are_all_named()
    {
        await Db.SeedFeedAsync(FirstFeed);
        await Db.SeedArticleAsync(DateTimeOffset.UtcNow.AddDays(-2));
        FeedFails();

        var message = await ScheduledRunFailureAsync();

        Assert.Contains(AllFeedsFailed, message);
        Assert.Contains(NoPresenters, message);
        Assert.Contains(StaleStories, message);
    }

    [Fact]
    public async Task Waiting_stories_are_still_analysed_when_every_feed_fails()
    {
        await SeedHealthyStateAsync();
        FeedFails();
        await SeedBatchAsync(1, 0);

        var message = await ScheduledRunFailureAsync();

        Assert.Contains(AllFeedsFailed, message);
        await using var db = Db.NewContext();
        Assert.Equal(0, await db.RssItems.CountAsync(x => x.TimeAnalysed == null));
        Assert.Equal(2, await db.Articles.CountAsync());
    }

    [Fact]
    public async Task Shutting_down_during_the_fetch_is_not_reported_as_a_condition()
    {
        await SeedHealthyStateAsync();
        using var shutdown = new CancellationTokenSource();
        Http.ThenCancel(shutdown);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Updater().RunScheduledAsync(shutdown.Token));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task A_manual_run_applies_none_of_the_scheduled_checks(bool fetch, bool analyse)
    {
        await Db.SeedFeedAsync(FirstFeed);
        await Db.SeedArticleAsync(DateTimeOffset.UtcNow.AddDays(-2));
        if (fetch)
        {
            FeedFails();
        }

        await Updater().RunManualAsync(fetch, analyse, limit: null);
    }

    [Fact]
    public async Task A_manual_analyse_with_a_bad_batch_that_saved_something_passes()
    {
        await Db.SeedPresenterAsync();
        await SeedBatchAsync(1, 4);

        await Updater().RunManualAsync(fetch: false, analyse: true, limit: null);
    }

    [Fact]
    public async Task A_manual_analyse_that_saved_nothing_still_fails()
    {
        await Db.SeedPresenterAsync();
        await SeedBatchAsync(0, 2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Updater().RunManualAsync(fetch: false, analyse: true, limit: null));

        Assert.Contains("all 2 articles", ex.Message);
    }
}
