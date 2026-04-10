namespace GameUpdater.Shared.Models;

public sealed class LauncherGameEntry
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string InstallPath { get; set; } = string.Empty;

    public string LaunchRelativePath { get; set; } = string.Empty;

    public string LaunchArguments { get; set; } = string.Empty;
}
