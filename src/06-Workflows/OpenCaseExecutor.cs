using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Lissie.Workflows;

// Plain code, no LLM: turns the triage agent's JSON into a typed CaseFile and records the decision as an event
public sealed class OpenCaseExecutor() : Executor<List<ChatMessage>, CaseFile>("OpenCase")
{
    private static int _lastCaseNumber;

    public override async ValueTask<CaseFile> HandleAsync(List<ChatMessage> message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var json = message.LastOrDefault(m => m.Role == ChatRole.Assistant)?.Text ?? string.Empty;
        var triage = JsonSerializer.Deserialize<ComplaintTriage>(json, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException($"Triage returned no verdict: '{json}'");

        var caseFile = new CaseFile($"LIS-{Interlocked.Increment(ref _lastCaseNumber):0000}", triage);
        await context.AddEventAsync(new TriageDecisionEvent(Id, caseFile), cancellationToken);
        return caseFile;
    }
}

public sealed class TriageDecisionEvent(string executorId, CaseFile caseFile) : ExecutorEvent(executorId, caseFile)
{
    public CaseFile CaseFile => caseFile;
}
