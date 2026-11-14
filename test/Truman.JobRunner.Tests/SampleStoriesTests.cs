using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Truman.Samples;
using Truman.JobRunner.Tests.TestSupport;

namespace Truman.JobRunner.Tests;

public class SampleSetTests
{
    private const int HighScore = 7;
    private static readonly string[] PresenterLabels = ["Default", "Jimmy", "Ricky"];

    private static readonly SampleSet Samples = SampleSet.ReadBundled();

    private static string[] ValueIds() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Values.json")))
            .RootElement.EnumerateArray().Select(v => v.GetProperty("id").GetString()!).ToArray();

    [Fact]
    public void There_are_about_twenty_stories_with_unique_slugs()
    {
        Assert.InRange(Samples.Stories.Length, 18, 26);
        Assert.Equal(Samples.Stories.Length, Samples.Stories.Select(s => s.Slug).Distinct().Count());
    }

    [Fact]
    public void Every_tone_from_one_to_ten_is_present()
    {
        Assert.Equal(Enumerable.Range(1, 10), Samples.Stories.Select(s => s.Tone).Distinct().Order());
    }

    [Fact]
    public void Every_value_is_scored_high_by_at_least_one_story()
    {
        foreach (var value in ValueIds())
        {
            Assert.Contains(Samples.Stories, s => s.Values.GetValueOrDefault(value) >= HighScore);
        }
    }

    [Fact]
    public void Only_known_values_are_scored_and_within_range()
    {
        var known = ValueIds().ToHashSet();
        Assert.Equal(known, SampleStoryLoader.ValueSetters.Keys.ToHashSet());
        foreach (var (value, score) in Samples.Stories.SelectMany(s => s.Values))
        {
            Assert.Contains(value, known);
            Assert.InRange(score, 0, 10);
        }
    }

    [Fact]
    public void At_least_one_story_scores_no_value_high()
    {
        Assert.Contains(Samples.Stories, s => s.Values.Values.All(score => score < HighScore));
    }

    [Fact]
    public void The_readers_values_are_matched_by_several_one_or_none()
    {
        var matches = Samples.Stories
            .Select(s => Samples.Reader.Values.Count(v => s.Values.GetValueOrDefault(v) >= HighScore))
            .ToArray();

        Assert.Contains(matches, m => m >= 3);
        Assert.Contains(matches, m => m == 1);
        Assert.Contains(matches, m => m == 0);
    }

    [Fact]
    public void Tags_are_shared_between_stories_including_the_readers()
    {
        var storiesPerTag = Samples.Stories.SelectMany(s => s.Tags).GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

        Assert.True(storiesPerTag.Count(t => t.Value >= 3) >= 3);
        Assert.All(Samples.Reader.FollowedTags, tag => Assert.True(storiesPerTag.GetValueOrDefault(tag) >= 3));
        Assert.All(Samples.Reader.BannedTags, tag => Assert.True(storiesPerTag.GetValueOrDefault(tag) >= 1));
    }

    [Fact]
    public void Stories_come_from_at_least_three_plainly_made_up_sources()
    {
        Assert.True(Samples.Stories.Select(s => s.Source).Distinct().Count() >= 3);
        Assert.All(Samples.Sources, source =>
        {
            Assert.StartsWith("Made-Up ", source.Name);
            Assert.EndsWith(".invalid", new Uri(source.Url).Host);
        });
        Assert.All(Samples.Stories, s => Assert.Contains(Samples.Sources, source => source.Name == s.Source));
    }

    [Fact]
    public void Ages_range_from_minutes_to_days()
    {
        Assert.Contains(Samples.Stories, s => s.MinutesAgo < 60);
        Assert.Contains(Samples.Stories, s => s.MinutesAgo >= 2 * 24 * 60);
    }

    [Fact]
    public void Every_story_has_distinct_content_for_every_presenter()
    {
        Assert.All(Samples.Stories, s =>
        {
            Assert.Equal(PresenterLabels.Order(), s.Presenters.Keys.Order());
            Assert.All(s.Presenters.Values, c =>
            {
                Assert.False(string.IsNullOrWhiteSpace(c.Title));
                Assert.False(string.IsNullOrWhiteSpace(c.Tldr));
                Assert.False(string.IsNullOrWhiteSpace(c.Content));
            });
            Assert.Equal(PresenterLabels.Length, s.Presenters.Values.Select(c => c.Title).Distinct().Count());
            Assert.Equal(PresenterLabels.Length, s.Presenters.Values.Select(c => c.Content).Distinct().Count());
        });
    }

    [Fact]
    public void Some_stories_are_short_and_some_long()
    {
        var lengths = Samples.Stories.Select(s => s.Presenters["Default"].Content.Length).ToArray();

        Assert.Contains(lengths, l => l < 300);
        Assert.Contains(lengths, l => l > 1000);
    }

