using System.Drawing.Drawing2D;

namespace GameRoot;

/// <summary>A row of steel tab buttons. The selected tab is lit with an amber bar.</summary>
internal sealed class TabStrip : Control
{
    private readonly string[] _tabs;
    private int _selected;
    private int _hover = -1;
    private readonly Font _font = new(Theme.FontFamily, 10.5f, FontStyle.Bold);

    public event EventHandler? SelectedIndexChanged;

    public TabStrip(params string[] tabs)
    {
        _tabs = tabs;
        DoubleBuffered = true;
        ResizeRedraw = true;
        Dock = DockStyle.Top;
        Height = S(40);
        Cursor = Cursors.Hand;
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private int S(float v) => (int)Math.Round(v * DeviceDpi / 96f);

    private Rectangle TabRect(int i)
    {
        int w = S(130), gap = S(4), left = S(12);
        return new Rectangle(left + i * (w + gap), S(6), w, Height - S(6));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Rail under the tabs.
        using (var rail = new Pen(Theme.PlateBottom, S(2)))
            g.DrawLine(rail, 0, Height - 1, Width, Height - 1);

        for (int i = 0; i < _tabs.Length; i++)
        {
            var r = TabRect(i);
            bool sel = i == _selected;
            using (var path = TopRounded(r, S(4)))
            {
                using var fill = sel
                    ? Theme.PlateBrush(r)
                    : new LinearGradientBrush(r, i == _hover ? Color.FromArgb(52, 55, 60) : Color.FromArgb(40, 43, 47),
                        Color.FromArgb(30, 32, 36), LinearGradientMode.Vertical);
                g.FillPath(fill, path);
                using var edge = new Pen(Theme.Edge);
                g.DrawPath(edge, path);
            }
            if (sel)
            {
                using var bar = new SolidBrush(Theme.Accent);
                g.FillRectangle(bar, r.Left + S(6), r.Top + S(3), r.Width - S(12), S(2));
            }
            TextRenderer.DrawText(g, _tabs[i].ToUpperInvariant(), _font, r, sel ? Color.White : Theme.DimText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    private static GraphicsPath TopRounded(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddLine(r.Right, r.Bottom, r.Left, r.Bottom);
        p.CloseFigure();
        return p;
    }

    private int HitTest(Point p)
    {
        for (int i = 0; i < _tabs.Length; i++)
            if (TabRect(i).Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = HitTest(e.Location);
        if (h != _hover) { _hover = h; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int h = HitTest(e.Location);
        if (h >= 0) SelectedIndex = h;
        base.OnMouseDown(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _font.Dispose();
        base.Dispose(disposing);
    }
}
