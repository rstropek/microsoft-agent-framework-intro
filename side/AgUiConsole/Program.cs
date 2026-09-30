using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

// A plain HTTP + SSE client: no agent framework, no model configuration, just the AG-UI protocol
var server = new Uri(args is [var url] ? url : "http://localhost:5300/");
using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
var threadId = $"lissie-{Guid.NewGuid():N}"[..14];
string? parentRunId = null;

Write($"=== AG-UI console -> {server} (threadId {threadId}, type 'exit' to quit) ===\n", ConsoleColor.Green);
for (var turn = 1; Prompt() is { } prompt; turn++)
{
    // RunAgentInput: same threadId every turn, only the NEW message - the server keeps the session per thread
    var input = new JsonObject
    {
        ["threadId"] = threadId,
        ["runId"] = $"run-{turn}",
        ["parentRunId"] = parentRunId,
        ["messages"] = new JsonArray(new JsonObject { ["id"] = $"msg-{turn}", ["role"] = "user", ["content"] = prompt }),
    };
    Write($"POST {server} {Json(input)}\n", ConsoleColor.DarkYellow);

    using var request = new HttpRequestMessage(HttpMethod.Post, server) { Content = JsonContent.Create(input) };
    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    response.EnsureSuccessStatusCode();

    // Every SSE item is one AG-UI event; text deltas are streamed inline and counted instead of printed one per line
    var deltas = 0;
    await foreach (var item in SseParser.Create(await response.Content.ReadAsStreamAsync()).EnumerateAsync())
    {
        var evt = JsonNode.Parse(item.Data)!.AsObject();
        var type = (string)evt["type"]!;
        if (type == "TEXT_MESSAGE_CONTENT")
        {
            Write(deltas++ == 0 ? "  TEXT_MESSAGE_CONTENT " : string.Empty, ConsoleColor.DarkGray);
            Write((string?)evt["delta"] ?? string.Empty, ConsoleColor.White);
            continue;
        }

        Write(deltas > 0 ? $"  ({deltas} deltas)\n" : string.Empty, ConsoleColor.DarkGray);
        deltas = 0;
        evt.Remove("type");
        evt.Remove("rawEvent");   // the server's internal representation, not needed by a client
        Write($"  {type} {Json(Compact(evt))}\n", type.StartsWith("TOOL_CALL", StringComparison.Ordinal) ? ConsoleColor.DarkCyan : ConsoleColor.DarkGray);
    }

    parentRunId = $"run-{turn}";
}

static string? Prompt()
{
    Write("Lissie> ", ConsoleColor.Yellow);
    var line = Console.ReadLine();
    Write(Console.IsInputRedirected ? $"{line}\n" : string.Empty, ConsoleColor.Gray);
    return line?.Trim() switch { null or "exit" or "quit" => null, "" => Prompt(), _ => line };
}

// Drop nulls and shorten long strings (encrypted reasoning, long tool results) so every event fits on one line
static JsonNode? Compact(JsonNode? node) => node switch
{
    JsonObject obj => new JsonObject(obj.Where(p => p.Value is not null).Select(p => KeyValuePair.Create(p.Key, Compact(p.Value)))),
    JsonArray array => new JsonArray([.. array.Select(Compact)]),
    JsonValue value when value.TryGetValue(out string? text) && text.Length > 100 => $"{text[..97]}...",
    _ => node?.DeepClone(),
};

static string Json(JsonNode? node) => node?.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) ?? "null";

static void Write(string text, ConsoleColor color)
{
    Console.ForegroundColor = color;
    Console.Write(text);
    Console.ResetColor();
}
