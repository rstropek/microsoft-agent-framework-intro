using System.Text.Json;
using Lissie.Common;
using Lissie.Workflows;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

var chatClient = AgentSetup.CreateChatClient();
var mode = args.FirstOrDefault() ?? "complaint";

if (mode == "sequential")
{
    // (a) The 3-line entry: two agents in a row, the second one sees what the first one said
    ConsoleChat.WriteHeader("Sequential: Lissie rants, the diplomat translates");
    Workflow sequential = AgentWorkflowBuilder.BuildSequential(
        chatClient.CreateAgent("Lissie", Staff.Lissie),
        chatClient.CreateAgent("Diplomat", Staff.Diplomat));
    await WorkflowTimeline.RunAsync(sequential, args.ElementAtOrDefault(1) ?? "The humans moved my food bowl 20 centimeters to the left.");
    return;
}

// (b) The Complaint Department: a graph of agents and hand-written executors
// Agent bound directly into the graph; structured output: the model must answer with the JSON schema of ComplaintTriage
var triage = chatClient.CreateAgent("Triage", Staff.Triage, ChatResponseFormat.ForJsonSchema<ComplaintTriage>(JsonSerializerOptions.Web))
    .BindAsExecutor(new AIAgentHostOptions { ForwardIncomingMessages = false });
var openCase = new OpenCaseExecutor();
var food = new SpecialistExecutor(chatClient.CreateAgent("FoodNegotiator", Staff.FoodNegotiator));
var cuddle = new SpecialistExecutor(chatClient.CreateAgent("CuddleCoordinator", Staff.CuddleCoordinator));
var drama = new SpecialistExecutor(chatClient.CreateAgent("DramaEscalation", Staff.DramaEscalation));
var notice = new NoticeExecutor();

Workflow workflow = new WorkflowBuilder(triage)
    .WithName("ComplaintDepartment")
    .AddEdge(triage, openCase)
    .AddSwitch(openCase, route => route
        .AddCase<CaseFile>(c => c is { Category: ComplaintCategory.Food }, food)
        .AddCase<CaseFile>(c => c is { Category: ComplaintCategory.Attention }, cuddle)
        .WithDefault(drama))
    .AddEdge(food, notice)
    .AddEdge(cuddle, notice)
    .AddEdge(drama, notice)
    .WithOutputFrom(notice)
    .Build();

if (mode == "diagram")
{
    Console.WriteLine(workflow.ToMermaidString());
    return;
}

ConsoleChat.WriteHeader("Complaint Department (modes: sequential | complaint | diagram; 'exit' to quit)");
foreach (var complaint in args.Length > 1 ? args[1..] : ReadComplaints())
{
    await WorkflowTimeline.RunAsync(workflow, complaint);
}

static IEnumerable<string> ReadComplaints()
{
    while (ConsoleChat.ReadPrompt() is { } complaint)
    {
        yield return complaint;
    }
}
