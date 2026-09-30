using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace Lissie.Workflows;

// An agent wrapped in a hand-written executor: typed input (CaseFile) and typed output (Resolution)
public sealed class SpecialistExecutor(AIAgent agent) : Executor<CaseFile, Resolution>(agent.Id)
{
    public override async ValueTask<Resolution> HandleAsync(CaseFile message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        List<AgentResponseUpdate> updates = [];
        await foreach (var update in agent.RunStreamingAsync(message.Briefing, cancellationToken: cancellationToken))
        {
            updates.Add(update);
            await context.AddEventAsync(new AgentResponseUpdateEvent(Id, update), cancellationToken);
        }

        return new(message, Id, updates.ToAgentResponse().Text);
    }
}
