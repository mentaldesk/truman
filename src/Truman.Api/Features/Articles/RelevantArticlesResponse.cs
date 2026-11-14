namespace Truman.Api.Features.Articles;

public class RelevantArticlesResponse
{
    public List<RelevantArticle> Articles { get; set; } = new();
}

public class RelevantArticle
{
    public int Id { get; set; }
    public string Link { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Tldr { get; set; } = string.Empty;
    // Content is now the first presenter's content for backward compatibility
    public string Content { get; set; } = string.Empty;
    public int Sentiment { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public double RelevanceScore { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? Publisher { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public RelevanceBreakdown Why { get; set; } = new();
    public bool IsSaved { get; set; }
}

public class RelevanceBreakdown
{
    public double ValuesShare { get; set; }
    public double TagsShare { get; set; }
    public double ToneShare { get; set; }
    public string[] MatchedValues { get; set; } = [];
    public string[] MatchedTags { get; set; } = [];
    public int Mood { get; set; }
} 