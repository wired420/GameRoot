namespace GameRoot;

/// <summary>How far a scan has got (0 to 1) and what it is doing.</summary>
internal sealed record ScanProgress(double Fraction, string Status);

/// <summary>Everything one scan produces, for all three tabs.</summary>
internal sealed record ScanData(
    IReadOnlyList<ReportLine> Security,
    (List<ListRow> Rows, string? Error) Services,
    (List<ListRow> Rows, string? Error) Drivers);

/// <summary>Runs the full read-only scan used by the splash screen and the Refresh button.</summary>
internal static class ScanService
{
    // Share of the bar given to the security checks; services and drivers take the rest.
    private const double SecurityShare = 0.8;

    public static ScanData RunAll(IProgress<ScanProgress>? progress = null)
    {
        var securityProgress = progress is null ? null : new Scaled(progress, 0, SecurityShare);
        var security = SecurityReport.Scan(securityProgress);

        progress?.Report(new ScanProgress(SecurityShare, "Listing running services..."));
        var services = Safe(SystemLists.RunningServices);

        progress?.Report(new ScanProgress(0.9, "Listing loaded drivers..."));
        var drivers = Safe(SystemLists.LoadedDrivers);

        progress?.Report(new ScanProgress(1, "Done"));
        return new ScanData(security, services, drivers);
    }

    public static (List<ListRow> Rows, string? Error) Safe(Func<List<ListRow>> query)
    {
        try { return (query(), null); }
        catch (Exception ex) { return ([], ex.Message); }
    }

    /// <summary>Maps a 0..1 progress report into a slice of the overall bar.</summary>
    private sealed class Scaled(IProgress<ScanProgress> inner, double from, double to) : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value) =>
            inner.Report(value with { Fraction = from + (to - from) * value.Fraction });
    }
}
