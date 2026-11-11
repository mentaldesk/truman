using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.JobRunner.Samples;

public sealed record SampleSet(SampleReader Reader, SampleSource[] Sources, SampleStory[] Stories)
{
    public static SampleSet Read(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SampleSet>(json, JsonOptions)
               ?? throw new InvalidOperationException($"No sample stories in {path}.");
    }

    public static SampleSet ReadBundled() => Read(Path.Combine(AppContext.BaseDirectory, "Samples", "SampleStories.json"));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
}

public sealed record SampleReader(string Email, int Mood, string[] Values, string[] FollowedTags, string[] BannedTags);

public sealed record SampleSource(string Name, string Url);

public sealed record SampleStory(
    string Slug,
    string Source,
    int MinutesAgo,
    int Tone,
    string[] Tags,
    Dictionary<string, int> Values,
    Dictionary<string, SampleContent> Presenters)
{
    public string Link => $"https://samples.invalid/stories/{Slug}";
}

public sealed record SampleContent(string Title, string Tldr, string Content);

/// Writes made-up stories straight to the database for `scripts/try.sh`; nothing else should call it.
public class SampleStoryLoader(ILogger<SampleStoryLoader> logger, IDbContextFactory<TrumanDbContext> dbFactory)
{
    public static readonly IReadOnlyDictionary<string, Action<Article, int>> ValueSetters = new Dictionary<string, Action<Article, int>>
    {
        ["freedom"] = (a, v) => a.Freedom = v,
        ["independence"] = (a, v) => a.Independence = v,
        ["self-respect"] = (a, v) => a.SelfRespect = v,
        ["self-actualization"] = (a, v) => a.SelfActualization = v,
        ["creativity"] = (a, v) => a.Creativity = v,
        ["honesty"] = (a, v) => a.Honesty = v,
        ["compassion"] = (a, v) => a.Compassion = v,
        ["loyalty"] = (a, v) => a.Loyalty = v,
        ["justice"] = (a, v) => a.Justice = v,
        ["responsibility"] = (a, v) => a.Responsibility = v,
        ["security"] = (a, v) => a.Security = v,
        ["equality"] = (a, v) => a.Equality = v,
        ["tradition"] = (a, v) => a.Tradition = v,
        ["obedience"] = (a, v) => a.Obedience = v,
        ["success"] = (a, v) => a.Success = v,
        ["ambition"] = (a, v) => a.Ambition = v,
        ["discipline"] = (a, v) => a.Discipline = v,
        ["knowledge"] = (a, v) => a.Knowledge = v,
        ["open-mindedness"] = (a, v) => a.OpenMindedness = v,
        ["peace-of-mind"] = (a, v) => a.PeaceOfMind = v,
        ["pleasure"] = (a, v) => a.Pleasure = v,
        ["connection"] = (a, v) => a.Connection = v,
        ["adventure"] = (a, v) => a.Adventure = v,
    };

    public async Task<int> LoadAsync(SampleSet samples, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var feeds = new Dictionary<string, Feed>();
        foreach (var source in samples.Sources)
        {
            feeds[source.Name] = await db.Feeds.FirstOrDefaultAsync(f => f.Url == source.Url, cancellationToken)
                                 ?? db.Feeds.Add(new Feed { Name = source.Name, Url = source.Url, IsEnabled = false }).Entity;
        }

        var presenters = await db.Presenters.ToListAsync(cancellationToken);
        var existing = (await db.RssItems.Select(r => r.Link).ToListAsync(cancellationToken)).ToHashSet();

        var added = 0;
        foreach (var story in samples.Stories.Where(s => !existing.Contains(s.Link)))
        {
            var item = new RssItem
            {
                Link = story.Link,
                Title = story.Presenters["Default"].Title,
                Description = story.Presenters["Default"].Content,
                PubDate = now.AddMinutes(-story.MinutesAgo),
                TimeAnalysed = now,
                Feed = feeds[story.Source]
            };
            var article = new Article
            {
                Link = story.Link,
                Sentiment = story.Tone,
                Tags = story.Tags,
                CreatedAt = now,
                RssItem = item
            };
            foreach (var (value, score) in story.Values)
            {
                ValueSetters[value](article, score);
            }
            foreach (var presenter in presenters)
            {
                if (story.Presenters.TryGetValue(presenter.Label, out var content))
                {
                    article.ArticlePresenters.Add(new ArticlePresenter
                    {
                        Presenter = presenter,
                        Title = content.Title,
                        Tldr = content.Tldr,
                        Content = content.Content
                    });
                }
            }
            db.Articles.Add(article);
            added++;
        }

        await AddReaderAsync(db, samples.Reader, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Added {Added} of {Total} sample stories", added, samples.Stories.Length);
        return added;
    }

    private static async Task AddReaderAsync(TrumanDbContext db, SampleReader reader, CancellationToken cancellationToken)
    {
        if (await db.UserProfiles.AnyAsync(p => p.Email == reader.Email, cancellationToken))
        {
            return;
        }

        var profile = new UserProfile
        {
            Email = reader.Email,
            Mood = reader.Mood,
            SelectedValues = JsonSerializer.Serialize(reader.Values)
        };
        foreach (var tag in reader.FollowedTags)
        {
            profile.TagPreferences.Add(new UserTagPreference { Tag = tag, Weight = 1 });
        }
        foreach (var tag in reader.BannedTags)
        {
            profile.TagPreferences.Add(new UserTagPreference { Tag = tag, Weight = 0 });
        }
        db.UserProfiles.Add(profile);
    }
}
