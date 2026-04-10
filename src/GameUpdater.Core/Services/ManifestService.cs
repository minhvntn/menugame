using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameUpdater.Shared.Models;

namespace GameUpdater.Core.Services;

public sealed class ManifestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppEnvironment _appEnvironment;

    public ManifestService(AppEnvironment appEnvironment)
    {
        _appEnvironment = appEnvironment;
    }

    public Task<GameManifest> BuildManifestAsync(GameRecord game, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => BuildManifest(game, cancellationToken), cancellationToken);
    }

    public async Task SaveManifestAsync(GameRecord game, GameManifest manifest, CancellationToken cancellationToken = default)
    {
        var path = GetManifestPath(game);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }

    public async Task<GameManifest?> LoadManifestAsync(GameRecord game, CancellationToken cancellationToken = default)
    {
        var path = GetManifestPath(game);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<GameManifest>(json, JsonOptions);
    }

    public async Task<string> GetManifestPreviewAsync(GameRecord game, CancellationToken cancellationToken = default)
    {
        var manifest = await LoadManifestAsync(game, cancellationToken);
        if (manifest is null)
        {
            return "Chưa tạo manifest.";
        }

        return JsonSerializer.Serialize(manifest, JsonOptions);
    }

    public void DeleteManifest(GameRecord game)
    {
        var path = GetManifestPath(game);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private GameManifest BuildManifest(GameRecord game, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(game.InstallPath))
        {
            throw new DirectoryNotFoundException($"Không tìm thấy thư mục trò chơi: {game.InstallPath}");
        }

        var manifest = new GameManifest
        {
            GameId = game.Id,
            GameName = game.Name,
            Version = game.Version,
            GeneratedAt = DateTime.UtcNow
        };

        var files = Directory
            .EnumerateFiles(game.InstallPath, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(filePath);

            manifest.Files.Add(new ManifestFileEntry
            {
                RelativePath = Path.GetRelativePath(game.InstallPath, filePath),
                Size = fileInfo.Length,
                Sha256 = ComputeSha256(filePath),
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
            });
        }

        return manifest;
    }

    private string GetManifestPath(GameRecord game)
    {
        return Path.Combine(
            _appEnvironment.ManifestDirectory,
            $"{game.Id:0000}-{ToSafeFileName(game.Name)}.manifest.json");
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static string ToSafeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        return builder.ToString().Trim().ToLowerInvariant();
    }
}
