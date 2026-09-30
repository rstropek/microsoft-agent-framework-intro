using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Lissie.Common.Middleware;

/// <summary>Agent-run middleware: who asked what, how long it took, which tools were used.</summary>
public sealed partial class ActivityLog(ILogger logger, string defaultCaller = ConsoleChat.UserLabel)
{
    public async Task<AgentResponse> RunAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent innerAgent, CancellationToken cancellationToken)
    {
        var started = LogRequest(innerAgent, messages);
        var response = await innerAgent.RunAsync(messages, session, options, cancellationToken);
        LogCompleted(innerAgent, started, response.Messages.SelectMany(m => m.Contents));
        return response;
    }

    // Streaming variant: pass every update through immediately, summarize at the end
    public async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent innerAgent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var started = LogRequest(innerAgent, messages);
        List<AIContent> contents = [];
        await foreach (var update in innerAgent.RunStreamingAsync(messages, session, options, cancellationToken))
        {
            contents.AddRange(update.Contents);
            yield return update;
        }

        LogCompleted(innerAgent, started, contents);
    }

    private long LogRequest(AIAgent agent, IEnumerable<ChatMessage> messages)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return Stopwatch.GetTimestamp();
        }

        var list = messages as IReadOnlyCollection<ChatMessage> ?? [.. messages];
        var approvalsOnly = list.SelectMany(m => m.Contents).All(c => c is ToolApprovalResponseContent);
        var caller = list.Select(m => m.AuthorName).FirstOrDefault(n => n is not null) ?? (approvalsOnly ? "Human in the loop" : defaultCaller);
        var request = string.Join("; ", list.SelectMany(m => m.Contents).Select(Describe).OfType<string>());
        var agentName = AgentName(agent);
        LogRequest(logger, caller, agentName, request);
        return Stopwatch.GetTimestamp();
    }

    private void LogCompleted(AIAgent agent, long started, IEnumerable<AIContent> contents)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        var list = contents as IReadOnlyCollection<AIContent> ?? [.. contents];
        var tools = list.OfType<FunctionCallContent>().Select(c => c.Name).Distinct().ToList();
        var pending = list.OfType<ToolApprovalRequestContent>().Select(r => ConsoleChat.Describe(r.ToolCall)).ToList();
        var agentName = AgentName(agent);
        var elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        LogCompleted(
            logger,
            agentName,
            elapsedMilliseconds,
            tools.Count > 0 ? string.Join(", ", tools) : "none",
            pending.Count > 0 ? string.Join(", ", pending) : "nothing");
    }

    private static string? Describe(AIContent content) => content switch
    {
        TextContent { Text.Length: > 0 } text => $"\"{text.Text}\"",
        ToolApprovalResponseContent { Approved: true } approval => $"approved {ConsoleChat.Describe(approval.ToolCall)}",
        ToolApprovalResponseContent approval => $"rejected {ConsoleChat.Describe(approval.ToolCall)}",
        _ => null,
    };

    private static string AgentName(AIAgent agent) => agent.Name ?? agent.Id;

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "{Caller} -> {Agent}: {Request}")]
    private static partial void LogRequest(ILogger logger, string caller, string agent, string request);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "{Agent} done in {ElapsedMilliseconds} ms | tools used: {Tools} | awaiting approval: {Pending}")]
    private static partial void LogCompleted(ILogger logger, string agent, long elapsedMilliseconds, string tools, string pending);
}
