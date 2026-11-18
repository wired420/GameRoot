using System.Diagnostics;

namespace GameRoot;

/// <summary>The About tab: logo, name, version, a short description and a link to the repository.</summary>
internal sealed class AboutPanel : Panel
{
    public const string RepoUrl = "https://github.com/wired420/GameRoot";

    private readonly Font _titleFont = new(Theme.FontFamily, 26f, FontStyle.Bold);
    private readonly Font _subFont = new(Theme.FontFamily, 11f, FontStyle.Regular);
    private readonly Font _bodyFont = new(Theme.FontFamily, 10f, FontStyle.Regular);
    private readonly LinkLabel _link;

    public AboutPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;
        DoubleBuffered = true;
        ResizeRedraw = true;

        _link = new LinkLabel
        {
            Text = RepoUrl,
            AutoSize = true,
            Font = new Font(Theme.FontFamily, 10.5f, FontStyle.Regular),
            LinkColor = Theme.Accent,
            ActiveLinkColor = Color.White,
            VisitedLinkColor = Theme.Accent,
            BackColor = Theme.Background,
            LinkBehavior = LinkBehavior.HoverUnderline,
        };
        _link.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true }); }
            catch { /* No browser registered; the URL is still visible to copy. */ }
        };
        Controls.Add(_link);
    }

    private int S(float v) => (int)Math.Round(v * DeviceDpi / 96f);

    private static string Version
    {
        get
        {
            var v = typeof(AboutPanel).Assembly.GetName().Version;
            return v is null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>Layout: logo centred, then the text block beneath it.</summary>
    private (Rectangle Logo, int TextTop) ComputeLayout()
    {
        int logo = S(180);
        int top = Math.Max(S(30), (ClientSize.Height - (logo + S(220))) / 2);
        return (new Rectangle((ClientSize.Width - logo) / 2, top, logo, logo), top + logo + S(20));
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        var (_, textTop) = ComputeLayout();
        _link.Location = new Point((ClientSize.Width - _link.PreferredWidth) / 2, textTop + S(150));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var (logo, y) = ComputeLayout();
        Logo.Draw(g, logo);

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
        int w = ClientSize.Width;
        TextRenderer.DrawText(g, "GAMEROOT", _titleFont, new Rectangle(0, y, w, S(46)), Color.FromArgb(235, 238, 240), flags);
        TextRenderer.DrawText(g, $"System check  ·  version {Version}", _subFont, new Rectangle(0, y + S(48), w, S(24)), Theme.Accent, flags);
        TextRenderer.DrawText(g,
            "Shows your PC's BIOS, Windows and Windows Security settings in one place,\nlists running services and loaded drivers, and exports it all as a report.",
            _bodyFont, new Rectangle(S(20), y + S(84), w - S(40), S(48)), Theme.DimText, flags);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        PerformLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _subFont.Dispose();
            _bodyFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
