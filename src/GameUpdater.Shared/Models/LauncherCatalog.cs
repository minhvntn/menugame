namespace GameUpdater.Shared.Models;

public sealed class LauncherCatalog
{
    public DateTime GeneratedAtUtc { get; set; }

    public List<LauncherGameEntry> Games { get; init; } = new();
}

