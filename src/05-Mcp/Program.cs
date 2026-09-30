using Lissie.Common;
using Lissie.SmartHome;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

if (args is not ([] or ["local" or "mcp"]))
{
    ConsoleChat.WriteInfo("Usage: dotnet run --project src/05-Mcp -- [local|mcp]");
    return;
}

await using var mcpClient = args is ["mcp"]
    ? await McpClient.CreateAsync(new HttpClientTransport(new() { Endpoint = new("http://localhost:5101/mcp") }))
    : null;

// The ONLY difference between the two modes: where the AIFunctions come from
IEnumerable<AIFunction> tools = mcpClient is null ? new SmartHomeTools().Functions : await mcpClient.ListToolsAsync();

ConsoleChat.WriteHeader($"Smart home tools ({(mcpClient is null ? "in-process" : "via MCP server")})");
foreach (var tool in tools.OrderBy(t => t.Name))
{
    ConsoleChat.WriteInfo($"{tool.Name}: {tool.Description}{Environment.NewLine}    {tool.JsonSchema}");
}

AIAgent agent = AgentSetup.CreateChatClient().AsAIAgent(
    instructions: $"{Persona.Instructions}\n{SmartHomeTools.AgentInstructions}",
    name: "Staff",
    tools: [.. tools]);

ConsoleChat.WriteHeader("Lissie's smart home (type 'exit' to quit)");
await agent.ChatAsync();
