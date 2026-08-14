namespace Thermalview.Core.Models;

/// <summary>
/// Represents a virtual thermal printer registered in the system.
/// </summary>
public class PrinterConfig
{
    /// <summary>
    /// User-defined name for the printer (e.g., "receipt-80mm").
    /// Also used as the CUPS queue name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Paper width in millimeters. Common values: 58, 80, 112.
    /// </summary>
    public int WidthMm { get; set; } = 80;

    /// <summary>
    /// Number of printable characters per line, derived from paper width.
    /// 58mm ≈ 32 chars, 80mm ≈ 48 chars, 112mm ≈ 69 chars.
    /// </summary>
    public int CharsPerLine => WidthMm switch
    {
        58 => 32,
        80 => 48,
        112 => 69,
        _ => (int)(WidthMm * 0.6) // approximate fallback
    };

    /// <summary>
    /// Port the server will listen on when started for this printer.
    /// </summary>
    public int Port { get; set; } = 5000;

    /// <summary>
    /// Timestamp when this printer was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
