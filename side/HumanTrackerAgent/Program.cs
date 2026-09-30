using A2A;
using A2A.AspNetCore;
using HumanTrackerAgent;
using Lissie.Common;
using Lissie.Common.Telemetry;
using Microsoft.Agents.AI.Hosting;

const string Name = "HumanTrackerAgent";
const string Url = "http://localhost:5202/";

var builder = WebApplication.CreateBuilder(args);
builder.AddLissieChatClient();
builder.AddLissieTelemetry("human-tracker-agent");

// An ordinary agent with its own instructions and one local tool ...
var tracker = builder.AddAIAgent(Name, HumanCalendar.Instructions)
    .WithAITool(HumanCalendar.WhereIsFunction)
    .WithInMemorySessionStore(withIsolation: false)   // same A2A contextId = same conversation (no auth, so no per-caller isolation)
    .AddA2AServer();

var app = builder.Build();

// ... exposed over A2A (HTTP+JSON binding) ...
app.MapA2AHttpJson(tracker, "/");

// ... and discoverable: the main agent's model reads this description to decide when to call the tracker
app.MapWellKnownAgentCard(new AgentCard
{
    Name = Name,
    Description = "Knows where Lissie's humans (Karin, the Primary Human, and Rainer, the Secondary Human) are right now, what they are doing and when they will be back. Ask it why nobody is available for petting or feeding.",
    Version = "1.0.0",
    SupportedInterfaces = [new() { Url = Url, ProtocolBinding = ProtocolBindingNames.HttpJson, ProtocolVersion = "1.0" }],
    Capabilities = new() { Streaming = true },
    DefaultInputModes = ["text/plain"],
    DefaultOutputModes = ["text/plain"],
    Skills =
    [
        new()
        {
            Id = "human-whereabouts",
            Name = "Human whereabouts",
            Description = "Location, current activity and expected return time of Karin and Rainer, from their calendars.",
            Tags = ["humans", "calendar", "availability"],
            Examples = ["Where is Karin?", "When is the Secondary Human back?", "Why is nobody petting Lissie?"],
        },
    ],
});

app.Run();
