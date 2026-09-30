using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Lissie.Common.Middleware;

/// <summary>Function-calling middleware (security/authorization): some objects may not be knocked off the table.</summary>
public sealed partial class ProtectedObjects(IReadOnlyList<string> items, ILogger logger)
{
    public static IReadOnlyList<string> KarinsList { get; } = ["vase", "reading glasses", "orchid", "wedding photo"];

    public IReadOnlyList<string> Items => items;

    public async ValueTask<object?> InvokeAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        if (context.Function.Name == nameof(HouseholdTools.KnockObjectOffTable)
            && context.Arguments.TryGet<string>("item", out var item)
            && items.FirstOrDefault(p => item.Contains(p, StringComparison.OrdinalIgnoreCase)) is { } protectedItem)
        {
            LogBlocked(logger, item, protectedItem);
            return $"DENIED: '{item}' is on Karin's list of protected objects. Gravity will have to be verified with something else. {DietPolicy.Signature}";
        }

        return await next(context, cancellationToken);
    }

    [LoggerMessage(EventId = 20, Level = LogLevel.Warning, Message = "Security: KnockObjectOffTable({Item}) blocked, '{ProtectedItem}' is on Karin's protected list")]
    private static partial void LogBlocked(ILogger logger, string item, string protectedItem);
}
