namespace Lissie.SmartHome;

public enum CatFlapMode { Locked, InOnly, OutOnly, Open }

public enum HeatingLevel { Off, Cozy, Toasty, Tropical }

public sealed record FoodBowlLevel(int KibbleGrams, int CapacityGrams, int TreatsInBowl, int TreatsDispensedToday, string Verdict);
