namespace Thermalview.Core.Models;

/// <summary>
/// Root configuration object persisted at ~/.thermalview/config.json.
/// </summary>
public class ThermalviewConfig
{
    /// <summary>
    /// List of registered virtual printers.
    /// </summary>
    public List<PrinterConfig> Printers { get; set; } = [];
}
