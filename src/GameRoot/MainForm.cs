using System.Diagnostics;
using System.Collections;

namespace GameRoot;

internal sealed class MainForm : Form
{
    private readonly TabStrip _tabs = new("Security", "Services", "Drivers", "About");
    private readonly AboutPanel _about = new();
    private readonly StatusBoard _board = new() { Dock = DockStyle.Fill };
    private readonly ListView _services = NewList(SystemLists.ServiceColumns);
    private readonly ListView _drivers = NewList(SystemLists.DriverColumns);
    private IReadOnlyList<ReportLine> _securityLines = [];
    private (List<ListRow> Rows, string? Error) _serviceRows = ([], null);
    private (List<ListRow> Rows, string? Error) _driverRows = ([], null);
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    private readonly ScanData? _initial;

    /// <summary>Opens with results already scanned by the splash screen.</summary>
    public MainForm(ScanData initial) : this() => _initial = initial;

    public MainForm()
    {
        Text = "GameRoot - system check (read-only, nothing is changed)";
        Size = new Size(1100, 820);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Theme.DarkTitleBar(this);
        if (Theme.AppIcon is { } icon) Icon = icon;

        // One content area; the tab strip shows one page at a time. Security is shown on launch.
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        Control[] pages = [_board, _services, _drivers, _about];
        foreach (var page in pages)
        {
            page.Visible = page == _board;
            content.Controls.Add(page);
        }
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            for (int i = 0; i < pages.Length; i++)
                pages[i].Visible = i == _tabs.SelectedIndex;
            UpdateStatus();
        };

        var refresh = new ToolStripButton("Refresh") { Alignment = ToolStripItemAlignment.Right };
        refresh.Click += async (_, _) => await LoadAllAsync();
        var statusStrip = new StatusStrip { Renderer = new Theme.Renderer(), SizingGrip = false };
        _status.ForeColor = Theme.Text;
        var export = new ToolStripButton("Export report...") { Alignment = ToolStripItemAlignment.Right };
        export.Click += (_, _) => ExportReport();
        statusStrip.Items.AddRange(new ToolStripItem[] { _status, refresh, export });
        if (_board.ContextMenuStrip is { } menu)
            menu.Renderer = new Theme.Renderer();

        Controls.Add(content);
        Controls.Add(_tabs);
        Controls.Add(statusStrip);
        Theme.DarkScrollBars(_board);

        _board.ToggleRequested += OnToggleRequested;
        _board.OpenRequested += OnOpenRequested;

