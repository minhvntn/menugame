namespace GameUpdater.Shared.Models;

public sealed class LauncherClientPolicy
{
    public string ClientWindowsWallpaperPath { get; set; } = string.Empty;

    public bool EnableCloseRunningApplicationHotKey { get; set; } = true;

    public string CafeDisplayName { get; set; } = "Cyber Game";

    public string BannerMessage { get; set; } = string.Empty;

    public bool EnableFullscreenKioskMode { get; set; }

    public string ThemeAccentColor { get; set; } = "#38BDF8";

    public string ThemeFontFamily { get; set; } = "Segoe UI";
}
