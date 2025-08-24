using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Truman.Api.Features.Articles;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Tests.Articles;

[Collection("Database collection")] // Use real Postgres testcontainer
public class RelevantArticlesServiceTests : IAsyncLifetime
{
    private readonly DbContextOptions<TrumanDbContext> _dbOptions;
    private readonly TrumanDbContext _ctx;

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
        return new ValueTask(_ctx.SaveChangesAsync());
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
            Title = link + " Title",
            Tldr = link + " Tldr",
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
        await _ctx.SaveChangesAsync();

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
        await _ctx.SaveChangesAsync();

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
        await _ctx.SaveChangesAsync();

        var respBrief = await service.GetRelevantArticlesAsync(new RelevantArticlesRequest{ Presenter = "Brief"}, user);
        Assert.Single(respBrief.Articles);
        Assert.Equal("Brief Title", respBrief.Articles[0].Title);

        var respMissing = await service.GetRelevantArticlesAsync(new RelevantArticlesRequest{ Presenter = "NonExistent"}, user);
        Assert.Single(respMissing.Articles);
        Assert.Equal("Default Title", respMissing.Articles[0].Title);
    }
}
