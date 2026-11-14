using System.Net;
using System.Text;

namespace Truman.JobRunner.Tests.TestSupport;

public sealed class ScriptedHttpHandler : HttpMessageHandler, IHttpClientFactory
{
    private readonly Queue<Func<byte[]>> _bodies = new();

    public List<Uri> Requests { get; } = [];

    public ScriptedHttpHandler Then(string body) => Then(Encoding.UTF8.GetBytes(body));

    public ScriptedHttpHandler Then(byte[] body)
    {
        _bodies.Enqueue(() => body);
        return this;
    }

    public ScriptedHttpHandler ThenThrow(Exception exception)
    {
        _bodies.Enqueue(() => throw exception);
        return this;
    }

    public ScriptedHttpHandler ThenCancel(CancellationTokenSource shutdown)
    {
        _bodies.Enqueue(() =>
        {
            shutdown.Cancel();
            shutdown.Token.ThrowIfCancellationRequested();
            return [];
        });
        return this;
    }

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);

        if (_bodies.Count == 0)
        {
            throw new InvalidOperationException($"No scripted response left for {request.RequestUri}.");
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(_bodies.Dequeue()())
        });
    }
}
