namespace Thermalview.Core.Models;

/// <summary>
/// Represents a fully parsed ticket ready for rendering.
/// This is the JSON payload sent to the frontend via WebSocket.
/// </summary>
public class TicketData
{
    /// <summary>
    /// Unique identifier for this ticket.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Name of the printer that received this ticket.
    /// </summary>
    public string PrinterName { get; set; } = string.Empty;

    /// <summary>
    /// Ordered list of elements that compose the ticket content.
    /// </summary>
    public List<TicketElement> Elements { get; set; } = [];

    /// <summary>
    /// Paper width in mm used when parsing this ticket.
    /// </summary>
    public int WidthMm { get; set; } = 80;

    /// <summary>
    /// Characters per line for the target paper width.
    /// </summary>
    public int CharsPerLine { get; set; } = 48;

    /// <summary>
    /// Timestamp when the ticket was received.
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Size of the raw ESC/POS data in bytes.
    /// </summary>
    public int RawSizeBytes { get; set; }
}
