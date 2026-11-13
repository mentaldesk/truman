using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Truman.Api.Features.Articles;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Tests.Articles;

[Collection("Database collection")]
public class SavedStoriesTests : IAsyncLifetime
{
    private readonly DbContextOptions<TrumanDbContext> _dbOptions;
    private readonly TrumanDbContext _ctx;
    private static readonly CancellationToken Ct = CancellationToken.None;

    public SavedStoriesTests(DatabaseFixture fixture)
    {
        _dbOptions = fixture.DbOptions;
        _ctx = new(_dbOptions);
    }

    public ValueTask InitializeAsync()
    {
        _ctx.SavedStories.RemoveRange(_ctx.SavedStories);
        _ctx.ArticlePresenters.RemoveRange(_ctx.ArticlePresenters);
        _ctx.Articles.RemoveRange(_ctx.Articles);
        _ctx.Presenters.RemoveRange(_ctx.Presenters);
        _ctx.UserTagPreferences.RemoveRange(_ctx.UserTagPreferences);
        _ctx.UserProfiles.RemoveRange(_ctx.UserProfiles);
        _ctx.RssItems.RemoveRange(_ctx.RssItems);
        _ctx.Feeds.RemoveRange(_ctx.Feeds);
        return new ValueTask(_ctx.SaveChangesAsync(Ct));
    }

    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private RelevantArticlesService Articles(TrumanDbContext? ctx = null) =>
        new(ctx ?? _ctx, NullLogger<RelevantArticlesService>.Instance);

    private SavedStoriesService Saved(TrumanDbContext? ctx = null) => new(ctx ?? _ctx);

    private static RelevantArticlesRequest As(string presenter) => new() { Presenter = presenter };

    private async Task<UserProfile> AddUserAsync(int mood = 5, params (string tag, int weight)[] tagPrefs)
    {
        var user = new UserProfile
        {
            Email = $"user_{Guid.NewGuid():N}@example.com",
            Mood = mood,
            SelectedValues = JsonSerializer.Serialize(new[] { "freedom" })
        };
        foreach (var (tag, weight) in tagPrefs)
            user.TagPreferences.Add(new UserTagPreference { Tag = tag, Weight = weight });
        _ctx.UserProfiles.Add(user);
        await _ctx.SaveChangesAsync(Ct);
        return user;
    }

    private async Task<Article> AddArticleAsync(string name, int sentiment = 7, string[]? tags = null, DateTimeOffset? createdAt = null, Action<Article>? conf = null)
    {
        var link = $"{name}_{Guid.NewGuid():N}";
        var article = new Article
        {
            Link = link,
            Sentiment = sentiment,
            Tags = tags ?? [],
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            RssItem = new RssItem { Link = link + "rss" }
        };
        conf?.Invoke(article);
        _ctx.Articles.Add(article);
        await _ctx.SaveChangesAsync(Ct);
        return article;
    }

    private static string[] Names(RelevantArticlesResponse resp) =>
        resp.Articles.Select(a => a.Link.Split('_')[0]).ToArray();

    [Fact]
    public async Task SavedStories_AreListedNewestSavedFirst_AndMarkedSaved()
    {
        var user = await AddUserAsync();
        var first = await AddArticleAsync("first");
        var second = await AddArticleAsync("second");

        Assert.True(await Saved().SaveAsync(user, first.Id));
        Assert.True(await Saved().SaveAsync(user, second.Id));

        var resp = await Articles().GetSavedArticlesAsync(As("Default"), user);

        Assert.Equal(["second", "first"], Names(resp));
        Assert.All(resp.Articles, a => Assert.True(a.IsSaved));
    }

    [Fact]
    public async Task SavingTwice_KeepsOneEntry()
    {
        var user = await AddUserAsync();
        var article = await AddArticleAsync("story");

        await Saved().SaveAsync(user, article.Id);
        await Saved().SaveAsync(user, article.Id);

        Assert.Equal(1, await _ctx.SavedStories.CountAsync(s => s.UserProfileId == user.Id, Ct));
    }

    [Fact]
    public async Task UnsavingAStoryThatIsNotSaved_Succeeds()
    {
        var user = await AddUserAsync();
        var article = await AddArticleAsync("story");

        await Saved().UnsaveAsync(user, article.Id);

        Assert.Empty((await Articles().GetSavedArticlesAsync(As("Default"), user)).Articles);
    }

    [Fact]
    public async Task Unsaving_TakesTheStoryOffTheList()
    {
        var user = await AddUserAsync();
        var keep = await AddArticleAsync("keep");
        var drop = await AddArticleAsync("drop");
        await Saved().SaveAsync(user, keep.Id);
        await Saved().SaveAsync(user, drop.Id);

        await Saved().UnsaveAsync(user, drop.Id);

        Assert.Equal(["keep"], Names(await Articles().GetSavedArticlesAsync(As("Default"), user)));
    }

