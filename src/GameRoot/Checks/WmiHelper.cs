using System.Management;

namespace GameRoot.Checks;

internal static class WmiHelper
{
    /// <summary>Returns the first instance of a WMI class, or null when there is none.</summary>
    public static ManagementBaseObject? First(string scope, string wmiClass)
    {
        using var searcher = new ManagementObjectSearcher(scope, $"SELECT * FROM {wmiClass}");
        foreach (ManagementBaseObject obj in searcher.Get())
            return obj;
        return null;
    }

    /// <summary>Reads one property, caching the instance per class for the run.</summary>
    public static object? Get(string scope, string wmiClass, string property)
    {
        var key = (scope, wmiClass);
        if (!Cache.TryGetValue(key, out var obj))
        {
            obj = First(scope, wmiClass);
            Cache[key] = obj;
        }
        if (obj is null)
            throw new NotAvailableException();
        return obj[property];
    }

    public static void ClearCache() => Cache.Clear();

    private static readonly Dictionary<(string, string), ManagementBaseObject?> Cache = new();
}

/// <summary>Thrown when a feature does not exist on this PC (for example, Defender is uninstalled).</summary>
public sealed class NotAvailableException : Exception;
