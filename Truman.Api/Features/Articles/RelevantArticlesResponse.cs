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
    public string Content { get; set; } = string.Empty;
    public int Sentiment { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public double RelevanceScore { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
} 