// This file exists for the ASP.NET Core project template.
// The actual entry point is Thermalview.Cli which uses ServerBuilder to start the server.
// This allows running the server standalone for development:
//   dotnet run --project src/Thermalview.Server

using Thermalview.Server;

var frontendPath = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "..", "frontend"));

var app = ServerBuilder.Build(
    printerName: "dev",
    port: 5000,
    frontendPath: frontendPath);

Console.WriteLine("Thermalview server starting on http://localhost:5000");
Console.WriteLine($"Frontend path: {frontendPath}");

app.Run();
