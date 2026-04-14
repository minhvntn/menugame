namespace GameUpdater.Shared.Models;

public sealed class GameRecord
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = "Chung";

    public string InstallPath { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0.0";

    public string LaunchRelativePath { get; set; } = string.Empty;

    public string LaunchArguments { get; set; } = string.Empty;

    public DateTime? LastScannedAt { get; set; }

    public DateTime? LastUpdatedAt { get; set; }

    public string Notes { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 999999;

    public bool IsHot { get; set; }
}
