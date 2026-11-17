using System.Runtime.Versioning;
using GameRoot.Checks;

[assembly: SupportedOSPlatform("windows")]

namespace GameRoot;

internal static class Program
{
    // Each section prints its checks as a plain On / Off, followed by a separator line.
    private static readonly ICheck[][] Sections =
    {
        new ICheck[] { new TpmCheck(), new SecureBootCheck() },
        new ICheck[] { new GameModeCheck(), new GpuSchedulingCheck(), new FastStartupCheck() },
    };

    private const int SeparatorWidth = 50;

    private static int Main(string[] args)
    {
        bool noPause = args.Contains("--no-pause", StringComparer.OrdinalIgnoreCase);

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("GameRoot only runs on Windows.");
            return 1;
        }

        int width = Sections.SelectMany(s => s).Max(c => c.Name.Length) + 2;

        Console.WriteLine("GameRoot - system check (read-only, nothing is changed)");
        Console.WriteLine(new string('-', SeparatorWidth));

        foreach (var section in Sections)
        {
            foreach (var check in section)
                Console.WriteLine($"{check.Name.PadRight(width)}: {Run(check).Value}");

            Console.WriteLine(new string('-', SeparatorWidth));
        }

        if (!noPause && !Console.IsInputRedirected)
        {
            Console.WriteLine();
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey(intercept: true);
        }
        return 0;
    }

    private static CheckResult Run(ICheck check)
    {
        try
        {
            return check.Run();
        }
        catch (Exception ex)
        {
            return new CheckResult($"Could not read ({ex.GetType().Name})");
        }
    }
}
