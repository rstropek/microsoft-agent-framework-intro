using AgUi;
using Lissie.Common;
using Lissie.Common.Middleware;
using Lissie.Common.Telemetry;
using Lissie.SmartHome;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
builder.AddLissieChatClient();
builder.AddLissieTelemetry("lissie-staff");
builder.Services.AddAGUIServer();

// Remote building blocks: smart home via MCP (05), vet and human tracker via A2A (07)
await using var smartHome = await Backends.ConnectMcpAsync("http://localhost:5101/mcp");
var smartHomeTools = await smartHome.ListToolsAsync();
AIAgent vet = await Backends.DiscoverA2AAsync("http://localhost:5201");
AIAgent humanTracker = await Backends.DiscoverA2AAsync("http://localhost:5202");

// Local tools (02); the smart home owns the food bowl now. Rainer is busy on stage, as the HumanTracker confirms.
var household = new HouseholdTools(new Household { Rainer = new(Human.Secondary, "Rainer", IsAvailable: false, "on stage at BASTA!, talking about agents") });

// Everything together: guarded chat client, all tools, middleware (03) and telemetry (04)
var staff = builder.AddAIAgent("Staff", (services, name) =>
{
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Lissie.Staff");
    return services.GetRequiredService<IChatClient>()
        .AsBuilder()
        .UsePromptInjectionGuard(logger)
        .Build()
        .AsAIAgent(
            instructions: $"{Persona.Instructions}\n{SmartHomeTools.AgentInstructions}",
            name: name,
            tools: [household.SummonHumanFunction, household.KnockObjectOffTableFunction, .. smartHomeTools, vet.AsAIFunction(), humanTracker.AsAIFunction()])
        .AsBuilder()
        .UseOpenTelemetry(configure: otel => otel.EnableSensitiveData = true)
        .UseActivityLog(logger)
        .UseProtectedObjects(ProtectedObjects.KarinsList, logger)
        .Build();
})
.WithInMemorySessionStore(withIsolation: false);   // AG-UI threadId -> server-side session: clients send only the new message

var app = builder.Build();

// AG-UI: POST a RunAgentInput, receive the run as a stream of Server-Sent Events
app.MapAGUIServer(staff, "/");
app.Run();
