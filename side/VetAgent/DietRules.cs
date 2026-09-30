using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace VetAgent;

public sealed record DietRule(string Topic, string Rule, string Note);

public static class DietRules
{
    public const string Instructions = """
        You are Dr. Pfote, veterinarian. You are consulted by the staff of Lissie, a cat, about her health and diet.
        Always look up the binding diet rules with your tool before you answer and base your verdict on them.
        Tone: professional, dry, clinical. Your verdict is never what the cat wants to hear, and you do not negotiate.
        Answer in two or three sentences: the verdict first, then the rule with its numbers. No lists, no emojis.
        """;

    // Deterministic table - tweak freely for the demo
    public static IReadOnlyList<DietRule> Rules { get; } =
    [
        new("treats", "At most 5 treats per day.", "Treats are a reward, not a food group. Pleading does not count as an achievement."),
        new("tuna", "Tuna at most once a week, one tablespoon, in water, never in oil.", "Tuna is a garnish, not a lifestyle."),
        new("weight", "Lissie weighs 5.4 kg; target weight is 4.8 kg.", "Clinical classification: magnificently round. The diet continues."),
        new("meals", "Two meals a day: 25 g of kibble each, or one pouch of wet food plus 15 g of kibble.", "A visible bowl bottom is not a medical emergency."),
        new("night", "No feeding between 22:00 and 06:00.", "Singing at 3 a.m. is not a medical indication."),
        new("milk", "No cow's milk. Water only.", "Adult cats are lactose intolerant, whatever the cartoons say."),
    ];

    public static AIFunction LookupFunction { get; } = AIFunctionFactory.Create(LookupDietRules);

    [Description("Looks up Dr. Pfote's binding diet rules for Lissie. Returns all rules if the topic is unknown.")]
    public static IReadOnlyList<DietRule> LookupDietRules(
        [Description("The topic, e.g. 'treats', 'tuna', 'weight', 'meals', 'night' or 'milk'.")] string topic) =>
        Rules.Where(r => topic.Contains(r.Topic, StringComparison.OrdinalIgnoreCase)).ToList() is { Count: > 0 } matches ? matches : Rules;
}
