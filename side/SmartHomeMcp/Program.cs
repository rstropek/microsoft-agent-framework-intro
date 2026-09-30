var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "SmartHomeMcp - coming soon");

app.Run();
