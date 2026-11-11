using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Truman.Api.Features.Articles;
using Truman.Data;
using Truman.Data.Entities;
using System.Threading; // added

namespace Truman.Api.Tests.Articles;

[Collection("Database collection")] // Use real Postgres testcontainer
public class RelevantArticlesServiceTests : IAsyncLifetime
{
    private readonly DbContextOptions<TrumanDbContext> _dbOptions;
    private readonly TrumanDbContext _ctx;
    private static readonly CancellationToken Ct = CancellationToken.None; // reuse token

    public RelevantArticlesServiceTests(DatabaseFixture fixture)
    {
        _dbOptions = fixture.DbOptions;
        _ctx = new(_dbOptions);
    }

    public ValueTask InitializeAsync()
    {
        // Remove dependent entities first to satisfy FK constraints
        _ctx.ArticlePresenters.RemoveRange(_ctx.ArticlePresenters);
        _ctx.Articles.RemoveRange(_ctx.Articles);
        _ctx.Presenters.RemoveRange(_ctx.Presenters);
        _ctx.UserTagPreferences.RemoveRange(_ctx.UserTagPreferences);
        _ctx.UserProfiles.RemoveRange(_ctx.UserProfiles);
        _ctx.RssItems.RemoveRange(_ctx.RssItems);
        _ctx.Feeds.RemoveRange(_ctx.Feeds);
        return new ValueTask(_ctx.SaveChangesAsync(Ct));
    }
    
    public ValueTask DisposeAsync()
    {
        return _ctx.DisposeAsync();
    }

    private UserProfile CreateUser(string email,
        IEnumerable<string>? values = null,
        IEnumerable<(string tag,int weight)>? tagPrefs = null)
    {
        var profile = new UserProfile
        {
            Email = email,
            Mood = 5,
            SelectedValues = JsonSerializer.Serialize(values?.ToList() ?? new List<string>())
        };
        if (tagPrefs != null)
        {
            foreach (var (tag, weight) in tagPrefs)
            {
                profile.TagPreferences.Add(new UserTagPreference { Tag = tag, Weight = weight });
            }
        }
        return profile;
    }

    private Article MakeArticle(string link, int sentiment, string[] tags, Action<Article>? conf = null)
    {
        var article = new Article
        {
            Link = link,
            Sentiment = sentiment,
            Tags = tags,
            Freedom = 0,
            Knowledge = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            RssItem = new RssItem { Link = link + "rss" }
        };
        conf?.Invoke(article);
        return article;
    }

    [Fact]
    public async Task FiltersOutOldAndLowSentimentAndBannedTags()
    {
        var service = new RelevantArticlesService(_ctx, NullLogger<RelevantArticlesService>.Instance);
        var user = CreateUser($"user_{Guid.NewGuid()}@example.com", new[]{"freedom"}, new[]{("ai",2),("politics",0)});
        _ctx.UserProfiles.Add(user);

        // Reuse a single presenter instance to satisfy unique constraint on PresenterStyle
        var defaultPresenter = new Presenter { Label = "Default", PresenterStyle = "DefaultStyle" };
        _ctx.Presenters.Add(defaultPresenter);

        var a1 = MakeArticle($"a1_{Guid.NewGuid():N}", 6, new[]{"ai"}, a =>
        {
            a.Freedom = 10;
            a.ArticlePresenters.Add(new ArticlePresenter
            {
                Presenter = defaultPresenter,
                Title = "A1 Default Title",
                Tldr = "A1 Default TLDR",
                Content = "A1 Default Content"
            });
        });
        var a2 = MakeArticle($"a2_{Guid.NewGuid():N}", 8, new[]{"ai"}, a =>
        {
            a.CreatedAt = DateTimeOffset.UtcNow.AddDays(-3);
            a.Freedom = 10;
            a.ArticlePresenters.Add(new ArticlePresenter
            {
                Presenter = defaultPresenter,
                Title = "Old Title",
                Tldr = "Old TLDR",
                Content = "Old Content"
            });
        });
        var a3 = MakeArticle($"a3_{Guid.NewGuid():N}", 2, new[]{"ai"}, a =>
        {
            a.Freedom = 10;
            a.ArticlePresenters.Add(new ArticlePresenter
            {
                Presenter = defaultPresenter,
                Title = "Low Title",
                Tldr = "Low TLDR",
                Content = "Low Content"
            });
        });
        var a4 = MakeArticle($"a4_{Guid.NewGuid():N}", 7, new[]{"Politics"}, a =>
        {
            a.Freedom = 10;
            a.ArticlePresenters.Add(new ArticlePresenter
            {
                Presenter = defaultPresenter,
                Title = "Banned Title",
                Tldr = "Banned TLDR",
                Content = "Banned Content"
            });
        });

        _ctx.Articles.AddRange(a1,a2,a3,a4);
        await _ctx.SaveChangesAsync(Ct);

        var resp = await service.GetRelevantArticlesAsync(new RelevantArticlesRequest{ Presenter = "Default"}, user);
        Assert.Single(resp.Articles);
        Assert.StartsWith("a1_", resp.Articles[0].Link);
    }