    [Fact]
    public async Task SavingAStoryThatDoesNotExist_ReturnsFalse()
    {
        var user = await AddUserAsync();

        Assert.False(await Saved().SaveAsync(user, int.MaxValue));
    }

    [Fact]
    public async Task SavedStories_ArePerReader()
    {
        var alice = await AddUserAsync();
        var bob = await AddUserAsync();
        var article = await AddArticleAsync("story");
        await Saved().SaveAsync(alice, article.Id);

        await Saved().UnsaveAsync(bob, article.Id);

        Assert.Empty((await Articles().GetSavedArticlesAsync(As("Default"), bob)).Articles);
        Assert.Equal(["story"], Names(await Articles().GetSavedArticlesAsync(As("Default"), alice)));
        Assert.False(Assert.Single((await Articles().GetRelevantArticlesAsync(As("Default"), bob)).Articles).IsSaved);
    }

    [Fact]
    public async Task SavedList_IgnoresTheFeedWindowMoodAndBannedTags()
    {
        var user = await AddUserAsync(mood: 8, ("politics", 0));
        var old = await AddArticleAsync("old", sentiment: 9, createdAt: DateTimeOffset.UtcNow.AddDays(-10));
        var gloomy = await AddArticleAsync("gloomy", sentiment: 2);
        var banned = await AddArticleAsync("banned", sentiment: 9, tags: ["Politics"]);
        foreach (var article in new[] { old, gloomy, banned })
            await Saved().SaveAsync(user, article.Id);

        Assert.Empty((await Articles().GetRelevantArticlesAsync(As("Default"), user)).Articles);
        Assert.Equal(["banned", "gloomy", "old"], Names(await Articles().GetSavedArticlesAsync(As("Default"), user)));
    }

    [Fact]
    public async Task SavedList_UsesTheRequestedPresenter_FallingBackLikeTheFeed()
    {
        var user = await AddUserAsync();
        var pDefault = new Presenter { Label = "Default", PresenterStyle = "DefaultStyle" };
        var pBrief = new Presenter { Label = "Brief", PresenterStyle = "BriefStyle" };
        var both = await AddArticleAsync("both", conf: a =>
        {
            a.ArticlePresenters.Add(new ArticlePresenter { Presenter = pDefault, Title = "Default Both", Tldr = "", Content = "" });
            a.ArticlePresenters.Add(new ArticlePresenter { Presenter = pBrief, Title = "Brief Both", Tldr = "", Content = "" });
        });
        var defaultOnly = await AddArticleAsync("defaultOnly", conf: a =>
            a.ArticlePresenters.Add(new ArticlePresenter { Presenter = pDefault, Title = "Default Only", Tldr = "", Content = "" }));
        await Saved().SaveAsync(user, both.Id);
        await Saved().SaveAsync(user, defaultOnly.Id);

        var resp = await Articles().GetSavedArticlesAsync(As("Brief"), user);

        Assert.Equal(["Default Only", "Brief Both"], resp.Articles.Select(a => a.Title));
    }

    [Fact]
    public async Task Feed_IsUnchangedBySaving_ExceptForTheSavedMark()
    {
        var user = await AddUserAsync();
        var low = await AddArticleAsync("low", sentiment: 5);
        await AddArticleAsync("mid", sentiment: 7);
        await AddArticleAsync("high", sentiment: 9);
        var before = await Articles().GetRelevantArticlesAsync(As("Default"), user);

        await Saved().SaveAsync(user, low.Id);
        var after = await Articles().GetRelevantArticlesAsync(As("Default"), user);

        Assert.Equal(Names(before), Names(after));
        Assert.Equal(before.Articles.Select(a => a.RelevanceScore), after.Articles.Select(a => a.RelevanceScore));
        Assert.Equal(["low"], after.Articles.Where(a => a.IsSaved).Select(a => a.Link.Split('_')[0]));
    }

    [Fact]
    public async Task SavedOnOneDevice_ShowsSavedOnAnother()
    {
        var user = await AddUserAsync();
        var article = await AddArticleAsync("story");

        await using (var phone = new TrumanDbContext(_dbOptions))
            await Saved(phone).SaveAsync(user, article.Id);

        await using var laptop = new TrumanDbContext(_dbOptions);
        var laptopUser = await laptop.UserProfiles.Include(u => u.TagPreferences).SingleAsync(u => u.Id == user.Id, Ct);
        Assert.True(Assert.Single((await Articles(laptop).GetRelevantArticlesAsync(As("Default"), laptopUser)).Articles).IsSaved);
    }
}
