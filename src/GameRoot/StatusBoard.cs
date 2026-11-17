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
        _sections = BuildSections(lines);
        UpdateScrollSize();
        Invalidate();
    }

    // ---- Structure rebuilt from the flat report lines ----

    private sealed class BoardGroup(string? title)
    {
        public string? Title { get; } = title;
        public List<ReportLine> Checks { get; } = [];
    }

    private sealed class BoardSection(string? title)
    {
        public string? Title { get; } = title;
        public List<BoardGroup> Groups { get; } = [];
        /// <summary>A simple section has no group headings (BIOS Settings, Windows Settings).</summary>
        public bool IsSimple => Groups.All(g => g.Title is null);
    }

    private List<BoardSection> _sections = [];

    private static List<BoardSection> BuildSections(IReadOnlyList<ReportLine> lines)
    {
        var result = new List<BoardSection>();
        BoardSection? section = null;
        BoardGroup? group = null;
        foreach (var l in lines)
        {
            switch (l.Kind)
            {
                case LineKind.SectionTitle:
                    section = new BoardSection(l.Text);
                    result.Add(section);
                    group = null;
                    break;
                case LineKind.GroupTitle:
                    if (section is null) { section = new BoardSection(null); result.Add(section); }
                    group = new BoardGroup(l.Text);
                    section.Groups.Add(group);
                    break;
                case LineKind.Check:
                    if (section is null) { section = new BoardSection(null); result.Add(section); }
                    if (group is null) { group = new BoardGroup(null); section.Groups.Add(group); }
                    group.Checks.Add(l);
                    break;
                case LineKind.Separator:
                    section = null;
                    group = null;
                    break;
            }
        }
        return result;
    }

    // ---- Layout ----

    private float DpiScale => DeviceDpi / 96f;
    private int S(float v) => (int)Math.Round(v * DpiScale);

    private int EdgeMargin => S(16);
    private int Gutter => S(28);
    private int PlateHeight => S(34);
    private int RowHeight => S(26);
    private int GroupHeight => S(30);
    private int LightSize => S(16);

    /// <summary>Width reserved right of each label for the light and its value text.</summary>
    private int ValueColumn()
    {
        int max = TextRenderer.MeasureText("OFF", _valueFont).Width;
        foreach (var l in _lines.Where(l => l.Kind == LineKind.Check))
            max = Math.Max(max, TextRenderer.MeasureText(Display(l.Value), _valueFont).Width);
        return LightSize + S(14) + max + S(8);
    }

    private int MinColumnWidth()
    {
        int label = 0;
        foreach (var l in _lines.Where(l => l.Kind == LineKind.Check))
            label = Math.Max(label, TextRenderer.MeasureText(l.Text, _labelFont).Width);
        return S(16) + label + S(24) + ValueColumn();
    }

    private int MinWidth() => EdgeMargin * 2 + Gutter + MinColumnWidth() * 2;

    private void UpdateScrollSize()
    {
        int width = Math.Max(ClientSize.Width, MinWidth());
        AutoScrollMinSize = new Size(MinWidth(), Render(null, width));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_lines.Count > 0) UpdateScrollSize();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

        if (_lines.Count == 0)
        {
            DrawText(g, "SCANNING...", _headerFont, DimText, EdgeMargin, EdgeMargin, S(40));
            return;
        }
        Render(g, Math.Max(ClientSize.Width, MinWidth()));
    }

    /// <summary>
    /// Lays out (and, when g is not null, draws) the board. Simple sections sit side by side in two
    /// columns; a section with group headings gets a full-width nameplate and its groups split into
    /// two balanced columns. Returns the total height.
    /// </summary>
    private int Render(Graphics? g, int width)
    {
        int m = EdgeMargin;
        int colW = (width - m * 2 - Gutter) / 2;
        int xL = m, xR = m + colW + Gutter;
        int y = m;
        int valueCol = ValueColumn();

        for (int i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            if (section.IsSimple)
            {
                var right = i + 1 < _sections.Count && _sections[i + 1].IsSimple ? _sections[++i] : null;
                int hL = SimpleCard(g, section, xL, y, colW, valueCol);
                int hR = right is null ? 0 : SimpleCard(g, right, xR, y, colW, valueCol);
                y += Math.Max(hL, hR);
            }
            else
            {
                if (section.Title is not null)
                {
                    if (g is not null)
                        DrawPlate(g, new Rectangle(m, y, width - m * 2, PlateHeight), section.Title.ToUpperInvariant(), _headerFont);
                    y += PlateHeight + S(4);
                }
                var (left, rightGroups) = SplitGroups(section.Groups);
                int yl = GroupColumn(g, left, xL, y, colW, valueCol);
                int yr = GroupColumn(g, rightGroups, xR, y, colW, valueCol);
                y = Math.Max(yl, yr);
            }

            // Seam between blocks.
            y += S(10);
            if (g is not null)
                DrawSeam(g, m, width - m, y);
            y += S(14);
        }
        return y + m;
    }

    private int SimpleCard(Graphics? g, BoardSection section, int x, int y, int w, int valueCol)
    {
        int top = y;
        if (section.Title is not null)
        {
            if (g is not null)
                DrawPlate(g, new Rectangle(x, y, w, PlateHeight), section.Title.ToUpperInvariant(), _headerFont);
            y += PlateHeight + S(4);
        }
        foreach (var check in section.Groups.SelectMany(gr => gr.Checks))
        {
            if (g is not null) DrawCheck(g, check, x + S(16), x + w, y, valueCol);
            y += RowHeight;
        }
        return y - top;
    }

    private int GroupColumn(Graphics? g, List<BoardGroup> groups, int x, int y, int w, int valueCol)
    {
        foreach (var group in groups)
        {
            if (group.Title is not null)
            {
                if (g is not null)
                {
                    DrawText(g, group.Title.ToUpperInvariant(), _groupFont, Hazard, x + S(8), y + S(6), GroupHeight - S(6));
                    using var pen = new Pen(Color.FromArgb(90, Hazard), 1);
                    g.DrawLine(pen, x + S(8), y + GroupHeight - S(3), x + w - S(4), y + GroupHeight - S(3));
                }
                y += GroupHeight;
            }
            foreach (var check in group.Checks)
            {
                if (g is not null) DrawCheck(g, check, x + S(16), x + w, y, valueCol);
                y += RowHeight;
            }
            y += S(4);
        }
        return y;
    }

    /// <summary>
    /// Splits groups into two columns of as close to equal height as possible, so the whole board
    /// fits without scrolling. Groups keep their original order within each column.
    /// </summary>
    private (List<BoardGroup> Left, List<BoardGroup> Right) SplitGroups(List<BoardGroup> groups)
    {
        int Height(BoardGroup gr) => (gr.Title is null ? 0 : GroupHeight) + gr.Checks.Count * RowHeight + S(4);
        int n = Math.Min(groups.Count, 16);
        int bestMask = 0, bestCost = int.MaxValue;
        // Group 0 always goes left, so each split is only tried once.
        for (int mask = 1; mask < (1 << n); mask += 2)
        {
            int left = 0, right = 0;
            for (int i = 0; i < n; i++)
                if ((mask & (1 << i)) != 0) left += Height(groups[i]); else right += Height(groups[i]);
            int cost = Math.Max(left, right);
            if (cost < bestCost) { bestCost = cost; bestMask = mask; }
        }
        var l = new List<BoardGroup>();
        var r = new List<BoardGroup>();
        for (int i = 0; i < groups.Count; i++)
            ((i < n && (bestMask & (1 << i)) != 0) ? l : r).Add(groups[i]);
        return (l, r);
    }

    /// <summary>Height needed to show the whole board without scrolling.</summary>
    public int ContentHeight => _lines.Count == 0 ? 0 : Render(null, Math.Max(ClientSize.Width, MinWidth()));

    /// <summary>Width needed to show the whole board without scrolling.</summary>
    public int ContentWidth => _lines.Count == 0 ? 0 : MinWidth();

    private void DrawCheck(Graphics g, ReportLine check, int x, int right, int y, int valueCol)
    {
        int h = RowHeight;
        int lightX = right - valueCol;
        DrawText(g, check.Text, _labelFont, LabelColor, x, y, h);

        int textW = TextRenderer.MeasureText(check.Text, _labelFont).Width;
        if (lightX - S(10) > x + textW + S(6))
        {
            using var dots = new Pen(Color.FromArgb(55, 60, 66), 1) { DashStyle = DashStyle.Dot };
            g.DrawLine(dots, x + textW + S(6), y + h / 2, lightX - S(10), y + h / 2);
        }

        var (color, lit) = ColorFor(check.Value);
        int d = LightSize;
        DrawLight(g, new Rectangle(lightX, y + (h - d) / 2, d, d), color, lit);
        DrawText(g, Display(check.Value), _valueFont, lit ? color : DimText, lightX + d + S(12), y, h);
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