    [Fact]
    public async Task OrdersByRelevance_UsingTagWeightsAndValueWeights()
    {
        var service = new RelevantArticlesService(_ctx, NullLogger<RelevantArticlesService>.Instance);
        var user = CreateUser($"user_{Guid.NewGuid()}@example.com", new[]{"freedom"}, new[]{("ai",3),("cloud",2),("ml",1)});
        _ctx.UserProfiles.Add(user);

        var basePresenter = new Presenter{ Label = "Default", PresenterStyle = "DefaultStyle"};

        // Equal sentiment & value scores so only tag alignment differentiates relevance
        var a1 = MakeArticle($"a1_{Guid.NewGuid():N}", 6, new[]{"ai"}, a => { a.Freedom = 8; });
        a1.ArticlePresenters.Add(new ArticlePresenter{ Presenter = basePresenter, Title="A1", Tldr="A1", Content="A1"});

        var a2 = MakeArticle($"a2_{Guid.NewGuid():N}", 6, new[]{"cloud","ml"}, a => { a.Freedom = 8; });
        a2.ArticlePresenters.Add(new ArticlePresenter{ Presenter = basePresenter, Title="A2", Tldr="A2", Content="A2"});

        _ctx.Articles.AddRange(a1,a2);
        await _ctx.SaveChangesAsync(Ct);

        var resp = await service.GetRelevantArticlesAsync(new RelevantArticlesRequest{ Presenter = "Default"}, user);
        Assert.Equal(2, resp.Articles.Count);
        Assert.StartsWith("a1_", resp.Articles[0].Link); // ai-only article should rank first now
        Assert.True(resp.Articles[0].RelevanceScore > resp.Articles[1].RelevanceScore);
        // Sanity: tag alignment should differ
        Assert.NotEqual(resp.Articles[0].RelevanceScore, resp.Articles[1].RelevanceScore);
    }

    [Fact]
    public async Task PresenterSelection_FallsBackToDefault_WhenRequestedMissing()
    {
        var service = new RelevantArticlesService(_ctx, NullLogger<RelevantArticlesService>.Instance);
        var user = CreateUser($"user_{Guid.NewGuid()}@example.com", new[]{"freedom"});
        _ctx.UserProfiles.Add(user);

        var pDefault = new Presenter{ Label = "Default", PresenterStyle = "DefaultStyle"};
        var pBrief = new Presenter{ Label = "Brief", PresenterStyle = "BriefStyle"};

        var art = MakeArticle($"a1_{Guid.NewGuid():N}", 7, new[]{"ai"}, a => { a.Freedom = 9; });
        art.ArticlePresenters.Add(new ArticlePresenter{ Presenter = pDefault, Title="Default Title", Tldr="Default TLDR", Content="Default Content"});
        art.ArticlePresenters.Add(new ArticlePresenter{ Presenter = pBrief, Title="Brief Title", Tldr="Brief TLDR", Content="Brief Content"});
        _ctx.Articles.Add(art);
        await _ctx.SaveChangesAsync(Ct);

        var respBrief = await service.GetRelevantArticlesAsync(new RelevantArticlesRequest{ Presenter = "Brief"}, user);
        Assert.Single(respBrief.Articles);
        Assert.Equal("Brief Title", respBrief.Articles[0].Title);

        var respMissing = await service.GetRelevantArticlesAsync(new RelevantArticlesRequest{ Presenter = "NonExistent"}, user);
        Assert.Single(respMissing.Articles);
        Assert.Equal("Default Title", respMissing.Articles[0].Title);
    }

    [Fact]
    public async Task CarriesPublisherAndPublishDate_FromTheItemsFeed()
    {
        var service = new RelevantArticlesService(_ctx, NullLogger<RelevantArticlesService>.Instance);
        var user = CreateUser($"user_{Guid.NewGuid()}@example.com");
        _ctx.UserProfiles.Add(user);

        var feed = new Feed { Name = "Phys.org", Url = $"https://example.invalid/{Guid.NewGuid():N}", IsEnabled = true };
        var published = new DateTimeOffset(2026, 11, 3, 8, 30, 0, TimeSpan.Zero);
        var fromFeed = MakeArticle($"feed_{Guid.NewGuid():N}", 7, [], a =>
        {
            a.RssItem.Feed = feed;
            a.RssItem.PubDate = published;
        });
        var orphan = MakeArticle($"orphan_{Guid.NewGuid():N}", 7, []);
        _ctx.Articles.AddRange(fromFeed, orphan);
        await _ctx.SaveChangesAsync(Ct);

        var resp = await service.GetRelevantArticlesAsync(new RelevantArticlesRequest { Presenter = "Default" }, user);

        var withFeed = Assert.Single(resp.Articles, a => a.Link.StartsWith("feed_"));
        Assert.Equal("Phys.org", withFeed.Publisher);
        Assert.Equal(published, withFeed.PublishedAt);

        var withoutFeed = Assert.Single(resp.Articles, a => a.Link.StartsWith("orphan_"));
        Assert.Null(withoutFeed.Publisher);
        Assert.Null(withoutFeed.PublishedAt);
    }

