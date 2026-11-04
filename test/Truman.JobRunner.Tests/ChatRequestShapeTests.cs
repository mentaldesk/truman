using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Anthropic;
using Microsoft.Extensions.AI;
using Truman.JobRunner.Tests.TestSupport;

namespace Truman.JobRunner.Tests;

/// What BuildOptions actually puts on the wire. ResponseFormat and Reasoning both land in
/// output_config, so an SDK bump could drop either one without a compile error: losing format
/// breaks structured outputs, losing effort silently triples the thinking spend.
[Collection(JobRunnerDatabaseCollection.Name)]
public class ChatRequestShapeTests(JobRunnerDatabaseFixture fixture) : ArticleAnalyserTestBase(fixture)
{
    // One body that satisfies both deserialisers, so a single canned reply serves both calls.
    private const string CannedReply =
        """{"tags":["economy"],"claim":"Rates will rise.","support":["a figure"],"title":"T","tldr":"L","content":"C"}""";

    private async Task<List<JsonObject>> CapturedRequestBodies()
    {
        await Db.SeedPendingAsync("https://example.invalid/story");
        await Db.SeedPresenterAsync();

        var recorder = new RecordingHandler(Model, CannedReply);
        var anthropic = new AnthropicClient
        {
            ApiKey = "not-a-real-key",
            BaseUrl = "http://anthropic.invalid",
            MaxRetries = 0,
            HttpClient = new HttpClient(recorder),
        };

        await Analyser(anthropic.AsIChatClient(defaultModelId: Model)).RunAsync();

        Assert.Equal(2, recorder.Bodies.Count);
        return [.. recorder.Bodies.Select(b => JsonNode.Parse(b)!.AsObject())];
    }

    [Fact]
    public async Task Both_calls_send_a_json_schema_format_and_a_low_effort_together()
    {
        var bodies = await CapturedRequestBodies();

        foreach (var body in bodies)
        {
            var outputConfig = Assert.IsType<JsonObject>(body["output_config"]);

            Assert.Equal("json_schema", outputConfig["format"]?["type"]?.GetValue<string>());
            Assert.Equal("low", outputConfig["effort"]?.GetValue<string>());
        }
    }

    [Fact]
    public async Task The_analysis_call_sends_the_article_analysis_schema()
    {
        var bodies = await CapturedRequestBodies();

        var properties = SchemaPropertyNames(bodies[0]);

        Assert.Equal(
            ["claim", "headline_gap", "padding", "source_depth", "support", "tags"],
            properties);
    }

    [Fact]
    public async Task The_presenter_call_sends_the_presenter_content_schema()
    {
        var bodies = await CapturedRequestBodies();

        Assert.Equal(["content", "title", "tldr"], SchemaPropertyNames(bodies[1]));
    }

    [Fact]
    public async Task Both_calls_send_the_configured_model_and_token_ceiling()
    {
        var bodies = await CapturedRequestBodies();

        foreach (var body in bodies)
        {
            Assert.Equal(Model, body["model"]?.GetValue<string>());
            Assert.Equal(10_000, body["max_tokens"]?.GetValue<int>());
        }
    }

    private static string[] SchemaPropertyNames(JsonObject body) =>
        [.. body["output_config"]!["format"]!["schema"]!["properties"]!.AsObject()
            .Select(p => p.Key)
            .Order()];

    private sealed class RecordingHandler(string model, string replyText) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

            var message = new JsonObject
            {
                ["id"] = "msg_1",
                ["type"] = "message",
                ["role"] = "assistant",
                ["model"] = model,
                ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = replyText }),
                ["stop_reason"] = "end_turn",
                ["usage"] = new JsonObject { ["input_tokens"] = 1, ["output_tokens"] = 1 },
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(message.ToJsonString(), Encoding.UTF8, "application/json"),
            };
        }
    }
}
