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

        Application.Run(new MainForm());
    }
}
