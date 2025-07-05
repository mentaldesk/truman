using System;

namespace Truman.Data.Entities;

public class Article
{
    public int Id { get; set; }
    public string Link { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Tldr { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Sentiment { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    
    // Value scores (0-10)
    public int Freedom { get; set; }
    public int Independence { get; set; }
    public int SelfRespect { get; set; }
    public int SelfActualization { get; set; }
    public int Creativity { get; set; }
    public int Honesty { get; set; }
    public int Compassion { get; set; }
    public int Loyalty { get; set; }
    public int Justice { get; set; }
    public int Responsibility { get; set; }
    public int Security { get; set; }
    public int Equality { get; set; }
    public int Tradition { get; set; }
    public int Obedience { get; set; }
    public int Success { get; set; }
    public int Ambition { get; set; }
    public int Discipline { get; set; }
    public int Knowledge { get; set; }
    public int OpenMindedness { get; set; }
    public int PeaceOfMind { get; set; }
    public int Pleasure { get; set; }
    public int Connection { get; set; }
    public int Adventure { get; set; }
    
    // Metadata
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int RssItemId { get; set; }
    public RssItem RssItem { get; set; } = null!;
} 