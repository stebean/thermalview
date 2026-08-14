# Thermalview

**Virtual thermal printer for Linux that intercepts ESC/POS commands and renders real-time ticket previews in the browser. Debug your print layouts without a physical printer. Powered by CUPS.**

![License](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-10-purple)
![Platform](https://img.shields.io/badge/platform-Linux-orange)

---

## What it does

Thermalview registers itself as a virtual printer in CUPS. When your application prints to it, the raw ESC/POS bytes are intercepted, parsed into structured JSON, and rendered in real time in your browser — faithful to the actual paper width.

**No physical printer needed. No internet connection. No dependencies.**

```
Your App → prints via CUPS → Thermalview intercepts → parses ESC/POS → renders in browser
```

## Features

- 🖨️ **Virtual CUPS printer** — appears as a real printer to your application
- 📡 **Real-time rendering** — see tickets as they print via WebSocket
- 📏 **Paper width simulation** — 58mm, 80mm, and 112mm support
- 🔤 **ESC/POS support** — alignment, bold, underline, font sizes, images, barcodes, cuts
- 📜 **Ticket history** — browse previously printed tickets
- 🎨 **Faithful rendering** — monospace font, proportional widths, torn paper edges
- ⚡ **Zero dependencies** — single self-contained binary, no .NET runtime needed
- 🔒 **Fully local** — nothing leaves your machine

## Quick Start

### Install

```bash
# Download the latest release
curl -sSL https://github.com/stebean/thermalview/releases/latest/download/thermalview-linux-x64 -o thermalview
chmod +x thermalview
sudo mv thermalview /usr/local/bin/
```

### Setup a virtual printer

```bash
# Register a virtual printer (80mm paper width)
thermalview install my-printer --width 80

# Start the server
thermalview start my-printer
```

This will:
1. Register a virtual printer named `my-printer` in CUPS
2. Start the web server on `http://localhost:5000`
3. Open your browser automatically

### Print to it

From your application, simply print to the `my-printer` printer. Thermalview will intercept the ESC/POS data and render it in the browser.

## CLI Commands

```
thermalview install <name> [--width 80] [--port 5000]
    Register a new virtual thermal printer in CUPS

thermalview start <name> [--no-browser]
    Start the Thermalview server for a printer

thermalview list
    List installed virtual printers

thermalview remove <name>
    Remove a virtual printer from CUPS and config
```

## Architecture

```
[Your App]
     |
     | Prints via CUPS
     ↓
[CUPS Virtual Printer]
     |
     | cups-backend.sh → HTTP POST
     ↓
[Thermalview Server (ASP.NET Core)]
     |
     ├── ESC/POS Parser → Structured JSON
     ├── WebSocket Hub → Broadcasts to browsers
     ├── HTTP API → Print endpoint, config, history
     └── Static Files → Serves the frontend
                ↓
[Browser — localhost:5000]
     |
     ├── WebSocket Client → Receives tickets in real time
     ├── Ticket Renderer → Renders JSON as thermal paper
     └── UI → Width selector, history, test prints
```

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

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- CUPS (usually pre-installed on Linux)

### Build & Run

```bash
# Clone the repo
git clone https://github.com/stebean/thermalview.git
cd thermalview

# Build
dotnet build

# Run the server in dev mode
dotnet run --project src/Thermalview.Server

# Or run the CLI
dotnet run --project src/Thermalview.Cli -- start my-printer
```

### Project Structure

```
thermalview/
├── src/
│   ├── Thermalview.Cli/         # CLI entry point (System.CommandLine)
│   ├── Thermalview.Server/      # ASP.NET Core server, WebSocket hub
│   ├── Thermalview.Parser/      # ESC/POS byte stream parser
│   └── Thermalview.Core/        # Shared models, config store
├── frontend/
│   ├── index.html               # UI structure
│   ├── style.css                # Dark theme, paper simulation
│   └── app.js                   # WebSocket client, renderer
├── scripts/
│   └── cups-backend.sh          # CUPS backend script
└── Thermalview.slnx             # .NET solution file
```

### Publishing

```bash
# Self-contained single-file binary for Linux x64
dotnet publish src/Thermalview.Cli/Thermalview.Cli.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -o publish/self-contained
```

## Configuration

Configuration is stored at `~/.thermalview/config.json`:

```json
{
  "printers": [
    {
      "name": "my-printer",
      "widthMm": 80,
      "port": 5000,
      "createdAt": "2026-01-01T00:00:00Z"
    }
  ]
}
```

Ticket history is saved in `~/.thermalview/tickets/`.

## What Thermalview does NOT do

- ❌ Send anything to the internet
- ❌ Require an account or API key
- ❌ Need .NET installed (self-contained binary)
- ❌ Touch your application code — it's just a system printer

## License

MIT
