namespace Truman.Api.Features.Articles;

public class RelevantArticlesRequest
{
    public int MinimumSentiment { get; set; }
    public List<UserValue> SelectedValues { get; set; } = new();
}

public class UserValue
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; } // Zero-based position in the stack rank
} 