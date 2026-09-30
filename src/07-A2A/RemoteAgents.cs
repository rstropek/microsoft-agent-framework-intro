using A2A;
using Lissie.Common;

namespace A2ADemo;

public static class RemoteAgents
{
    public const string VetUrl = "http://localhost:5201";
    public const string HumanTrackerUrl = "http://localhost:5202";

    // Fetches /.well-known/agent-card.json and prints what the remote agent says about itself
    public static async Task<AgentCard> DiscoverAsync(string url, string project)
    {
        try
        {
            AgentCard card = await new A2ACardResolver(new Uri(url)).GetAgentCardAsync();
            ConsoleChat.WriteAgentCard(card);
            return card;
        }
        catch (A2AException e) when (e.InnerException is HttpRequestException)
        {
            Console.Error.WriteLine($"No A2A agent at {url}. Start it first: dotnet run --project {project}");
            Environment.Exit(1);
            throw;
        }
    }

    extension(ConsoleChat)
    {
        public static void WriteAgentCard(AgentCard card)
        {
            ConsoleChat.WriteInfo($"{card.Name} - {card.Description}");
            foreach (var endpoint in card.SupportedInterfaces)
            {
                ConsoleChat.WriteInfo($"  endpoint: {endpoint.Url} ({endpoint.ProtocolBinding} {endpoint.ProtocolVersion})");
            }

            foreach (var skill in card.Skills)
            {
                ConsoleChat.WriteInfo($"  skill '{skill.Name}': {skill.Description} e.g. \"{string.Join("\", \"", skill.Examples ?? [])}\"");
            }
        }
    }
}
