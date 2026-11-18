using System.Drawing.Drawing2D;

namespace GameRoot;

/// <summary>
/// The scanning window shown at launch. It runs the full scan with a progress bar, then closes;
/// Program then opens the main window with the results.
/// </summary>
internal sealed class SplashForm : Form
{
    private const int CountdownSeconds = 5;

    private double _fraction;
    private string _status = "Starting...";
    private readonly Font _titleFont = new(Theme.FontFamily, 22f, FontStyle.Bold);
    private readonly Font _subFont = new(Theme.FontFamily, 10.5f, FontStyle.Regular);
    private readonly Font _statusFont = new(Theme.FontFamily, 9f, FontStyle.Regular);

    /// <summary>The scan results, or null if the scan failed.</summary>
    public ScanData? Result { get; private set; }

    /// <summary>The error message when the scan failed.</summary>
    public string? Error { get; private set; }

    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        Text = "GameRoot - scanning";
        BackColor = Theme.Background;
        DoubleBuffered = true;
        ClientSize = new Size(S(480), S(300));
        if (Theme.AppIcon is { } icon) Icon = icon;
    }

    private int S(float v) => (int)Math.Round(v * DeviceDpi / 96f);

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Created here, on the UI thread, so reports are marshalled back to it.
        var progress = new Progress<ScanProgress>(p =>
        {
            _fraction = Math.Max(_fraction, Math.Min(1, p.Fraction));
            _status = p.Status;
            Invalidate();
        });
        try
        {
            Result = await Task.Run(() => ScanService.RunAll(progress));
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Close();
            return;
        }

        // Hold for a visible countdown so a fast scan doesn't look like a window flashing past.
        _fraction = 1;
        for (int i = CountdownSeconds; i > 0; i--)
        {
            _status = $"Scan complete. Opening GameRoot in {i}...";
            Invalidate();
            await Task.Delay(1000);
        }
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = ClientRectangle;

        // Frame: steel border with an amber accent along the top.
        using (var edge = new Pen(Theme.PlateTop, S(2)))
            g.DrawRectangle(edge, S(1), S(1), r.Width - S(2), r.Height - S(2));
        using (var accent = new SolidBrush(Theme.Accent))
            g.FillRectangle(accent, S(2), S(2), r.Width - S(4), S(3));

        // Logo on the left, name and notice on the right.
        int logo = S(130);
        var logoRect = new Rectangle(S(28), S(36), logo, logo);
        Logo.Draw(g, logoRect);

        int tx = logoRect.Right + S(24);
        TextRenderer.DrawText(g, "GAMEROOT", _titleFont, new Point(tx, S(56)), Color.FromArgb(235, 238, 240), TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "System check", _subFont, new Point(tx + S(2), S(98)), Theme.Accent, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "Scanning your system.\nThis only reads settings.", _subFont,
            new Rectangle(tx + S(2), S(126), r.Width - tx - S(20), S(48)), Theme.DimText, TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);

        // Progress bar.
        var bar = new Rectangle(S(28), S(214), r.Width - S(56), S(18));
        using (var track = new SolidBrush(Color.FromArgb(14, 15, 17)))
            g.FillRectangle(track, bar);
        int fillW = (int)(bar.Width * _fraction);
        if (fillW > 0)
        {
            var fill = new Rectangle(bar.X, bar.Y, fillW, bar.Height);
            using var brush = new LinearGradientBrush(fill, Color.FromArgb(255, 200, 60), Color.FromArgb(190, 130, 10), 90f);
            g.FillRectangle(brush, fill);
            // Tick marks give the bar a gauge-like look.
            using var tick = new Pen(Color.FromArgb(60, 0, 0, 0));
            for (int x = bar.X + S(12); x < fill.Right; x += S(12))
                g.DrawLine(tick, x, bar.Top, x, bar.Bottom);
        }
        using (var frame = new Pen(Theme.PlateTop))
            g.DrawRectangle(frame, bar);

        // Status line and percentage.
        TextRenderer.DrawText(g, _status, _statusFont, new Rectangle(bar.X, bar.Bottom + S(8), bar.Width - S(60), S(20)),
            Theme.Text, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, $"{(int)Math.Round(_fraction * 100)}%", _statusFont,
            new Rectangle(bar.Right - S(60), bar.Bottom + S(8), S(60), S(20)), Theme.Accent, TextFormatFlags.Right);
    }

    // Let the borderless window be dragged.
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _subFont.Dispose();
            _statusFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
