using System.Text.Json.Serialization;

namespace Truman.JobRunner;

/// <summary>
/// Data class for JSON deserialization of article content responses
/// </summary>
public class ArticleContent
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}