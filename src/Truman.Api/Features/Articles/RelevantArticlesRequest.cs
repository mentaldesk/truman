namespace Truman.Api.Features.Articles;

public class RelevantArticlesRequest
{
    public string Presenter { get; set; } = string.Empty;
    public int? Mood { get; set; }
} 