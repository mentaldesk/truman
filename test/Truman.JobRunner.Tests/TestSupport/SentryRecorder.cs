using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Sentry;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace Truman.JobRunner.Tests.TestSupport;

public sealed record RecordedCheckIn(string MonitorSlug, string Status, string? TraceId);

/// Initialises the SDK to keep what it would send, so a test can read the trace a run produced.
public sealed class SentryRecorder : ITransport, IDisposable
{
    private readonly ConcurrentQueue<SentryTransaction> _transactions = new();
    private readonly ConcurrentQueue<RecordedCheckIn> _checkIns = new();
    private readonly IDisposable _sdk;

    public SentryRecorder()
    {
        _sdk = SentrySdk.Init(options =>
        {
            options.Dsn = "https://key@sentry.invalid/1";
            options.Transport = this;
            options.TracesSampleRate = 1.0;
            options.AutoSessionTracking = false;
            options.SetBeforeSendTransaction(transaction =>
            {
                _transactions.Enqueue(transaction);
                return null;
            });
        });
    }

    public async Task<IReadOnlyList<SentryTransaction>> TransactionsAsync()
    {
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
        return _transactions.ToList();
    }

    public async Task<IReadOnlyList<RecordedCheckIn>> CheckInsAsync()
    {
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
        return _checkIns.ToList();
    }

    // SentryCheckIn's trace id isn't public, so check-ins are read back from what would go on the wire.
    public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        foreach (var item in envelope.Items.Where(i => i.TryGetType() == "check_in"))
        {
            using var stream = new MemoryStream();
            await item.SerializeAsync(stream, null, cancellationToken);
            var bytes = stream.ToArray();
            var payload = JsonNode.Parse(bytes.AsSpan(Array.IndexOf(bytes, (byte)'\n') + 1))!;
            _checkIns.Enqueue(new RecordedCheckIn(
                payload["monitor_slug"]!.GetValue<string>(),
                payload["status"]!.GetValue<string>(),
                payload["contexts"]?["trace"]?["trace_id"]?.GetValue<string>()));
        }
    }

    public void Dispose() => _sdk.Dispose();
}
