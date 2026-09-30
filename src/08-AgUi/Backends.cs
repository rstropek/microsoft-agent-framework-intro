using A2A;
using Microsoft.Agents.AI;
using ModelContextProtocol.Client;

namespace AgUi;

// The finale needs the MCP server and both A2A agents; fail fast with a hint instead of a stack trace
public static class Backends
{
    public static Task<McpClient> ConnectMcpAsync(string url) =>
        RequireAsync(url, () => McpClient.CreateAsync(new HttpClientTransport(new() { Endpoint = new(url) })));

    public static Task<AIAgent> DiscoverA2AAsync(string url) =>
        RequireAsync(url, async () => (await new A2ACardResolver(new Uri(url)).GetAgentCardAsync()).AsAIAgent());

    private static async Task<T> RequireAsync<T>(string url, Func<Task<T>> connect)
    {
        try
        {
            return await connect();
        }
        catch (Exception e) when (e is HttpRequestException || e.InnerException is HttpRequestException)
        {
            Console.Error.WriteLine($"Nothing answers at {url}. Start the backends first: scripts/start-backends.sh");
            Environment.Exit(1);
            throw;
        }
    }
}
