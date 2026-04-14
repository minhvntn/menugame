using GameUpdater.Shared.Models;

namespace GameLauncher.Client.Models;

public sealed class LauncherGameRow
{
    public LauncherGameEntry Source { get; init; } = new();

    public string Name => Source.Name;

    public string Category => Source.Category;

    public string Version => Source.Version;

    public string InstallPath => Source.InstallPath;

    public string LaunchRelativePath => Source.LaunchRelativePath;

    public string LaunchArguments => Source.LaunchArguments;

    public string ResolvedExecutablePath { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public int SortOrder => Source.SortOrder;

    public bool IsHot => Source.IsHot;
}
