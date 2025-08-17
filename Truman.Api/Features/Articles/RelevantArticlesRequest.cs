namespace Truman.Api.Features.Articles;

public class RelevantArticlesRequest
{
    public int MinimumSentiment { get; set; }
    public List<string> SelectedValues { get; set; } = new();
    public string Presenter { get; set; } = string.Empty;
} 