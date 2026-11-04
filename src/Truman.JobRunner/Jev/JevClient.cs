using System.Net.Http.Json;

namespace Truman.JobRunner.Jev;

/// Client for TypeSafe's System One endpoint. One request carries many named questions.
public sealed class JevClient(HttpClient httpClient)
{
    public const string HttpClientName = "JevClient";

    private const string Model = "jev-latest";

    public async Task<JevResponse> AskAsync(
        string state,
        IReadOnlyDictionary<string, JevQuestion> questions,
        CancellationToken cancellationToken = default)
    {
        var request = new JevRequest { Model = Model, State = state, Questions = questions };

        using var response = await httpClient.PostAsJsonAsync("v1/systemone", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JevResponse>(cancellationToken)
               ?? throw new InvalidOperationException("Jev returned an empty response body.");
    }
}
