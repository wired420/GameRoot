using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GameRoot;

/// <summary>
/// The Security tab: a dark industrial panel where each setting gets a glowing indicator light.
/// Red = On, green = Off, amber = a third state (Evaluation, Audit), grey = could not be read.
/// </summary>
internal sealed class StatusBoard : Panel
{
    private static readonly Color Background = Color.FromArgb(24, 26, 29);
    private static readonly Color PlateTop = Color.FromArgb(70, 74, 80);
    private static readonly Color PlateBottom = Color.FromArgb(44, 47, 52);
    private static readonly Color LabelColor = Color.FromArgb(205, 210, 214);
    private static readonly Color DimText = Color.FromArgb(120, 126, 132);
    private static readonly Color Hazard = Color.FromArgb(232, 178, 30);

    private static readonly Color Red = Color.FromArgb(255, 48, 40);
    private static readonly Color Green = Color.FromArgb(40, 255, 96);
    private static readonly Color Amber = Color.FromArgb(255, 170, 0);
    private static readonly Color Unlit = Color.FromArgb(70, 72, 76);

    private IReadOnlyList<ReportLine> _lines = [];
    private readonly Font _labelFont;
    private readonly Font _valueFont;
    private readonly Font _headerFont;
    private readonly Font _groupFont;

    public StatusBoard()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        AutoScroll = true;
        BackColor = Background;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        string family = FontExists("Bahnschrift") ? "Bahnschrift" : "Segoe UI";
        _labelFont = new Font(family, 10.5f, FontStyle.Regular);
        _valueFont = new Font(family, 10.5f, FontStyle.Bold);
        _headerFont = new Font(family, 12f, FontStyle.Bold);
        _groupFont = new Font(family, 10f, FontStyle.Bold);

