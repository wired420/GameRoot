using System.Drawing.Drawing2D;

namespace GameRoot;

/// <summary>
/// The GameRoot logo, drawn in code so it scales cleanly: a steel shield (security) laced with
/// amber circuit traces (wires), a game controller at its centre (games), and a glowing
/// indicator light at the top like the ones on the Security tab.
/// </summary>
internal static class Logo
{
    private static readonly Color Amber = Color.FromArgb(232, 178, 30);
    private static readonly Color Red = Color.FromArgb(255, 48, 40);

    public static void Draw(Graphics g, Rectangle bounds)
    {
        var oldMode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int size = Math.Min(bounds.Width, bounds.Height);
        float ox = bounds.X + (bounds.Width - size) / 2f;
        float oy = bounds.Y + (bounds.Height - size) / 2f;
        float u = size / 100f;
        PointF P(float x, float y) => new(ox + x * u, oy + y * u);

        // Shield outline in a 100 x 100 design space.
        using var shield = new GraphicsPath();
        shield.AddLines(new[] { P(50, 6), P(86, 16), P(86, 48) });
        shield.AddBezier(P(86, 48), P(86, 70), P(70, 86), P(50, 96));
        shield.AddBezier(P(50, 96), P(30, 86), P(14, 70), P(14, 48));
        shield.AddLines(new[] { P(14, 48), P(14, 16), P(50, 6) });
        shield.CloseFigure();

        var box = shield.GetBounds();
        using (var fill = new LinearGradientBrush(box, Color.FromArgb(78, 83, 90), Color.FromArgb(30, 32, 36), 90f))
            g.FillPath(fill, shield);

        // Circuit traces, clipped to the shield, each ending in a solder pad.
        var oldClip = g.Clip;
        g.SetClip(shield, CombineMode.Intersect);
        using (var trace = new Pen(Color.FromArgb(200, Amber), Math.Max(1f, 1.6f * u)) { LineJoin = LineJoin.Round })
        using (var pad = new SolidBrush(Amber))
        {
            PointF[][] traces =
            [
                [P(14, 30), P(24, 30), P(30, 36)],
                [P(86, 30), P(76, 30), P(70, 36)],
                [P(30, 16), P(30, 24), P(36, 30)],
                [P(70, 16), P(70, 24), P(64, 30)],
                [P(22, 70), P(30, 70), P(36, 64)],
                [P(78, 70), P(70, 70), P(64, 64)],
                [P(50, 92), P(50, 72)],
            ];
            float r = 2.2f * u;
            foreach (var t in traces)
            {
                g.DrawLines(trace, t);
                var end = t[t.Length - 1];
                g.FillEllipse(pad, end.X - r, end.Y - r, r * 2, r * 2);
            }
        }
        g.Clip = oldClip;

        // Shield rim.
        using (var rim = new Pen(Amber, Math.Max(1.5f, 2.6f * u)) { LineJoin = LineJoin.Round })
            g.DrawPath(rim, shield);

        // Game controller.
        var body = RectangleF.FromLTRB(ox + 29 * u, oy + 42 * u, ox + 71 * u, oy + 62 * u);
        using (var ctrl = Capsule(body))
        {
            using var cf = new LinearGradientBrush(body, Color.FromArgb(46, 49, 54), Color.FromArgb(18, 19, 22), 90f);
            g.FillPath(cf, ctrl);
            using var co = new Pen(Amber, Math.Max(1f, 1.8f * u));
            g.DrawPath(co, ctrl);
        }
        // D-pad.
        using (var dpad = new SolidBrush(Color.FromArgb(225, 228, 232)))
        {
            g.FillRectangle(dpad, ox + 35 * u, oy + 50.5f * u, 9 * u, 3 * u);
            g.FillRectangle(dpad, ox + 38 * u, oy + 47.5f * u, 3 * u, 9 * u);
        }
        // Face buttons.
        float br = 2.4f * u;
        using (var b1 = new SolidBrush(Red))
            g.FillEllipse(b1, ox + 61 * u - br, oy + 49 * u - br, br * 2, br * 2);
        using (var b2 = new SolidBrush(Color.FromArgb(40, 255, 96)))
            g.FillEllipse(b2, ox + 65.5f * u - br, oy + 54.5f * u - br, br * 2, br * 2);

        // Indicator light at the top of the shield.
        var lamp = new RectangleF(ox + 45.5f * u, oy + 15 * u, 9 * u, 9 * u);
        using (var haloPath = new GraphicsPath())
        {
            var halo = RectangleF.Inflate(lamp, 5 * u, 5 * u);
            haloPath.AddEllipse(halo);
            using var glow = new PathGradientBrush(haloPath)
            {
                CenterColor = Color.FromArgb(140, Red),
                SurroundColors = [Color.FromArgb(0, Red)],
            };
            g.FillEllipse(glow, halo);
        }
        using (var lens = new SolidBrush(Red))
            g.FillEllipse(lens, lamp);
        using (var spec = new SolidBrush(Color.FromArgb(160, 255, 255, 255)))
            g.FillEllipse(spec, lamp.X + lamp.Width * 0.2f, lamp.Y + lamp.Height * 0.15f, lamp.Width * 0.45f, lamp.Height * 0.35f);

        g.SmoothingMode = oldMode;
    }

    private static GraphicsPath Capsule(RectangleF r)
    {
        float d = r.Height;
        var p = new GraphicsPath();
        p.AddArc(r.Left, r.Top, d, d, 90, 180);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 180);
        p.CloseFigure();
        return p;
    }
}
