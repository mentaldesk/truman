using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Truman.JobRunner.Tests.TestSupport;

namespace Truman.JobRunner.Tests;

/// The spine DescribeStructure prepends to every presenter prompt, asserted on the prompt itself.
[Collection(JobRunnerDatabaseCollection.Name)]
public class ArticleStructureTests(JobRunnerDatabaseFixture fixture) : ArticleAnalyserTestBase(fixture)
{
    private const string Link = "https://example.invalid/story";
    private const string Style = "a terse newsreader";

    private const string BareArticle = """
        Title: A headline
        Link: https://example.invalid/story

        Body text.
        """;

    private const string PresenterReply = """{"title":"T","tldr":"L","content":"C"}""";

    private static string Analysis(
        string? claim = "Rates will rise.",
        string[]? support = null,
        string[]? padding = null,
        string? headlineGap = null,
        string? sourceDepth = null)
    {
        var node = new JsonObject
        {
            ["tags"] = new JsonArray("economy"),
            ["claim"] = claim,
            ["support"] = ToArray(support),
            ["padding"] = ToArray(padding),
            ["headline_gap"] = headlineGap,
            ["source_depth"] = sourceDepth,
        };
        return node.ToJsonString();

        static JsonNode? ToArray(string[]? values) =>
            values is null ? null : new JsonArray([.. values.Select(v => (JsonNode?)JsonValue.Create(v))]);
    }

    public static IEnumerable<TheoryDataRow<string?>> BlankOrMissing =>
        [new(null), new(""), new("   "), new("\t\n ")];

    public static IEnumerable<TheoryDataRow<string[]?>> EmptyOrMissingList =>
        [new(null), new([]), new([""]), new(["   ", "\t"])];

    private async Task<string> PresenterPromptFor(string analysisJson)
    {
        await Db.SeedPendingAsync(Link);
        await Db.SeedPresenterAsync(Style);

        Chat.Then(analysisJson).Always(PresenterReply);

        await Analyser().RunAsync();

        Assert.Equal(2, Chat.Calls.Count);
        return Chat.Calls[1].UserText;
    }

    [Theory]
    [MemberData(nameof(BlankOrMissing))]
    public async Task A_missing_claim_falls_back_to_the_bare_article(string? claim)
    {
        var prompt = await PresenterPromptFor(Analysis(
            claim: claim,
            support: ["a figure"],
            padding: ["a quote"],
            headlineGap: "withholds",
            sourceDepth: "full"));

        Assert.Equal($"Rewrite the following article in the style of {Style}.\n\n{BareArticle}", prompt);
        Assert.DoesNotContain("Structural analysis", prompt);
    }

    [Fact]
    public async Task A_missing_claim_is_not_recorded_as_a_failure()
    {
        await Db.SeedPendingAsync(Link);
        await Db.SeedPresenterAsync(Style);

        Chat.Then(Analysis(claim: null)).Always(PresenterReply);

        Assert.Equal(new AnalysisOutcome(1, 1, 0), await Analyser().RunAsync());

        await using var db = Db.NewContext();
        Assert.Equal(1, await db.Articles.CountAsync());
    }

    [Fact]
    public async Task A_full_analysis_renders_every_spine_line()
    {
        var prompt = await PresenterPromptFor(Analysis(
            claim: "Rates will rise.",
            support: ["a central bank figure", "two economists"],
            padding: ["a celebrity quote"],
            headlineGap: "withholds",
            sourceDepth: "full"));

        Assert.Equal($"""
            Rewrite the following article in the style of {Style}.

            Structural analysis of the article:
            - Claim: Rates will rise.
            - Support: a central bank figure; two economists
            - Padding, do not spend words on: a celebrity quote
            - Headline relation to claim: withholds
            - Source depth: full

            {BareArticle}
            """, prompt);
    }

    [Fact]
    public async Task A_payload_carrying_only_tags_and_claim_renders_claim_and_support_only()
    {
        var prompt = await PresenterPromptFor("""{"tags":["economy"],"claim":"Rates will rise."}""");

        Assert.Equal($"""
            Rewrite the following article in the style of {Style}.

            Structural analysis of the article:
            - Claim: Rates will rise.
            - Support: (none identified)

            {BareArticle}
            """, prompt);
    }

    [Theory]
    [MemberData(nameof(EmptyOrMissingList))]
    public async Task Absent_support_renders_the_none_identified_placeholder(string[]? support)
    {
        var prompt = await PresenterPromptFor(Analysis(support: support));

        Assert.Contains("- Support: (none identified)", prompt);
    }

    [Fact]
    public async Task Support_entries_are_joined_and_blank_entries_dropped()
    {
        var prompt = await PresenterPromptFor(Analysis(support: ["first", "  ", "second"]));

        Assert.Contains("- Support: first; second", prompt);
    }

    [Theory]
    [MemberData(nameof(EmptyOrMissingList))]
    public async Task The_padding_line_is_omitted_when_padding_is_absent(string[]? padding)
    {
        var prompt = await PresenterPromptFor(Analysis(padding: padding));

        Assert.DoesNotContain("Padding", prompt);
    }

    [Fact]
    public async Task The_padding_line_is_present_when_padding_is_set()
    {
        var prompt = await PresenterPromptFor(Analysis(padding: ["colour piece", "a photo caption"]));

        Assert.Contains("- Padding, do not spend words on: colour piece; a photo caption", prompt);
    }

    [Theory]
    [MemberData(nameof(BlankOrMissing))]
    public async Task The_headline_gap_line_is_omitted_when_the_field_is_absent(string? headlineGap)
    {
        var prompt = await PresenterPromptFor(Analysis(headlineGap: headlineGap));

        Assert.DoesNotContain("Headline relation to claim", prompt);
    }

    [Fact]
    public async Task The_headline_gap_line_is_present_when_the_field_is_set()
    {
        var prompt = await PresenterPromptFor(Analysis(headlineGap: "overstates"));

        Assert.Contains("- Headline relation to claim: overstates", prompt);
    }

    [Theory]
    [MemberData(nameof(BlankOrMissing))]
    public async Task The_source_depth_line_is_omitted_when_the_field_is_absent(string? sourceDepth)
    {
        var prompt = await PresenterPromptFor(Analysis(sourceDepth: sourceDepth));

        Assert.DoesNotContain("Source depth", prompt);
    }

    [Fact]
    public async Task The_source_depth_line_is_present_when_the_field_is_set()
    {
        var prompt = await PresenterPromptFor(Analysis(sourceDepth: "summary"));

        Assert.Contains("- Source depth: summary", prompt);
    }
}
