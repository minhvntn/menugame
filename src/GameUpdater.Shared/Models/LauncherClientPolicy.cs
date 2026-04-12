namespace GameUpdater.Shared.Models;

public sealed class LauncherClientPolicy
{
    public string ClientWindowsWallpaperPath { get; set; } = string.Empty;

    public bool EnableCloseRunningApplicationHotKey { get; set; } = true;
}
