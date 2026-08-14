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

## Integrating with your Application / POS (e.g. Flutter)

### Option A: Print via System CUPS Queue (Native)
Print directly from your app or system print dialog to the registered virtual printer (e.g. `printer80mm`). CUPS routes the print job to Thermalview, which immediately parses and displays the ticket.

### Option B: Send ESC/POS Bytes via HTTP POST
If your POS generates raw ESC/POS byte streams (e.g. Flutter using `esc_pos_utils_2`), send the bytes directly to Thermalview's API:

```dart
import 'package:http/http.dart' as http;

// Generate raw ESC/POS bytes
List<int> bytes = await generateReceiptBytes();

// POST directly to Thermalview
final response = await http.post(
  Uri.parse('http://localhost:5000/api/print'),
  headers: {'Content-Type': 'application/octet-stream'},
  body: bytes,
);
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

```
[Your App / POS]
     |
     | Prints via CUPS (or HTTP POST)
     ↓
[CUPS Virtual Printer]
     |
     | cups-backend.sh → HTTP POST /api/print
     ↓
[Thermalview Server (ASP.NET Core)]
     |
     ├── ESC/POS Parser → Structured JSON
     ├── WebSocket Hub → Broadcasts to browsers
     ├── HTTP API → Print endpoint, config, history
     └── Embedded Static Files → Serves frontend UI
                ↓
[Browser — localhost:5000]
     |
     ├── WebSocket Client → Receives tickets in real time
     ├── Ticket Renderer → Renders JSON as thermal paper
     └── UI → Width selector, history, test prints
```

---

## Supported ESC/POS Commands

| Command | Description |
|---------|-------------|
| `ESC @` | Initialize printer |
| `ESC a` | Set alignment (left/center/right) |
| `ESC E` | Bold on/off |
| `ESC -` | Underline on/off |
| `ESC !` | Select print mode (font, bold, size) |
| `ESC d` | Feed n lines |
| `GS V`  | Paper cut (full/partial) |
| `GS !`  | Character size (width/height multiplier) |
| `GS v 0`| Raster bit image |
| `GS k`  | Print barcode |

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
