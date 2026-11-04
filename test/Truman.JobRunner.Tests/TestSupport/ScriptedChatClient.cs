using Microsoft.Extensions.AI;

namespace Truman.JobRunner.Tests.TestSupport;

public sealed record RecordedCall(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)
{
    public string SystemText => TextOf(ChatRole.System);
    public string UserText => TextOf(ChatRole.User);

    private string TextOf(ChatRole role) =>
        string.Concat(Messages.Where(m => m.Role == role).Select(m => m.Text));
}

/// Returns queued responses in order, then repeats the last one. Records every request.
public sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<ChatResponse> _queued = new();
    private ChatResponse _fallback = Response("{}");

    public List<RecordedCall> Calls { get; } = [];

    public ScriptedChatClient Then(string text, ChatFinishReason? finishReason = null)
    {
        _queued.Enqueue(Response(text, finishReason));
        return this;
    }

    public ScriptedChatClient Always(string text, ChatFinishReason? finishReason = null)
    {
        _fallback = Response(text, finishReason);
        return this;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(new RecordedCall([.. messages], options));
        return Task.FromResult(_queued.Count > 0 ? _queued.Dequeue() : _fallback);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("ArticleAnalyser does not stream.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    private static ChatResponse Response(string text, ChatFinishReason? finishReason = null) =>
        new(new ChatMessage(ChatRole.Assistant, text)) { FinishReason = finishReason ?? ChatFinishReason.Stop };
}
