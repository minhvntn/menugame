using System.Text;
using System.Text.Json;
using GameUpdater.Core.Abstractions;
using GameUpdater.Shared.Models;

namespace GameUpdater.Core.Services;

public sealed class CatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly IGameRepository _gameRepository;

    public CatalogService(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
    }

    public async Task<LauncherCatalog> BuildCatalogAsync(
        LauncherClientPolicy? clientPolicy = null,
        CancellationToken cancellationToken = default)
    {
        var games = await _gameRepository.GetAllAsync(cancellationToken);

        var catalog = new LauncherCatalog
        {
            GeneratedAtUtc = DateTime.UtcNow,
            ClientPolicy = ClonePolicy(clientPolicy)
        };

        foreach (var game in games
                     .OrderBy(item => item.SortOrder)
                     .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            catalog.Games.Add(new LauncherGameEntry
            {
                Id = game.Id,
                Name = game.Name,
                Category = game.Category,
                Version = game.Version,
                InstallPath = game.InstallPath,
                LaunchRelativePath = game.LaunchRelativePath ?? string.Empty,
                LaunchArguments = game.LaunchArguments ?? string.Empty,
                SortOrder = game.SortOrder,
                IsHot = game.IsHot
            });
        }

        return catalog;
    }

    public async Task ExportToFileAsync(
        string outputPath,
        LauncherClientPolicy? clientPolicy = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Vui lòng chọn đường dẫn xuất danh mục.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var effectivePolicy = PrepareClientPolicyForOutput(clientPolicy, fullPath);
        var catalog = await BuildCatalogAsync(effectivePolicy, cancellationToken);
        var json = JsonSerializer.Serialize(catalog, JsonOptions);
        await File.WriteAllTextAsync(fullPath, json, Encoding.UTF8, cancellationToken);
    }

    private static LauncherClientPolicy PrepareClientPolicyForOutput(
        LauncherClientPolicy? clientPolicy,
        string catalogFullPath)
    {
        var effective = ClonePolicy(clientPolicy);
        var wallpaperPath = effective.ClientWindowsWallpaperPath;
        if (string.IsNullOrWhiteSpace(wallpaperPath))
        {
            return effective;
        }

        if (TryGetHttpUri(wallpaperPath, out _))
        {
            return effective;
        }

        try
        {
            var catalogDirectory = Path.GetDirectoryName(catalogFullPath);
            if (string.IsNullOrWhiteSpace(catalogDirectory))
            {
                return effective;
            }

            var sourcePath = ResolveWallpaperSourcePath(wallpaperPath, catalogDirectory);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return effective;
            }

            var extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            var targetFileName = $"client.wallpaper{extension}";
            var targetPath = Path.Combine(catalogDirectory, targetFileName);

            if (!string.Equals(
                    Path.GetFullPath(sourcePath),
                    Path.GetFullPath(targetPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
            }

            // Always persist as relative path so client can resolve from catalog location.
            effective.ClientWindowsWallpaperPath = targetFileName.Replace('\\', '/');
        }
        catch
        {
            // Keep original path when copy fails (permission/share issue).
        }

        return effective;
    }

    private static string ResolveWallpaperSourcePath(string configuredPath, string catalogDirectory)
    {
        var trimmed = configuredPath.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(trimmed))
        {
            return Path.GetFullPath(trimmed);
        }

        return Path.GetFullPath(Path.Combine(catalogDirectory, trimmed));
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

    private static LauncherClientPolicy ClonePolicy(LauncherClientPolicy? clientPolicy)
    {
        return new LauncherClientPolicy
        {
            ClientWindowsWallpaperPath = clientPolicy?.ClientWindowsWallpaperPath?.Trim() ?? string.Empty,
            EnableCloseRunningApplicationHotKey = clientPolicy?.EnableCloseRunningApplicationHotKey ?? true,
            CafeDisplayName = string.IsNullOrWhiteSpace(clientPolicy?.CafeDisplayName) ? "Cyber Game" : clientPolicy.CafeDisplayName.Trim(),
            BannerMessage = clientPolicy?.BannerMessage?.Trim() ?? string.Empty,
            EnableFullscreenKioskMode = clientPolicy?.EnableFullscreenKioskMode ?? false,
            ThemeAccentColor = string.IsNullOrWhiteSpace(clientPolicy?.ThemeAccentColor) ? "#38BDF8" : clientPolicy.ThemeAccentColor.Trim()
        };
    }
}
