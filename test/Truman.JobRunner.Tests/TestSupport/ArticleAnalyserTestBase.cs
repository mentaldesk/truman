using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Testing;

namespace Truman.JobRunner.Tests.TestSupport;

public abstract class ArticleAnalyserTestBase(JobRunnerDatabaseFixture fixture) : IAsyncLifetime
{
    protected const string Model = "claude-haiku-5-5";

    protected JobRunnerDatabaseFixture Db { get; } = fixture;
    protected ScriptedChatClient Chat { get; } = new();
    protected FakeLogger<ArticleAnalyser> Logger { get; } = new();

    protected string LoggedText => string.Join("\n", Logger.Collector.GetSnapshot().Select(r => r.Message));

    public async ValueTask InitializeAsync() => await Db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected ArticleAnalyser Analyser(IChatClient? chatClient = null) =>
        new(Logger, Db.ContextFactory, chatClient ?? Chat, JevStub.Scorer(), Model);
}
