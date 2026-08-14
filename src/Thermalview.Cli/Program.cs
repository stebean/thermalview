using System.CommandLine;
using Thermalview.Core.Models;
using Thermalview.Core.Services;
using Thermalview.Server;

// ──────────────────────────────────────────────
// Thermalview CLI — Virtual Thermal Printer
// ──────────────────────────────────────────────

var rootCommand = new RootCommand("Thermalview — Virtual thermal printer for Linux");

// ── thermalview install ──
var installCommand = new Command("install", "Register a new virtual thermal printer in CUPS");
var installNameArg = new Argument<string>("name") { Description = "Name for the virtual printer" };
var installWidthOption = new Option<int>("--width", "-w") { Description = "Paper width in mm (58, 80, or 112)", DefaultValueFactory = _ => 80 };
var installPortOption = new Option<int>("--port", "-p") { Description = "Server port", DefaultValueFactory = _ => 5000 };

installCommand.Arguments.Add(installNameArg);
installCommand.Options.Add(installWidthOption);
installCommand.Options.Add(installPortOption);

installCommand.SetAction(parseResult =>
{
    var name = parseResult.GetValue(installNameArg)!;
    var width = parseResult.GetValue(installWidthOption);
    var port = parseResult.GetValue(installPortOption);

    var configStore = new ConfigStore();

    // Validate width
    if (width is not (58 or 80 or 112))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: Invalid width {width}mm. Must be 58, 80, or 112.");
        Console.ResetColor();
        return;
    }

    // Check if printer already exists
    if (configStore.GetPrinter(name) is not null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: A printer named '{name}' already exists.");
        Console.ResetColor();
        return;
    }

    var printer = new PrinterConfig
    {
        Name = name,
        WidthMm = width,
        Port = port
    };

    // Register in CUPS
    Console.WriteLine($"Installing virtual printer '{name}' ({width}mm, port {port})...");

    if (!RegisterCupsPrinter(name))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Error: Failed to register printer in CUPS. Is CUPS installed?");
        Console.ResetColor();
        return;
    }

    // Save config
    configStore.AddPrinter(printer);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"✓ Printer '{name}' installed successfully!");
    Console.ResetColor();
    Console.WriteLine($"  Width: {width}mm ({printer.CharsPerLine} chars/line)");
    Console.WriteLine($"  Port:  {port}");
    Console.WriteLine();
    Console.WriteLine($"Start it with: thermalview start {name}");
});

// ── thermalview start ──
var startCommand = new Command("start", "Start the Thermalview server for a printer");
var startNameArg = new Argument<string>("name") { Description = "Name of the printer to start" };
var startNoBrowserOption = new Option<bool>("--no-browser") { Description = "Don't open the browser automatically" };

startCommand.Arguments.Add(startNameArg);
startCommand.Options.Add(startNoBrowserOption);

startCommand.SetAction(async parseResult =>
{
    var name = parseResult.GetValue(startNameArg)!;
    var noBrowser = parseResult.GetValue(startNoBrowserOption);

    var configStore = new ConfigStore();
    var printer = configStore.GetPrinter(name);

    if (printer is null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: Printer '{name}' not found. Run 'thermalview list' to see installed printers.");
        Console.ResetColor();
        return;
    }

    // Resolve frontend path
    var frontendPath = ResolveFrontendPath();

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(@"
  _____ _                           _       _               
 |_   _| |__   ___ _ __ _ __ ___  __ _| |_   _(_) _____      __
   | | | '_ \ / _ \ '__| '_ ` _ \ / _` | \ \ / / |/ _ \ \ /\ / /
   | | | | | |  __/ |  | | | | | | (_| | |\ V /| |  __/\ V  V / 
   |_| |_| |_|\___|_|  |_| |_| |_|\__,_|_| \_/ |_|\___| \_/\_/  
");
    Console.ResetColor();

    Console.WriteLine($"  Printer: {name} ({printer.WidthMm}mm, {printer.CharsPerLine} chars/line)");
    Console.WriteLine($"  Server:  http://localhost:{printer.Port}");
    Console.WriteLine($"  Frontend: {frontendPath}");
    Console.WriteLine();

    // Open browser
    if (!noBrowser)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = $"http://localhost:{printer.Port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            Console.WriteLine("  Browser opened automatically");
        }
        catch
        {
            Console.WriteLine($"  Open http://localhost:{printer.Port} in your browser");
        }
    }

    Console.WriteLine();
    Console.WriteLine("  Press Ctrl+C to stop the server");
    Console.WriteLine("  ─────────────────────────────────────────────");
    Console.WriteLine();

    // Start server
    var app = ServerBuilder.Build(name, printer.Port, frontendPath);
    await app.RunAsync();
});

