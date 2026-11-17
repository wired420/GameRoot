using System.Collections;

namespace GameRoot;

internal sealed class MainForm : Form
{
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly StatusBoard _board = new() { Dock = DockStyle.Fill };
    private readonly ListView _services = NewList(SystemLists.ServiceColumns);
    private readonly ListView _drivers = NewList(SystemLists.DriverColumns);
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    public MainForm()
    {
        Text = "GameRoot - system check (read-only, nothing is changed)";
        Size = new Size(1000, 760);
        StartPosition = FormStartPosition.CenterScreen;

        var security = new TabPage("Security");
        security.Controls.Add(_board);
        var services = new TabPage("Services");
        services.Controls.Add(_services);
        var drivers = new TabPage("Drivers");
        drivers.Controls.Add(_drivers);
        _tabs.TabPages.AddRange(new[] { security, services, drivers });
        _tabs.SelectedIndexChanged += (_, _) => UpdateStatus();

        var refresh = new ToolStripButton("Refresh") { Alignment = ToolStripItemAlignment.Right };
        refresh.Click += async (_, _) => await LoadAllAsync();
        var statusStrip = new StatusStrip();
        statusStrip.Items.AddRange(new ToolStripItem[] { _status, refresh });

        Controls.Add(_tabs);
        Controls.Add(statusStrip);

        // Security is the tab shown on launch.
        _tabs.SelectedTab = security;
        Shown += async (_, _) => await LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        UseWaitCursor = true;
        _status.Text = "Scanning...";

        var report = Task.Run(SecurityReport.Scan);
        var services = Task.Run(() => Safe(SystemLists.RunningServices));
        var drivers = Task.Run(() => Safe(SystemLists.LoadedDrivers));

        _board.SetLines(await report);
        Fill(_services, await services);
        Fill(_drivers, await drivers);

        UseWaitCursor = false;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        _status.Text = _tabs.SelectedIndex switch
        {
            1 => $"{_services.Items.Count} services running",
            2 => $"{_drivers.Items.Count} drivers loaded",
            _ => "Read-only scan. Nothing on this PC is changed. Red = on, green = off. Right-click to copy as text.",
        };
    }

    private static (List<ListRow> Rows, string? Error) Safe(Func<List<ListRow>> query)
    {
        try { return (query(), null); }
        catch (Exception ex) { return ([], ex.Message); }
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
            c.Width = Math.Max(c.Width, TextRenderer.MeasureText(c.Text, list.Font).Width + 24);
        list.EndUpdate();
    }

    private static ListView NewList(string[] columns)
    {
        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = true,
        };
        foreach (var c in columns)
            list.Columns.Add(c);

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
