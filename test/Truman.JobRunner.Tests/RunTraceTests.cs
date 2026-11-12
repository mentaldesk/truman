using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Sentry;
using Truman.Data.Entities;
using Truman.JobRunner.Tests.TestSupport;

namespace Truman.JobRunner.Tests;

/// The trace a JobRunner run sends to Sentry: one run transaction, a span per feed, and each story's outcome.
[Collection(JobRunnerDatabaseCollection.Name)]
public sealed class RunTraceTests(JobRunnerDatabaseFixture fixture) : ArticleAnalyserTestBase(fixture), IDisposable
{
    private const string Title = "A distinctive headline";
    private const string Body = "A distinctive body of story text.";
    private const string GoodAnalysis = """{"tags":["economy"],"claim":"Rates will rise.","support":["a figure"]}""";
    private const string PresenterReply = """{"title":"T","tldr":"L","content":"C"}""";
    private const string NotAFeed = "error code: 1020";

    private readonly SentryRecorder _sentry = new();

    private ScriptedHttpHandler Http { get; } = new();

    public void Dispose() => _sentry.Dispose();

    private ArticleUpdater Updater(IChatClient? chatClient = null) =>
        new(new RssFetcher(Http, new FakeLogger<RssFetcher>(), Db.ContextFactory), Analyser(chatClient), Db.ContextFactory);

