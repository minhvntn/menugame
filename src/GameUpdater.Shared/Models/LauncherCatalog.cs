namespace GameUpdater.Shared.Models;

public sealed class LauncherCatalog
{
    public DateTime GeneratedAtUtc { get; set; }

    public LauncherClientPolicy ClientPolicy { get; set; } = new();

    public List<LauncherGameEntry> Games { get; init; } = new();
}
