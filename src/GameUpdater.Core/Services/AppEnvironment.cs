namespace GameUpdater.Core.Services;

public sealed class AppEnvironment
{
    private AppEnvironment(string baseDirectory)
    {
        BaseDirectory = baseDirectory;
        DataDirectory = Path.Combine(baseDirectory, "data");
        ManifestDirectory = Path.Combine(baseDirectory, "manifests");
        BackupDirectory = Path.Combine(baseDirectory, "backups");
        LogDirectory = Path.Combine(baseDirectory, "logs");
        DownloadDirectory = Path.Combine(baseDirectory, "downloads");
    }

    public string BaseDirectory { get; }

    public string DataDirectory { get; }

    public string ManifestDirectory { get; }

    public string BackupDirectory { get; }

    public string LogDirectory { get; }

    public string DownloadDirectory { get; }

    public static AppEnvironment Create(string baseDirectory)
    {
        return new AppEnvironment(baseDirectory);
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ManifestDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(DownloadDirectory);
    }
}
