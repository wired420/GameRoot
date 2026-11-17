using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GameRoot;

/// <summary>Shared colours and helpers for the dark industrial look.</summary>
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(24, 26, 29);
    public static readonly Color Surface = Color.FromArgb(32, 35, 39);
    public static readonly Color SurfaceAlt = Color.FromArgb(37, 40, 45);
    public static readonly Color PlateTop = Color.FromArgb(70, 74, 80);
    public static readonly Color PlateBottom = Color.FromArgb(44, 47, 52);
    public static readonly Color Edge = Color.FromArgb(14, 15, 17);
    public static readonly Color Text = Color.FromArgb(205, 210, 214);
    public static readonly Color DimText = Color.FromArgb(120, 126, 132);
    public static readonly Color Accent = Color.FromArgb(232, 178, 30);
    public static readonly Color Selection = Color.FromArgb(78, 64, 22);

    public static string FontFamily { get; } = FontExists("Bahnschrift") ? "Bahnschrift" : "Segoe UI";

    public static LinearGradientBrush PlateBrush(Rectangle r) =>
        new(r, PlateTop, PlateBottom, LinearGradientMode.Vertical);

    private static bool FontExists(string name)
    {
        using var fonts = new System.Drawing.Text.InstalledFontCollection();
        return fonts.Families.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Gives a control dark scroll bars (Windows 10 1809 and later).</summary>
    public static void DarkScrollBars(Control c)
    {
        void Apply() { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } }
        if (c.IsHandleCreated) Apply();
        c.HandleCreated += (_, _) => Apply();
    }

    /// <summary>Makes the window's title bar dark (Windows 11, and Windows 10 20H1+).</summary>
    public static void DarkTitleBar(Form f)
    {
        f.HandleCreated += (_, _) =>
        {
            int on = 1;
            try { DwmSetWindowAttribute(f.Handle, 20, ref on, sizeof(int)); } catch { }
        };
    }

    /// <summary>Colours for the status strip and right-click menus.</summary>
    public sealed class Renderer() : ToolStripProfessionalRenderer(new Colors())
    {
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Color.White : Text;
            base.OnRenderItemText(e);
        }
    }

    private sealed class Colors : ProfessionalColorTable
    {
        public override Color StatusStripGradientBegin => PlateBottom;
        public override Color StatusStripGradientEnd => Color.FromArgb(36, 38, 42);
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color MenuBorder => Edge;
        public override Color MenuItemBorder => Accent;
        public override Color MenuItemSelected => Selection;
        public override Color ButtonSelectedHighlight => Selection;
        public override Color ButtonSelectedGradientBegin => Selection;
        public override Color ButtonSelectedGradientMiddle => Selection;
        public override Color ButtonSelectedGradientEnd => Selection;
        public override Color ButtonSelectedBorder => Accent;
        public override Color ButtonPressedGradientBegin => PlateTop;
        public override Color ButtonPressedGradientMiddle => PlateTop;
        public override Color ButtonPressedGradientEnd => PlateTop;
        public override Color ButtonPressedBorder => Accent;
    }
}
