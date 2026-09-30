using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Lissie.Common.Telemetry;

/// <summary>
/// OpenTelemetry setup shared by all Lissie processes. Export goes to OTLP; endpoint from OTEL_EXPORTER_OTLP_ENDPOINT,
/// default http://localhost:4317 (the Aspire dashboard container).
/// </summary>
public static class LissieTelemetry
{
    /// <summary>ActivitySource and Meter name of our own spans and metrics.</summary>
    public const string SourceName = "Lissie";

    public const string AgentFrameworkSourceName = "Experimental.Microsoft.Agents.AI";
    public const string ExtensionsAISourceName = "Experimental.Microsoft.Extensions.AI";
    public const string WorkflowsSourceName = "Microsoft.Agents.AI.Workflows";
    public const string McpSourceName = "Experimental.ModelContextProtocol";
    public const string A2ASourceName = "A2A";
    public const string A2AAspNetCoreSourceName = "A2A.AspNetCore";

    public const int MetricExportIntervalMilliseconds = 5000;

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    /// <summary>All ActivitySource names of the sample (verified against the restored packages).</summary>
    public static IReadOnlyList<string> ActivitySourceNames { get; } =
        [SourceName, AgentFrameworkSourceName, ExtensionsAISourceName, WorkflowsSourceName, McpSourceName, A2ASourceName, A2AAspNetCoreSourceName];

    /// <summary>All Meter names of the sample (workflows and A2A.AspNetCore have no meter).</summary>
    public static IReadOnlyList<string> MeterNames { get; } =
        [SourceName, AgentFrameworkSourceName, ExtensionsAISourceName, McpSourceName, A2ASourceName];

    /// <summary>Tracing, metrics and logs for a console app. Dispose at the end to flush.</summary>
    public static ConsoleTelemetry StartConsole(string serviceName) => new(serviceName);

    extension(TracerProviderBuilder tracing)
    {
        public TracerProviderBuilder AddLissieSources() => tracing.AddSource([.. ActivitySourceNames]);
    }

    extension(MeterProviderBuilder metrics)
    {
        public MeterProviderBuilder AddLissieMeters() => metrics.AddMeter([.. MeterNames]);
    }

    extension(IHostApplicationBuilder builder)
    {
        /// <summary>ASP.NET Core / generic host: traces, metrics and logs via OTLP, incl. ASP.NET Core and HttpClient instrumentation.</summary>
        public IHostApplicationBuilder AddLissieTelemetry(string serviceName)
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName))
                .WithTracing(tracing => tracing
                    .AddLissieSources()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation())    // injects traceparent: one connected trace across processes
                .WithMetrics(metrics => metrics
                    .AddLissieMeters()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation())
                .UseOtlpExporter();

            // Metrics every 5 s instead of every 60 s: the dashboard reacts while the audience watches
            builder.Services.Configure<MetricReaderOptions>(reader =>
                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = MetricExportIntervalMilliseconds);
            return builder;
        }
    }

    extension(ChatClientBuilder builder)
    {
        /// <summary>Chat-level spans ("chat {model}") on the Microsoft.Extensions.AI source.</summary>
        public ChatClientBuilder UseLissieTelemetry(bool enableSensitiveData = true) =>
            builder.UseOpenTelemetry(configure: telemetry => telemetry.EnableSensitiveData = enableSensitiveData);
    }

    extension(AIAgentBuilder builder)
    {
        /// <summary>
        /// Agent-level spans ("invoke_agent"); also instruments the agent's own chat client (chat + execute_tool spans)
        /// unless that client is already instrumented.
        /// </summary>
        public AIAgentBuilder UseLissieTelemetry(bool enableSensitiveData = true) =>
            builder.UseOpenTelemetry(configure: telemetry => telemetry.EnableSensitiveData = enableSensitiveData);
    }
}

/// <summary>Tracer, meter and logger providers of a console app, all exporting via OTLP.</summary>
public sealed class ConsoleTelemetry : IDisposable
{
    internal ConsoleTelemetry(string serviceName)
    {
        var resource = ResourceBuilder.CreateDefault().AddService(serviceName);
        TracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .AddLissieSources()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter()
            .Build();
        MeterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resource)
            .AddLissieMeters()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter((_, reader) =>
                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = LissieTelemetry.MetricExportIntervalMilliseconds)
            .Build();
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(logging => logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resource);
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.AddOtlpExporter();
        }));
    }

    public TracerProvider TracerProvider { get; }

    public MeterProvider MeterProvider { get; }

    public ILoggerFactory LoggerFactory { get; }

    public void Dispose()
    {
        LoggerFactory.Dispose();
        MeterProvider.Dispose();
        TracerProvider.Dispose();
    }
}
