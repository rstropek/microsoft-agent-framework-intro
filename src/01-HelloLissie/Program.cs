using System.ClientModel.Primitives;
using Azure.Identity;
using Lissie.Common;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Responses;

// Endpoint + deployment come from user-secrets or environment variables, never from the repo
var settings = AgentSetup.Settings;

// An AIAgent on top of the Azure OpenAI Responses API, authenticated with the Azure CLI login
AIAgent agent = new OpenAIClient(
        new BearerTokenPolicy(new AzureCliCredential(), "https://cognitiveservices.azure.com/.default"),
        new OpenAIClientOptions { Endpoint = settings.ResponsesEndpoint })
    .GetResponsesClient()
    .AsAIAgent(model: settings.Deployment, instructions: Persona.Instructions, name: "Staff");

// 1. One question, one answer
ConsoleChat.WriteHeader("RunAsync");
AgentResponse response = await agent.RunAsync("Good morning, staff. Report.");
ConsoleChat.WriteAgent(response.Text);

// 2. The same, token by token
ConsoleChat.WriteHeader("RunStreamingAsync");
await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("Explain in one sentence why the sunny spot on the sofa is mine."))
{
    Console.Write(update);
}

Console.WriteLine();

// 3. Multi-turn: the session remembers Her Majesty's complaints
ConsoleChat.WriteHeader("Chat with AgentSession (type 'exit' to quit)");
AgentSession session = await agent.CreateSessionAsync();
while (ConsoleChat.ReadPrompt() is { } input)
{
    await agent.StreamToConsoleAsync(input, session);
}
