using Thermalview.Core.Models;

namespace Thermalview.Parser;

/// <summary>
/// Parses raw ESC/POS byte streams into structured TicketData.
/// Pure logic, no I/O — fully unit-testable.
/// 
/// Supported commands:
///   ESC a n       — Set alignment (0=left, 1=center, 2=right)
///   ESC E n       — Bold on/off
///   ESC - n       — Underline on/off
///   ESC ! n       — Select print mode (font, bold, double height/width)
///   ESC d n       — Print and feed n lines
///   ESC @ 	      — Initialize printer (reset)
///   GS V n        — Cut paper (full/partial)
///   GS ! n        — Character size (width/height multiplier)
///   GS v 0        — Raster bit image
///   LF            — Line feed
///   HT            — Horizontal tab
/// </summary>
public class EscPosParser
{
    // ESC/POS constants
    private const byte ESC = 0x1B;
    private const byte GS = 0x1D;
    private const byte LF = 0x0A;
    private const byte CR = 0x0D;
    private const byte HT = 0x09;

    // Current formatting state
    private string _alignment = "left";
    private bool _bold;
    private bool _underline;
    private int _fontSize = 1;
    private bool _fontB;

    // Buffer for accumulating text
    private readonly List<char> _textBuffer = [];

    // Result elements
    private readonly List<TicketElement> _elements = [];

    /// <summary>
    /// Parses raw ESC/POS bytes into a structured TicketData object.
    /// </summary>
    /// <param name="rawData">Raw ESC/POS byte stream.</param>
    /// <param name="printerName">Name of the printer that received the data.</param>
    /// <param name="widthMm">Paper width in millimeters.</param>
    /// <param name="charsPerLine">Characters per line for the paper width.</param>
    public TicketData Parse(byte[] rawData, string printerName = "", int widthMm = 80, int charsPerLine = 48)
    {
        Reset();

        int i = 0;
        while (i < rawData.Length)
        {
            byte b = rawData[i];

            switch (b)
            {
                case ESC:
                    i = HandleEscCommand(rawData, i);
                    break;

                case GS:
                    i = HandleGsCommand(rawData, i);
                    break;

                case LF:
                    FlushTextBuffer();
                    i++;
                    break;

                case CR:
                    // Ignore CR, we handle LF
                    i++;
                    break;

                case HT:
                    // Tab → 4 spaces
                    _textBuffer.AddRange("    ");
                    i++;
                    break;

                default:
                    // Printable character — add to buffer
                    if (b >= 0x20 && b <= 0x7E)
                    {
                        _textBuffer.Add((char)b);
                    }
                    else if (b >= 0x80)
                    {
                        // Extended ASCII / code page character
                        _textBuffer.Add((char)b);
                    }
                    i++;
                    break;
            }
        }

        // Flush any remaining text
        FlushTextBuffer();

        return new TicketData
        {
            PrinterName = printerName,
            Elements = [.. _elements],
            WidthMm = widthMm,
            CharsPerLine = charsPerLine,
            RawSizeBytes = rawData.Length
        };
    }

    /// <summary>
    /// Resets the parser state for a new ticket.
    /// </summary>
    private void Reset()
    {
        _alignment = "left";
        _bold = false;
        _underline = false;
        _fontSize = 1;
        _fontB = false;
        _textBuffer.Clear();
        _elements.Clear();
    }

    /// <summary>
    /// Handles ESC (0x1B) commands.
    /// </summary>
    private int HandleEscCommand(byte[] data, int pos)
    {
        if (pos + 1 >= data.Length)
            return pos + 1;

        FlushTextBuffer();

        byte cmd = data[pos + 1];

        switch (cmd)
        {
            // ESC @ — Initialize printer
            case (byte)'@':
                Reset();
                return pos + 2;

            // ESC a n — Justification (alignment)
            case (byte)'a':
                if (pos + 2 < data.Length)
                {
                    _alignment = data[pos + 2] switch
                    {
                        0 => "left",
                        1 => "center",
                        2 => "right",
                        48 => "left",   // '0'
                        49 => "center", // '1'
                        50 => "right",  // '2'
                        _ => "left"
                    };
                    return pos + 3;
                }
                return pos + 2;

            // ESC E n — Bold emphasis
            case (byte)'E':
                if (pos + 2 < data.Length)
                {
                    _bold = data[pos + 2] != 0;
                    return pos + 3;
                }
                return pos + 2;

            // ESC - n — Underline
            case (byte)'-':
                if (pos + 2 < data.Length)
                {
                    _underline = data[pos + 2] != 0;
                    return pos + 3;
                }
                return pos + 2;

            // ESC ! n — Select print mode
            case (byte)'!':
                if (pos + 2 < data.Length)
                {
                    byte mode = data[pos + 2];
                    _fontB = (mode & 0x01) != 0;      // Bit 0: Font B
                    _bold = (mode & 0x08) != 0;        // Bit 3: Bold
                    _underline = (mode & 0x80) != 0;   // Bit 7: Underline

                    bool doubleHeight = (mode & 0x10) != 0;  // Bit 4
                    bool doubleWidth = (mode & 0x20) != 0;   // Bit 5
                    _fontSize = (doubleHeight || doubleWidth) ? 2 : 1;

                    return pos + 3;
                }
                return pos + 2;

            // ESC d n — Print and feed n lines
            case (byte)'d':
                if (pos + 2 < data.Length)
                {
                    int lines = data[pos + 2];
                    if (lines > 0)
                    {
                        _elements.Add(new FeedElement { Lines = lines });
                    }
                    return pos + 3;
                }
                return pos + 2;

            // ESC p — Open cash drawer (ignore)
            case (byte)'p':
                return pos + 5; // ESC p m t1 t2

            default:
                // Unknown ESC command — skip
                return pos + 2;
        }
    }

