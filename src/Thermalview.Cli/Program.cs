using System.CommandLine;
using System.Globalization;
using System.Text.RegularExpressions;
using Thermalview.Core.Models;
using Thermalview.Core.Services;
using Thermalview.Server;

// Force 100% English CLI strings regardless of OS locale
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

// ──────────────────────────────────────────────
// Thermalview CLI — Virtual Thermal Printer
// ──────────────────────────────────────────────

var rootCommand = new RootCommand("Thermalview — Virtual thermal printer for Linux");

// ── thermalview install ──
// Interactive wizard: asks for width and name, registers CUPS,
// then starts the server and opens the browser automatically.
var installCommand = new Command("install", "Register a new virtual thermal printer");

installCommand.SetAction(async _ =>
{
    Console.Clear();
    PrintBanner();

    // ── Step 1: Paper width ──
    Console.WriteLine("Select the paper roll width:");
    Console.WriteLine("  1) 58mm");
    Console.WriteLine("  2) 80mm");
    Console.WriteLine("  3) 112mm");
    Console.WriteLine();

    int widthMm;
    while (true)
    {
        Console.Write("Option: ");
        var input = Console.ReadLine()?.Trim();
        widthMm = input switch
        {
            "1" => 58,
            "2" => 80,
            "3" => 112,
            _ => 0
        };

        if (widthMm > 0) break;

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("  Invalid option. Enter 1, 2, or 3.");
        Console.ResetColor();
    }

    Console.WriteLine();

    // ── Step 2: Printer name ──
    string printerName;
    var configStore = new ConfigStore();

    while (true)
    {
        Console.Write("Printer name (no spaces): ");
        var raw = Console.ReadLine()?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(raw))
        {
            WriteError("  Name cannot be empty.");
            continue;
        }

        if (!Regex.IsMatch(raw, @"^[a-zA-Z0-9_\-]+$"))
        {
            WriteError("  Use only letters, numbers, underscores, or hyphens.");
            continue;
        }

        if (configStore.GetPrinter(raw) is not null)
        {
            WriteError($"  A printer named '{raw}' already exists. Try a different name.");
            continue;
        }

        printerName = raw;
        break;
    }

    Console.WriteLine();

    // ── Step 3: Fixed server port 5000 ──
    int port = 5000;

    // ── Step 4: Register in CUPS ──
    Console.Write("  Registering printer in CUPS... ");

    bool cupsOk = RegisterCupsPrinter(printerName);
    if (cupsOk)
    {
        WriteSuccess($"Printer \"{printerName}\" registered in CUPS");
    }
    else
    {
        WriteWarning($"Could not register in CUPS (manual setup may be required)");
    }

    // ── Step 5: Save config ──
    var printer = new PrinterConfig
    {
        Name = printerName,
        WidthMm = widthMm,
        Port = port
    };
    configStore.AddPrinter(printer);

    // Enable active printer in CUPS and disable other virtual printers
    EnableActiveCupsPrinter(printerName, configStore.ListPrinters());

    // ── Step 6: Start server ──
    WriteSuccess($"Server starting at http://localhost:{port}");

    // Open browser
    Console.Write("  Opening browser... ");
    OpenBrowser($"http://localhost:{port}");
    Console.WriteLine();

    Console.WriteLine();
    Console.WriteLine("  Press Ctrl+C to stop");
    Console.WriteLine("  ─────────────────────────────────────────────");
    Console.WriteLine();

    // Start the server (blocking)
    var frontendPath = ResolveFrontendPath();
    var app = ServerBuilder.Build(printerName, port, frontendPath);
    await app.RunAsync();
});

// ── thermalview start <name> ──
var startCommand = new Command("start", "Start the server for an installed printer");
var startNameArg = new Argument<string>("name")
{
    Description = "Name of the printer to start",
    Arity = ArgumentArity.ZeroOrOne
};
var startNoBrowserOption = new Option<bool>("--no-browser") { Description = "Don't open the browser automatically" };

startCommand.Arguments.Add(startNameArg);
startCommand.Options.Add(startNoBrowserOption);

