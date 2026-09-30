using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Lissie.Common.Middleware;

public static class MiddlewareExtensions
{
    extension(AIAgentBuilder builder)
    {
        public AIAgentBuilder UseActivityLog(ILogger logger)
        {
            var activityLog = new ActivityLog(logger);
            return builder.Use(runFunc: activityLog.RunAsync, runStreamingFunc: activityLog.RunStreamingAsync);
        }

        public AIAgentBuilder UseDietPolicy(DietLimits limits, ILogger logger) => builder.Use(new DietPolicy(limits, logger).InvokeAsync);

        public AIAgentBuilder UseProtectedObjects(IReadOnlyList<string> items, ILogger logger) => builder.Use(new ProtectedObjects(items, logger).InvokeAsync);
    }

    extension(ChatClientBuilder builder)
    {
        public ChatClientBuilder UsePromptInjectionGuard(ILogger logger)
        {
            var guard = new PromptInjectionGuard(logger);
            return builder.Use(guard.GetResponseAsync, guard.GetStreamingResponseAsync);
        }
    }
}