    /// <summary>
    /// Handles GS (0x1D) commands.
    /// </summary>
    private int HandleGsCommand(byte[] data, int pos)
    {
        if (pos + 1 >= data.Length)
            return pos + 1;

        FlushTextBuffer();

        byte cmd = data[pos + 1];

        switch (cmd)
        {
            // GS V n — Cut paper
            case (byte)'V':
                if (pos + 2 < data.Length)
                {
                    byte cutMode = data[pos + 2];
                    bool fullCut = cutMode == 0 || cutMode == 48; // 0 or '0'

                    // Some implementations use GS V m n (4 bytes total)
                    if (cutMode == 65 || cutMode == 66) // 'A' or 'B' (partial with feed)
                    {
                        _elements.Add(new CutElement { FullCut = cutMode == 65 });
                        return pos + 4; // GS V m n
                    }

                    _elements.Add(new CutElement { FullCut = fullCut });
                    return pos + 3;
                }
                return pos + 2;

            // GS ! n — Character size
            case (byte)'!':
                if (pos + 2 < data.Length)
                {
                    byte size = data[pos + 2];
                    int widthMul = ((size >> 4) & 0x07) + 1;
                    int heightMul = (size & 0x07) + 1;
                    _fontSize = Math.Max(widthMul, heightMul);
                    return pos + 3;
                }
                return pos + 2;

            // GS v 0 — Raster bit image
            case (byte)'v':
                return HandleRasterImage(data, pos);

            // GS k — Print barcode
            case (byte)'k':
                return HandleBarcode(data, pos);

            default:
                // Unknown GS command — skip
                return pos + 2;
        }
    }

    /// <summary>
    /// Handles GS v 0 (raster bit image) command.
    /// Format: GS v 0 m xL xH yL yH [data]
    /// </summary>
    private int HandleRasterImage(byte[] data, int pos)
    {
        // GS v 0 m xL xH yL yH
        if (pos + 7 >= data.Length)
            return pos + 2;

        // Skip 'v' check, pos+1 is 'v'
        // pos+2 should be '0' (0x30 or 0x00)
        int mode = data[pos + 3]; // m: density mode
        int xL = data[pos + 4];
        int xH = data[pos + 5];
        int yL = data[pos + 6];
        int yH = data[pos + 7];

        int bytesPerLine = xL + (xH * 256);
        int heightLines = yL + (yH * 256);
        int imageWidth = bytesPerLine * 8;
        int totalBytes = bytesPerLine * heightLines;

        int dataStart = pos + 8;
        int dataEnd = dataStart + totalBytes;

        if (dataEnd > data.Length)
        {
            // Not enough data — skip what we can
            return data.Length;
        }

        // Convert 1-bit raster data to a simple base64 representation
        // The frontend will handle rendering the monochrome bitmap
        var imageData = new byte[totalBytes];
        Array.Copy(data, dataStart, imageData, 0, totalBytes);

        _elements.Add(new ImageElement
        {
            DataBase64 = Convert.ToBase64String(imageData),
            Width = imageWidth,
            Height = heightLines
        });

        return dataEnd;
    }

    /// <summary>
    /// Handles GS k (barcode) command.
    /// Supports both Format A (GS k m d1...dk NUL) and Format B (GS k m n d1...dn).
    /// </summary>
    private int HandleBarcode(byte[] data, int pos)
    {
        if (pos + 2 >= data.Length)
            return pos + 2;

        byte barcodeType = data[pos + 2];

        string typeName = barcodeType switch
        {
            0 or 65 => "UPC-A",
            1 or 66 => "UPC-E",
            2 or 67 => "EAN13",
            3 or 68 => "EAN8",
            4 or 69 => "CODE39",
            5 or 70 => "ITF",
            6 or 71 => "CODABAR",
            7 or 72 => "CODE93",
            8 or 73 => "CODE128",
            _ => "UNKNOWN"
        };

        // Format A (type 0-6): data terminated by NUL
        if (barcodeType <= 6)
        {
            int dataStart = pos + 3;
            int nullPos = Array.IndexOf(data, (byte)0x00, dataStart);
            if (nullPos < 0) nullPos = data.Length;

            var barcodeData = System.Text.Encoding.ASCII.GetString(data, dataStart, nullPos - dataStart);
            _elements.Add(new BarcodeElement { Data = barcodeData, BarcodeType = typeName });
            return nullPos + 1;
        }

        // Format B (type 65-73): GS k m n d1...dn
        if (pos + 3 < data.Length)
        {
            int n = data[pos + 3];
            int dataStart = pos + 4;
            int dataEnd = Math.Min(dataStart + n, data.Length);

            var barcodeData = System.Text.Encoding.ASCII.GetString(data, dataStart, dataEnd - dataStart);
            _elements.Add(new BarcodeElement { Data = barcodeData, BarcodeType = typeName });
            return dataEnd;
        }

        return pos + 3;
    }

    /// <summary>
    /// Flushes the accumulated text buffer as a TextElement.
    /// </summary>
    private void FlushTextBuffer()
    {
        if (_textBuffer.Count == 0)
            return;

        var text = new string(_textBuffer.ToArray());
        _textBuffer.Clear();

        // Don't add empty whitespace-only lines unless they're meaningful
        if (string.IsNullOrWhiteSpace(text))
        {
            _elements.Add(new FeedElement { Lines = 1 });
            return;
        }

        _elements.Add(new TextElement
        {
            Content = text,
            Align = _alignment,
            Bold = _bold,
            Underline = _underline,
            FontSize = _fontSize,
            FontB = _fontB
        });
    }
}
