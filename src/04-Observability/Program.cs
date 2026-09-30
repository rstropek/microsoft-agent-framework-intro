using System.ComponentModel;
using System.Diagnostics;
using Lissie.Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string SourceName = "Lissie.Observability";          // our own ActivitySource
var otlpEndpoint = new Uri("http://localhost:4317");       // Aspire dashboard, OTLP/gRPC

// Who we are, which sources we listen to, where it all goes
var resource = ResourceBuilder.CreateDefault().AddService("04-Observability");
string[] sources = [SourceName, "Experimental.Microsoft.Agents.AI", "Experimental.Microsoft.Extensions.AI"];
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resource)
    .AddSource(sources)
    .AddHttpClientInstrumentation()
    .AddOtlpExporter(otlp => otlp.Endpoint = otlpEndpoint)
    .Build();
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resource)
    .AddMeter(sources)
    .AddHttpClientInstrumentation()
    .AddOtlpExporter((otlp, reader) =>
    {
        otlp.Endpoint = otlpEndpoint;
        reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000;
    })
    .Build();
using var loggerFactory = LoggerFactory.Create(logging => logging.AddOpenTelemetry(otel =>
{
    otel.SetResourceBuilder(resource);
    otel.IncludeFormattedMessage = true;
    otel.AddOtlpExporter(otlp => otlp.Endpoint = otlpEndpoint);
}));

// Our own span inside a tool: shows up nested in the agent's trace
using var activitySource = new ActivitySource(SourceName);

[Description("Inspects the sunny spot on the sofa: temperature and fur coverage.")]
async Task<string> InspectSunnySpot()
{
    using var activity = activitySource.StartActivity("Inspect sunny spot");
    activity?.SetTag("lissie.spot.temperature_celsius", 31);
    await Task.Delay(300);
    activity?.AddEvent(new("Fur coverage measured"));
    return "Sunny spot: 31 degrees, fur coverage 87 percent. Perfect, apart from the Secondary Human's laptop nearby.";
}

var householdTools = new HouseholdTools(new Household());

// Telemetry on the chat client: one "chat" span per model call ...
IChatClient chatClient = AgentSetup.CreateChatClient()
    .AsBuilder()
    .UseOpenTelemetry(loggerFactory, configure: otel => otel.EnableSensitiveData = true)
    .Build();

// ... and on the agent: one "invoke_agent" span per run. Sensitive data = prompts, arguments and results in the trace
AIAgent agent = chatClient
    .AsAIAgent(
        instructions: Persona.Instructions,
        name: "Staff",
        tools: [AIFunctionFactory.Create(InspectSunnySpot, name: nameof(InspectSunnySpot)), .. householdTools.CreateTools(dispenseFoodRequiresApproval: false)],
        loggerFactory: loggerFactory)
    .AsBuilder()
    .UseOpenTelemetry(configure: otel => otel.EnableSensitiveData = true)
    .Build();

ConsoleChat.WriteHeader("Lissie's staff, observed - http://localhost:18888 (type 'exit' to quit)");
AgentSession session = await agent.CreateSessionAsync();
while (ConsoleChat.ReadPrompt() is { } input)
{
    await agent.StreamToConsoleAsync(input, session);
}
