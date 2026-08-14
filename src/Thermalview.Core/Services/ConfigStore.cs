using System.Text.Json;
using System.Text.Json.Serialization;
using Thermalview.Core.Models;

namespace Thermalview.Core.Services;

/// <summary>
/// Manages persistent configuration stored at ~/.thermalview/config.json.
/// Thread-safe: uses a lock for read/write operations.
/// </summary>
public class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };

    private readonly string _configDir;
    private readonly string _configPath;
    private readonly object _lock = new();

    public ConfigStore() : this(GetDefaultConfigDir()) { }

    public ConfigStore(string configDir)
    {
        _configDir = configDir;
        _configPath = Path.Combine(_configDir, "config.json");
    }

    /// <summary>
    /// Returns the default config directory: ~/.thermalview/
    /// Respects $SUDO_USER and $HOME to ensure consistent config location under Linux.
    /// </summary>
    public static string GetDefaultConfigDir()
    {
        var sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
        string? home = null;

        if (!string.IsNullOrWhiteSpace(sudoUser) && sudoUser != "root")
        {
            home = $"/home/{sudoUser}";
        }

        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("HOME");
        }

        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(home ?? ".", ".thermalview");
    }

    /// <summary>
    /// Path to the tickets history directory.
    /// </summary>
    public string TicketsDir => Path.Combine(_configDir, "tickets");

    /// <summary>
    /// Loads the config from disk. Returns a new empty config if the file doesn't exist.
    /// </summary>
    public ThermalviewConfig Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_configPath))
            {
                return new ThermalviewConfig();
            }

            var json = File.ReadAllText(_configPath);
            var result = JsonSerializer.Deserialize<ThermalviewConfig>(json, JsonOptions);
            return result ?? new ThermalviewConfig();
        }
    }

    /// <summary>
    /// Saves the config to disk. Creates the directory if it doesn't exist.
    /// </summary>
    public void Save(ThermalviewConfig config)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(_configDir);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configPath, json);
        }
    }

    /// <summary>
    /// Gets a printer by name, or null if not found.
    /// </summary>
    public PrinterConfig? GetPrinter(string name)
    {
        var config = Load();
        return config.Printers.Find(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds a printer to the config. Throws if a printer with the same name already exists.
    /// </summary>
    public void AddPrinter(PrinterConfig printer)
    {
        var config = Load();

        if (config.Printers.Any(p =>
            string.Equals(p.Name, printer.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"A printer named '{printer.Name}' already exists.");
        }

        config.Printers.Add(printer);
        Save(config);
    }

    /// <summary>
    /// Removes a printer by name. Returns true if removed, false if not found.
    /// </summary>
    public bool RemovePrinter(string name)
    {
        var config = Load();
        var removed = config.Printers.RemoveAll(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        if (removed > 0)
        {
            Save(config);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns all registered printers.
    /// </summary>
    public IReadOnlyList<PrinterConfig> ListPrinters()
    {
        return Load().Printers.AsReadOnly();
    }

    /// <summary>
    /// Saves a ticket to the history directory as a JSON file.
    /// </summary>
    public void SaveTicket(TicketData ticket)
    {
        Directory.CreateDirectory(TicketsDir);
        var filename = $"{ticket.ReceivedAt:yyyyMMdd_HHmmss}_{ticket.Id}.json";
        var path = Path.Combine(TicketsDir, filename);
        var json = JsonSerializer.Serialize(ticket, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Loads recent tickets from the history directory.
    /// </summary>
    public List<TicketData> LoadRecentTickets(int count = 50)
    {
        if (!Directory.Exists(TicketsDir))
            return [];

        return Directory.GetFiles(TicketsDir, "*.json")
            .OrderByDescending(f => f)
            .Take(count)
            .Select(f =>
            {
                var json = File.ReadAllText(f);
                return JsonSerializer.Deserialize<TicketData>(json, JsonOptions);
            })
            .Where(t => t is not null)
            .Cast<TicketData>()
            .ToList();
    }

    /// <summary>
    /// Deletes all saved tickets in the history directory.
    /// </summary>
    public void ClearHistory()
    {
        if (!Directory.Exists(TicketsDir))
            return;

        foreach (var file in Directory.GetFiles(TicketsDir, "*.json"))
        {
            try { File.Delete(file); } catch { }
        }
    }
}
