using GameLauncher.Client.Forms;
using GameLauncher.Client.Services;

namespace GameLauncher.Client;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var settingsService = new SettingsService(AppContext.BaseDirectory);
        var catalogService = new CatalogReaderService();
        var launchService = new GameLaunchService();
        var prewarmService = new GamePrewarmService();

        Application.Run(new MainForm(settingsService, catalogService, launchService, prewarmService));
    }
}
