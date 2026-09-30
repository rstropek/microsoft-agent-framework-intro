using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Lissie.SmartHome;

// Plain C# methods + [Description]. Depends only on Microsoft.Extensions.AI.Abstractions:
// no Agent Framework, no MCP. Both hosts consume the very same AIFunction instances.
public sealed class SmartHomeTools
{
    public const int MaxTreatsPerOrder = 5;
    public const int DailyTreatLimit = 12;
    public const int MaxLaserMinutes = 15;

    public const string AgentInstructions = """
        You also operate Her Majesty's smart home: food bowl sensor, treat dispenser, cat flap, heating pad and laser pointer.
        Use these devices whenever Her Majesty asks for them, and report the device's answer faithfully.
        """;

    private const int KibbleGrams = 12;
    private const int BowlCapacityGrams = 250;

    private readonly Lock _lock = new();
    private int _treatsInBowl;
    private int _treatsToday;

    public IReadOnlyList<AIFunction> Functions => field ??=
    [
        AIFunctionFactory.Create(GetFoodBowlLevel),
        AIFunctionFactory.Create(DispenseTreats),
        AIFunctionFactory.Create(SetCatFlap),
        AIFunctionFactory.Create(SetHeatingPad),
        AIFunctionFactory.Create(StartLaserPointer),
    ];

    [Description("Reads the smart food bowl sensor: kibble left, treats in the bowl and treats dispensed today.")]
    public FoodBowlLevel GetFoodBowlLevel()
    {
        lock (_lock)
        {
            return new(KibbleGrams, BowlCapacityGrams, _treatsInBowl, _treatsToday, _treatsInBowl > 0
                ? "Treats detected. The situation is under control."
                : "The bottom of the bowl is visible. The sensor recommends immediate escalation.");
        }
    }

    [Description("Dispenses treats from the smart treat dispenser into the food bowl.")]
    public string DispenseTreats([Description("Number of treats to dispense, 1 to 5 per order.")] int count)
    {
        if (count is < 1 or > MaxTreatsPerOrder)
        {
            return $"Order rejected: the dispenser accepts 1 to {MaxTreatsPerOrder} treats per order. Larger orders require a written request to the Primary Human.";
        }

        lock (_lock)
        {
            if (_treatsToday + count > DailyTreatLimit)
            {
                return $"Order rejected: {_treatsToday} of {DailyTreatLimit} daily treats already dispensed. The dispenser has switched to 'Karin said no' mode.";
            }

            _treatsInBowl += count;
            _treatsToday += count;
            return $"Dispensed {count} treats with a satisfying rattle. Treats in the bowl: {_treatsInBowl}. Dispensed today: {_treatsToday} of {DailyTreatLimit}.";
        }
    }

    [Description("Sets the mode of the smart cat flap.")]
    public static string SetCatFlap([Description("Locked, InOnly (come home only), OutOnly (leave only) or Open.")] CatFlapMode mode) => mode switch
    {
        CatFlapMode.Open => "The cat flap is open in both directions. The neighbor's tomcat has been informed that this is not an invitation.",
        CatFlapMode.InOnly => "The cat flap now lets Her Majesty in but not out. Leaving will require staring at the door until a human opens it.",
        CatFlapMode.OutOnly => "The cat flap now lets Her Majesty out but not in. Returning will require meowing at the window, as tradition demands.",
        _ => "The cat flap is locked. Nothing gets in or out, including the Secondary Human's excuses.",
    };

    [Description("Sets the heating pad on Her Majesty's favorite sofa spot.")]
    public static string SetHeatingPad([Description("Off, Cozy (30 degrees), Toasty (36 degrees) or Tropical (42 degrees, the maximum).")] HeatingLevel level) => level switch
    {
        HeatingLevel.Cozy => "Heating pad set to Cozy (30 degrees). Ideal for a light afternoon nap of four to six hours.",
        HeatingLevel.Toasty => "Heating pad set to Toasty (36 degrees). Ready in three minutes; the Secondary Human has been asked to sit elsewhere.",
        HeatingLevel.Tropical => "Heating pad set to Tropical (42 degrees), the maximum. Warm enough to forget that the Primary Human is out.",
        _ => "Heating pad switched off. The sofa will have to do.",
    };

    [Description("Starts the automatic laser pointer for a play session.")]
    public static string StartLaserPointer([Description("Session length in minutes, 1 to 15.")] int minutes) =>
        minutes is < 1 or > MaxLaserMinutes
            ? $"Session rejected: laser sessions last 1 to {MaxLaserMinutes} minutes. Beyond that, the red dot demands union breaks."
            : $"Laser pointer active for {minutes} minutes. The red dot will circle the living room {minutes * 12} times and then vanish under the sofa, as always. It cannot be caught.";
}
