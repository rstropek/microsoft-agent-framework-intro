using A2A;
using A2A.AspNetCore;
using Lissie.Common;
using Lissie.Common.Telemetry;
using Microsoft.Agents.AI.Hosting;
using VetAgent;

const string Name = "VetAgent";
const string Url = "http://localhost:5201/";

var builder = WebApplication.CreateBuilder(args);
builder.AddLissieChatClient();
builder.AddLissieTelemetry("vet-agent");

// An ordinary agent with its own instructions and one local tool ...
var vet = builder.AddAIAgent(Name, DietRules.Instructions)
    .WithAITool(DietRules.LookupFunction)
    .WithInMemorySessionStore(withIsolation: false)   // same A2A contextId = same conversation (no auth, so no per-caller isolation)
    .AddA2AServer();

var app = builder.Build();

// ... exposed over A2A (HTTP+JSON binding) ...
app.MapA2AHttpJson(vet, "/");

// ... and discoverable: the main agent's model reads this description to decide when to call the vet
app.MapWellKnownAgentCard(new AgentCard
{
    Name = Name,
    Description = "Dr. Pfote, the household veterinarian. The authority on Lissie's health, weight, diet, treats and feeding times. Ask in plain language and include concrete amounts.",
    Version = "1.0.0",
    SupportedInterfaces = [new() { Url = Url, ProtocolBinding = ProtocolBindingNames.HttpJson, ProtocolVersion = "1.0" }],
    Capabilities = new() { Streaming = true },
    DefaultInputModes = ["text/plain"],
    DefaultOutputModes = ["text/plain"],
    Skills =
    [
        new()
        {
            Id = "diet-verdict",
            Name = "Diet verdict",
            Description = "Professional verdict on food, treats, tuna, feeding times and weight, based on binding diet rules.",
            Tags = ["health", "diet", "treats", "weight"],
            Examples = ["Are 47 treats a day okay?", "May Lissie have tuna for breakfast?"],
        },
    ],
});

app.Run();