// ── thermalview list ──
var listCommand = new Command("list", "List installed virtual printers");

listCommand.SetAction(_ =>
{
    var configStore = new ConfigStore();
    var printers = configStore.ListPrinters();

    if (printers.Count == 0)
    {
        Console.WriteLine("No printers installed. Run 'thermalview install <name>' to add one.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"  {"Name",-25} {"Width",-10} {"Port",-8} {"Created",-20}");
    Console.WriteLine($"  {"─",-25} {"─",-10} {"─",-8} {"─",-20}");

    foreach (var p in printers)
    {
        Console.WriteLine($"  {p.Name,-25} {p.WidthMm + "mm",-10} {p.Port,-8} {p.CreatedAt:yyyy-MM-dd HH:mm}");
    }

    Console.WriteLine();
    Console.WriteLine($"  {printers.Count} printer(s) installed");
    Console.WriteLine();
});

// ── thermalview remove ──
var removeCommand = new Command("remove", "Remove a virtual printer");
var removeNameArg = new Argument<string>("name") { Description = "Name of the printer to remove" };

removeCommand.Arguments.Add(removeNameArg);

removeCommand.SetAction(parseResult =>
{
    var name = parseResult.GetValue(removeNameArg)!;
    var configStore = new ConfigStore();

    if (configStore.GetPrinter(name) is null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: Printer '{name}' not found.");
        Console.ResetColor();
        return;
    }

    // Remove from CUPS
    UnregisterCupsPrinter(name);

    // Remove from config
    configStore.RemovePrinter(name);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"✓ Printer '{name}' removed.");
    Console.ResetColor();
});

// ── Register commands ──
rootCommand.Subcommands.Add(installCommand);
rootCommand.Subcommands.Add(startCommand);
rootCommand.Subcommands.Add(listCommand);
rootCommand.Subcommands.Add(removeCommand);

return rootCommand.Parse(args).Invoke();

// ──────────────────────────────────────────────
// Helper functions
// ──────────────────────────────────────────────

/// <summary>
/// Registers a virtual printer in CUPS using lpadmin.
/// The printer uses a custom CUPS backend that POSTs raw data to Thermalview.
/// </summary>
static bool RegisterCupsPrinter(string name)
{
    try
    {
        // Find the cups-backend.sh script
        var backendScript = FindCupsBackendScript();
        if (backendScript is null)
        {
            Console.WriteLine("Warning: cups-backend.sh not found. You'll need to set up CUPS manually.");
            return true; // Don't block installation
        }

        // Install the backend script to CUPS backends directory
        var cupsBackendDir = "/usr/lib/cups/backend";
        var backendDest = Path.Combine(cupsBackendDir, "thermalview");

        var installProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sudo",
            Arguments = $"cp {backendScript} {backendDest}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        installProcess?.WaitForExit();

        // Set permissions
        var chmodProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sudo",
            Arguments = $"chmod 755 {backendDest}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        chmodProcess?.WaitForExit();

        // Register the printer with lpadmin
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sudo",
            Arguments = $"lpadmin -p {name} -E -v thermalview:/ -m raw",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        process?.WaitForExit();

        return process?.ExitCode == 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not register CUPS printer: {ex.Message}");
        return true; // Don't block installation
    }
}

/// <summary>
/// Removes a printer from CUPS.
/// </summary>
static void UnregisterCupsPrinter(string name)
{
    try
    {
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sudo",
            Arguments = $"lpadmin -x {name}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        process?.WaitForExit();
    }
    catch
    {
        Console.WriteLine("Warning: Could not remove CUPS printer. You may need to remove it manually.");
    }
}

/// <summary>
/// Finds the cups-backend.sh script relative to the executable.
/// </summary>
static string? FindCupsBackendScript()
{
    var candidates = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "scripts", "cups-backend.sh"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "cups-backend.sh"),
        Path.Combine(AppContext.BaseDirectory, "..", "scripts", "cups-backend.sh"),
    };

    return candidates.FirstOrDefault(File.Exists);
}

/// <summary>
/// Resolves the path to the frontend directory.
/// </summary>
static string ResolveFrontendPath()
{
    var candidates = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "frontend"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "frontend"),
        Path.Combine(AppContext.BaseDirectory, "..", "frontend"),
    };

    var found = candidates.FirstOrDefault(Directory.Exists);
    return found is not null ? Path.GetFullPath(found) : Path.Combine(AppContext.BaseDirectory, "frontend");
}
