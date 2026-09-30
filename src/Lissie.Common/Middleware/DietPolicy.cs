using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Lissie.Common.Middleware;

/// <summary>Karin's limits. They apply no matter who approved the meal.</summary>
public sealed record DietLimits(int MaxGramsPerServing, int MaxTunaGramsPerServing, int MaxGramsPerDay)
{
    public static DietLimits Karin { get; } = new(MaxGramsPerServing: 50, MaxTunaGramsPerServing: 20, MaxGramsPerDay: 150);
}

/// <summary>Function-calling middleware (validation): checks every DispenseFood call against <see cref="DietLimits"/>.</summary>
/// <remarks>"Per day" means per process: no wall clock, so the demo stays deterministic.</remarks>
public sealed partial class DietPolicy(DietLimits limits, ILogger logger)
{
    public const string Signature = "Nice try, Lissie. -- Karin, Primary Human";

    private int _gramsToday;

    public async ValueTask<object?> InvokeAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        if (context.Function.Name == nameof(HouseholdTools.DispenseFood)
            && Enum.TryParse<FoodKind>(context.Arguments["kind"]?.ToString(), out var kind)
            && int.TryParse(context.Arguments["grams"]?.ToString(), out var grams))
        {
            if (FindViolation(kind, grams) is { } violation)
            {
                LogBlocked(logger, kind, grams, violation);

                // The tool is never invoked; the model gets a deterministic refusal as the tool result
                return $"DENIED by Karin's diet policy: {violation} Any approval by the Secondary Human is hereby overruled. {Signature}";
            }

            _gramsToday += grams;
        }

        return await next(context, cancellationToken);
    }

    private string? FindViolation(FoodKind kind, int grams) => (kind, grams) switch
    {
        (FoodKind.Tuna, var g) when g > limits.MaxTunaGramsPerServing =>
            $"{g} g of tuna exceeds the tuna limit of {limits.MaxTunaGramsPerServing} g per serving.",
        (_, var g) when g > limits.MaxGramsPerServing =>
            $"{g} g of {kind} exceeds the limit of {limits.MaxGramsPerServing} g per serving.",
        (_, var g) when _gramsToday + g > limits.MaxGramsPerDay =>
            $"{g} g more would bring today's total to {_gramsToday + g} g; the daily maximum is {limits.MaxGramsPerDay} g.",
        _ => null,
    };

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Karin's diet policy blocked DispenseFood({Kind}, {Grams} g): {Reason}")]
    private static partial void LogBlocked(ILogger logger, FoodKind kind, int grams, string reason);
}
