using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Lissie.Common.Middleware;

/// <summary>Agent-run middleware: who asked what, how long it took, which tools were used.</summary>
public sealed partial class ActivityLog(ILogger logger)
{
    public async Task<AgentResponse> RunAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent innerAgent, CancellationToken cancellationToken)
    {
        LogRequest(innerAgent.Name, messages);
        var started = Stopwatch.GetTimestamp();
        var response = await innerAgent.RunAsync(messages, session, options, cancellationToken);
        LogCompleted(innerAgent.Name, started, response.Messages.SelectMany(m => m.Contents));
        return response;
    }

    // Streaming variant: pass every update through immediately, summarize at the end
    public async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options, AIAgent innerAgent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LogRequest(innerAgent.Name, messages);
        var started = Stopwatch.GetTimestamp();
        List<AIContent> contents = [];
        await foreach (var update in innerAgent.RunStreamingAsync(messages, session, options, cancellationToken))
        {
            contents.AddRange(update.Contents);
            yield return update;
        }

        LogCompleted(innerAgent.Name, started, contents);
    }

    private void LogRequest(string? agent, IEnumerable<ChatMessage> messages)
    {
        var request = string.Join(" ", messages.Select(m => m.Text)).Trim() is { Length: > 0 } text ? text : "(tool approval)";
        LogRequest(logger, agent, request);
    }

    private void LogCompleted(string? agent, long started, IEnumerable<AIContent> contents)
    {
        var tools = string.Join(", ", contents.OfType<FunctionCallContent>().Select(c => c.Name).Distinct()) is { Length: > 0 } names ? names : "none";
        var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        LogCompleted(logger, agent, elapsed, tools);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Lissie -> {Agent}: {Request}")]
    private static partial void LogRequest(ILogger logger, string? agent, string request);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "{Agent} done in {ElapsedMilliseconds} ms | tools used: {Tools}")]
    private static partial void LogCompleted(ILogger logger, string? agent, long elapsedMilliseconds, string tools);
}
