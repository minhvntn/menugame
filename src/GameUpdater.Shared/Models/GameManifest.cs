namespace GameUpdater.Shared.Models;

public sealed class GameManifest
{
    public int GameId { get; set; }

    public string GameName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; }

    public List<ManifestFileEntry> Files { get; init; } = new();
}

