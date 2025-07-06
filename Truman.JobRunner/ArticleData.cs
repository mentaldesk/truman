using System.Text.Json.Serialization;

namespace Truman.JobRunner;

/// <summary>
/// Data class for JSON deserialization of article analysis responses
/// </summary>
public class ArticleData
{
    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("tldr")]
    public string Tldr { get; set; } = string.Empty;
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    [JsonPropertyName("sentiment")]
    public int Sentiment { get; set; }
    
    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = Array.Empty<string>();
    
    [JsonPropertyName("freedom")]
    public int Freedom { get; set; }
    
    [JsonPropertyName("independence")]
    public int Independence { get; set; }
    
    [JsonPropertyName("self-respect")]
    public int SelfRespect { get; set; }
    
    [JsonPropertyName("self-actualization")]
    public int SelfActualization { get; set; }
    
    [JsonPropertyName("creativity")]
    public int Creativity { get; set; }
    
    [JsonPropertyName("honesty")]
    public int Honesty { get; set; }
    
    [JsonPropertyName("compassion")]
    public int Compassion { get; set; }
    
    [JsonPropertyName("loyalty")]
    public int Loyalty { get; set; }
    
    [JsonPropertyName("justice")]
    public int Justice { get; set; }
    
    [JsonPropertyName("responsibility")]
    public int Responsibility { get; set; }
    
    [JsonPropertyName("security")]
    public int Security { get; set; }
    
    [JsonPropertyName("equality")]
    public int Equality { get; set; }
    
    [JsonPropertyName("tradition")]
    public int Tradition { get; set; }
    
    [JsonPropertyName("obedience")]
    public int Obedience { get; set; }
    
    [JsonPropertyName("success")]
    public int Success { get; set; }
    
    [JsonPropertyName("ambition")]
    public int Ambition { get; set; }
    
    [JsonPropertyName("discipline")]
    public int Discipline { get; set; }
    
    [JsonPropertyName("knowledge")]
    public int Knowledge { get; set; }
    
    [JsonPropertyName("open-mindedness")]
    public int OpenMindedness { get; set; }
    
    [JsonPropertyName("peace-of-mind")]
    public int PeaceOfMind { get; set; }
    
    [JsonPropertyName("pleasure")]
    public int Pleasure { get; set; }
    
    [JsonPropertyName("connection")]
    public int Connection { get; set; }
    
    [JsonPropertyName("adventure")]
    public int Adventure { get; set; }
}