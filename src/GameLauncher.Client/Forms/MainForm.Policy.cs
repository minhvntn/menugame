using System.Net.Http;
using GameUpdater.Shared.Models;

namespace GameLauncher.Client.Forms;

public sealed partial class MainForm
{
    private static readonly HttpClient WallpaperHttpClient = new();

    private async Task ApplyServerPolicyAsync(LauncherClientPolicy? policy)
    {
        var effectivePolicy = policy ?? new LauncherClientPolicy();
        _enableCloseAppHotKeyFromServer = effectivePolicy.EnableCloseRunningApplicationHotKey;
        UpdateCloseAppHotKeyRegistration();
        ApplyBrandingPolicy(effectivePolicy);
        ApplyKioskPolicy(effectivePolicy.EnableFullscreenKioskMode);

        var wallpaperPath = effectivePolicy.ClientWindowsWallpaperPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(wallpaperPath))
        {
            return;
        }

        var resolvedWallpaperPath = ResolvePolicyWallpaperPath(wallpaperPath, _catalogPath);
        await TrySetWindowsWallpaperAsync(resolvedWallpaperPath);
    }

    private void ApplyBrandingPolicy(LauncherClientPolicy policy)
    {
        _cafeNameLabel.Text = string.IsNullOrWhiteSpace(policy.CafeDisplayName)
            ? CafeDisplayName
            : policy.CafeDisplayName.Trim();

        var bannerMessage = policy.BannerMessage?.Trim() ?? string.Empty;
        _bannerMessageLabel.Text = string.IsNullOrWhiteSpace(bannerMessage)
            ? "Chào mừng quý khách"
            : bannerMessage;
        _bannerMessageLabel.Visible = true;
        ThemeFontFamily = string.IsNullOrWhiteSpace(policy.ThemeFontFamily)
            ? "Segoe UI"
            : policy.ThemeFontFamily.Trim();

        if (TryParseHtmlColor(policy.ThemeAccentColor, out var accentColor))
        {
            _bannerMessageLabel.BackColor = accentColor;
        }
    }

    private void ApplyKioskPolicy(bool enabled)
    {
        if (enabled)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            return;
        }

        TopMost = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        if (WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Normal;
        }
    }

    private static bool TryParseHtmlColor(string? input, out Color color)
    {
        color = Color.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        try
        {
            color = ColorTranslator.FromHtml(input.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TrySetWindowsWallpaperAsync(string imagePathOrUri)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imagePathOrUri))
            {
                return false;
            }

            string localPath;
            if (TryGetHttpUri(imagePathOrUri, out var wallpaperUri))
            {
                localPath = await DownloadWallpaperToCacheAsync(wallpaperUri);
            }
            else
            {
                localPath = Path.GetFullPath(imagePathOrUri);
            }

            return ApplyWindowsWallpaper(localPath);
        }
        catch
        {
            return false;
        }
    }

    private static bool ApplyWindowsWallpaper(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return false;
        }

        return SystemParametersInfo(
            SpiSetDeskWallpaper,
            0,
            imagePath,
            SpifUpdateIniFile | SpifSendWinIniChange);
    }

    private static async Task<string> DownloadWallpaperToCacheAsync(Uri wallpaperUri)
    {
        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameLauncher.Client",
            "wallpaper-cache");

        Directory.CreateDirectory(cacheDirectory);

        var extension = Path.GetExtension(wallpaperUri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        var localPath = Path.Combine(cacheDirectory, $"server-wallpaper{extension}");
        using var response = await WallpaperHttpClient.GetAsync(wallpaperUri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var sourceStream = await response.Content.ReadAsStreamAsync();
        await using var targetStream = new FileStream(
            localPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await sourceStream.CopyToAsync(targetStream);
        await targetStream.FlushAsync();
        return localPath;
    }

    private static string ResolvePolicyWallpaperPath(string configuredWallpaperPath, string catalogPath)
    {
        var wallpaperPath = configuredWallpaperPath.Trim();
        if (string.IsNullOrWhiteSpace(wallpaperPath))
        {
            return string.Empty;
        }

        if (TryGetHttpUri(wallpaperPath, out _))
        {
            return wallpaperPath;
        }

        if (Path.IsPathRooted(wallpaperPath))
        {
            return Path.GetFullPath(wallpaperPath);
        }

        if (TryGetHttpUri(catalogPath, out var catalogUri))
        {
            return new Uri(catalogUri, wallpaperPath).ToString();
        }

        try
        {
            var catalogFullPath = Path.GetFullPath(catalogPath);
            var catalogDirectory = Path.GetDirectoryName(catalogFullPath);
            if (!string.IsNullOrWhiteSpace(catalogDirectory))
            {
                return Path.GetFullPath(Path.Combine(catalogDirectory, wallpaperPath));
            }
        }
        catch
        {
            // Keep fallback behavior below.
        }

        return wallpaperPath;
    }

    private static bool TryGetHttpUri(string input, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (!string.Equals(candidate.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = candidate;
        return true;
    }
}
