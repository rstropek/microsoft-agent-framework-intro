using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Lissie.Common.Telemetry;

public static class LissieTelemetry
{
    extension(IHostApplicationBuilder builder)
    {
        /// <summary>
        /// Traces, metrics and logs via OTLP (OTEL_EXPORTER_OTLP_ENDPOINT, default http://localhost:4317 = Aspire dashboard).
        /// HttpClient instrumentation injects traceparent: one connected trace across processes.
        /// </summary>
        public IHostApplicationBuilder AddLissieTelemetry(string serviceName)
        {
            // Source and meter names verified against the restored packages (Agent Framework, M.E.AI, Workflows, MCP, A2A)
            string[] sources = ["Experimental.Microsoft.Agents.AI", "Experimental.Microsoft.Extensions.AI", "Microsoft.Agents.AI.Workflows",
                "Experimental.ModelContextProtocol", "A2A", "A2A.AspNetCore"];
            string[] meters = ["Experimental.Microsoft.Agents.AI", "Experimental.Microsoft.Extensions.AI", "Experimental.ModelContextProtocol", "A2A"];

            builder.Logging.AddOpenTelemetry(logging => logging.IncludeFormattedMessage = true);
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName))
                .WithTracing(tracing => tracing.AddSource(sources).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
                .WithMetrics(metrics => metrics.AddMeter(meters).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
                .UseOtlpExporter();

            // Metrics every 5 s instead of 60 s: the dashboard reacts while the audience watches
            builder.Services.Configure<MetricReaderOptions>(reader => reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000);
            return builder;
        }
    }
}
