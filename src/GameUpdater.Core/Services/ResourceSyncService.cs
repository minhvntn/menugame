using System.Diagnostics;
using GameUpdater.Shared.Models;

namespace GameUpdater.Core.Services;

public sealed class ResourceSyncService
{
    private const int BufferSize = 1024 * 1024;

    public async Task<ResourceSyncResult> SyncGameAsync(
        GameRecord game,
        string sourceRoot,
        string targetRoot,
        IProgress<UpdateProgressInfo>? progress = null,
        long? maxBytesPerSecond = null,
        Func<CancellationToken, ValueTask>? waitIfPausedAsync = null,
        ResourceSyncMode syncMode = ResourceSyncMode.Incremental,
        CancellationToken cancellationToken = default)
    {
        if (game is null)
        {
            throw new InvalidOperationException("Trò chơi không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            throw new InvalidOperationException("Vui lòng cấu hình thư mục nguồn tài nguyên.");
        }

        if (string.IsNullOrWhiteSpace(targetRoot))
        {
            throw new InvalidOperationException("Vui lòng cấu hình thư mục đích tài nguyên.");
        }

        if (maxBytesPerSecond.HasValue && maxBytesPerSecond.Value < 0)
        {
            throw new InvalidOperationException("Giá trị giới hạn băng thông không hợp lệ.");
        }

        if (Uri.TryCreate(sourceRoot, UriKind.Absolute, out var sourceUri) &&
            (sourceUri.Scheme is "http" or "https"))
        {
            throw new InvalidOperationException("Nguồn HTTP/HTTPS chưa hỗ trợ đồng bộ trực tiếp. Vui lòng dùng đường dẫn thư mục local/UNC.");
        }

        var normalizedSourceRoot = Path.GetFullPath(sourceRoot);
        var normalizedTargetRoot = Path.GetFullPath(targetRoot);
        var normalizedInstallPath = Path.GetFullPath(game.InstallPath);

        if (!Directory.Exists(normalizedSourceRoot))
        {
            throw new DirectoryNotFoundException($"Không tìm thấy thư mục nguồn: {normalizedSourceRoot}");
        }

        var relativeGamePath = ResolveRelativeGamePath(game, normalizedInstallPath, normalizedTargetRoot);
        var sourceGamePath = Path.GetFullPath(Path.Combine(normalizedSourceRoot, relativeGamePath));

        if (!Directory.Exists(sourceGamePath))
        {
            throw new DirectoryNotFoundException($"Không tìm thấy thư mục nguồn của {game.Name}:{Environment.NewLine}{sourceGamePath}");
        }

        Directory.CreateDirectory(normalizedInstallPath);

        var sourceFiles = Directory
            .EnumerateFiles(sourceGamePath, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderBy(info => info.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sourceFiles.Count == 0)
        {
            throw new InvalidOperationException($"Thư mục nguồn của {game.Name} đang trống.");
        }

        var totalBytes = sourceFiles.Sum(info => info.Length);
        var processedBytes = 0L;
        var copied = 0;
        var skipped = 0;
        var transferStopwatch = Stopwatch.StartNew();
        var lastProgressReport = TimeSpan.Zero;
        var limiter = maxBytesPerSecond.HasValue && maxBytesPerSecond.Value > 0
            ? new TransferRateLimiter(maxBytesPerSecond.Value)
            : null;

        for (var index = 0; index < sourceFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WaitIfPausedAsync(waitIfPausedAsync, cancellationToken).ConfigureAwait(false);

            var sourceInfo = sourceFiles[index];
            var sourceFile = sourceInfo.FullName;
            var relativePath = Path.GetRelativePath(sourceGamePath, sourceFile);
            var targetFile = ResolveSafePath(normalizedInstallPath, relativePath);
            var targetInfo = new FileInfo(targetFile);

            if (targetInfo.Exists)
            {
                var isUpToDate =
                    targetInfo.Length == sourceInfo.Length &&
                    targetInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc;

                if (syncMode == ResourceSyncMode.MissingOnly || isUpToDate)
                {
                    skipped++;
                    processedBytes += sourceInfo.Length;
                    TryReportProgress(relativePath, force: false);
                    continue;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            EnsureWritable(targetFile);

            await CopyFileAsync(
                    sourceFile,
                    targetFile,
                    limiter,
                    bytesCopied =>
                    {
                        processedBytes += bytesCopied;
                        TryReportProgress(relativePath, force: false);
                    },
                    waitIfPausedAsync,
                    cancellationToken)
                .ConfigureAwait(false);

            File.SetLastWriteTimeUtc(targetFile, sourceInfo.LastWriteTimeUtc);
            copied++;

            TryReportProgress(relativePath, force: true);
        }

        progress?.Report(UpdateProgressInfo.Create(100, "Hoàn tất đồng bộ tài nguyên."));

        return new ResourceSyncResult
        {
            SourcePath = sourceGamePath,
            TargetPath = normalizedInstallPath,
            TotalFiles = sourceFiles.Count,
            CopiedFiles = copied,
            SkippedFiles = skipped
        };

        void TryReportProgress(string relativePath, bool force)
        {
            var elapsed = transferStopwatch.Elapsed;
            if (!force && elapsed - lastProgressReport < TimeSpan.FromMilliseconds(250))
            {
                return;
            }

            lastProgressReport = elapsed;
            var percent = totalBytes <= 0
                ? 100
                : (int)Math.Round((processedBytes * 100d) / totalBytes);

            var copiedMb = processedBytes / 1024d / 1024d;
            var totalMb = totalBytes / 1024d / 1024d;
            var speedMbPerSecond = elapsed.TotalSeconds <= 0
                ? 0
                : copiedMb / elapsed.TotalSeconds;

            var message = $"Đang đồng bộ {relativePath} ({copiedMb:N1}/{totalMb:N1} MB, {speedMbPerSecond:N1} MB/s)";
            progress?.Report(UpdateProgressInfo.Create(percent, message));
        }
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string targetPath,
        TransferRateLimiter? limiter,
        Action<int> onBytesCopied,
        Func<CancellationToken, ValueTask>? waitIfPausedAsync,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];

        await using var sourceStream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using var targetStream = new FileStream(
            targetPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        while (true)
        {
            await WaitIfPausedAsync(waitIfPausedAsync, cancellationToken).ConfigureAwait(false);

            var bytesRead = await sourceStream
                .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead <= 0)
            {
                break;
            }

            await targetStream
                .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                .ConfigureAwait(false);

            onBytesCopied(bytesRead);

            if (limiter is not null)
            {
                await limiter.DelayIfNeededAsync(bytesRead, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static ValueTask WaitIfPausedAsync(
        Func<CancellationToken, ValueTask>? waitIfPausedAsync,
        CancellationToken cancellationToken)
    {
        return waitIfPausedAsync is null
            ? ValueTask.CompletedTask
            : waitIfPausedAsync(cancellationToken);
    }

    private static string ResolveRelativeGamePath(GameRecord game, string installPath, string targetRoot)
    {
        var normalizedRoot = EnsureTrailingSeparator(targetRoot);
        var normalizedInstall = EnsureTrailingSeparator(installPath);

        if (normalizedInstall.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(targetRoot, installPath);
        }

        var folderName = Path.GetFileName(installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.IsNullOrWhiteSpace(folderName))
        {
            return folderName;
        }

        return ToSafeFolderName(game.Name);
    }

    private static string EnsureTrailingSeparator(string value)
    {
        return value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }

    private static string ResolveSafePath(string root, string relativePath)
    {
        var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        var candidatePath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));

        if (!candidatePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Phát hiện đường dẫn không an toàn: {relativePath}");
        }

        return candidatePath;
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

    private static string ToSafeFolderName(string value)
    {
        return string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    }

    private sealed class TransferRateLimiter
    {
        private readonly long _bytesPerSecond;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private long _totalBytes;

        public TransferRateLimiter(long bytesPerSecond)
        {
            _bytesPerSecond = bytesPerSecond;
        }

        public async ValueTask DelayIfNeededAsync(int bytesTransferred, CancellationToken cancellationToken)
        {
            if (_bytesPerSecond <= 0 || bytesTransferred <= 0)
            {
                return;
            }

            _totalBytes += bytesTransferred;

            var expectedSeconds = _totalBytes / (double)_bytesPerSecond;
            var actualSeconds = _stopwatch.Elapsed.TotalSeconds;
            var delayMilliseconds = (int)Math.Ceiling((expectedSeconds - actualSeconds) * 1000d);
            if (delayMilliseconds > 0)
            {
                await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

public enum ResourceSyncMode
{
    Incremental = 0,
    MissingOnly = 1
}

public sealed class ResourceSyncResult
{
    public string SourcePath { get; init; } = string.Empty;

    public string TargetPath { get; init; } = string.Empty;

    public int TotalFiles { get; init; }

    public int CopiedFiles { get; init; }

    public int SkippedFiles { get; init; }
}
