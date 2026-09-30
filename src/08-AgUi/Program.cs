var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "08-AgUi - coming soon");

app.Run();
