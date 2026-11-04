using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Truman.JobRunner.Jev;

namespace Truman.JobRunner.Tests.TestSupport;

/// JevArticleScorer and JevClient are both sealed, so the real pair is driven over a stub transport.
public static class JevStub
{
    public static JevArticleScorer Scorer() =>
        new(new JevClient(new HttpClient(new EchoAnswersHandler())
        {
            BaseAddress = new Uri("https://jev.invalid/"),
        }));

    /// Answers whatever questions the scorer asked, so the value list stays the source of truth.
    private sealed class EchoAnswersHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var questions = JsonNode.Parse(body)!["questions"]!.AsObject();

            var answers = new JsonObject();
            foreach (var (name, question) in questions)
            {
                answers[name] = question!["type"]!.GetValue<string>() == "score"
                    ? new JsonObject { ["type"] = "score", ["score"] = 2.0 }
                    : new JsonObject { ["type"] = "noul", ["noul"] = 0.5 };
            }

            var payload = new JsonObject { ["model"] = "jev-latest", ["answers"] = answers };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
            };
        }
    }
}
