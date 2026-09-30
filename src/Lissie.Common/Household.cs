namespace Lissie.Common;

public enum Human { Primary, Secondary }

public enum FoodKind { Kibble, WetFood, Tuna, Treats }

public sealed record HumanStatus(Human Role, string Name, bool IsAvailable, string Whereabouts);

public sealed record FoodBowl(FoodKind Content, int Grams, int CapacityGrams)
{
    public string Verdict => Grams switch
    {
        0 => "Empty. A national emergency.",
        < 20 => "The bottom of the bowl is visible. Unacceptable.",
        < 100 => "Adequate, by human standards.",
        _ => "Full. Her Majesty may now ignore it.",
    };
}

public sealed class Household
{
    private readonly Lock _lock = new();
    private readonly List<string> _floor = [];

    public HumanStatus Karin { get; init; } = new(Human.Primary, "Karin", IsAvailable: false, "out for groceries, back in about 45 minutes");

    public HumanStatus Rainer { get; init; } = new(Human.Secondary, "Rainer", IsAvailable: true, "in the home office, pretending to be busy");

    public FoodBowl Bowl { get; private set; } = new(FoodKind.Kibble, Grams: 12, CapacityGrams: 250);

    public HumanStatus this[Human who] => who is Human.Primary ? Karin : Rainer;

    public FoodBowl AddFood(FoodKind kind, int grams)
    {
        lock (_lock)
        {
            return Bowl = Bowl with { Content = kind, Grams = Math.Min(Bowl.CapacityGrams, Bowl.Grams + grams) };
        }
    }

    public int PushOffTable(string item)
    {
        lock (_lock)
        {
            _floor.Add(item);
            return _floor.Count;
        }
    }
}