        Shown += async (_, _) =>
        {
            if (_initial is not null) ShowData(_initial);
            else await LoadAllAsync();
        };
    }

    /// <summary>Opens the Windows page or console for a read-only row. GameRoot changes nothing itself.</summary>
    private void OnOpenRequested(object? sender, string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            _status.Text = "Opened the Windows settings page. Change it there if you want to.";
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not open that page: {ex.Message}";
        }
    }

    /// <summary>Flips a setting when its slider is clicked, then rescans the Security tab.</summary>
    private async void OnToggleRequested(object? sender, ReportLine line)
    {
        if (line.Toggle is not { } toggle || line.IsOn is not bool on)
            return;

        bool target = !on;
        _status.Text = $"Turning {line.Text} {(target ? "on" : "off")}...";
        try
        {
            await Task.Run(() => toggle.Set(target));
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not change {line.Text}: {ex.Message}";
            return;
        }

        // Re-read so the slider and light reflect what Windows actually stored.
        _securityLines = await Task.Run(SecurityReport.Scan);
        _board.SetLines(_securityLines);

        string note = toggle.NeedsReboot ? " Restart the PC for this to take full effect." : "";
        _status.Text = $"{line.Text} is now {(target ? "on" : "off")}.{note}";
    }

    private async Task LoadAllAsync()
    {
        UseWaitCursor = true;
        _status.Text = "Scanning...";

        var data = await Task.Run(() => ScanService.RunAll());
        ShowData(data);
        UseWaitCursor = false;
    }

    private void ShowData(ScanData data)
    {
        _securityLines = data.Security;
        _serviceRows = data.Services;
        _driverRows = data.Drivers;
        _board.SetLines(_securityLines);
        FitToBoard();
        Fill(_services, _serviceRows);
        Fill(_drivers, _driverRows);
        UpdateStatus();
    }

    /// <summary>Grows the window, within the screen, so the Security tab fits without scrolling.</summary>
    private void FitToBoard()
    {
        if (WindowState != FormWindowState.Normal) return;
        var area = Screen.FromControl(this).WorkingArea;
        int extraW = Math.Max(0, _board.ContentWidth - _board.ClientSize.Width);
        int extraH = Math.Max(0, _board.ContentHeight - _board.ClientSize.Height);
        if (extraW == 0 && extraH == 0) return;

        var bounds = new Rectangle(Left, Top, Math.Min(Width + extraW, area.Width), Math.Min(Height + extraH, area.Height));
        bounds.X = Math.Max(area.Left, Math.Min(bounds.X, area.Right - bounds.Width));
        bounds.Y = Math.Max(area.Top, Math.Min(bounds.Y, area.Bottom - bounds.Height));
        Bounds = bounds;
    }

    /// <summary>Saves Security, Services and Drivers to one plain-text file.</summary>
    private void ExportReport()
    {
        if (_securityLines.Count == 0)
        {
            _status.Text = "Wait for the scan to finish, then export.";
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Export GameRoot report",
            Filter = "Text file (*.txt)|*.txt",
            FileName = $"GameRoot-report-{Environment.MachineName}-{DateTime.Now:yyyy-MM-dd-HHmm}.txt",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, ReportExporter.Build(_securityLines, _serviceRows, _driverRows), new System.Text.UTF8Encoding(true));
            _status.Text = $"Report saved to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not save the report: {ex.Message}";
        }
    }

    private void UpdateStatus()
    {
        _status.Text = _tabs.SelectedIndex switch
        {
            1 => $"{_services.Items.Count} services running",
            2 => $"{_drivers.Items.Count} drivers loaded",
            3 => AboutPanel.RepoUrl,
            _ => "Red = on, green = off. Sliders change a setting; Open jumps to its Windows page. Right-click to copy.",
        };
    }


    private static void Fill(ListView list, (List<ListRow> Rows, string? Error) result)
    {
        list.BeginUpdate();
        list.Items.Clear();
        if (result.Error is not null)
            list.Items.Add(new ListViewItem($"Could not read the list: {result.Error}"));
        foreach (var row in result.Rows)
            list.Items.Add(new ListViewItem(row.Cells));
        list.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
        foreach (ColumnHeader c in list.Columns)
            c.Width = Math.Max(c.Width, TextRenderer.MeasureText(c.Text, list.Font).Width + 32);
        StretchLastColumn(list);
        list.EndUpdate();
    }

    private static void StretchLastColumn(ListView list)
    {
        if (list.Columns.Count == 0) return;
        var last = list.Columns[list.Columns.Count - 1];
        int others = 0;
        for (int i = 0; i < list.Columns.Count - 1; i++)
            others += list.Columns[i].Width;
        int remaining = list.ClientSize.Width - others;
        int content = TextRenderer.MeasureText(last.Text, list.Font).Width + 32;
        foreach (ListViewItem item in list.Items)
            if (item.SubItems.Count == list.Columns.Count)
                content = Math.Max(content, TextRenderer.MeasureText(item.SubItems[list.Columns.Count - 1].Text, list.Font).Width + 16);
        last.Width = Math.Max(content, remaining);
    }

    private static ListView NewList(string[] columns)
    {
        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = true,
            OwnerDraw = true,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = new Font(Theme.FontFamily, 9.75f),
        };
        foreach (var c in columns)
            list.Columns.Add(c);

        var headerFont = new Font(Theme.FontFamily, 9.75f, FontStyle.Bold);
        list.DrawColumnHeader += (_, e) =>
        {
            using (var fill = Theme.PlateBrush(e.Bounds))
                e.Graphics.FillRectangle(fill, e.Bounds);
            using (var edge = new Pen(Theme.Edge))
            {
                e.Graphics.DrawLine(edge, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
                e.Graphics.DrawLine(edge, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
            var text = Rectangle.Inflate(e.Bounds, -6, 0);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text?.ToUpperInvariant(), headerFont, text, Theme.Accent,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };
        list.DrawItem += (_, e) => e.DrawDefault = false;
        list.DrawSubItem += (_, e) =>
        {
            if (e.Item is null) return;
            bool selected = e.Item.Selected;
            var back = selected ? Theme.Selection : (e.ItemIndex % 2 == 0 ? Theme.Surface : Theme.SurfaceAlt);
            using (var b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);
            var text = Rectangle.Inflate(e.Bounds, -6, 0);
            TextRenderer.DrawText(e.Graphics, e.SubItem?.Text, list.Font, text, selected ? Color.White : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };

        // Owner-drawn rows flicker without double buffering, which ListView doesn't expose publicly.
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(list, true);

        // Stretch the last column so no unpainted header area is left on the right.
        list.Resize += (_, _) => StretchLastColumn(list);
        Theme.DarkScrollBars(list);

        // Click a column header to sort; click again to reverse.
        list.ColumnClick += (_, e) =>
        {
            var current = list.ListViewItemSorter as ColumnSorter;
            bool descending = current is not null && current.Column == e.Column && !current.Descending;
            list.ListViewItemSorter = new ColumnSorter(e.Column, descending);
        };

        // Ctrl+C copies the selected rows as tab-separated text.
        list.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.C && list.SelectedItems.Count > 0)
            {
                var lines = list.SelectedItems.Cast<ListViewItem>()
                    .Select(i => string.Join("\t", i.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text)));
                Clipboard.SetText(string.Join(Environment.NewLine, lines));
            }
        };
        return list;
    }

    private sealed class ColumnSorter(int column, bool descending) : IComparer
    {
        public int Column => column;
        public bool Descending => descending;

        public int Compare(object? x, object? y)
        {
            string a = Cell(x), b = Cell(y);
            int result = long.TryParse(a, out var na) && long.TryParse(b, out var nb)
                ? na.CompareTo(nb)
                : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            return descending ? -result : result;
        }

        private string Cell(object? o) =>
            o is ListViewItem item && column < item.SubItems.Count ? item.SubItems[column].Text : "";
    }
}
