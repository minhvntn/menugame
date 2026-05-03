using GameLauncher.Client.Forms;
using GameLauncher.Client.Services;
using System.Threading;

namespace GameLauncher.Client;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Global\GameLauncher.Client.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var settingsService = new SettingsService(AppContext.BaseDirectory);
        var catalogService = new CatalogReaderService();
        var launchService = new GameLaunchService();

        Application.Run(new MainForm(settingsService, catalogService, launchService));
    }
}
