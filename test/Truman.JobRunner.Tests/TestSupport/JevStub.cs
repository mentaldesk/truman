using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Truman.JobRunner.Jev;

namespace Truman.JobRunner.Tests.TestSupport;

/// JevArticleScorer and JevClient are both sealed, so the real pair is driven over a stub transport.
public static class JevStub
{
    public const int InputTokens = 11;
    public const int OutputTokens = 7;

    public static JevArticleScorer Scorer(Func<JsonObject, JsonObject>? respond = null) =>
        new(new JevClient(new HttpClient(new StubHandler(respond ?? (q => Payload(Answers(q)))))
        {
            BaseAddress = new Uri("https://jev.invalid/"),
        }));

    public static JevArticleScorer FailingScorer(HttpStatusCode status) =>
        new(new JevClient(new HttpClient(new StubHandler(status))
        {
            BaseAddress = new Uri("https://jev.invalid/"),
        }));

    /// Answers whatever questions the scorer asked, so the value list stays the source of truth.
    public static JsonObject Answers(JsonObject questions, double noul = 0.5, double score = 2.0)
    {
        var answers = new JsonObject();
        foreach (var (name, question) in questions)
        {
            answers[name] = question!["type"]!.GetValue<string>() == "score"
                ? new JsonObject { ["type"] = "score", ["score"] = score }
                : new JsonObject { ["type"] = "noul", ["noul"] = noul };
        }

        return answers;
    }

    public static JsonObject Payload(JsonObject answers, bool withUsage = true)
    {
        var payload = new JsonObject { ["model"] = JevClient.Model, ["answers"] = answers };
        if (withUsage)
        {
            payload["usage"] = new JsonObject
            {
                ["input_tokens"] = InputTokens,
                ["output_tokens"] = OutputTokens,
            };
        }

        return payload;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<JsonObject, JsonObject>? _respond;
        private readonly HttpStatusCode _status;

        public StubHandler(Func<JsonObject, JsonObject> respond)
        {
            _respond = respond;
            _status = HttpStatusCode.OK;
        }

        public StubHandler(HttpStatusCode status) => _status = status;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_respond is null)
            {
                return new HttpResponseMessage(_status) { Content = new StringContent(string.Empty) };
            }

            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var questions = JsonNode.Parse(body)!["questions"]!.AsObject();

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(
                    _respond(questions).ToJsonString(), Encoding.UTF8, "application/json"),
            };
        }
    }
}
