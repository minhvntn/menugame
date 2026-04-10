using System.Text.Json;
using GameLauncher.Client.Models;
using GameUpdater.Shared.Models;

namespace GameLauncher.Client.Services;

public sealed class CatalogReaderService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<LauncherGameRow>> LoadCatalogRowsAsync(string catalogPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            throw new InvalidOperationException("Chưa cấu hình đường dẫn danh mục trò chơi.");
        }

        var fullPath = Path.GetFullPath(catalogPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Không tìm thấy tệp danh mục trò chơi.", fullPath);
        }

        var json = await File.ReadAllTextAsync(fullPath, cancellationToken);
        var catalog = JsonSerializer.Deserialize<LauncherCatalog>(json, JsonOptions)
            ?? throw new InvalidOperationException("Tệp danh mục trò chơi không đúng định dạng.");

        var rows = new List<LauncherGameRow>(catalog.Games.Count);
        foreach (var game in catalog.Games.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var resolvedExecutable = ResolveExecutablePath(game);
            var status = File.Exists(resolvedExecutable) ? "Sẵn sàng" : "Thiếu tệp chạy";

            rows.Add(new LauncherGameRow
            {
                Source = game,
                ResolvedExecutablePath = resolvedExecutable,
                Status = status
            });
        }

        return rows;
    }

    private static string ResolveExecutablePath(LauncherGameEntry game)
    {
        if (string.IsNullOrWhiteSpace(game.LaunchRelativePath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(game.LaunchRelativePath))
        {
            return Path.GetFullPath(game.LaunchRelativePath);
        }

        if (string.IsNullOrWhiteSpace(game.InstallPath))
        {
            return string.Empty;
        }

        return Path.GetFullPath(Path.Combine(game.InstallPath, game.LaunchRelativePath));
    }
}
