using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Lissie.Workflows;

// Keeping track of execution and decisions: every workflow event becomes one line of a timeline
public static class WorkflowTimeline
{
    public static TimeSpan RunTimeout { get; } = TimeSpan.FromSeconds(60);

    public static async Task RunAsync(Workflow workflow, string input)
    {
        var clock = Stopwatch.StartNew();
        string? speaker = null;
        using var timeout = new CancellationTokenSource(RunTimeout);   // a stalled model call must not freeze a live demo

        await using (StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input, cancellationToken: timeout.Token))
        {
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));   // agents bound into a graph only speak when they get the turn
            await foreach (WorkflowEvent evt in run.WatchStreamAsync(timeout.Token))
            {
                if (evt is AgentResponseUpdateEvent update)
                {
                    Speak(update);
                    continue;
                }

                EndSpeech();
                switch (evt)
                {
                    case WorkflowStartedEvent or SuperStepCompletedEvent or ExecutorCompletedEvent { Data: null }:
                        break;
                    case SuperStepStartedEvent step:
                        Line($"-- superstep {step.StepNumber} --", ConsoleColor.DarkGray);
                        break;
                    case ExecutorInvokedEvent { Data: CaseFile caseFile } invoked:
                        Line($"ROUTE     switch on Category = {caseFile.Category} -> {invoked.ExecutorId}", ConsoleColor.Magenta);
                        break;
                    case ExecutorInvokedEvent invoked:
                        Line($"invoked   {invoked.ExecutorId} <- {Describe(invoked.Data)}", ConsoleColor.DarkGray);
                        break;
                    case ExecutorCompletedEvent completed:
                        Line($"completed {completed.ExecutorId} -> {Describe(completed.Data)}", ConsoleColor.DarkGray);
                        break;
                    case TriageDecisionEvent { CaseFile: var c }:
                        Line($"DECISION  {c.CaseNumber}: {c.Category}, urgency {c.Triage.Urgency} - \"{c.Triage.Summary}\"", ConsoleColor.Magenta);
                        break;
                    case WorkflowOutputEvent { Data: string document } output:
                        Line($"OUTPUT    from {output.ExecutorId}", ConsoleColor.Green);
                        Console.WriteLine(document);
                        break;
                    case WorkflowOutputEvent output:
                        Line($"OUTPUT    from {output.ExecutorId}: {Describe(output.Data)}", ConsoleColor.Green);
                        break;
                    case ExecutorFailedEvent failed:
                        Line($"FAILED    {failed.ExecutorId}: {failed.Data?.Message}", ConsoleColor.Red);
                        break;
                    case WorkflowErrorEvent error:
                        Line($"ERROR     {error.Exception?.Message}", ConsoleColor.Red);
                        break;
                    default:
                        Line(evt.GetType().Name, ConsoleColor.DarkGray);
                        break;
                }
            }
        }

        EndSpeech();
        if (timeout.IsCancellationRequested)
        {
            Line($"TIMEOUT   run cancelled after {RunTimeout.TotalSeconds:0} s - try again", ConsoleColor.Red);
        }
        else
        {
            Line($"done in {clock.Elapsed.TotalSeconds:0.0} s", ConsoleColor.DarkGray);
        }

        void Speak(AgentResponseUpdateEvent update)
        {
            if (update.Update.Text is not { Length: > 0 } text)
            {
                return;
            }

            if (speaker != update.ExecutorId)
            {
                EndSpeech();
                speaker = update.ExecutorId;
                Write($"{"",8}{speaker}> ", ConsoleColor.Cyan);
            }

            Write(text, ConsoleColor.White);
        }

        void EndSpeech()
        {
            if (speaker is not null)
            {
                Console.WriteLine();
                speaker = null;
            }
        }

        void Line(string text, ConsoleColor color) => Write($"{clock.Elapsed.TotalSeconds,6:0.00}s  {text}{Environment.NewLine}", color);
    }

    private static string Describe(object? data) => data switch
    {
        null => "(nothing)",
        string text => $"\"{Shorten(text)}\"",
        List<ChatMessage> messages => $"{messages.Count} chat message(s): \"{Shorten(messages.LastOrDefault()?.Text)}\"",
        CaseFile caseFile => $"CaseFile {caseFile.CaseNumber}",
        Resolution resolution => $"Resolution by {resolution.Specialist}",
        _ => data.GetType().Name,
    };

    private static string Shorten(string? text)
    {
        var line = (text ?? string.Empty).ReplaceLineEndings(" ").Trim();
        return line.Length <= 70 ? line : $"{line[..67]}...";
    }

    private static void Write(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = previous;
    }
}
