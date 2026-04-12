using System.Net.Http.Headers;
using System.Text.Json;
using GameLauncher.Client.Models;
using GameUpdater.Shared.Models;

namespace GameLauncher.Client.Services;

public sealed class CatalogReaderService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<LauncherCatalog> LoadCatalogAsync(string catalogPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            throw new InvalidOperationException("Chưa cấu hình đường dẫn danh mục trò chơi.");
        }

        var normalizedPath = catalogPath.Trim();
        var json = TryGetHttpCatalogUri(normalizedPath, out var catalogUri)
            ? await LoadCatalogJsonFromHttpAsync(catalogUri, cancellationToken)
            : await LoadCatalogJsonFromFileAsync(normalizedPath, cancellationToken);

        var catalog = JsonSerializer.Deserialize<LauncherCatalog>(json, JsonOptions)
            ?? throw new InvalidOperationException("Tệp danh mục trò chơi không đúng định dạng.");

        catalog.ClientPolicy ??= new LauncherClientPolicy();
        return catalog;
    }

    public async Task<IReadOnlyList<LauncherGameRow>> LoadCatalogRowsAsync(string catalogPath, CancellationToken cancellationToken = default)
    {
        var catalog = await LoadCatalogAsync(catalogPath, cancellationToken);
        return BuildRows(catalog);
    }

    public static IReadOnlyList<LauncherGameRow> BuildRows(LauncherCatalog catalog)
    {
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

    private static async Task<string> LoadCatalogJsonFromFileAsync(string catalogPath, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(catalogPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Không tìm thấy tệp danh mục trò chơi: {fullPath}", fullPath);
        }

        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    private static async Task<string> LoadCatalogJsonFromHttpAsync(Uri catalogUri, CancellationToken cancellationToken)
    {
        try
        {
            return await HttpClient.GetStringAsync(catalogUri, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Không tải được danh mục từ URL: {catalogUri}", exception);
        }
    }

    private static bool TryGetHttpCatalogUri(string catalogPath, out Uri uri)
    {
        uri = default!;
        if (!Uri.TryCreate(catalogPath, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (candidate.Scheme is not ("http" or "https"))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GameLauncher.Client", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        return client;
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
