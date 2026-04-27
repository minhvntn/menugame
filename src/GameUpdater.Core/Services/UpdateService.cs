using System.IO.Compression;
using GameUpdater.Core.Abstractions;
using GameUpdater.Shared.Models;

namespace GameUpdater.Core.Services;

public sealed class UpdateService
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogRepository _logRepository;
    private readonly ManifestService _manifestService;
    private readonly BackupService _backupService;

    public UpdateService(
        IGameRepository gameRepository,
        ILogRepository logRepository,
        ManifestService manifestService,
        BackupService backupService)
    {
        _gameRepository = gameRepository;
        _logRepository = logRepository;
        _manifestService = manifestService;
        _backupService = backupService;
    }

    public async Task<string?> ApplyUpdateAsync(
        UpdateRequest request,
        IProgress<UpdateProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        string? backupPath = null;

        try
        {
            progress?.Report(UpdateProgressInfo.Create(0, "Đang chuẩn bị cập nhật."));

            if (request.CreateBackup)
            {
                progress?.Report(UpdateProgressInfo.Create(5, "Đang tạo bản sao lưu."));
                backupPath = request.SourceKind switch
                {
                    UpdateSourceKind.Folder => await _backupService.CreateBackupFromFolderAsync(request.Game, request.SourcePath, cancellationToken),
                    UpdateSourceKind.Zip => await _backupService.CreateBackupFromZipAsync(request.Game, request.SourcePath, cancellationToken),
                    _ => throw new InvalidOperationException("Nguồn cập nhật không được hỗ trợ.")
                };
            }

            progress?.Report(UpdateProgressInfo.Create(10, "Đang áp dụng tệp cập nhật."));

            switch (request.SourceKind)
            {
                case UpdateSourceKind.Folder:
                    await CopyFromFolderAsync(request.Game, request.SourcePath, progress, cancellationToken);
                    break;
                case UpdateSourceKind.Zip:
                    await CopyFromZipAsync(request.Game, request.SourcePath, progress, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException("Nguồn cập nhật không được hỗ trợ.");
            }

            request.Game.Version = request.TargetVersion.Trim();
            request.Game.LastUpdatedAt = DateTime.UtcNow;
            request.Game.LastScannedAt = request.Game.LastUpdatedAt;

            progress?.Report(UpdateProgressInfo.Create(85, "Đang tạo lại manifest."));

            var manifest = await _manifestService.BuildManifestAsync(request.Game, cancellationToken);
            manifest.Version = request.Game.Version;
            manifest.GeneratedAt = DateTime.UtcNow;
            await _manifestService.SaveManifestAsync(request.Game, manifest, cancellationToken);
            await _gameRepository.UpdateAsync(request.Game, cancellationToken);

            var successMessage = backupPath is null
                ? $"Đã cập nhật {request.Game.Name} lên phiên bản {request.Game.Version}."
                : $"Đã cập nhật {request.Game.Name} lên phiên bản {request.Game.Version}. Bản sao lưu: {backupPath}";

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = request.Game.Id,
                GameName = request.Game.Name,
                Action = "Cập nhật trò chơi",
                Status = "Thành công",
                Message = successMessage,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            progress?.Report(UpdateProgressInfo.Create(100, "Cập nhật hoàn tất."));
            return backupPath;
        }
        catch (Exception exception)
        {
            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = request.Game.Id,
                GameName = request.Game.Name,
                Action = "Cập nhật trò chơi",
                Status = "Thất bại",
                Message = exception.Message,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            progress?.Report(UpdateProgressInfo.Create(100, $"Cập nhật thất bại: {exception.Message}"));
            throw;
        }
    }

    private static Task CopyFromFolderAsync(
        GameRecord game,
        string sourceFolder,
        IProgress<UpdateProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sourceFolder))
        {
            throw new DirectoryNotFoundException($"Không tìm thấy thư mục bản vá: {sourceFolder}");
        }

        var sourceFiles = Directory
            .EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            // Delta-friendly ordering: copy smaller files first.
            .OrderBy(info => info.Length)
            .ThenBy(info => info.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sourceFiles.Count == 0)
        {
            throw new InvalidOperationException("Thư mục bản vá đang trống.");
        }

        for (var index = 0; index < sourceFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceInfo = sourceFiles[index];
            var sourceFile = sourceInfo.FullName;
            var relativePath = Path.GetRelativePath(sourceFolder, sourceFile);
            var targetPath = ResolveSafePath(game.InstallPath, relativePath);
            var targetInfo = new FileInfo(targetPath);

            if (targetInfo.Exists && IsUpToDate(targetInfo, sourceInfo.Length, sourceInfo.LastWriteTimeUtc))
            {
                var skipPercent = 10 + (int)Math.Round(((index + 1d) / sourceFiles.Count) * 70d);
                progress?.Report(UpdateProgressInfo.Create(skipPercent, $"Bỏ qua {relativePath} (không thay đổi)"));
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            EnsureWritable(targetPath);
            File.Copy(sourceFile, targetPath, overwrite: true);
            File.SetLastWriteTimeUtc(targetPath, sourceInfo.LastWriteTimeUtc);

            var percent = 10 + (int)Math.Round(((index + 1d) / sourceFiles.Count) * 70d);
            progress?.Report(UpdateProgressInfo.Create(percent, $"Đã chép {relativePath}"));
        }

        return Task.CompletedTask;
    }

    private static async Task CopyFromZipAsync(
        GameRecord game,
        string zipPath,
        IProgress<UpdateProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("Không tìm thấy tệp zip nguồn.", zipPath);
        }

        using var archive = ZipFile.OpenRead(zipPath);
        var fileEntries = archive.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            // Delta-friendly ordering: extract smaller files first.
            .OrderBy(entry => entry.Length)
            .ThenBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fileEntries.Count == 0)
        {
            throw new InvalidOperationException("Tệp zip không chứa dữ liệu.");
        }

        for (var index = 0; index < fileEntries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = fileEntries[index];
            var relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var targetPath = ResolveSafePath(game.InstallPath, relativePath);
            var targetInfo = new FileInfo(targetPath);
            var sourceLastWriteUtc = TryGetZipLastWriteTimeUtc(entry);

            if (targetInfo.Exists && IsUpToDate(targetInfo, entry.Length, sourceLastWriteUtc))
            {
                var skipPercent = 10 + (int)Math.Round(((index + 1d) / fileEntries.Count) * 70d);
                progress?.Report(UpdateProgressInfo.Create(skipPercent, $"Bỏ qua {relativePath} (không thay đổi)"));
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            EnsureWritable(targetPath);

            await using var sourceStream = entry.Open();
            await using var targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(targetStream, cancellationToken);

            if (sourceLastWriteUtc.HasValue)
            {
                File.SetLastWriteTimeUtc(targetPath, sourceLastWriteUtc.Value);
            }

            var percent = 10 + (int)Math.Round(((index + 1d) / fileEntries.Count) * 70d);
            progress?.Report(UpdateProgressInfo.Create(percent, $"Đã giải nén {relativePath}"));
        }
    }

    private static DateTime? TryGetZipLastWriteTimeUtc(ZipArchiveEntry entry)
    {
        var utc = entry.LastWriteTime.UtcDateTime;
        return utc.Year < 1980 ? null : utc;
    }

    private static bool IsUpToDate(FileInfo targetInfo, long sourceLength, DateTime? sourceLastWriteUtc)
    {
        if (!targetInfo.Exists || targetInfo.Length != sourceLength)
        {
            return false;
        }

        if (!sourceLastWriteUtc.HasValue)
        {
            return true;
        }

        // Zip entries can have 2-second timestamp precision.
        var delta = (targetInfo.LastWriteTimeUtc - sourceLastWriteUtc.Value).Duration();
        return delta <= TimeSpan.FromSeconds(2);
    }

    private static void ValidateRequest(UpdateRequest request)
    {
        if (request.Game is null || request.Game.Id <= 0)
        {
            throw new InvalidOperationException("Vui lòng chọn trò chơi hợp lệ trước khi cập nhật.");
        }

        if (string.IsNullOrWhiteSpace(request.SourcePath))
        {
            throw new InvalidOperationException("Vui lòng chọn đường dẫn nguồn cập nhật.");
        }

        if (string.IsNullOrWhiteSpace(request.TargetVersion))
        {
            throw new InvalidOperationException("Vui lòng nhập phiên bản đích.");
        }

        if (!Directory.Exists(request.Game.InstallPath))
        {
            throw new DirectoryNotFoundException($"Không tìm thấy đường dẫn cài đặt: {request.Game.InstallPath}");
        }
    }

    private static void EnsureWritable(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }

    private static string ResolveSafePath(string root, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var candidatePath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));

        if (!candidatePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Phát hiện đường dẫn không an toàn: {relativePath}");
        }

        return candidatePath;
    }
}