    private static string Rss(params (string Link, string Body)[] items) =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <rss version="2.0"><channel><title>Example</title>
         {string.Join("\n", items.Select(i => $"<item><title>{Title}</title><link>{i.Link}</link><description>{i.Body}</description></item>"))}
         </channel></rss>
         """;

    private async Task SeedFeedAsync(string name)
    {
        await using var db = Db.NewContext();
        db.Feeds.Add(new Feed { Name = name, Url = $"https://{name.ToLowerInvariant()}.invalid/feed/", IsEnabled = true });
        await db.SaveChangesAsync();
    }

    private async Task<SentryTransaction> RunTransactionAsync() =>
        Assert.Single(await _sentry.TransactionsAsync(), t => t.Name == "update-articles");

    private async Task<IReadOnlyList<SentryTransaction>> StoryTransactionsAsync() =>
        (await _sentry.TransactionsAsync()).Where(t => t.Name == "analyse-article").ToList();

    private static SentrySpan Stage(SentryTransaction run, string operation)
    {
        var stage = Assert.Single(run.Spans, s => s.Operation == operation);
        Assert.Equal(run.SpanId, stage.ParentSpanId);
        return stage;
    }

    private static SentrySpan FeedSpan(SentryTransaction run, string name) =>
        Assert.Single(run.Spans, s => s.Operation == "feed.fetch" && s.Description == name);

    [Fact]
    public async Task A_scheduled_run_is_one_trace_with_a_fetch_and_an_analysis_stage_and_checks_in()
    {
        await SeedFeedAsync("Example");
        await Db.SeedPresenterAsync();
        Http.Then(Rss(("https://example.invalid/one", Body)));
        Chat.Then(GoodAnalysis).Then(PresenterReply);

        await Updater().RunScheduledAsync();

        var run = await RunTransactionAsync();
        Assert.Equal("job.run", run.Operation);
        Assert.Equal(SpanStatus.Ok, run.Status);
        Assert.Equal("scheduled", run.Tags["job.trigger"]);
        Stage(run, "job.fetch");
        Stage(run, "job.analyse");

        var checkIns = await _sentry.CheckInsAsync();
        Assert.Equal(["in_progress", "ok"], checkIns.Select(c => c.Status));
        Assert.All(checkIns, c =>
        {
            Assert.Equal(ArticleUpdater.MonitorSlug, c.MonitorSlug);
            Assert.Equal(run.TraceId.ToString(), c.TraceId);
        });
    }

    [Theory]
    [InlineData(true, false, "job.fetch")]
    [InlineData(false, true, "job.analyse")]
    public async Task A_manual_run_is_its_own_trace_and_does_not_check_in(bool fetch, bool analyse, string stage)
    {
        await SeedFeedAsync("Example");
        await Db.SeedPresenterAsync();
        Http.Then(Rss());

        await Updater().RunManualAsync(fetch, analyse, limit: null);

        var run = await RunTransactionAsync();
        Assert.Equal("manual", run.Tags["job.trigger"]);
        Stage(run, stage);
        Assert.Empty(await _sentry.CheckInsAsync());
    }

    [Fact]
    public async Task Each_feed_gets_a_span_with_its_counts_and_outcome()
    {
        await SeedFeedAsync("Full");
        await SeedFeedAsync("Broken");
        await SeedFeedAsync("Teaser");
        await SeedFeedAsync("Slow");
        await Db.SeedPendingAsync("https://full.invalid/old");

        var fullBody = new string('x', 300);
        var fullFeed = Rss(("https://full.invalid/old", fullBody), ("https://full.invalid/a", fullBody), ("https://full.invalid/b", new string('x', 100)), ("https://full.invalid/c", new string('x', 500)));
        Http.Then(fullFeed)
            .Then(NotAFeed).Then(NotAFeed)
            .Then(Rss(("https://teaser.invalid/a", "Short."), ("https://teaser.invalid/b", "Shorter")))
            .ThenThrow(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing."));

        await Updater().RunManualAsync(fetch: true, analyse: false, limit: null);

        var run = await RunTransactionAsync();
        var fetchStage = Stage(run, "job.fetch");
        Assert.All(run.Spans.Where(s => s.Operation == "feed.fetch"), s => Assert.Equal(fetchStage.SpanId, s.ParentSpanId));

        var full = FeedSpan(run, "Full");
        Assert.Equal(SpanStatus.Ok, full.Status);
        Assert.Equal(4, full.Data["feed.items"]);
        Assert.Equal(3, full.Data["feed.new"]);
        Assert.Equal(1, full.Data["feed.attempts"]);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(fullFeed), full.Data["feed.response_bytes"]);
        Assert.Equal(300, full.Data["feed.body_median"]);
        Assert.Equal("https://full.invalid/feed/", full.Data["feed.url"]);

        var broken = FeedSpan(run, "Broken");
        Assert.Equal(SpanStatus.InternalError, broken.Status);
        Assert.Equal("not XML", broken.Data["feed.failure"]);
        Assert.Equal(2, broken.Data["feed.attempts"]);
        Assert.Equal(NotAFeed.Length, broken.Data["feed.response_bytes"]);

        var teaser = FeedSpan(run, "Teaser");
        Assert.Equal(SpanStatus.Ok, teaser.Status);
        Assert.Equal(2, teaser.Data["feed.new"]);
        Assert.Equal(6, teaser.Data["feed.body_median"]);

        var slow = FeedSpan(run, "Slow");
        Assert.Equal(SpanStatus.InternalError, slow.Status);
        Assert.Contains("HttpClient.Timeout", (string)slow.Data["feed.failure"]!);
    }

    [Fact]
    public async Task A_feed_that_fails_partway_keeps_the_counts_it_got_to()
    {
        await SeedFeedAsync("Partial");
        Http.Then($"""
                   <?xml version="1.0" encoding="UTF-8"?>
                   <rss version="2.0"><channel><title>Example</title>
                   <item><title>{Title}</title><link>https://partial.invalid/a</link><description>{Body}</description></item>
                   <item><title>{Title}</title><description>{Body}</description></item>
                   </channel></rss>
                   """);

        await Updater().RunManualAsync(fetch: true, analyse: false, limit: null);

        var partial = FeedSpan(await RunTransactionAsync(), "Partial");
        Assert.Equal(SpanStatus.InternalError, partial.Status);
        Assert.Equal(2, partial.Data["feed.items"]);
        Assert.Equal(1, partial.Data["feed.new"]);
        Assert.Equal(Body.Length, partial.Data["feed.body_median"]);
    }

    [Fact]
    public async Task A_retry_counts_as_an_attempt_on_the_feed_span()
    {
        await SeedFeedAsync("Flaky");
        Http.Then(NotAFeed).Then(Rss(("https://flaky.invalid/a", Body)));

        await Updater().RunManualAsync(fetch: true, analyse: false, limit: null);

        var flaky = FeedSpan(await RunTransactionAsync(), "Flaky");
        Assert.Equal(SpanStatus.Ok, flaky.Status);
        Assert.Equal(2, flaky.Data["feed.attempts"]);
        Assert.Equal(1, flaky.Data["feed.new"]);
    }

    [Fact]
    public async Task A_saved_story_continues_the_run_trace_ends_ok_and_has_a_save_span()
    {
        await Db.SeedPendingAsync("https://example.invalid/one", Title, Body);
        await Db.SeedPresenterAsync();
        Chat.Then(GoodAnalysis).Then(PresenterReply);

        await Updater().RunManualAsync(fetch: false, analyse: true, limit: null);

        var run = await RunTransactionAsync();
        var story = Assert.Single(await StoryTransactionsAsync());
        Assert.Equal(run.TraceId, story.TraceId);
        Assert.Equal(Stage(run, "job.analyse").SpanId, story.ParentSpanId);
        Assert.Equal(SpanStatus.Ok, story.Status);
        Assert.False(story.Tags.ContainsKey("analysis.stage"));

        var save = Assert.Single(story.Spans, s => s.Description == "save-article");
        Assert.Equal("db", save.Operation);
        Assert.Equal(SpanStatus.Ok, save.Status);
    }

    [Theory]
    [InlineData("article-analysis")]
    [InlineData("presenter-content")]
    [InlineData("validation")]
    [InlineData("save-results")]
    [InlineData("analysis-loop")]
    public async Task A_written_off_story_ends_failed_and_tagged_with_its_stage(string stage)
    {
        const string link = "https://example.invalid/one";
        await Db.SeedPendingAsync(link, Title, Body);
        await Db.SeedPresenterAsync();
        IChatClient? chatClient = null;

        switch (stage)
        {
            case "article-analysis":
                Chat.Always("not json");
                break;
            case "presenter-content":
                Chat.Then(GoodAnalysis).Always("not json");
                break;
            case "validation":
                Chat.Then(GoodAnalysis).Always("""{"title":"","tldr":"","content":""}""");
                break;
            case "save-results":
                await SeedArticleWithLinkAsync(link);
                Chat.Then(GoodAnalysis).Then(PresenterReply);
                break;
            case "analysis-loop":
                chatClient = Substitute.For<IChatClient>();
                chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
                    .Returns<Task<ChatResponse>>(_ => throw new HttpRequestException("model unreachable"));
                break;
        }

        await Updater(chatClient).RunManualAsync(fetch: false, analyse: true, limit: null)
            .ContinueWith(_ => { }); // Every story failing fails the run; the trace is what's under test.

        var run = await RunTransactionAsync();
        var story = Assert.Single(await StoryTransactionsAsync());
        Assert.Equal(run.TraceId, story.TraceId);
        Assert.Equal(SpanStatus.InternalError, story.Status);
        Assert.Equal(stage, story.Tags["analysis.stage"]);
    }

    [Fact]
    public async Task A_story_that_throws_reaches_sentry_as_one_event_described_on_the_issue_only()
    {
        const string link = "https://example.invalid/one";
        await Db.SeedPendingAsync(link, Title, Body);
        await Db.SeedPresenterAsync();
        var failure = new HttpRequestException("model unreachable");
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns<Task<ChatResponse>>(_ => throw failure);

        await Updater(chatClient).RunManualAsync(fetch: false, analyse: true, limit: null)
            .ContinueWith(_ => { });

        var issue = Assert.Single(await _sentry.EventsAsync());
        Assert.Same(failure, issue.Exception);
        Assert.Equal(link, issue.Tags["analysis.link"]);
        Assert.Equal("analysis-loop", issue.Tags["analysis.stage"]);
        Assert.Contains("analysis.reason", issue.Extra.Keys);

        var story = Assert.Single(await StoryTransactionsAsync());
        Assert.DoesNotContain("analysis.reason", story.Extra.Keys);
    }

    [Fact]
    public async Task No_story_text_reaches_a_span_tag_or_extra()
    {
        await SeedFeedAsync("Example");
        await Db.SeedPresenterAsync();
        Http.Then(Rss(("https://example.invalid/one", Body), ("https://example.invalid/two", Body)));
        Chat.Then(GoodAnalysis).Then(PresenterReply).Always("not json");

        await Updater(Chat.WithSentry()).RunManualAsync(fetch: true, analyse: true, limit: null);

        var transactions = await _sentry.TransactionsAsync();
        Assert.Equal(3, transactions.Count);
        Assert.Contains(transactions.SelectMany(t => t.Spans), s => s.Operation.StartsWith("gen_ai"));
        var recorded = transactions.SelectMany(t =>
            t.Tags.Values
                .Concat(t.Extra.Values.Select(v => v?.ToString()))
                .Concat(t.Spans.SelectMany(s => s.Data.Values.Concat(s.Tags.Values).Append(s.Description).Select(v => v?.ToString()))));
        Assert.All(recorded, value =>
        {
            Assert.DoesNotContain(Title, value ?? "");
            Assert.DoesNotContain(Body, value ?? "");
            Assert.DoesNotContain("not json", value ?? "");
        });
    }

    private async Task SeedArticleWithLinkAsync(string link)
    {
        await using var db = Db.NewContext();
        var other = new RssItem { Link = "https://example.invalid/other", TimeAnalysed = DateTimeOffset.UtcNow };
        db.RssItems.Add(other);
        await db.SaveChangesAsync();
        db.Articles.Add(new Article { Link = link, RssItemId = other.Id, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }
}
