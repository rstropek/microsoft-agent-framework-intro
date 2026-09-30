using A2A;
using A2ADemo;
using Lissie.Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// 1. Discovery: every A2A agent describes itself in its agent card (/.well-known/agent-card.json)
ConsoleChat.WriteHeader("Discovery");
AgentCard vetCard = await RemoteAgents.DiscoverAsync(RemoteAgents.VetUrl, "side/VetAgent");
AgentCard trackerCard = await RemoteAgents.DiscoverAsync(RemoteAgents.HumanTrackerUrl, "side/HumanTrackerAgent");

AIAgent vet = vetCard.AsAIAgent();
AIAgent humanTracker = trackerCard.AsAIAgent();

// 2. A remote A2A agent is just an AIAgent - same RunAsync, same sessions (A2A contextId)
ConsoleChat.WriteHeader($"Direct call: {vet.Name}");
AgentSession consultation = await vet.CreateSessionAsync();
foreach (var question in (string[])["Lissie wants 47 treats a day. Verdict?", "Would 46 be acceptable as a compromise?"])
{
    ConsoleChat.WriteInfo($"Staff -> {vet.Name}: {question}");
    AgentResponse verdict = await vet.RunAsync(question, consultation);
    Console.WriteLine($"{vet.Name}> {verdict.Text}");
}

// 3. Remote agents as tools: the model picks them based on the descriptions from their agent cards
AIAgent staff = AgentSetup.CreateChatClient().AsAIAgent(
    instructions: Persona.Instructions,
    name: "Staff",
    tools: [vet.AsAIFunction(), humanTracker.AsAIFunction()]);

ConsoleChat.WriteHeader("Lissie's staff with remote colleagues (type 'exit' to quit)");
await staff.ChatAsync();