    [Fact]
    public async Task DeletingAFeed_KeepsItsItemsAndArticles_WithNoFeed()
    {
        var feed = new Feed { Name = "Phys.org", Url = $"https://example.invalid/{Guid.NewGuid():N}", IsEnabled = true };
        var article = MakeArticle($"feed_{Guid.NewGuid():N}", 7, [], a => a.RssItem.Feed = feed);
        _ctx.Articles.Add(article);
        await _ctx.SaveChangesAsync(Ct);

        await using (var admin = new TrumanDbContext(_dbOptions))
        {
            admin.Feeds.Remove((await admin.Feeds.FindAsync([feed.Id], Ct))!);
            await admin.SaveChangesAsync(Ct);
        }

        await using var fresh = new TrumanDbContext(_dbOptions);
        var kept = await fresh.Articles.Include(a => a.RssItem).SingleAsync(a => a.Id == article.Id, Ct);
        Assert.Null(kept.RssItem.FeedId);
    }

    [Fact]
    public async Task ExplainsEachStory_WithSharesAndWhatMatched()
    {
        var service = new RelevantArticlesService(_ctx, NullLogger<RelevantArticlesService>.Instance);
        var user = CreateUser($"user_{Guid.NewGuid()}@example.com",
            ["security", "knowledge", "freedom"], [("nz", 2), ("science", 1), ("cats", 0)]);
        _ctx.UserProfiles.Add(user);

        var article = MakeArticle($"a_{Guid.NewGuid():N}", 8, ["science", "NZ", "weather"], a =>
        {
            a.Freedom = 9;
            a.Security = 7;
            a.Knowledge = 6;
        });
        _ctx.Articles.Add(article);
        await _ctx.SaveChangesAsync(Ct);

        var resp = await service.GetRelevantArticlesAsync(new RelevantArticlesRequest { Presenter = "Default" }, user);

        var why = Assert.Single(resp.Articles).Why;
        Assert.Equal(["security", "freedom"], why.MatchedValues);
        Assert.Equal(["nz", "science"], why.MatchedTags);
        Assert.Equal(5, why.Mood);
        Assert.Equal(1.0, why.ValuesShare + why.TagsShare + why.ToneShare, 9);
        Assert.All([why.ValuesShare, why.TagsShare, why.ToneShare], share => Assert.True(share > 0));
    }

    [Fact]
    public async Task ExplainsAStoryWithNothingToShow_WithoutDividingByZero()
    {
        var service = new RelevantArticlesService(_ctx, NullLogger<RelevantArticlesService>.Instance);
        var user = CreateUser($"user_{Guid.NewGuid()}@example.com", ["freedom"]);
        _ctx.UserProfiles.Add(user);
        _ctx.Articles.Add(MakeArticle($"a_{Guid.NewGuid():N}", 5, ["ai"]));
        await _ctx.SaveChangesAsync(Ct);

        var resp = await service.GetRelevantArticlesAsync(new RelevantArticlesRequest { Presenter = "Default" }, user);

        var story = Assert.Single(resp.Articles);
        Assert.Equal(0, story.RelevanceScore);
        Assert.Equal(0, story.Why.ValuesShare);
        Assert.Equal(0, story.Why.TagsShare);
        Assert.Equal(0, story.Why.ToneShare);
        Assert.Empty(story.Why.MatchedValues);
        Assert.Empty(story.Why.MatchedTags);
    }

    [Fact]
    public async Task OrderIsUnchanged_ByTheExplanation()
    {
        var service = new RelevantArticlesService(_ctx, NullLogger<RelevantArticlesService>.Instance);
        var user = CreateUser($"user_{Guid.NewGuid()}@example.com", ["freedom", "knowledge"], [("nz", 1)]);
        _ctx.UserProfiles.Add(user);
        _ctx.Articles.AddRange(
            MakeArticle($"tone_{Guid.NewGuid():N}", 9, []),
            MakeArticle($"tag_{Guid.NewGuid():N}", 5, ["nz"]),
            MakeArticle($"value_{Guid.NewGuid():N}", 6, [], a => a.Freedom = 10),
            MakeArticle($"both_{Guid.NewGuid():N}", 6, ["nz"], a => a.Knowledge = 8));
        await _ctx.SaveChangesAsync(Ct);

        var resp = await service.GetRelevantArticlesAsync(new RelevantArticlesRequest { Presenter = "Default" }, user);

        // 0.4*(10/2)+0.2, 0.4*(8/1.5/2)+0.4+0.2, 0.2*4, 0.4*1
        Assert.Equal(["value_", "both_", "tone_", "tag_"], resp.Articles.Select(a => a.Link.Split('_')[0] + "_"));
        Assert.Equal(resp.Articles.Select(a => a.RelevanceScore).OrderDescending(), resp.Articles.Select(a => a.RelevanceScore));
    }
}
