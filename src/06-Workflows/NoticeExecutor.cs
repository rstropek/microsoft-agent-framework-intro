using Microsoft.Agents.AI.Workflows;

namespace Lissie.Workflows;

// Plain code, no LLM: the formal notice is the workflow's output
public sealed class NoticeExecutor() : Executor<Resolution, string>("Notice")
{
    public override ValueTask<string> HandleAsync(Resolution message, IWorkflowContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult($"""
            NOTICE OF COMPLAINT {message.Case.CaseNumber}
            To: Karin (Primary Human) · Cc: Rainer (Secondary Human)
            Category: {message.Case.Category} · Urgency: {message.Case.Triage.Urgency}
            Subject: {message.Case.Triage.Summary}
            Handled by: {message.Specialist}

            {message.Text.Trim()}

            Filed on behalf of Her Majesty Lissie. Replies will be ignored.
            """);
}
