using Microsoft.Extensions.AI;

#pragma warning disable SENTRY0001

namespace Truman.JobRunner;

public static class AnalyserChatClient
{
    // Story text must never reach Sentry, so the gen_ai spans carry no prompts or replies.
    public static IChatClient WithSentry(this IChatClient client) =>
        client.AddSentry(o =>
        {
            o.Experimental.AgentName = "truman-analyser";
            o.Experimental.RecordInputs = false;
            o.Experimental.RecordOutputs = false;
        });
}
