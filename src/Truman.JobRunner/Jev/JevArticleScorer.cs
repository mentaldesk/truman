using System.Text.Json;
using System.Text.Json.Serialization;

namespace Truman.JobRunner.Jev;

public sealed record ValueDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("description")] string Description);

/// Scores an article against the configured values, plus sentiment, in one Jev request.
public sealed class JevArticleScorer(JevClient client)
{
    // Positions map to 0-4; the result is rescaled to the 0-10 the schema uses.
    private static readonly string[] SentimentLevels =
        ["Very negative", "Negative", "Neutral", "Positive", "Very positive"];

    public const string SentimentKey = "sentiment";

    private readonly IReadOnlyList<ValueDefinition> _values = LoadValues();

    public async Task<IReadOnlyDictionary<string, int>> ScoreAsync(
        string article,
        CancellationToken cancellationToken = default)
    {
        var questions = new Dictionary<string, JevQuestion>(_values.Count + 1);

        foreach (var value in _values)
        {
            questions[value.Id] = JevQuestion.Noul(
                $"Is this article related to the value '{value.Id}' ({value.Description})?");
        }

        questions[SentimentKey] = JevQuestion.Score(
            "How positive is the overall tone of this article?", SentimentLevels);

        var response = await client.AskAsync(article, questions, cancellationToken);

        var scores = new Dictionary<string, int>(response.Answers.Count);
        foreach (var (name, answer) in response.Answers)
        {
            scores[name] = name == SentimentKey
                ? Rescale(answer.Score, SentimentLevels.Length)
                : ToScore(answer.Noul);
        }

        return scores;
    }

    // A noul answer is the probability the statement holds, so it maps straight onto 0-10.
    private static int ToScore(double? probability) =>
        probability is { } value ? Math.Clamp((int)Math.Round(value * 10), 0, 10) : 0;

    private static int Rescale(double? score, int levelCount) =>
        score is { } value && levelCount > 1
            ? Math.Clamp((int)Math.Round(value / (levelCount - 1) * 10), 0, 10)
            : 0;

    private static IReadOnlyList<ValueDefinition> LoadValues()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Values.json");
        return JsonSerializer.Deserialize<List<ValueDefinition>>(File.ReadAllText(path))
               ?? throw new InvalidOperationException($"No values found in {path}.");
    }
}
