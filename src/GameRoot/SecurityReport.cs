using System.Management;
using System.Text;
using GameRoot.Checks;

namespace GameRoot;

/// <summary>A group of checks with an optional heading.</summary>
internal sealed record Group(string? Title, ICheck[] Checks);

/// <summary>A section of the report. A separator line is printed after each section.</summary>
internal sealed record Section(string? Title, params Group[] Groups);

/// <summary>Builds the read-only security report shown on the Security tab.</summary>
internal static class SecurityReport
{
    private static Section[] Sections() =>
    [
        new(null, new Group(null, [new TpmCheck(), new SecureBootCheck()])),
        new(null, new Group(null, [new GameModeCheck(), new GpuSchedulingCheck(), new FastStartupCheck()])),
        new("Windows Security",
            new Group("Virus & threat protection", WindowsSecurityChecks.Defender()),
            new Group("Account protection", WindowsSecurityChecks.AccountProtection()),
            new Group("Firewall & network protection", WindowsSecurityChecks.Firewall()),
            new Group("App & browser control", WindowsSecurityChecks.AppAndBrowser()),
            new Group("Exploit protection (system settings)", WindowsSecurityChecks.ExploitProtection()),
            new Group("Device security", WindowsSecurityChecks.DeviceSecurity())),
    ];

    private const int Indent = 2;

    public static string Build()
    {
        WmiHelper.ClearCache();
        var sections = Sections();

        int width = sections
            .SelectMany(s => s.Groups.Select(g => (Depth: Depth(s, g), g.Checks)))
            .SelectMany(x => x.Checks.Select(c => x.Depth * Indent + c.Name.Length))
            .Max() + 2;
        string separator = new('-', width + 18);

        var sb = new StringBuilder();
        sb.AppendLine(separator);

        foreach (var section in sections)
        {
            if (section.Title is not null)
                sb.AppendLine(section.Title);

            foreach (var group in section.Groups)
            {
                int depth = Depth(section, group);
                if (group.Title is not null)
                    sb.AppendLine($"{Pad(depth - 1)}{group.Title}");

                foreach (var check in group.Checks)
                {
                    string label = Pad(depth) + check.Name;
                    sb.AppendLine($"{label.PadRight(width)}: {Run(check).Value}");
                }
            }

            sb.AppendLine(separator);
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
