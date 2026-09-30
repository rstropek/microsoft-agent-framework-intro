using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Lissie.Workflows;

[JsonConverter(typeof(JsonStringEnumConverter<ComplaintCategory>))]
public enum ComplaintCategory { Food, Attention, Outrage }

[JsonConverter(typeof(JsonStringEnumConverter<Urgency>))]
public enum Urgency { Low, Medium, High, Catastrophic }

// Structured output of the triage agent: the JSON schema is generated from this record
public sealed record ComplaintTriage(
    ComplaintCategory Category,
    Urgency Urgency,
    [property: Description("One line, third person, at most 15 words.")] string Summary);

public sealed record CaseFile(string CaseNumber, ComplaintTriage Triage)
{
    public ComplaintCategory Category => Triage.Category;

    public string Briefing => $"Case {CaseNumber}. Urgency: {Triage.Urgency}. Her Majesty's complaint: {Triage.Summary}";
}

public sealed record Resolution(CaseFile Case, string Specialist, string Text);
