// Thermalview Server — ASP.NET Core setup will be added in Phase 4
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Thermalview is running");

app.Run();
