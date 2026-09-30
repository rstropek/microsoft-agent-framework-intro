using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Lissie.Common;

public static class ConsoleChat
{
    public const string UserLabel = "Lissie";
    public const string AgentLabel = "Staff";

    private static bool _atLineStart = true;

    public static void WriteHeader(string title) => WriteLine($"=== {title} ===", ConsoleColor.Green);

    public static void WriteInfo(string text) => WriteLine(text, ConsoleColor.DarkGray);

    public static void WriteAgent(string text)
    {
        Write($"{AgentLabel}> ", ConsoleColor.Cyan);
        WriteLine(text, ConsoleColor.White);
    }

    public static string? ReadPrompt(string label = UserLabel)
    {
        while (true)
        {
            Write($"{label}> ", ConsoleColor.Yellow);
            var line = ReadLineEchoed();
            switch (line?.Trim())
            {
                case null or "exit" or "quit":
                    return null;
                case "":
                    continue;
                default:
                    return line;
            }
        }
    }

    public static bool Confirm(string question)
    {
        Write($"  {question} [y/n] ", ConsoleColor.Magenta);
        return ReadLineEchoed()?.Trim().ToUpperInvariant() is "Y" or "YES";
    }

    public static bool Confirm(ToolApprovalRequestContent request) => Confirm($"Approve {Describe(request.ToolCall)}?");

    public static string Describe(ToolCallContent call) => call switch
    {
        FunctionCallContent function => $"{function.Name}({string.Join(", ", function.Arguments?.Select(a => $"{a.Key}: {a.Value}") ?? [])})",
        _ => call.ToString() ?? call.CallId,
    };

    public static string Describe(FunctionResultContent result) => result switch
    {
        { Exception: { } exception } => $"{exception.GetType().Name}: {exception.Message}",
        { Result: JsonElement json } => json.ValueKind is JsonValueKind.String ? json.GetString() ?? string.Empty : JsonSerializer.Serialize(json),
        _ => result.Result?.ToString() ?? "(no result)",
    };

    extension(AgentResponse response)
    {
        public IReadOnlyList<ToolApprovalRequestContent> ApprovalRequests =>
            [.. response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>()];
    }

    extension(AIAgent agent)
    {
        public Task<AgentResponse> StreamToConsoleAsync(string message, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default) =>
            agent.StreamToConsoleAsync([new ChatMessage(ChatRole.User, message)], session, options, cancellationToken);

        public async Task<AgentResponse> StreamToConsoleAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
        {
            List<AgentResponseUpdate> updates = [];
            var labelPending = true;
            await foreach (var update in agent.RunStreamingAsync(messages, session, options, cancellationToken))
            {
                updates.Add(update);
                foreach (var content in update.Contents)
                {
                    switch (content)
                    {
                        case TextContent { Text.Length: > 0 } text:
                            if (labelPending)
                            {
                                Write($"{AgentLabel}> ", ConsoleColor.Cyan);
                                labelPending = false;
                            }

                            Write(text.Text, ConsoleColor.White);
                            break;
                        case FunctionCallContent { InformationalOnly: false } call:
                            WriteToolLine($"-> {Describe(call)}");
                            labelPending = true;
                            break;
                        case FunctionResultContent result:
                            WriteToolLine($"<- {Truncate(Describe(result))}");
                            labelPending = true;
                            break;
                        default:
                            break;
                    }
                }

                // End the line as soon as a response is finished, so log output of middleware starts on a fresh line
                if (update.FinishReason is not null)
                {
                    EnsureNewLine();
                    labelPending = true;
                }
            }

            EnsureNewLine();
            return updates.ToAgentResponse();
        }

        public async Task ChatAsync(AgentSession? session = null, CancellationToken cancellationToken = default)
        {
            session ??= await agent.CreateSessionAsync(cancellationToken);
            while (ReadPrompt() is { } input)
            {
                var response = await agent.StreamToConsoleAsync(input, session, cancellationToken: cancellationToken);
                while (response.ApprovalRequests is { Count: > 0 } requests)
                {
                    List<ChatMessage> answers = [.. requests.Select(r => new ChatMessage(ChatRole.User, [r.CreateResponse(Confirm(r))]))];
                    response = await agent.StreamToConsoleAsync(answers, session, cancellationToken: cancellationToken);
                }
            }
        }
    }

    private static string? ReadLineEchoed()
    {
        var line = Console.ReadLine();
        if (Console.IsInputRedirected)
        {
            WriteLine(line ?? string.Empty, ConsoleColor.Gray);
        }

        _atLineStart = true;
        return line;
    }

    private static void WriteToolLine(string text)
    {
        EnsureNewLine();
        WriteLine($"   {text}", ConsoleColor.DarkGray);
    }

    private static string Truncate(string text) => text.Length <= 160 ? text : $"{text[..157]}...";

    private static void EnsureNewLine()
    {
        if (!_atLineStart)
        {
            Console.WriteLine();
            _atLineStart = true;
        }
    }

    private static void Write(string text, ConsoleColor color)
    {
        if (text.Length == 0)
        {
            return;
        }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = previous;
        _atLineStart = text.EndsWith('\n');
    }

    private static void WriteLine(string text, ConsoleColor color) => Write($"{text}{Environment.NewLine}", color);
}