startCommand.SetAction(async parseResult =>
{
    var name = parseResult.GetValue(startNameArg);
    var noBrowser = parseResult.GetValue(startNoBrowserOption);

    var configStore = new ConfigStore();

    // No name given → show list and hint
    if (string.IsNullOrWhiteSpace(name))
    {
        Console.WriteLine();
        WriteError("Specify the printer name:");
        Console.WriteLine("  thermalview start <name>");
        Console.WriteLine();

        var printers = configStore.ListPrinters();
        if (printers.Count > 0)
        {
            Console.WriteLine("Installed printers:");
            foreach (var p in printers)
                Console.WriteLine($"  - {p.Name}");
        }
        else
        {
            Console.WriteLine("No printers installed. Run 'thermalview install' first.");
        }

        Console.WriteLine();
        return;
    }

    var printer = configStore.GetPrinter(name);

    if (printer is null)
    {
        WriteError($"Printer '{name}' not found.");

        var all = configStore.ListPrinters();
        if (all.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Installed printers:");
            foreach (var p in all) Console.WriteLine($"  - {p.Name}");
        }

        return;
    }

    // Enable this active printer in CUPS and disable other virtual printers
    EnableActiveCupsPrinter(printer.Name, configStore.ListPrinters());

    PrintBanner();
    Console.WriteLine($"  Printer : {printer.Name}  ({printer.WidthMm}mm · {printer.CharsPerLine} chars/line)");
    Console.WriteLine($"  Server  : http://localhost:5000");
    Console.WriteLine();

    if (!noBrowser) OpenBrowser("http://localhost:5000");

    Console.WriteLine("  Press Ctrl+C to stop");
    Console.WriteLine("  ─────────────────────────────────────────────");
    Console.WriteLine();

    var frontendPath = ResolveFrontendPath();
    var app = ServerBuilder.Build(name, 5000, frontendPath);
    await app.RunAsync();
});

// ── thermalview list ──
var listCommand = new Command("list", "List installed virtual printers");

listCommand.SetAction(_ =>
{
    var configStore = new ConfigStore();
    var printers = configStore.ListPrinters();

    Console.WriteLine();

    if (printers.Count == 0)
    {
        Console.WriteLine("No printers installed. Run 'thermalview install' to add one.");
        Console.WriteLine();
        return;
    }

    Console.WriteLine($"  {"Name",-28} {"Width",-8} {"Port",-8} {"Installed"}");
    Console.WriteLine($"  {"─",-28} {"─",-8} {"─",-8} {"─",-20}");

    foreach (var p in printers)
        Console.WriteLine($"  {p.Name,-28} {p.WidthMm + "mm",-8} {p.Port,-8} {p.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}");

    Console.WriteLine();
    Console.WriteLine($"  {printers.Count} printer(s) installed");
    Console.WriteLine();
});

// ── thermalview remove <name> ──
var removeCommand = new Command("remove", "Remove a virtual printer");
var removeNameArg = new Argument<string>("name") { Description = "Name of the printer to remove" };

removeCommand.Arguments.Add(removeNameArg);

removeCommand.SetAction(parseResult =>
{
    var name = parseResult.GetValue(removeNameArg)!;
    var configStore = new ConfigStore();

    if (configStore.GetPrinter(name) is null)
    {
        WriteError($"Printer '{name}' not found.");
        return;
    }

    UnregisterCupsPrinter(name);
    configStore.RemovePrinter(name);

    WriteSuccess($"Printer '{name}' removed.");
});

// ── Register commands ──
rootCommand.Subcommands.Add(installCommand);
rootCommand.Subcommands.Add(startCommand);
rootCommand.Subcommands.Add(listCommand);
rootCommand.Subcommands.Add(removeCommand);

return rootCommand.Parse(args).Invoke();

// ──────────────────────────────────────────────
// Helpers
// ──────────────────────────────────────────────

static void PrintBanner()
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine();
    Console.WriteLine("  🖨️  Thermalview — ESC/POS Virtual Printer");
    Console.ResetColor();
    Console.WriteLine();
}