        var copy = new ToolStripMenuItem("Copy report as text");
        copy.Click += (_, _) => { if (_lines.Count > 0) Clipboard.SetText(SecurityReport.ToText(_lines)); };
        ContextMenuStrip = new ContextMenuStrip();
        ContextMenuStrip.Items.Add(copy);
    }

    public void SetLines(IReadOnlyList<ReportLine> lines)
    {
        _lines = lines;
        AutoScrollMinSize = new Size(MinWidth(), TotalHeight());
        Invalidate();
    }

    private float DpiScale => DeviceDpi / 96f;
    private int S(float v) => (int)Math.Round(v * DpiScale);

    private int RowHeight(LineKind kind) => kind switch
    {
        LineKind.SectionTitle => S(40),
        LineKind.GroupTitle => S(34),
        LineKind.Separator => S(26),
        _ => S(30),
    };

    private int TotalHeight() => S(16) + _lines.Sum(l => RowHeight(l.Kind)) + S(16);

    private int LabelColumnWidth()
    {
        int max = 0;
        foreach (var l in _lines.Where(l => l.Kind == LineKind.Check))
            max = Math.Max(max, S(28) + l.Depth * S(20) + TextRenderer.MeasureText(l.Text, _labelFont).Width);
        return max + S(24);
    }

    private int MinWidth() => LabelColumnWidth() + S(220);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

        int width = Math.Max(ClientSize.Width, MinWidth());
        int labelCol = LabelColumnWidth();
        int y = S(16);
        int margin = S(16);

        if (_lines.Count == 0)
        {
            DrawText(g, "SCANNING...", _headerFont, DimText, margin, y, S(40));
            return;
        }

        foreach (var line in _lines)
        {
            int h = RowHeight(line.Kind);
            switch (line.Kind)
            {
                case LineKind.SectionTitle:
                    DrawPlate(g, new Rectangle(margin, y + S(4), width - margin * 2, h - S(8)), line.Text.ToUpperInvariant(), _headerFont);
                    break;

                case LineKind.GroupTitle:
                {
                    int x = margin + S(12) + line.Depth * S(20);
                    DrawText(g, line.Text.ToUpperInvariant(), _groupFont, Hazard, x, y + S(6), h - S(6));
                    using var pen = new Pen(Color.FromArgb(90, Hazard), 1);
                    g.DrawLine(pen, x, y + h - S(4), width - margin - S(12), y + h - S(4));
                    break;
                }

                case LineKind.Check:
                {
                    int x = margin + S(12) + line.Depth * S(20);
                    DrawText(g, line.Text, _labelFont, LabelColor, x, y, h);

                    // Dotted leader from the label to the light.
                    int textW = TextRenderer.MeasureText(line.Text, _labelFont).Width;
                    using (var dots = new Pen(Color.FromArgb(55, 60, 66), 1) { DashStyle = DashStyle.Dot })
                        g.DrawLine(dots, x + textW + S(6), y + h / 2, labelCol - S(10), y + h / 2);

                    var (color, lit) = ColorFor(line.Value);
                    int d = S(16);
                    DrawLight(g, new Rectangle(labelCol, y + (h - d) / 2, d, d), color, lit);
                    string value = Display(line.Value);
                    DrawText(g, value, _valueFont, lit ? color : DimText, labelCol + d + S(12), y, h);
                    break;
                }

                case LineKind.Separator:
                    DrawSeam(g, margin, width - margin, y + h / 2);
                    break;
            }
            y += h;
        }
    }

    private static (Color Color, bool Lit) ColorFor(string? value) => value switch
    {
        "On" => (Red, true),
        "Off" => (Green, true),
        "Evaluation mode" or "Audit mode" => (Amber, true),
        _ => (Unlit, false),
    };

    private static string Display(string? value) => value switch
    {
        "On" => "ON",
        "Off" => "OFF",
        null => "",
        _ => value.ToUpperInvariant(),
    };

    private static void DrawText(Graphics g, string text, Font font, Color color, int x, int y, int h)
    {
        TextRenderer.DrawText(g, text, font, new Rectangle(x, y, 2000, h), color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
            TextFormatFlags.PreserveGraphicsTranslateTransform);
    }

    /// <summary>A brushed-steel nameplate with rivets.</summary>
    private void DrawPlate(Graphics g, Rectangle r, string text, Font font)
    {
        using (var path = Rounded(r, S(4)))
        {
            using var fill = new LinearGradientBrush(r, PlateTop, PlateBottom, LinearGradientMode.Vertical);
            g.FillPath(fill, path);
            using var edge = new Pen(Color.FromArgb(20, 20, 22), 1);
            g.DrawPath(edge, path);
        }
        using (var hi = new Pen(Color.FromArgb(60, 255, 255, 255), 1))
            g.DrawLine(hi, r.Left + S(4), r.Top + 1, r.Right - S(4), r.Top + 1);

        int rv = S(6);
        foreach (var (cx, cy) in new[] { (r.Left + S(10), r.Top + r.Height / 2), (r.Right - S(10), r.Top + r.Height / 2) })
        {
            var rr = new Rectangle(cx - rv / 2, cy - rv / 2, rv, rv);
            using var rivet = new LinearGradientBrush(rr, Color.FromArgb(150, 154, 160), Color.FromArgb(40, 42, 46), 45f);
            g.FillEllipse(rivet, rr);
        }
        DrawText(g, text, font, Color.FromArgb(235, 238, 240), r.Left + S(24), r.Top, r.Height);
    }

    /// <summary>A subtle engraved seam across the panel, used as the separator.</summary>
    private static void DrawSeam(Graphics g, int left, int right, int y)
    {
        using var groove = new Pen(Color.FromArgb(10, 11, 12), 1);
        using var light = new Pen(Color.FromArgb(58, 62, 68), 1);
        g.DrawLine(groove, left, y, right, y);
        g.DrawLine(light, left, y + 1, right, y + 1);
    }

    /// <summary>An indicator lamp: dark bezel, coloured lens with a soft glow and a specular highlight.</summary>
    private void DrawLight(Graphics g, Rectangle r, Color color, bool lit)
    {
        if (lit)
        {
            // Glow halo.
            var halo = Rectangle.Inflate(r, S(9), S(9));
            using var haloPath = new GraphicsPath();
            haloPath.AddEllipse(halo);
            using var glow = new PathGradientBrush(haloPath)
            {
                CenterColor = Color.FromArgb(150, color),
                SurroundColors = [Color.FromArgb(0, color)],
            };
            g.FillEllipse(glow, halo);
        }

        // Bezel.
        var bezel = Rectangle.Inflate(r, S(3), S(3));
        using (var bz = new LinearGradientBrush(bezel, Color.FromArgb(110, 114, 120), Color.FromArgb(18, 18, 20), 60f))
            g.FillEllipse(bz, bezel);

        // Lens.
        using (var lensPath = new GraphicsPath())
        {
            lensPath.AddEllipse(r);
            using var lens = new PathGradientBrush(lensPath)
            {
                CenterPoint = new PointF(r.Left + r.Width * 0.45f, r.Top + r.Height * 0.4f),
                CenterColor = lit ? Blend(color, Color.White, 0.45f) : Color.FromArgb(95, 98, 102),
                SurroundColors = [lit ? Blend(color, Color.Black, 0.35f) : Color.FromArgb(38, 40, 43)],
            };
            g.FillEllipse(lens, r);
        }

        // Specular highlight.
        var spec = new Rectangle(r.Left + r.Width / 5, r.Top + r.Height / 7, r.Width / 2, r.Height / 3);
        using (var sh = new SolidBrush(Color.FromArgb(lit ? 150 : 60, 255, 255, 255)))
            g.FillEllipse(sh, spec);
    }

    private static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = Math.Max(1, radius * 2);
        var p = new GraphicsPath();
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static bool FontExists(string name)
    {
        using var fonts = new InstalledFontCollection();
        return fonts.Families.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _labelFont.Dispose();
            _valueFont.Dispose();
            _headerFont.Dispose();
            _groupFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
