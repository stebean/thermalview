using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Thermalview.Core.Models;

namespace Thermalview.Server.Hubs;

/// <summary>
/// Lightweight WebSocket hub that manages browser connections
/// and broadcasts parsed ticket data in real time.
/// No SignalR dependency — uses raw WebSockets for minimal footprint.
/// </summary>
public class TicketHub
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };

    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();

    /// <summary>
    /// Number of currently connected clients.
    /// </summary>
    public int ClientCount => _clients.Count;

    /// <summary>
    /// Accepts and manages a new WebSocket connection.
    /// Keeps the connection alive until the client disconnects.
    /// </summary>
    public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken ct)
    {
        var clientId = Guid.NewGuid().ToString("N")[..8];
        _clients.TryAdd(clientId, webSocket);

        Console.WriteLine($"[WebSocket] Client {clientId} connected ({ClientCount} total)");

        try
        {
            // Send a welcome message
            var welcome = new { type = "connected", clientId, message = "Thermalview WebSocket connected" };
            await SendToClientAsync(webSocket, welcome, ct);

            // Keep connection alive — listen for close/ping
            var buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch (WebSocketException)
        {
            // Client disconnected unexpectedly
        }
        catch (OperationCanceledException)
        {
            // Server shutting down
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            Console.WriteLine($"[WebSocket] Client {clientId} disconnected ({ClientCount} total)");

            if (webSocket.State == WebSocketState.Open ||
                webSocket.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Server closing connection",
                        CancellationToken.None);
                }
                catch { /* Best effort */ }
            }
        }
    }

    /// <summary>
    /// Broadcasts a new ticket to all connected WebSocket clients.
    /// </summary>
    public async Task BroadcastTicketAsync(TicketData ticket)
    {
        var message = new { type = "ticket", data = ticket };

        var deadClients = new List<string>();

        foreach (var (clientId, socket) in _clients)
        {
            if (socket.State != WebSocketState.Open)
            {
                deadClients.Add(clientId);
                continue;
            }

            try
            {
                await SendToClientAsync(socket, message, CancellationToken.None);
            }
            catch
            {
                deadClients.Add(clientId);
            }
        }

        // Cleanup dead connections
        foreach (var id in deadClients)
            _clients.TryRemove(id, out _);

        Console.WriteLine($"[WebSocket] Broadcasted ticket {ticket.Id} to {ClientCount} client(s)");
    }

    /// <summary>
    /// Sends a JSON message to a single client.
    /// </summary>
    private static async Task SendToClientAsync(WebSocket socket, object message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        await socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            ct);
    }
}
