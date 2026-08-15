# Thermalview

**Virtual thermal printer for Linux that intercepts ESC/POS commands and renders real-time ticket previews in the browser. Debug your print layouts without a physical printer. Powered by CUPS.**

![License](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-10-purple)
![Platform](https://img.shields.io/badge/platform-Linux-orange)

---

## What it does

Thermalview registers itself as a virtual printer in CUPS. When your POS or desktop application prints to it, the raw ESC/POS bytes are intercepted, parsed into structured JSON, and rendered in real time in your browser — faithful to thermal paper styling and paper roll widths.

**No physical printer needed. No internet connection. Zero dependencies.**

```
Your POS / App → prints via CUPS → Thermalview intercepts → parses ESC/POS → renders in browser
```

## Features

- 🖨️ **Virtual CUPS printer** — appears as a real system printer to your application
- 📡 **Real-time rendering** — see tickets instantly via WebSocket as they print
- 📏 **Paper width simulation** — 58mm, 80mm, and 112mm paper roll support
- 🔤 **ESC/POS command engine** — alignment, bold, underline, font sizes, raster images, barcodes, paper cuts
- 📜 **Ticket history** — browse and re-examine previously printed tickets
- 🎨 **Faithful rendering** — monospace font, proportional widths, torn paper edges
- ⚡ **Single standalone binary** — 100% self-contained, no .NET runtime or external files needed
- 🔒 **Fully local** — runs entirely offline on your dev machine

---

## Quick Start

### 1. Download & Install Binary

```bash
# Download the latest single-file release
curl -sSL https://github.com/stebean/thermalview/releases/latest/download/thermalview -o thermalview && chmod +x thermalview && sudo mv thermalview /usr/local/bin/
```

### 2. Register Virtual Printer (Interactive Wizard)

Simply run:

```bash
thermalview install
```

The interactive installer will guide you:

```
  🖨️  Thermalview — ESC/POS Virtual Printer

Select the paper roll width:
  1) 58mm
  2) 80mm
  3) 112mm

Option: 2

Printer name (no spaces): printer80mm

  ✅ Printer "printer80mm" registered in CUPS
  ✅ Server starting at http://localhost:5000
  🌐 Opening browser at http://localhost:5000
```

---

## CLI Commands

| Command | Description |
|---------|-------------|
| `thermalview install` | Interactive wizard to create & register a virtual printer |
| `thermalview start <name>` | Start the web server for a printer and open browser |
| `thermalview start` | Show list of installed printers when name is omitted |
| `thermalview list` | List all installed virtual printers with their configurations |
| `thermalview remove <name>` | Unregister a printer from CUPS and delete configuration |

---

## Architecture

<img width="1536" height="1024" alt="Architecture" src="https://github.com/user-attachments/assets/3d367500-d905-4f38-92cc-253a2c240722" />

---

## Supported ESC/POS Commands (EPSON Standard)

Thermalview parses the **Standard EPSON ESC/POS Command Set** — the universal protocol used by EPSON (TM-T20, TM-T88, etc.), Bixolon, Star Micronics (ESC/POS mode), Xprinter, Rongta, Custom, and 99% of thermal receipt printers worldwide:

| Command | Hex / Bytes | Description |
|---------|-------------|-------------|
| `ESC @` | `0x1B 0x40` | Initialize printer (Reset formatting) |
| `ESC a` | `0x1B 0x61 n` | Set alignment (`0`=Left, `1`=Center, `2`=Right) |
| `ESC E` | `0x1B 0x45 n` | Bold text on/off (`1`/`0`) |
| `ESC -` | `0x1B 0x2D n` | Underline text on/off (`1`/`0`) |
| `ESC !` | `0x1B 0x21 n` | Select print mode (Font B, Bold, Double height/width) |
| `ESC d` | `0x1B 0x64 n` | Print & Feed `n` lines |
| `GS V`  | `0x1D 0x56 n` | Paper cut (Full cut / Partial cut) |
| `GS !`  | `0x1D 0x21 n` | Character size (Width & height multipliers) |
| `GS v 0`| `0x1D 0x76 0` | Raster bit image (Monochrome logos & bitmaps) |
| `GS k`  | `0x1D 0x6B m` | Barcode printing (UPC-A, EAN13, CODE39, CODE128) |

---

## Local Development & Building

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- CUPS (pre-installed on Linux)

### Build & Run locally

```bash
git clone https://github.com/stebean/thermalview.git
cd thermalview

# Build solution
dotnet build

# Run interactive CLI
dotnet run --project src/Thermalview.Cli -- install
```

### Build Single-File Self-Contained Binary

```bash
dotnet publish src/Thermalview.Cli/Thermalview.Cli.csproj \
  -c Release \
  -p:PublishSingleFile=true \
  -o publish/
```

This compiles a single ~16MB standalone binary `publish/thermalview` with embedded frontend assets and CUPS scripts.

---

## License

[MIT](LICENSE) © stebean