    [Fact]
    public void Every_story_says_it_is_made_up()
    {
        Assert.All(Samples.Stories.SelectMany(s => s.Presenters.Values),
            c => Assert.Contains("made-up sample", c.Content, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_reader_starts_with_a_middling_mood_and_known_values()
    {
        Assert.InRange(Samples.Reader.Mood, 4, 6);
        Assert.InRange(Samples.Reader.Values.Length, 2, 6);
        Assert.All(Samples.Reader.Values, v => Assert.Contains(v, ValueIds()));
        Assert.NotEmpty(Samples.Reader.FollowedTags);
        Assert.NotEmpty(Samples.Reader.BannedTags);
    }

    [Fact]
    public void About_twenty_stories_show_on_first_load()
    {
        var shown = Samples.Stories.Count(s =>
            s.Tone >= Samples.Reader.Mood && !s.Tags.Intersect(Samples.Reader.BannedTags).Any());

        Assert.InRange(shown, 17, 23);
    }
}

[Collection(JobRunnerDatabaseCollection.Name)]
public class SampleStoryLoaderTests(JobRunnerDatabaseFixture database) : IAsyncLifetime
{
    private static readonly SampleSet Samples = SampleSet.ReadBundled();

    public async ValueTask InitializeAsync()
    {
        await database.ResetAsync();
        await using var db = database.NewContext();
        await db.Database.ExecuteSqlRawAsync("""TRUNCATE "UserTagPreferences", "UserProfiles" RESTART IDENTITY CASCADE;""");
        await database.SeedPresenterAsync("A British news presenter", "Default");
        await database.SeedPresenterAsync("Jimmy Carr", "Jimmy");
        await database.SeedPresenterAsync("Ricky Gervais", "Ricky");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private SampleStoryLoader Loader() => new(NullLogger<SampleStoryLoader>.Instance, database.ContextFactory);

    [Fact]
    public async Task Loads_every_story_with_content_for_every_presenter()
    {
        var now = DateTimeOffset.UtcNow;

        var added = await Loader().LoadAsync(Samples, now);

        Assert.Equal(Samples.Stories.Length, added);
        await using var db = database.NewContext();
        var articles = await db.Articles.Include(a => a.ArticlePresenters).Include(a => a.RssItem).ThenInclude(r => r.Feed).ToListAsync();
        Assert.Equal(Samples.Stories.Length, articles.Count);
        Assert.All(articles, a =>
        {
            Assert.Equal(3, a.ArticlePresenters.Count);
            Assert.NotNull(a.RssItem.Feed);
            Assert.Equal(now, a.CreatedAt, TimeSpan.FromSeconds(1));
        });
        var comet = articles.Single(a => a.Link.EndsWith("/volunteer-astronomers-spot-comet"));
        Assert.Equal(9, comet.Knowledge);
        Assert.Equal(8, comet.Sentiment);
    }

    [Fact]
    public async Task Sample_feeds_are_disabled_so_a_fetch_never_reads_them()
    {
        await Loader().LoadAsync(Samples, DateTimeOffset.UtcNow);

        await using var db = database.NewContext();
        var feeds = await db.Feeds.ToListAsync();
        Assert.Equal(Samples.Sources.Length, feeds.Count);
        Assert.All(feeds, f => Assert.False(f.IsEnabled));
    }

    [Fact]
    public async Task Creates_the_try_reader_with_a_profile()
    {
        await Loader().LoadAsync(Samples, DateTimeOffset.UtcNow);

        await using var db = database.NewContext();
        var profile = await db.UserProfiles.Include(p => p.TagPreferences).SingleAsync(p => p.Email == "try@localhost");
        Assert.Equal(Samples.Reader.Mood, profile.Mood);
        Assert.Equal(Samples.Reader.Values, JsonSerializer.Deserialize<string[]>(profile.SelectedValues));
        Assert.Equal(Samples.Reader.FollowedTags, profile.TagPreferences.Where(t => t.Weight > 0).Select(t => t.Tag));
        Assert.Equal(Samples.Reader.BannedTags, profile.TagPreferences.Where(t => t.Weight == 0).Select(t => t.Tag));
    }

    [Fact]
    public async Task Loading_twice_adds_nothing_the_second_time()
    {
        await Loader().LoadAsync(Samples, DateTimeOffset.UtcNow);

        var addedAgain = await Loader().LoadAsync(Samples, DateTimeOffset.UtcNow);

        Assert.Equal(0, addedAgain);
        await using var db = database.NewContext();
        Assert.Equal(Samples.Stories.Length, await db.Articles.CountAsync());
        Assert.Equal(Samples.Sources.Length, await db.Feeds.CountAsync());
        Assert.Equal(1, await db.UserProfiles.CountAsync());
    }
}

public class SamplesOnlyInATryTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "truman.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repo root.");
    }

    [Theory]
    [InlineData("compose.yaml")]
    [InlineData("compose.prod.yaml")]
    [InlineData("compose.preview.yaml")]
    [InlineData("deploy.sh")]
    [InlineData("src/Truman.JobRunner/Dockerfile")]
    public void Nothing_but_try_loads_the_samples(string file)
    {
        Assert.DoesNotContain("--samples", File.ReadAllText(Path.Combine(RepoRoot(), file)));
    }

    [Fact]
    public void Try_loads_the_samples()
    {
        Assert.Contains("--samples", File.ReadAllText(Path.Combine(RepoRoot(), "scripts/try.sh")));
    }

    [Theory]
    [InlineData]
    [InlineData("--fetch")]
    [InlineData("--analyse")]
    [InlineData("--run-migrations")]
    [InlineData("--limit", "5")]
    public void No_other_invocation_loads_the_samples(params string[] args)
    {
        Assert.False(JobRunnerArguments.Parse(args).Samples);
    }

    [Fact]
    public void Samples_alone_neither_fetch_nor_analyse()
    {
        var parsed = JobRunnerArguments.Parse(["--samples"]);

        Assert.True(parsed.Samples);
        Assert.False(parsed.IsScheduled);
        Assert.False(parsed.Fetch);
        Assert.False(parsed.Analyse);
    }
}
