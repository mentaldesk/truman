using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Truman.Api.Features.Articles;
using Truman.Data;
using Truman.Data.Entities;
using Truman.Samples;

namespace Truman.Api.Tests.Articles;

[Collection("Database collection")]
public class SampleStoriesFeedTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly SampleSet Samples = SampleSet.ReadBundled();

    public async ValueTask InitializeAsync()
    {
        await using var db = new TrumanDbContext(fixture.DbOptions);
        await db.Database.ExecuteSqlRawAsync(
            """TRUNCATE "ArticlePresenters", "Articles", "RssItems", "Presenters", "Feeds", "UserTagPreferences", "UserProfiles" RESTART IDENTITY CASCADE;""");
        db.Presenters.AddRange(
            new Presenter { Label = "Default", PresenterStyle = "A British news presenter" },
            new Presenter { Label = "Jimmy", PresenterStyle = "Jimmy Carr" },
            new Presenter { Label = "Ricky", PresenterStyle = "Ricky Gervais" });
        await db.SaveChangesAsync();

        var loader = new SampleStoryLoader(NullLogger<SampleStoryLoader>.Instance, new Factory(fixture.DbOptions));
        await loader.LoadAsync(Samples, DateTimeOffset.UtcNow);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<RelevantArticlesResponse> FeedAsync(string presenter = "Default", Action<UserProfile>? change = null)
    {
        await using var db = new TrumanDbContext(fixture.DbOptions);
        var reader = await db.UserProfiles.Include(p => p.TagPreferences).SingleAsync(p => p.Email == Samples.Reader.Email);
        change?.Invoke(reader);
        var service = new RelevantArticlesService(db, NullLogger<RelevantArticlesService>.Instance);
        return await service.GetRelevantArticlesAsync(new RelevantArticlesRequest { Presenter = presenter }, reader);
    }

    [Fact]
    public async Task The_try_reader_sees_about_twenty_stories_with_badges_on_first_load()
    {
        var feed = await FeedAsync();

        Assert.InRange(feed.Articles.Count, 17, 23);
        Assert.All(feed.Articles, a => Assert.False(string.IsNullOrEmpty(a.Title)));
        Assert.All(feed.Articles, a => Assert.StartsWith("Made-Up ", a.Publisher));
        Assert.Contains(feed.Articles, a => a.Why.MatchedTags.Length + a.Why.MatchedValues.Length > 3);
        Assert.Contains(feed.Articles, a => a.Why.MatchedTags.Length + a.Why.MatchedValues.Length == 0);
    }

    [Fact]
    public async Task The_mood_hides_and_shows_stories()
    {
        var low = await FeedAsync(change: r => r.Mood = 1);
        var high = await FeedAsync(change: r => r.Mood = 9);

        Assert.True(low.Articles.Count > high.Articles.Count);
        Assert.All(high.Articles, a => Assert.True(a.Sentiment >= 9));
        Assert.Contains(low.Articles, a => a.Sentiment == 1);
    }

    [Fact]
    public async Task The_banned_tag_hides_its_stories_until_unbanned()
    {
        var banned = Samples.Reader.BannedTags[0];
        var feed = await FeedAsync(change: r => r.Mood = 1);
        var unbanned = await FeedAsync(change: r =>
        {
            r.Mood = 1;
            r.TagPreferences.Clear();
        });

        Assert.DoesNotContain(feed.Articles, a => a.Tags.Contains(banned));
        Assert.Contains(unbanned.Articles, a => a.Tags.Contains(banned));
    }

    [Fact]
    public async Task Banning_a_shared_tag_hides_several_stories()
    {
        var before = await FeedAsync();
        var after = await FeedAsync(change: r =>
            r.TagPreferences.Add(new UserTagPreference { Tag = "local", Weight = 0 }));

        Assert.True(before.Articles.Count - after.Articles.Count >= 3);
    }

    [Fact]
    public async Task Following_a_tag_moves_its_stories_up()
    {
        var followed = Samples.Reader.FollowedTags[0];
        var feed = await FeedAsync();
        var unfollowed = await FeedAsync(change: r =>
            r.TagPreferences.Remove(r.TagPreferences.Single(p => p.Tag == followed)));

        var stories = feed.Articles.Where(a => a.Tags.Contains(followed)).Select(a => a.Id).ToList();
        int Rank(RelevantArticlesResponse f, int id) => f.Articles.FindIndex(a => a.Id == id);

        Assert.NotEmpty(stories);
        Assert.All(stories, id => Assert.True(Rank(feed, id) <= Rank(unfollowed, id)));
        Assert.Contains(stories, id => Rank(feed, id) < Rank(unfollowed, id));
    }

    [Fact]
    public async Task Switching_presenter_changes_every_story()
    {
        var byDefault = await FeedAsync("Default");
        var byJimmy = await FeedAsync("Jimmy");

        Assert.All(byDefault.Articles, a =>
        {
            var other = byJimmy.Articles.Single(j => j.Id == a.Id);
            Assert.NotEqual(a.Title, other.Title);
            Assert.NotEqual(a.Content, other.Content);
        });
    }

    private sealed class Factory(DbContextOptions<TrumanDbContext> options) : IDbContextFactory<TrumanDbContext>
    {
        public TrumanDbContext CreateDbContext() => new(options);
    }
}
