var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "HumanTrackerAgent - coming soon");

app.Run();
