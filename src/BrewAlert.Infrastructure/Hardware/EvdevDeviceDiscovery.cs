namespace BrewAlert.Infrastructure.Hardware;

/// <summary>
/// Discovers USB RFID keyboard-wedge evdev device paths from sysfs.
/// </summary>
public static class EvdevDeviceDiscovery
{
    public const string DefaultSysfsInputRoot = "/sys/class/input";
    private static readonly string[] NameKeywords =
        ["rfid", "sycreader", "id&ic", "ic reader", "id reader", "nfc"];

    public static string? FindReaderDevicePath(
        string? configuredDevicePath,
        string sysfsInputRoot = DefaultSysfsInputRoot,
        Func<string, bool>? deviceExists = null)
    {
        var exists = deviceExists ?? File.Exists;
        if (!string.IsNullOrWhiteSpace(configuredDevicePath))
        {
            var trimmed = configuredDevicePath.Trim();
            if (exists(trimmed)) return trimmed;
        }

        if (!Directory.Exists(sysfsInputRoot)) return null;

        IEnumerable<string> eventDirs;
        try
        {
            eventDirs = Directory.EnumerateDirectories(sysfsInputRoot, "event*")
                .OrderBy(d => d, StringComparer.Ordinal);
        }
        catch (IOException)
        {
            return null;
        }

        foreach (var eventDir in eventDirs)
        {
            var eventName = Path.GetFileName(eventDir);
            var deviceDir = Path.Combine(eventDir, "device");
            var name = ReadTrimmed(Path.Combine(deviceDir, "name"));
            var vendor = ReadTrimmed(Path.Combine(deviceDir, "id", "vendor"));
            var product = ReadTrimmed(Path.Combine(deviceDir, "id", "product"));

            if (!IsRfidDevice(name, vendor, product)) continue;

            var devPath = $"/dev/input/{eventName}";
            if (exists(devPath)) return devPath;
        }

        return null;
    }

    internal static bool IsRfidDevice(string name, string vendor, string product)
    {
        if (string.Equals(vendor, "ffff", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(product, "0035", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(name)) return false;

        foreach (var keyword in NameKeywords)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ReadTrimmed(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
