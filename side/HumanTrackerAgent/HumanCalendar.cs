using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace HumanTrackerAgent;

public sealed record Whereabouts(string Name, string Rank, string Location, string Activity, string BackAt);

public static class HumanCalendar
{
    public const string Instructions = """
        You are the Human Tracker of Lissie's household, a cat's answer to air traffic control.
        You know where the humans are and when they will be back. Always check the calendar with your tool; never guess.
        Karin is the Primary Human (by far), Rainer the Secondary Human.
        Report like a departure board with a dry sense of humor: who, where, doing what, back when.
        Answer in two or three sentences. No lists, no emojis.
        """;

    // Hard-coded calendars - tweak freely for the demo
    public static IReadOnlyList<Whereabouts> Entries { get; } =
    [
        new("Karin", "Primary Human", "the supermarket", "buying groceries, cat food included (allegedly)", "in about 45 minutes"),
        new("Rainer", "Secondary Human", "on stage at the BASTA! conference", "giving a talk about AI agents instead of petting Lissie", "this evening"),
    ];

    public static AIFunction WhereIsFunction { get; } = AIFunctionFactory.Create(WhereIs);

    [Description("Returns where a human currently is, what they are doing and when they will be back. Returns all humans if the name is unknown.")]
    public static IReadOnlyList<Whereabouts> WhereIs(
        [Description("Name or rank of the human, e.g. 'Karin', 'Rainer', 'Primary', 'Secondary' or 'all'.")] string human) =>
        Entries.Where(e => e.Name.Contains(human, StringComparison.OrdinalIgnoreCase) || e.Rank.StartsWith(human, StringComparison.OrdinalIgnoreCase))
            .ToList() is { Count: > 0 } matches ? matches : Entries;
}
