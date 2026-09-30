var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "VetAgent - coming soon");

app.Run();
