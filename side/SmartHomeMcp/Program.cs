using Lissie.SmartHome;
using ModelContextProtocol.Server;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// No tool logic here: the very same AIFunctions the agent uses in-process, exposed as MCP tools.
// One instance per server process, so treats dispensed in one call are still in the bowl in the next.
var smartHome = new SmartHomeTools();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools(smartHome.Functions.Select(function => McpServerTool.Create(function)));

// OTLP to http://localhost:4317 (or OTEL_EXPORTER_OTLP_ENDPOINT); runs fine without a collector
const string McpTelemetry = "Experimental.ModelContextProtocol";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("smarthome-mcp"))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddSource(McpTelemetry))
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddMeter(McpTelemetry))
    .WithLogging()
    .UseOtlpExporter();

var app = builder.Build();
app.MapMcp("/mcp");
app.Run();
