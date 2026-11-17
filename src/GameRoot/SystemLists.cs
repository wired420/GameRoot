using System.Management;

namespace GameRoot;

/// <summary>One row in the Services or Drivers list.</summary>
internal sealed record ListRow(params string[] Cells);

/// <summary>Read-only queries for the Services and Drivers tabs.</summary>
internal static class SystemLists
{
    public static readonly string[] ServiceColumns = ["Display name", "Name", "Start type", "PID", "Account", "Path"];
    public static readonly string[] DriverColumns = ["Display name", "Name", "Start type", "Type", "Path"];

    /// <summary>All Windows services that are currently running.</summary>
    public static List<ListRow> RunningServices()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT DisplayName, Name, StartMode, ProcessId, StartName, PathName FROM Win32_Service WHERE State = 'Running'");
        var rows = new List<ListRow>();
        foreach (ManagementBaseObject s in searcher.Get())
        {
            rows.Add(new ListRow(
                Str(s["DisplayName"]), Str(s["Name"]), Str(s["StartMode"]),
                Str(s["ProcessId"]), Str(s["StartName"]), Str(s["PathName"])));
        }
        return rows.OrderBy(r => r.Cells[0], StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>All kernel and file system drivers that are currently loaded (running).</summary>
    public static List<ListRow> LoadedDrivers()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT DisplayName, Name, StartMode, ServiceType, PathName FROM Win32_SystemDriver WHERE State = 'Running'");
        var rows = new List<ListRow>();
        foreach (ManagementBaseObject d in searcher.Get())
        {
            rows.Add(new ListRow(
                Str(d["DisplayName"]), Str(d["Name"]), Str(d["StartMode"]),
                Str(d["ServiceType"]), CleanDriverPath(Str(d["PathName"]))));
        }
        return rows.OrderBy(r => r.Cells[0], StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string Str(object? o) => o?.ToString() ?? "";

    /// <summary>Turns paths like \SystemRoot\... or \??\C:\... into normal paths.</summary>
    private static string CleanDriverPath(string path)
    {
        if (path.StartsWith(@"\??\", StringComparison.Ordinal))
            return path.Substring(4);
        if (path.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), path.Substring(12));
        return path;
    }
}
