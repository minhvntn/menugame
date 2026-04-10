using GameUpdater.Core.Services;
using GameUpdater.Data;
using GameUpdater.Data.Repositories;
using GameUpdater.WinForms.Forms;

namespace GameUpdater.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var appEnvironment = AppEnvironment.Create(AppContext.BaseDirectory);
        appEnvironment.EnsureCreated();

        var databasePath = Path.Combine(appEnvironment.DataDirectory, "gameupdater.db");
        DatabaseInitializer.InitializeAsync(databasePath).GetAwaiter().GetResult();

        var gameRepository = new SqliteGameRepository(databasePath);
        var logRepository = new SqliteLogRepository(databasePath);
        var manifestService = new ManifestService(appEnvironment);
        var backupService = new BackupService(appEnvironment);
        var resourceSyncService = new ResourceSyncService();
        var catalogService = new CatalogService(gameRepository);
        var gameService = new GameService(gameRepository, logRepository, manifestService);
        var updateService = new UpdateService(gameRepository, logRepository, manifestService, backupService);

        Application.Run(new MainForm(gameService, updateService, resourceSyncService, catalogService, logRepository));
    }
}
