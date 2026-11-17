namespace GameRoot;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var answer = MessageBox.Show(
            "This software may alter security settings upon your computer at your request. " +
            "If this is not your intention, please choose No to close this software now. " +
            "Otherwise select Yes to continue.",
            "GameRoot",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return;

        // Scan behind a splash window with a progress bar, then open the main window with the results.
        ScanData? data;
        string? error;
        using (var splash = new SplashForm())
        {
            Application.Run(splash);
            data = splash.Result;
            error = splash.Error;
        }

        if (data is null)
        {
            MessageBox.Show($"The scan could not finish: {error ?? "unknown error"}", "GameRoot",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm(data));
    }
}
