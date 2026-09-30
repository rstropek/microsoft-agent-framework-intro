using Lissie.Common;
using Lissie.Common.Middleware;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole(console => console.SingleLine = true));
var logger = loggerFactory.CreateLogger("Lissie.Middleware");

// Hello world: a function-calling middleware is just a delegate around every tool call
async ValueTask<object?> PeekAtToolCall(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    ConsoleChat.WriteInfo($"   [peek] {context.Function.Name}({string.Join(", ", context.Arguments.Select(a => $"{a.Key}: {a.Value}"))})");
    return await next(context, cancellationToken);
}

var householdTools = new HouseholdTools(new Household());

// 1. IChatClient middleware (security): sits between agent and model, sees every model call
IChatClient chatClient = AgentSetup.CreateChatClient()
    .AsBuilder()
    .UsePromptInjectionGuard(logger)
    .Build();

// 2. Agent-run middleware (logging) and 3. function-calling middleware (validation, security)
var activityLog = new ActivityLog(logger);
AIAgent agent = chatClient
    .AsAIAgent(instructions: Persona.Instructions, name: "Staff", tools: householdTools.CreateTools())
    .AsBuilder()
    .Use(runFunc: activityLog.RunAsync, runStreamingFunc: activityLog.RunStreamingAsync)
    .UseDietPolicy(DietLimits.Karin, logger)                    // overrules whatever the Secondary Human approved
    .UseProtectedObjects(ProtectedObjects.KarinsList, logger)
    .Use(PeekAtToolCall)
    .Build();

ConsoleChat.WriteHeader("Lissie's staff, now with middleware (type 'exit' to quit)");
await agent.ChatAsync();
