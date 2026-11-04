using System.Text.Json.Serialization;

namespace Truman.JobRunner.Jev;

public sealed class JevQuestion
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("instructions")]
    public required string Instructions { get; init; }

    // Ordered level descriptions for a score question; a noul question has none.
    [JsonPropertyName("criteria")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Criteria { get; init; }

    public static JevQuestion Noul(string instructions) =>
        new() { Type = "noul", Instructions = instructions };

    public static JevQuestion Score(string instructions, IReadOnlyList<string> levels) =>
        new() { Type = "score", Instructions = instructions, Criteria = levels };
}

public sealed class JevRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("questions")]
    public required IReadOnlyDictionary<string, JevQuestion> Questions { get; init; }
}

public sealed class JevAnswer
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    // Probability that the statement is true, 0-1.
    [JsonPropertyName("noul")]
    public double? Noul { get; init; }

    // Probability-weighted average over criteria positions, 0 to criteria.Count - 1.
    [JsonPropertyName("score")]
    public double? Score { get; init; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; init; }
}

public sealed class JevResponse
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("answers")]
    public Dictionary<string, JevAnswer> Answers { get; init; } = [];

    [JsonPropertyName("usage")]
    public JevUsage? Usage { get; init; }
}

public sealed class JevUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }
}
