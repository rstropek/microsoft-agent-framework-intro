using System.ComponentModel;
using Lissie.Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// A tool is just a method with descriptions for the model
[Description("Rates the quality of a nap spot on a scale from 1 to 10.")]
static string RateNapSpot([Description("The spot to evaluate, e.g. 'keyboard'.")] string spot) => spot.ToUpperInvariant() switch
{
    var s when s.Contains("KEYBOARD") || s.Contains("LAPTOP") => $"{spot}: 10/10. Warm, and it stops the Secondary Human from working.",
    var s when s.Contains("BOX") => $"{spot}: 9/10. If it fits, it sits.",
    var s when s.Contains("BED") => $"{spot}: 3/10. Far too obviously intended for napping.",
    _ => $"{spot}: 6/10. Acceptable, pending inspection.",
};

var household = new Household();
var householdTools = new HouseholdTools(household);

AIAgent agent = AgentSetup.CreateChatClient().AsAIAgent(
    instructions: Persona.Instructions,
    name: "Staff",
    tools:
    [
        AIFunctionFactory.Create(RateNapSpot, name: nameof(RateNapSpot)),
        householdTools.SummonHumanFunction,
        householdTools.GetFoodBowlStatusFunction,
        householdTools.KnockObjectOffTableFunction,
        // Human-in-the-loop: the literal human has to approve every meal
        new ApprovalRequiredAIFunction(householdTools.DispenseFoodFunction),
    ]);

ConsoleChat.WriteHeader("Lissie's staff, now with tools (type 'exit' to quit)");
AgentSession session = await agent.CreateSessionAsync();
while (ConsoleChat.ReadPrompt() is { } input)
{
    AgentResponse response = await agent.StreamToConsoleAsync(input, session);

    // The run stops with approval requests instead of calling DispenseFood; we answer and continue
    while (response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList() is { Count: > 0 } requests)
    {
        List<ChatMessage> answers = [.. requests.Select(request =>
            new ChatMessage(ChatRole.User, [request.CreateResponse(ConsoleChat.Confirm(request))]))];
        response = await agent.StreamToConsoleAsync(answers, session);
    }
}