static void WriteSuccess(string message)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("  ✅ ");
    Console.ResetColor();
    Console.WriteLine(message);
}

static void WriteWarning(string message)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("  ⚠️  ");
    Console.ResetColor();
    Console.WriteLine(message);
}

static void WriteError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Write("  ❌ ");
    Console.ResetColor();
    Console.WriteLine(message);
}

/// <summary>
/// Opens the browser using xdg-open (Linux).
/// </summary>
static void OpenBrowser(string url)
{
    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "xdg-open",
            Arguments = url,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("  🌐 ");
        Console.ResetColor();
        Console.WriteLine($"Opening browser at {url}");
    }
    catch
    {
        Console.WriteLine($"  Open {url} in your browser");
    }
}

/// <summary>
/// Finds the first available port starting from <paramref name="start"/>.
/// </summary>
static int FindFreePort(int start)
{
    for (int port = start; port < start + 100; port++)
    {
        try
        {
            using var listener = new System.Net.Sockets.TcpListener(
                System.Net.IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return port;
        }
        catch { /* port in use, try next */ }
    }
    return start;
}

/// <summary>
/// Registers a virtual printer in CUPS using lpadmin.
/// </summary>
static bool RegisterCupsPrinter(string name)
{
    try
    {
        // Extract the backend script from embedded resources
        var backendScript = ExtractCupsBackendScript();

        if (backendScript is not null)
        {
            var cupsBackendDest = "/usr/lib/cups/backend/thermalview";
            RunSudo($"cp \"{backendScript}\" \"{cupsBackendDest}\"");
            RunSudo($"chmod 755 \"{cupsBackendDest}\"");

            // Clean up temp file
            try { File.Delete(backendScript); } catch { }
        }

        var result = RunSudo($"lpadmin -p {name} -E -v thermalview:/ -m raw");
        return result == 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n  (Warning: {ex.Message})");
        return false;
    }
}

/// <summary>
/// Enables the active printer in CUPS and disables any inactive virtual printers.
/// </summary>
static void EnableActiveCupsPrinter(string activeName, IEnumerable<PrinterConfig> allPrinters)
{
    try
    {
        RunSudo($"cupsenable {activeName} && cupsaccept {activeName}");
        foreach (var p in allPrinters)
        {
            if (!string.Equals(p.Name, activeName, StringComparison.OrdinalIgnoreCase))
            {
                RunSudo($"cupsdisable {p.Name}");
            }
        }
    }
    catch
    {
        /* best effort */
    }
}

/// <summary>
/// Removes a printer from CUPS.
/// </summary>
static void UnregisterCupsPrinter(string name)
{
    try { RunSudo($"lpadmin -x {name}"); }
    catch { /* best effort */ }
}

/// <summary>
/// Runs a sudo command and returns the exit code.
/// </summary>
static int RunSudo(string arguments)
{
    var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = "sudo",
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    });
    p?.WaitForExit();
    return p?.ExitCode ?? 1;
}

/// <summary>
/// Extracts cups-backend.sh from embedded resources to a temp file.
/// Returns the temp file path, or null if the resource isn't found.
/// </summary>
static string? ExtractCupsBackendScript()
{
    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
    using var stream = assembly.GetManifestResourceStream("cups-backend.sh");

    if (stream is null)
    {
        // Fallback: look on disk (dev mode)
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "scripts", "cups-backend.sh"),
            Path.Combine(AppContext.BaseDirectory, "..", "scripts", "cups-backend.sh"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "cups-backend.sh"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    // Write to a temp file
    var tmpPath = Path.Combine(Path.GetTempPath(), "thermalview-cups-backend.sh");
    using var fs = File.Create(tmpPath);
    stream.CopyTo(fs);
    return tmpPath;
}

/// <summary>
/// Finds the frontend directory for dev mode (optional, embedded is used in production).
/// </summary>
static string? ResolveFrontendPath()
{
    var candidates = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "frontend"),
        Path.Combine(AppContext.BaseDirectory, "..", "frontend"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "frontend"),
    };
    return candidates.FirstOrDefault(Directory.Exists);
}
