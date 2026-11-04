using System.Text.Json;
using System.Text.Json.Serialization;
using Sentry;

namespace Truman.JobRunner.Jev;

public sealed record ValueDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("description")] string Description);

/// MissingAnswers counts the questions Jev did not answer usably. Those score 0, which is
/// otherwise indistinguishable from a genuine 0.
public sealed record JevScores(IReadOnlyDictionary<string, int> Values, int MissingAnswers);

/// Scores an article against the configured values, plus sentiment, in one Jev request.
public sealed class JevArticleScorer(JevClient client)
{
    // Positions map to 0-4; the result is rescaled to the 0-10 the schema uses.
    private static readonly string[] SentimentLevels =
        ["Very negative", "Negative", "Neutral", "Positive", "Very positive"];

    public const string SentimentKey = "sentiment";

    private readonly IReadOnlyList<ValueDefinition> _values = LoadValues();

    public Task<JevScores> ScoreAsync(string article, CancellationToken cancellationToken = default) =>
        ScoreAsync(article, SentrySdk.GetSpan(), cancellationToken);

    internal async Task<JevScores> ScoreAsync(string article, ISpan? parent, CancellationToken cancellationToken)
    {
        var questions = BuildQuestions();

        // Sentry's gen_ai attributes, so this call sits beside the Anthropic ones in the AI views.
        var span = parent?.StartChild("gen_ai.chat", $"chat {JevClient.Model}");
        span?.SetData("gen_ai.operation.name", "chat");
        span?.SetData("gen_ai.request.model", JevClient.Model);
        span?.SetData("jev.questions", questions.Count);

        JevResponse response;
        try
        {
            response = await client.AskAsync(article, questions, cancellationToken);
        }
        catch (Exception ex)
        {
            span?.Finish(ex);
            throw;
        }

        var scores = new Dictionary<string, int>(questions.Count);
        var missingAnswers = 0;
        foreach (var name in questions.Keys)
        {
            if (Answer(response, name) is { } score)
            {
                scores[name] = score;
                continue;
            }

            scores[name] = 0;
            missingAnswers++;
        }

        span?.SetData("gen_ai.response.model", response.Model);
        if (response.Usage is { } usage)
        {
            span?.SetData("gen_ai.usage.input_tokens", usage.InputTokens);
            span?.SetData("gen_ai.usage.output_tokens", usage.OutputTokens);
            span?.SetData("gen_ai.usage.total_tokens", usage.InputTokens + usage.OutputTokens);
        }
        span?.SetData("jev.answers.missing", missingAnswers);
        span?.Finish(SpanStatus.Ok);

        return new JevScores(scores, missingAnswers);
    }

    private Dictionary<string, JevQuestion> BuildQuestions()
    {
        var questions = new Dictionary<string, JevQuestion>(_values.Count + 1);

        foreach (var value in _values)
        {
            questions[value.Id] = JevQuestion.Noul(
                $"Is this article related to the value '{value.Id}' ({value.Description})?");
        }

        questions[SentimentKey] = JevQuestion.Score(
            "How positive is the overall tone of this article?", SentimentLevels);

        return questions;
    }

    private static int? Answer(JevResponse response, string name)
    {
        if (!response.Answers.TryGetValue(name, out var answer))
        {
            return null;
        }

        return name == SentimentKey
            ? Rescale(answer.Score, SentimentLevels.Length)
            : ToScore(answer.Noul);
    }

    // A noul answer is the probability the statement holds, so it maps straight onto 0-10.
    private static int? ToScore(double? probability) =>
        probability is { } value ? Math.Clamp((int)Math.Round(value * 10), 0, 10) : null;

    private static int? Rescale(double? score, int levelCount) =>
        score is { } value && levelCount > 1
            ? Math.Clamp((int)Math.Round(value / (levelCount - 1) * 10), 0, 10)
            : null;

    private static IReadOnlyList<ValueDefinition> LoadValues()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Values.json");
        return JsonSerializer.Deserialize<List<ValueDefinition>>(File.ReadAllText(path))
               ?? throw new InvalidOperationException($"No values found in {path}.");
    }
}
