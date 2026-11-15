using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Truman.Api.Features.Articles;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Tests.Articles;

public class FeedVisitTests
{
    private static readonly DateTimeOffset Morning = new(2026, 11, 10, 7, 40, 0, TimeSpan.Zero);

    [Fact]
    public void FirstVisit_SetsNoLine()
    {
        var profile = new UserProfile();

        FeedVisit.Record(profile, Morning);

        Assert.Null(profile.NewSince);
        Assert.Equal(Morning, profile.FeedSeenAt);
    }

    [Fact]
    public void ComingBackAfterMoreThanAnHour_MovesTheLineToTheLastTimeTheFeedWasOpen()
    {
        var profile = new UserProfile { FeedSeenAt = Morning };

        FeedVisit.Record(profile, Morning.AddHours(1).AddMinutes(1));

        Assert.Equal(Morning, profile.NewSince);
    }

    [Fact]
    public void ComingBackWithinTheHour_KeepsTheLine()
    {
        var earlier = Morning.AddHours(-5);
        var profile = new UserProfile { FeedSeenAt = Morning, NewSince = earlier };

        FeedVisit.Record(profile, Morning.AddMinutes(59));
        FeedVisit.Record(profile, Morning.AddMinutes(118));

        Assert.Equal(earlier, profile.NewSince);
        Assert.Equal(Morning.AddMinutes(118), profile.FeedSeenAt);
    }
}

[Collection("Database collection")]
public class FeedVisitFeedTests : IAsyncLifetime
{
    private readonly DbContextOptions<TrumanDbContext> _dbOptions;
    private readonly TrumanDbContext _ctx;
    private readonly ManualClock _clock = new(new DateTimeOffset(DateTime.UtcNow.Date.AddHours(7).AddMinutes(40), TimeSpan.Zero));

    public FeedVisitFeedTests(DatabaseFixture fixture)
    {
        _dbOptions = fixture.DbOptions;
        _ctx = new(_dbOptions);
    }

    public async ValueTask InitializeAsync()
    {
        _ctx.SavedStories.RemoveRange(_ctx.SavedStories);
        _ctx.ArticlePresenters.RemoveRange(_ctx.ArticlePresenters);
        _ctx.Articles.RemoveRange(_ctx.Articles);
        _ctx.UserProfiles.RemoveRange(_ctx.UserProfiles);
        _ctx.RssItems.RemoveRange(_ctx.RssItems);
        await _ctx.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private Article AddArticle(string name, int sentiment, DateTimeOffset createdAt, DateTimeOffset? publishedAt = null)
    {
        var link = $"{name}_{Guid.NewGuid():N}";
        var article = new Article
        {
            Link = link,
            Sentiment = sentiment,
            CreatedAt = createdAt,
            RssItem = new RssItem { Link = link + "rss", PubDate = publishedAt }
        };
        _ctx.Articles.Add(article);
        return article;
    }

    private async Task<RelevantArticlesResponse> OpenFeedAsync(TrumanDbContext ctx, string email)
    {
        var profile = await ctx.UserProfiles.Include(u => u.TagPreferences).SingleAsync(u => u.Email == email);
        var service = new RelevantArticlesService(ctx, NullLogger<RelevantArticlesService>.Instance, _clock);
        return await service.GetRelevantArticlesAsync(new RelevantArticlesRequest(), profile);
    }

    [Fact]
    public async Task StoriesAddedSinceTheLastVisitAreNew_JudgedByWhenTruman_AddedThem()
    {
        var email = $"user_{Guid.NewGuid()}@example.com";
        _ctx.UserProfiles.Add(new UserProfile { Email = email, Mood = 0 });
        var lastVisit = _clock.GetUtcNow();
        AddArticle("before", 5, lastVisit.AddMinutes(-10));
        AddArticle("oldNews", 5, lastVisit.AddHours(2), publishedAt: lastVisit.AddDays(-7));
        await _ctx.SaveChangesAsync();

        var first = await OpenFeedAsync(_ctx, email);
        Assert.Null(first.NewSince);
        Assert.All(first.Articles, a => Assert.False(a.IsNew));

        _clock.Advance(TimeSpan.FromHours(3));
        var back = await OpenFeedAsync(_ctx, email);

        Assert.Equal(lastVisit, back.NewSince);
        Assert.True(Assert.Single(back.Articles, a => a.Link.StartsWith("oldNews_")).IsNew);
        Assert.False(Assert.Single(back.Articles, a => a.Link.StartsWith("before_")).IsNew);
    }

    [Fact]
    public async Task TheLineFollowsTheReader_NotTheDevice()
    {
        var email = $"user_{Guid.NewGuid()}@example.com";
        _ctx.UserProfiles.Add(new UserProfile { Email = email });
        await _ctx.SaveChangesAsync();
        var phoneVisit = _clock.GetUtcNow();

        await using (var phone = new TrumanDbContext(_dbOptions))
            await OpenFeedAsync(phone, email);

        _clock.Advance(TimeSpan.FromHours(2));
        await using var laptop = new TrumanDbContext(_dbOptions);
        var resp = await OpenFeedAsync(laptop, email);

        Assert.Equal(phoneVisit, resp.NewSince);
    }

    [Fact]
    public async Task EachSectionKeepsItsRelevanceOrder()
    {
        var email = $"user_{Guid.NewGuid()}@example.com";
        _ctx.UserProfiles.Add(new UserProfile { Email = email, Mood = 0, FeedSeenAt = _clock.GetUtcNow() });
        var line = _clock.GetUtcNow();
        AddArticle("old6", 6, line.AddHours(-1));
        AddArticle("new9", 9, line.AddMinutes(30));
        AddArticle("old8", 8, line.AddHours(-2));
        AddArticle("new3", 3, line.AddMinutes(40));
        await _ctx.SaveChangesAsync();

        _clock.Advance(TimeSpan.FromHours(2));
        var resp = await OpenFeedAsync(_ctx, email);

        Assert.Equal(["new9", "new3"], resp.Articles.Where(a => a.IsNew).Select(a => a.Link.Split('_')[0]));
        Assert.Equal(["old8", "old6"], resp.Articles.Where(a => !a.IsNew).Select(a => a.Link.Split('_')[0]));
    }

    [Fact]
    public async Task TheMigrationAddsTheLastVisit_WithoutTouchingExistingSettings()
    {
        var connection = new NpgsqlConnectionStringBuilder(_ctx.Database.GetConnectionString())
        {
            Database = $"migration_{Guid.NewGuid():N}"
        };
        var options = new DbContextOptionsBuilder<TrumanDbContext>().UseNpgsql(connection.ConnectionString).Options;
        await using var db = new TrumanDbContext(options);
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync("20261111101741_AddSavedStories");
        await db.Database.ExecuteSqlRawAsync(
            """insert into "UserProfiles" ("Email", "Mood", "SelectedValues") values ('reader@example.com', 3, '["freedom"]')""");
        await migrator.MigrateAsync();

        var profile = await db.UserProfiles.SingleAsync();
        Assert.Equal(("reader@example.com", 3, """["freedom"]"""), (profile.Email, profile.Mood, profile.SelectedValues));
        Assert.Null(profile.FeedSeenAt);
        Assert.Null(profile.NewSince);
        await db.Database.EnsureDeletedAsync();
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
