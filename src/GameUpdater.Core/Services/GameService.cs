using GameUpdater.Core.Abstractions;
using GameUpdater.Shared.Models;

namespace GameUpdater.Core.Services;

public sealed class GameService
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogRepository _logRepository;
    private readonly ManifestService _manifestService;

    public GameService(IGameRepository gameRepository, ILogRepository logRepository, ManifestService manifestService)
    {
        _gameRepository = gameRepository;
        _logRepository = logRepository;
        _manifestService = manifestService;
    }

    public Task<IReadOnlyList<GameRecord>> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        return _gameRepository.GetAllAsync(cancellationToken);
    }

    public async Task<int> SaveGameAsync(GameRecord game, CancellationToken cancellationToken = default)
    {
        ValidateGame(game);
        var action = game.Id == 0 ? "Thêm trò chơi" : "Cập nhật trò chơi";

        if (game.Id == 0)
        {
            game.Id = await _gameRepository.InsertAsync(game, cancellationToken);
        }
        else
        {
            await _gameRepository.UpdateAsync(game, cancellationToken);
        }

        await _logRepository.AddAsync(new UpdateLogEntry
        {
            GameId = game.Id,
            GameName = game.Name,
            Action = action,
            Status = "Thành công",
            Message = $"{game.Name} đã được lưu với đường dẫn {game.InstallPath}",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return game.Id;
    }

    public async Task DeleteGameAsync(GameRecord game, CancellationToken cancellationToken = default)
    {
        await _gameRepository.DeleteAsync(game.Id, cancellationToken);
        _manifestService.DeleteManifest(game);

        await _logRepository.AddAsync(new UpdateLogEntry
        {
            GameId = game.Id,
            GameName = game.Name,
            Action = "Xóa trò chơi",
            Status = "Thành công",
            Message = $"{game.Name} đã bị xóa khỏi danh sách quản lý.",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task<GameManifest> ScanGameAsync(GameRecord game, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateGame(game);
            var manifest = await _manifestService.BuildManifestAsync(game, cancellationToken);
            await _manifestService.SaveManifestAsync(game, manifest, cancellationToken);

            game.LastScannedAt = DateTime.UtcNow;
            await _gameRepository.UpdateAsync(game, cancellationToken);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id,
                GameName = game.Name,
                Action = "Quét manifest",
                Status = "Thành công",
                Message = $"Đã quét {manifest.Files.Count} tệp.",
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            return manifest;
        }
        catch (Exception exception)
        {
            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id,
                GameName = game.Name,
                Action = "Quét manifest",
                Status = "Thất bại",
                Message = exception.Message,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            throw;
        }
    }

    public Task<string> GetManifestPreviewAsync(GameRecord game, CancellationToken cancellationToken = default)
    {
        return _manifestService.GetManifestPreviewAsync(game, cancellationToken);
    }

    private static void ValidateGame(GameRecord game)
    {
        if (string.IsNullOrWhiteSpace(game.Name))
        {
            throw new InvalidOperationException("Vui lòng nhập tên trò chơi.");
        }

        if (string.IsNullOrWhiteSpace(game.InstallPath))
        {
            throw new InvalidOperationException("Vui lòng nhập đường dẫn cài đặt.");
        }

        if (!Path.IsPathRooted(game.InstallPath))
        {
            throw new InvalidOperationException("Đường dẫn cài đặt phải là đường dẫn tuyệt đối.");
        }

        if (!Directory.Exists(game.InstallPath))
        {
            throw new DirectoryNotFoundException($"Không tìm thấy đường dẫn cài đặt: {game.InstallPath}");
        }

        if (string.IsNullOrWhiteSpace(game.Version))
        {
            game.Version = "1.0.0";
        }

        if (string.IsNullOrWhiteSpace(game.Category))
        {
            game.Category = "Chung";
        }
    }
}
