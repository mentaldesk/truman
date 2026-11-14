using System.Net;
using System.Text.Json.Nodes;
using NSubstitute;
using Sentry;
using Truman.JobRunner.Jev;
using Truman.JobRunner.Tests.TestSupport;

namespace Truman.JobRunner.Tests;

/// The Jev scorer's mapping onto 0-10, its count of unusable answers, and the gen_ai span it records.
public class JevScoringTests
{
    private const string Article = "Title: A headline\nLink: https://example.invalid/story";
    private const string AValue = "freedom";

    // 23 values from Values.json, plus sentiment.
    private const int QuestionCount = 24;

    [Fact]
    public async Task Scores_every_question_asked()
    {
        var scores = await JevStub.Scorer().ScoreAsync(Article);

        Assert.Equal(QuestionCount, scores.Values.Count);
        Assert.Equal(0, scores.MissingAnswers);
        Assert.Contains(JevArticleScorer.SentimentKey, scores.Values.Keys);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.5, 5)]
    [InlineData(0.84, 8)]
    [InlineData(1.0, 10)]
    public async Task Maps_a_noul_probability_onto_0_to_10(double noul, int expected)
    {
        var scorer = JevStub.Scorer(q => JevStub.Payload(JevStub.Answers(q, noul: noul)));

        var scores = await scorer.ScoreAsync(Article);

        Assert.Equal(expected, scores.Values[AValue]);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(2.0, 5)]
    [InlineData(4.0, 10)]
    public async Task Rescales_sentiment_from_its_level_position(double score, int expected)
    {
        var scorer = JevStub.Scorer(q => JevStub.Payload(JevStub.Answers(q, score: score)));

        var scores = await scorer.ScoreAsync(Article);

        Assert.Equal(expected, scores.Values[JevArticleScorer.SentimentKey]);
    }

    [Fact]
    public async Task Counts_an_answer_that_carries_no_value_as_missing()
    {
        var scorer = JevStub.Scorer(q =>
        {
            var answers = JevStub.Answers(q);
            answers[AValue] = new JsonObject { ["type"] = "noul" };
            return JevStub.Payload(answers);
        });

        var scores = await scorer.ScoreAsync(Article);

        Assert.Equal(1, scores.MissingAnswers);
        Assert.Equal(0, scores.Values[AValue]);
    }

    [Fact]
    public async Task Counts_a_question_jev_did_not_answer_as_missing()
    {
        var scorer = JevStub.Scorer(q =>
        {
            var answers = JevStub.Answers(q);
            answers.Remove(AValue);
            return JevStub.Payload(answers);
        });

        var scores = await scorer.ScoreAsync(Article);

        Assert.Equal(1, scores.MissingAnswers);
        Assert.Equal(0, scores.Values[AValue]);
        Assert.Equal(QuestionCount, scores.Values.Count);
    }

    [Fact]
    public async Task Records_the_call_as_a_gen_ai_decide_span()
    {
        var (parent, span) = Spans();

        await JevStub.Scorer().ScoreAsync(Article, parent, CancellationToken.None);

        parent.Received(1).StartChild("gen_ai.decide");
        Assert.Equal($"decide {JevClient.Model}", span.Description);
        span.Received().SetData("gen_ai.operation.name", "decide");
        span.Received().SetData("gen_ai.provider.name", "typesafe");
        span.Received().SetData("gen_ai.request.model", JevClient.Model);
        span.Received().SetData("gen_ai.response.model", JevClient.Model);
        span.Received().SetData("jev.questions", QuestionCount);
        span.Received().SetData("jev.answers.missing", 0);
        span.Received(1).Finish(SpanStatus.Ok);
    }

    [Fact]
    public async Task Records_every_question_with_its_definition_and_the_story_only_as_its_length()
    {
        var (parent, span) = Spans();

        await JevStub.Scorer().ScoreAsync(Article, parent, CancellationToken.None);

        var messages = JsonNode.Parse(Data(span, "gen_ai.input.messages"))!.AsArray();
        var evaluation = Assert.Single(messages)!.AsObject();
        Assert.Equal("evaluation", evaluation["type"]!.GetValue<string>());
        Assert.False(evaluation.ContainsKey("state"));
        var questions = evaluation["questions"]!.AsObject();
        Assert.Equal(QuestionCount, questions.Count);
        Assert.Contains(AValue, questions[AValue]!["instructions"]!.GetValue<string>());
        Assert.Equal("score", questions[JevArticleScorer.SentimentKey]!["type"]!.GetValue<string>());
        Assert.Equal(5, questions[JevArticleScorer.SentimentKey]!["criteria"]!.AsArray().Count);
        Assert.DoesNotContain("A headline", Data(span, "gen_ai.input.messages"));
        span.Received().SetData("jev.state.length", Article.Length);
    }

    [Fact]
    public async Task Records_the_answers_jev_gave()
    {
        var (parent, span) = Spans();
        var scorer = JevStub.Scorer(q =>
        {
            var answers = JevStub.Answers(q, noul: 0.84, score: 3.0);
            answers.Remove(AValue);
            return JevStub.Payload(answers);
        });

        await scorer.ScoreAsync(Article, parent, CancellationToken.None);

        var messages = JsonNode.Parse(Data(span, "gen_ai.output.messages"))!.AsArray();
        var evaluation = Assert.Single(messages)!.AsObject();
        Assert.Equal("evaluation", evaluation["type"]!.GetValue<string>());
        var answers = evaluation["answers"]!.AsObject();
        Assert.Equal(QuestionCount - 1, answers.Count);
        Assert.False(answers.ContainsKey(AValue));
        Assert.Equal(0.84, answers["justice"]!["noul"]!.GetValue<double>());
        Assert.Equal(3.0, answers[JevArticleScorer.SentimentKey]!["score"]!.GetValue<double>());
    }

    [Fact]
    public async Task Records_the_token_usage_jev_reports()
    {
        var (parent, span) = Spans();

        await JevStub.Scorer().ScoreAsync(Article, parent, CancellationToken.None);

        span.Received().SetData("gen_ai.usage.input_tokens", JevStub.InputTokens);
        span.Received().SetData("gen_ai.usage.output_tokens", JevStub.OutputTokens);
        span.Received().SetData("gen_ai.usage.total_tokens", JevStub.InputTokens + JevStub.OutputTokens);
    }

    [Fact]
    public async Task Leaves_the_usage_attributes_off_when_jev_reports_none()
    {
        var (parent, span) = Spans();
        var scorer = JevStub.Scorer(q => JevStub.Payload(JevStub.Answers(q), withUsage: false));

        await scorer.ScoreAsync(Article, parent, CancellationToken.None);

        span.DidNotReceive().SetData("gen_ai.usage.input_tokens", Arg.Any<object>());
        span.DidNotReceive().SetData("gen_ai.usage.total_tokens", Arg.Any<object>());
    }

    [Fact]
    public async Task Counts_every_question_as_missing_when_jev_answers_nothing()
    {
        var scorer = JevStub.Scorer(_ => JevStub.Payload(new JsonObject()));

        var scores = await scorer.ScoreAsync(Article);

        Assert.Equal(QuestionCount, scores.MissingAnswers);
        Assert.All(scores.Values.Values, score => Assert.Equal(0, score));
    }

    [Fact]
    public async Task Finishes_the_span_with_the_exception_when_jev_fails()
    {
        var (parent, span) = Spans();
        var scorer = JevStub.FailingScorer(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => scorer.ScoreAsync(Article, parent, CancellationToken.None));

        span.Received(1).Finish(Arg.Any<HttpRequestException>());
        span.DidNotReceive().Finish(SpanStatus.Ok);
        span.DidNotReceive().SetData("gen_ai.output.messages", Arg.Any<object>());
    }

    private static string Data(ISpan span, string key) =>
        Assert.Single(span.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(ISpan.SetData) && (string)c.GetArguments()[0]! == key)
            .GetArguments()[1]!.ToString()!;

    private static (ISpan Parent, ISpan Child) Spans()
    {
        var parent = Substitute.For<ISpan>();
        var child = Substitute.For<ISpan>();
        parent.StartChild(Arg.Any<string>()).Returns(child);
        return (parent, child);
    }
}
