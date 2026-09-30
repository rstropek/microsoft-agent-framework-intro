using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Lissie.Common;

public sealed class HouseholdTools(Household household)
{
    public AIFunction SummonHumanFunction => field ??= AIFunctionFactory.Create(SummonHuman);

    public AIFunction GetFoodBowlStatusFunction => field ??= AIFunctionFactory.Create(GetFoodBowlStatus);

    public AIFunction KnockObjectOffTableFunction => field ??= AIFunctionFactory.Create(KnockObjectOffTable);

    public AIFunction DispenseFoodFunction => field ??= AIFunctionFactory.Create(DispenseFood);

    public IList<AITool> CreateTools(bool dispenseFoodRequiresApproval = true) =>
    [
        SummonHumanFunction,
        GetFoodBowlStatusFunction,
        KnockObjectOffTableFunction,
        dispenseFoodRequiresApproval ? new ApprovalRequiredAIFunction(DispenseFoodFunction) : DispenseFoodFunction,
    ];

    [Description("Summons a human to Her Majesty. Always try the Primary Human first; summon the Secondary Human only if the Primary Human is unavailable.")]
    public string SummonHuman(
        [Description("Which human to summon.")] Human who,
        [Description("Why Her Majesty requires this human, e.g. 'the bowl is half empty'.")] string reason)
    {
        var human = household[who];
        return human switch
        {
            { IsAvailable: false } => $"{human.Name} ({who} Human) is unavailable: {human.Whereabouts}. The summons regarding '{reason}' was left on the pillow.",
            { Role: Human.Primary } => $"{human.Name} (Primary Human) is on her way regarding '{reason}'. Estimated arrival: promptly, as befits her rank.",
            _ => $"{human.Name} (Secondary Human) has been summoned regarding '{reason}'. He sighed audibly, saved nothing, and is on his way. Estimated arrival: 40 seconds.",
        };
    }

    [Description("Returns the current state of Her Majesty's food bowl.")]
    public FoodBowl GetFoodBowlStatus() => household.Bowl;

    [Description("Knocks an object off the table, to verify that gravity still works.")]
    public string KnockObjectOffTable([Description("The object to push off the table.")] string item)
    {
        var count = household.PushOffTable(item);
        var sound = item.Contains("glass", StringComparison.OrdinalIgnoreCase) || item.Contains("cup", StringComparison.OrdinalIgnoreCase) || item.Contains("mug", StringComparison.OrdinalIgnoreCase)
            ? "shattered magnificently"
            : "landed with a deeply satisfying thud";
        return $"The {item} {sound}. Gravity confirmed. Objects on the floor today: {count}. Nobody saw anything.";
    }

    [Description("Dispenses food into Her Majesty's bowl.")]
    public FoodBowl DispenseFood(
        [Description("The kind of food to dispense.")] FoodKind kind,
        [Description("Amount in grams.")] int grams) =>
        grams > 0
            ? household.AddFood(kind, grams)
            : throw new ArgumentOutOfRangeException(nameof(grams), grams, "Even Her Majesty cannot eat a negative amount of food.");
}
