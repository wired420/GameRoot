using System.Text;
using System.Text.RegularExpressions;
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
        sb.AppendLine($"Computer : {AnonymousComputerName()}");
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

        // Strip anything that identifies the person or PC before it goes in the file.
        var rows = data.Rows.Select(r => r.Cells.Select((c, i) => Mask(c, i < columns.Length ? columns[i] : "")).ToArray()).ToList();

        // Pad every column but the last so the table lines up in a monospace font.
        var widths = columns.Select((c, i) =>
            Math.Max(c.Length, rows.Select(r => i < r.Length ? r[i].Length : 0).DefaultIfEmpty(0).Max())).ToArray();

        string Line(IReadOnlyList<string> cells) => string.Join("  ",
            cells.Select((c, i) => i == cells.Count - 1 ? c : c.PadRight(widths[i]))).TrimEnd();

        sb.AppendLine(Line(columns));
        sb.AppendLine(Line(widths.Select(w => new string('-', w)).ToArray()));
        foreach (var row in rows)
            sb.AppendLine(Line(row));
    }

    // Service accounts built into Windows; these are the same on every PC, so they stay as-is.
    private static readonly string[] BuiltInAccountPrefixes =
        ["LocalSystem", "LocalService", "NetworkService", @"NT AUTHORITY\", @"NT SERVICE\"];

    // C:\Users\<name>\... -> C:\Users\<user>\..., leaving Windows' own profile folders alone.
    private static readonly Regex UserProfilePath = new(
        @"(?i)(?<prefix>[A-Z]:\\Users\\)(?!(Public|Default|Default User|All Users)(\\|$|""))(?<name>[^\\""]+)",
        RegexOptions.Compiled);

    // \\server\share -> \\<server>\share
    private static readonly Regex UncServer = new(@"(?<![A-Za-z0-9])\\\\(?<server>[^\\\s""]+)", RegexOptions.Compiled);

    /// <summary>
    /// Hides anything that identifies the person or PC in one table cell: non-built-in service
    /// accounts, user profile folders, network server names, and the user, computer and domain names.
    /// </summary>
    internal static string Mask(string cell, string column)
    {
        if (string.IsNullOrEmpty(cell))
            return cell;

        if (column == "Account")
        {
            bool builtIn = BuiltInAccountPrefixes.Any(a => cell.StartsWith(a, StringComparison.OrdinalIgnoreCase));
            return builtIn ? cell : "<account hidden>";
        }

        string result = UserProfilePath.Replace(cell, m => m.Groups["prefix"].Value + "<user>");
        result = UncServer.Replace(result, @"\\<server>");
        result = ReplaceWord(result, Environment.UserName, "<user>");
        result = ReplaceWord(result, Environment.MachineName, "<computer>");
        if (!string.Equals(Environment.UserDomainName, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            result = ReplaceWord(result, Environment.UserDomainName, "<domain>");
        return result;
    }

    private static string ReplaceWord(string text, string? word, string replacement)
    {
        if (string.IsNullOrWhiteSpace(word) || word!.Length < 3)
            return text;
        return Regex.Replace(text, $@"(?i)(?<![A-Za-z0-9]){Regex.Escape(word)}(?![A-Za-z0-9])", replacement);
    }

    /// <summary>
    /// The computer's name is never written to the report. Domain-joined PCs say so; everything
    /// else shows the generic DESKTOP-XXXXXX placeholder.
    /// </summary>
    private static string AnonymousComputerName()
    {
        bool onDomain = false;
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT PartOfDomain FROM Win32_ComputerSystem");
            foreach (System.Management.ManagementBaseObject cs in searcher.Get())
                onDomain = cs["PartOfDomain"] is true;
        }
        catch
        {
            // If it can't be read, fall back to the generic placeholder.
        }
        return onDomain ? "On a domain (name hidden)" : "DESKTOP-XXXXXX (name hidden)";
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
