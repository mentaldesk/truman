using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sentry;
using Truman.Data;

namespace Truman.JobRunner;

public class ArticleUpdater(
    RssFetcher fetcher,
    ArticleAnalyser analyser,
    IDbContextFactory<TrumanDbContext> contextFactory)
{
    private const int FewestAttemptsToJudgeABatch = 5;
    private static readonly TimeSpan NewestStoryMaxAge = TimeSpan.FromHours(24);

    internal const string MonitorSlug = "update-articles";

    public async Task RunManualAsync(bool fetch, bool analyse, int? limit, CancellationToken cancellationToken = default)
    {
        using var _ = SentrySdk.PushScope();
        var transaction = SentrySdk.StartTransaction("update-articles", "job.run");
        transaction.SetTag("job.trigger", "manual");
        SentrySdk.ConfigureScope(scope => scope.Transaction = transaction);

        try
        {
            if (fetch)
            {
                await fetcher.RunAsync(cancellationToken);
            }

            if (analyse)
            {
                (await analyser.RunAsync(limit, cancellationToken)).ThrowIfNothingSucceeded();
            }

            transaction.Finish(SpanStatus.Ok);
        }
        catch (Exception ex)
        {
            transaction.Finish(ex);
            throw;
        }
    }

    public async Task RunScheduledAsync(CancellationToken cancellationToken = default)
    {
        using var _ = SentrySdk.PushScope();
        var transaction = SentrySdk.StartTransaction("update-articles", "job.run");
        transaction.SetTag("job.trigger", "scheduled");
        SentrySdk.ConfigureScope(scope => scope.Transaction = transaction);

        try
        {
            // Only the bare invocation is the scheduled one, so only it checks in; a manual run
            // would otherwise satisfy the schedule and hide a run that never happened.
            // The interval must match the crontab that invokes this. Declaring it here upserts it to Sentry.
            await SentrySdk.WithMonitorAsync(MonitorSlug, () => RunScheduledStagesAsync(cancellationToken), monitor =>
            {
                monitor.Interval("0 */6 * * *");
                monitor.TimeZone = "Etc/UTC";
                monitor.CheckInMargin = TimeSpan.FromMinutes(30);
                monitor.MaxRuntime = TimeSpan.FromMinutes(60);
                monitor.FailureIssueThreshold = 1;
                monitor.RecoveryThreshold = 1;
            });

            transaction.Finish(SpanStatus.Ok);
        }
        catch (Exception ex)
        {
            transaction.Finish(ex);
            throw;
        }
    }

    private async Task RunScheduledStagesAsync(CancellationToken cancellationToken)
    {
        var fetch = await fetcher.RunAsync(cancellationToken);
        var analysis = await analyser.RunAsync(limit: null, cancellationToken);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var hasPresenters = await db.Presenters.AnyAsync(cancellationToken);
        var newestStory = await db.Articles.MaxAsync(a => (DateTimeOffset?)a.CreatedAt, cancellationToken);

        var problems = FindProblems(fetch, analysis, hasPresenters, newestStory);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException($"Scheduled run failed: {string.Join("; ", problems)}.");
        }
    }

    private static List<string> FindProblems(
        FetchOutcome fetch,
        AnalysisOutcome analysis,
        bool hasPresenters,
        DateTimeOffset? newestStory)
    {
        var problems = new List<string>();

        if (fetch.EnabledFeeds == 0)
        {
            problems.Add("No feeds are enabled");
        }
        else if (fetch.FailedFeeds == fetch.EnabledFeeds)
        {
            problems.Add("Every enabled feed failed to load");
        }

        if (!hasPresenters)
        {
            problems.Add("No presenters are configured to write stories");
        }

        var badBatch = analysis.Attempted >= FewestAttemptsToJudgeABatch && analysis.Failed * 2 >= analysis.Attempted;
        if (analysis.NothingSucceeded || badBatch)
        {
            problems.Add($"{analysis.Failed} of {analysis.Attempted} stories failed analysis");
        }

        if (newestStory is null)
        {
            problems.Add("No new story saved in the last 24 hours (no story has ever been saved)");
        }
        else if (DateTimeOffset.UtcNow - newestStory > NewestStoryMaxAge)
        {
            var newest = newestStory.Value.UtcDateTime.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);
            problems.Add($"No new story saved in the last 24 hours (newest: {newest} UTC)");
        }

        return problems;
    }
}
