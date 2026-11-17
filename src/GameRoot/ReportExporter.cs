using System.Text;
using Microsoft.Win32;

namespace GameRoot;

/// <summary>Builds a plain-text report of everything on all three tabs.</summary>
internal static class ReportExporter
{
    public static string Build(
        IReadOnlyList<ReportLine> security,
        (List<ListRow> Rows, string? Error) services,
        (List<ListRow> Rows, string? Error) drivers)
    {
        var sb = new StringBuilder();
        const string rule = "======================================================================";

        sb.AppendLine("GameRoot system report");
        sb.AppendLine(rule);
        sb.AppendLine($"Computer : {Environment.MachineName}");
        sb.AppendLine($"Windows  : {WindowsVersion()}");
        sb.AppendLine($"Created  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("This report was made by a read-only scan. Nothing on the PC was changed.");
        sb.AppendLine();

        sb.AppendLine("SECURITY");
        sb.AppendLine(rule);
        sb.Append(SecurityReport.ToText(security));
        sb.AppendLine();

        Table(sb, $"SERVICES ({services.Rows.Count} running)", rule, SystemLists.ServiceColumns, services);
        sb.AppendLine();
        Table(sb, $"DRIVERS ({drivers.Rows.Count} loaded)", rule, SystemLists.DriverColumns, drivers);

        return sb.ToString();
    }

    private static void Table(StringBuilder sb, string title, string rule, string[] columns, (List<ListRow> Rows, string? Error) data)
    {
        sb.AppendLine(title);
        sb.AppendLine(rule);
        if (data.Error is not null)
        {
            sb.AppendLine($"Could not read the list: {data.Error}");
            return;
        }

        // Pad every column but the last so the table lines up in a monospace font.
        var widths = columns.Select((c, i) =>
            Math.Max(c.Length, data.Rows.Select(r => i < r.Cells.Length ? r.Cells[i].Length : 0).DefaultIfEmpty(0).Max())).ToArray();

        string Line(IReadOnlyList<string> cells) => string.Join("  ",
            cells.Select((c, i) => i == cells.Count - 1 ? c : c.PadRight(widths[i]))).TrimEnd();

        sb.AppendLine(Line(columns));
        sb.AppendLine(Line(widths.Select(w => new string('-', w)).ToArray()));
        foreach (var row in data.Rows)
            sb.AppendLine(Line(row.Cells));
    }

    private static string WindowsVersion()
    {
        const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var k = root.OpenSubKey(key);
        string product = k?.GetValue("ProductName") as string ?? "Windows";
        string display = k?.GetValue("DisplayVersion") as string ?? "";
        string build = k?.GetValue("CurrentBuildNumber") as string ?? Environment.OSVersion.Version.Build.ToString();
        string ubr = k?.GetValue("UBR") is int u ? $".{u}" : "";
        // ProductName still says "Windows 10" on Windows 11; the build number tells them apart.
        if (int.TryParse(build, out var b) && b >= 22000)
            product = product.Replace("Windows 10", "Windows 11");
        return $"{product} {display} (build {build}{ubr})".Replace("  ", " ");
    }
}
