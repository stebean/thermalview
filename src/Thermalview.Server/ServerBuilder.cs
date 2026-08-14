using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;
using Thermalview.Core.Models;
using Thermalview.Core.Services;
using Thermalview.Parser;
using Thermalview.Server.Hubs;

namespace Thermalview.Server;

/// <summary>
/// Configures and builds the ASP.NET Core web application.
/// </summary>
public class ServerBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };

    /// <summary>
    /// Creates and configures the web application with all endpoints.
    /// </summary>
    /// <param name="printerName">Name of the printer this server instance serves.</param>
    /// <param name="port">Port to listen on.</param>
    /// <param name="frontendPath">
    /// Optional path to the frontend directory on disk.
    /// When null or not found, falls back to embedded resources.
    /// </param>
    public static WebApplication Build(string printerName, int port, string? frontendPath = null)
    {
        var builder = WebApplication.CreateBuilder();

        // Configure Kestrel to listen on the specified port
        builder.WebHost.UseUrls($"http://localhost:{port}");

        // Register services
        builder.Services.AddSingleton<TicketHub>();
        builder.Services.AddSingleton<EscPosParser>();
        builder.Services.AddSingleton<ConfigStore>();

        // Suppress default ASP.NET Core logging noise
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var app = builder.Build();

        // Enable WebSockets
        app.UseWebSockets();

        // ── Static file serving ──
        // Priority: filesystem (dev) → embedded resources (production binary)
        IFileProvider fileProvider = BuildFileProvider(frontendPath);

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

        // --- API Endpoints ---

        MapPrintEndpoint(app, printerName);
        MapWebSocketEndpoint(app);
        MapConfigEndpoints(app, printerName);
        MapTicketEndpoints(app);

        return app;
    }

    /// <summary>
    /// Builds the file provider for serving static frontend files.
    /// Uses the filesystem when a valid path is provided (dev mode),
    /// otherwise serves from embedded resources baked into the assembly.
    /// </summary>
    private static IFileProvider BuildFileProvider(string? frontendPath)
    {
        // Dev mode: use the filesystem when the directory exists
        if (frontendPath is not null && Directory.Exists(frontendPath))
        {
            Console.WriteLine($"[Server] Serving frontend from disk: {frontendPath}");
            return new PhysicalFileProvider(frontendPath);
        }

        // Production: serve from embedded resources
        Console.WriteLine("[Server] Serving frontend from embedded resources");
        var assembly = typeof(ServerBuilder).Assembly;
        try
        {
            return new ManifestEmbeddedFileProvider(assembly, "frontend");
        }
        catch
        {
            return new EmbeddedFileProvider(assembly, "frontend");
        }
    }

    /// <summary>
    /// POST /api/print — Receives raw ESC/POS bytes from the CUPS backend.
    /// Parses and broadcasts via WebSocket.
    /// </summary>
    private static void MapPrintEndpoint(WebApplication app, string printerName)
    {
        app.MapPost("/api/print", async (HttpContext ctx) =>
        {
            var hub = ctx.RequestServices.GetRequiredService<TicketHub>();
            var parser = ctx.RequestServices.GetRequiredService<EscPosParser>();
            var configStore = ctx.RequestServices.GetRequiredService<ConfigStore>();

            // Read raw bytes from the request body
            using var ms = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(ms);
            var rawData = ms.ToArray();

            if (rawData.Length == 0)
            {
                ctx.Response.StatusCode = 400;
                return Results.Json(new { error = "Empty request body" }, JsonOptions);
            }

            // Get printer config for width info
            var printer = configStore.GetPrinter(printerName);
            int widthMm = printer?.WidthMm ?? 80;
            int charsPerLine = printer?.CharsPerLine ?? 48;

            // Parse ESC/POS data
            var ticket = parser.Parse(rawData, printerName, widthMm, charsPerLine);

            Console.WriteLine($"[Print] Received {rawData.Length} bytes → {ticket.Elements.Count} elements (ticket {ticket.Id})");

            // Save to history
            configStore.SaveTicket(ticket);

            // Broadcast to all connected browsers
            await hub.BroadcastTicketAsync(ticket);

            return Results.Json(new
            {
                success = true,
                ticketId = ticket.Id,
                elements = ticket.Elements.Count,
                rawBytes = rawData.Length
            }, JsonOptions);
        });
    }

    /// <summary>
    /// WebSocket endpoint at /ws/tickets for real-time ticket streaming.
    /// </summary>
    private static void MapWebSocketEndpoint(WebApplication app)
    {
        app.Map("/ws/tickets", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("WebSocket connections only");
                return;
            }

            var hub = ctx.RequestServices.GetRequiredService<TicketHub>();
            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            await hub.HandleConnectionAsync(ws, ctx.RequestAborted);
        });
    }

    /// <summary>
    /// GET /api/config — Returns current printer configuration and active printer details.
    /// </summary>
    private static void MapConfigEndpoints(WebApplication app, string printerName)
    {
        app.MapGet("/api/config", (HttpContext ctx) =>
        {
            var configStore = ctx.RequestServices.GetRequiredService<ConfigStore>();
            var printers = configStore.ListPrinters();
            var activePrinter = configStore.GetPrinter(printerName);
            return Results.Json(new { activePrinterName = printerName, activePrinter, printers }, JsonOptions);
        });
    }

    /// <summary>
    /// GET /api/tickets — Returns recent ticket history.
    /// DELETE /api/tickets — Clears ticket history.
    /// </summary>
    private static void MapTicketEndpoints(WebApplication app)
    {
        app.MapGet("/api/tickets", (HttpContext ctx) =>
        {
            var configStore = ctx.RequestServices.GetRequiredService<ConfigStore>();
            var count = int.TryParse(ctx.Request.Query["count"], out var c) ? c : 50;
            var tickets = configStore.LoadRecentTickets(count);
            return Results.Json(new { tickets, total = tickets.Count }, JsonOptions);
        });

        app.MapDelete("/api/tickets", (HttpContext ctx) =>
        {
            var configStore = ctx.RequestServices.GetRequiredService<ConfigStore>();
            configStore.ClearHistory();
            return Results.Json(new { success = true }, JsonOptions);
        });
    }
}
