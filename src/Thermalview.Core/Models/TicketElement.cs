using System.Text.Json.Serialization;

namespace Thermalview.Core.Models;

/// <summary>
/// Base class for all ticket elements.
/// Uses polymorphic JSON serialization so the frontend can identify element types.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextElement), "text")]
[JsonDerivedType(typeof(ImageElement), "image")]
[JsonDerivedType(typeof(BarcodeElement), "barcode")]
[JsonDerivedType(typeof(CutElement), "cut")]
[JsonDerivedType(typeof(FeedElement), "feed")]
public abstract class TicketElement;

/// <summary>
/// A line of text with formatting attributes.
/// </summary>
public class TextElement : TicketElement
{
    /// <summary>
    /// The text content of this line.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Text alignment: "left", "center", or "right".
    /// </summary>
    public string Align { get; set; } = "left";

    /// <summary>
    /// Whether the text is bold.
    /// </summary>
    public bool Bold { get; set; }

    /// <summary>
    /// Whether the text is underlined.
    /// </summary>
    public bool Underline { get; set; }

    /// <summary>
    /// Font size multiplier. 1 = normal, 2 = double height/width.
    /// </summary>
    public int FontSize { get; set; } = 1;

    /// <summary>
    /// Whether this text uses the secondary (Font B) typeface.
    /// Font B is typically narrower with more characters per line.
    /// </summary>
    public bool FontB { get; set; }
}

/// <summary>
/// A raster image embedded in the ticket.
/// </summary>
public class ImageElement : TicketElement
{
    /// <summary>
    /// Base64-encoded image data (PNG format).
    /// </summary>
    public string DataBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public int Height { get; set; }
}

/// <summary>
/// A barcode element.
/// </summary>
public class BarcodeElement : TicketElement
{
    /// <summary>
    /// Barcode data content.
    /// </summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Barcode type (e.g., "CODE128", "EAN13", "QR").
    /// </summary>
    public string BarcodeType { get; set; } = "CODE128";
}

/// <summary>
/// Represents a paper cut command.
/// </summary>
public class CutElement : TicketElement
{
    /// <summary>
    /// Whether this is a full cut (true) or partial cut (false).
    /// </summary>
    public bool FullCut { get; set; } = true;
}

/// <summary>
/// Represents a paper feed (line spacing / blank lines).
/// </summary>
public class FeedElement : TicketElement
{
    /// <summary>
    /// Number of lines or dots to feed.
    /// </summary>
    public int Lines { get; set; } = 1;
}
