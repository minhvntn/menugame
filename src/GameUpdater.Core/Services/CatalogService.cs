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

    public async Task<LauncherCatalog> BuildCatalogAsync(CancellationToken cancellationToken = default)
    {
        var games = await _gameRepository.GetAllAsync(cancellationToken);

        var catalog = new LauncherCatalog
        {
            GeneratedAtUtc = DateTime.UtcNow
        };

        foreach (var game in games.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            catalog.Games.Add(new LauncherGameEntry
            {
                Id = game.Id,
                Name = game.Name,
                Category = game.Category,
                Version = game.Version,
                InstallPath = game.InstallPath,
                LaunchRelativePath = game.LaunchRelativePath ?? string.Empty,
                LaunchArguments = game.LaunchArguments ?? string.Empty
            });
        }

        return catalog;
    }

    public async Task ExportToFileAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Vui lòng chọn đường dẫn xuất danh mục.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var catalog = await BuildCatalogAsync(cancellationToken);
        var json = JsonSerializer.Serialize(catalog, JsonOptions);
        await File.WriteAllTextAsync(fullPath, json, Encoding.UTF8, cancellationToken);
    }
}
