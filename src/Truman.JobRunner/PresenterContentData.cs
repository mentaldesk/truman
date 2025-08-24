using System.Text.Json.Serialization;

namespace Truman.JobRunner;

/// <summary>
/// Data class for JSON deserialization of presenter-specific content responses
/// </summary>
public class PresenterContentData
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("tldr")]
    public string Tldr { get; set; } = string.Empty;
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
} 