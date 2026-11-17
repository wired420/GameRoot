using System.Management;
using System.Text;
using GameRoot.Checks;

namespace GameRoot;

/// <summary>A group of checks with an optional heading.</summary>
internal sealed record Group(string? Title, ICheck[] Checks);

/// <summary>A section of the report. A separator line is printed after each section.</summary>
internal sealed record Section(string? Title, params Group[] Groups);

internal enum LineKind { SectionTitle, GroupTitle, Check, Separator }

/// <summary>One line of the report: a heading, a check with its value, or a separator.</summary>
/// <param name="Toggle">Set on a check whose setting the app can change; null for read-only rows.</param>
internal sealed record ReportLine(LineKind Kind, int Depth, string Text, string? Value = null, IToggle? Toggle = null)
{
    /// <summary>The on/off state a slider should show. Null when the value isn't a plain on/off.</summary>
    public bool? IsOn => Value switch { "On" => true, "Off" => false, _ => null };
}

/// <summary>Builds the read-only security report shown on the Security tab.</summary>
internal static class SecurityReport
{
    private static Section[] Sections() =>
    [
        new("BIOS Settings", new Group(null, [new TpmCheck(), new SecureBootCheck()])),
        new("Windows Settings", new Group(null, [new GameModeCheck(), new GpuSchedulingCheck(), new FastStartupCheck()])),
        new("Windows Security",
            new Group("Virus & threat protection", WindowsSecurityChecks.Defender()),
            new Group("Account protection", WindowsSecurityChecks.AccountProtection()),
            new Group("Firewall & network protection", WindowsSecurityChecks.Firewall()),
            new Group("App & browser control", WindowsSecurityChecks.AppAndBrowser()),
            new Group("Exploit protection (system settings)", WindowsSecurityChecks.ExploitProtection()),
            new Group("Device security", WindowsSecurityChecks.DeviceSecurity())),
    ];

    private const int Indent = 2;

    /// <summary>Runs every check and returns the report as structured lines for the status board.</summary>
    public static List<ReportLine> Scan()
    {
        WmiHelper.ClearCache();
        var lines = new List<ReportLine>();

        foreach (var section in Sections())
        {
            if (section.Title is not null)
                lines.Add(new ReportLine(LineKind.SectionTitle, 0, section.Title));

            foreach (var group in section.Groups)
            {
                int depth = Depth(section, group);
                if (group.Title is not null)
                    lines.Add(new ReportLine(LineKind.GroupTitle, depth - 1, group.Title));

                foreach (var check in group.Checks)
                {
                    string value = Run(check).Value;
                    // A slider appears only when the value is a plain On/Off we can actually set.
                    var toggle = check as IToggle;
                    bool settable = toggle is not null && value is "On" or "Off";
                    lines.Add(new ReportLine(LineKind.Check, depth, check.Name, value, settable ? toggle : null));
                }
            }

            lines.Add(new ReportLine(LineKind.Separator, 0, ""));
        }
        return lines;
    }

    /// <summary>The report as plain text, for copying to the clipboard.</summary>
    public static string ToText(IReadOnlyList<ReportLine> lines)
    {
        int width = lines.Where(l => l.Kind == LineKind.Check).Select(l => l.Depth * Indent + l.Text.Length).DefaultIfEmpty(0).Max() + 2;
        string separator = new('-', width + 18);
        var sb = new StringBuilder();
        sb.AppendLine(separator);
        foreach (var l in lines)
        {
            sb.AppendLine(l.Kind switch
            {
                LineKind.Separator => separator,
                LineKind.Check => $"{(Pad(l.Depth) + l.Text).PadRight(width)}: {l.Value}",
                _ => Pad(l.Depth) + l.Text,
            });
        }
        return sb.ToString();
    }

    private static int Depth(Section s, Group g) => (s.Title is null ? 0 : 1) + (g.Title is null ? 0 : 1);

    private static string Pad(int depth) => new(' ', Math.Max(0, depth) * Indent);

    private static CheckResult Run(ICheck check)
    {
        try
        {
            return check.Run();
        }
        catch (NotAvailableException)
        {
            return new CheckResult("Not available on this PC");
        }
        catch (UnauthorizedAccessException)
        {
            return new CheckResult("Needs admin to read");
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
        {
            return new CheckResult("Needs admin to read");
        }
        catch (ManagementException ex) when (ex.ErrorCode is ManagementStatus.InvalidNamespace or ManagementStatus.InvalidClass)
        {
            return new CheckResult("Not available on this PC");
        }
        catch (Exception ex)
        {
            return new CheckResult($"Could not read ({ex.GetType().Name})");
        }
    }
}
