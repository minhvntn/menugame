using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using GameUpdater.Shared.Models;

namespace GameUpdater.Core.Services;

public sealed class ResourceSyncService
{
    private const int BufferSize = 1024 * 1024;
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly Regex AnchorHrefRegex = new(
        "<a\\s+[^>]*href\\s*=\\s*['\\\"](?<href>[^'\\\"]+)['\\\"][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool IsHttpSourceRoot(string sourceRoot)
    {
        return TryGetHttpRootUri(sourceRoot, out _);
    }

    public async Task<IReadOnlyList<string>> GetHttpTopLevelDirectoryKeysAsync(
        string sourceRoot,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetHttpRootUri(sourceRoot, out var sourceRootUri))
        {
            return Array.Empty<string>();
        }

        var items = await GetDirectoryItemsAsync(sourceRootUri, cancellationToken).ConfigureAwait(false);
        return items
            .Where(item => item.IsDirectory)
            .Select(item => item.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<bool> IsSourceMirroredToTargetAsync(
        string sourcePath,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) ||
            string.IsNullOrWhiteSpace(targetPath))
        {
            return false;
        }

        string normalizedTargetPath;
        try
        {
            normalizedTargetPath = Path.GetFullPath(targetPath);
        }
        catch
        {
            return false;
        }

        if (!Directory.Exists(normalizedTargetPath))
        {
            return false;
        }

        if (TryGetHttpRootUri(sourcePath, out var sourceUri))
        {
            var httpSourceFiles = await EnumerateHttpFilesAsync(sourceUri, cancellationToken).ConfigureAwait(false);
            if (httpSourceFiles.Count == 0)
            {
                return false;
            }

            foreach (var sourceFile in httpSourceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string targetFile;
                try
                {
                    targetFile = ResolveSafePath(normalizedTargetPath, sourceFile.RelativePath);
                }
                catch
                {
                    return false;
                }

                if (!File.Exists(targetFile))
                {
                    return false;
                }
            }

            return true;
        }

        string normalizedSourcePath;
        try
        {
            normalizedSourcePath = Path.GetFullPath(sourcePath);
        }
        catch
        {
            return false;
        }

        if (!Directory.Exists(normalizedSourcePath))
        {
            return false;
        }

        var directorySourceFiles = Directory.EnumerateFiles(normalizedSourcePath, "*", SearchOption.AllDirectories).ToList();
        if (directorySourceFiles.Count == 0)
        {
            return false;
        }

        foreach (var sourceFile in directorySourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(normalizedSourcePath, sourceFile);
            string targetFile;
            try
            {
                targetFile = ResolveSafePath(normalizedTargetPath, relativePath);
            }
            catch
            {
                return false;
            }

            if (!File.Exists(targetFile))
            {
                return false;
            }

            long sourceLength;
            long targetLength;
            try
            {
                sourceLength = new FileInfo(sourceFile).Length;
                targetLength = new FileInfo(targetFile).Length;
            }
            catch
            {
                return false;
            }

            if (sourceLength != targetLength)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<ResourceSyncResult> SyncGameAsync(
        GameRecord game,
        string sourceRoot,
        string targetRoot,
        IProgress<UpdateProgressInfo>? progress = null,
        long? maxBytesPerSecond = null,
        Func<CancellationToken, ValueTask>? waitIfPausedAsync = null,
        ResourceSyncMode syncMode = ResourceSyncMode.Incremental,
        CancellationToken cancellationToken = default,
        Func<long?>? getMaxBytesPerSecond = null)
    {
        if (game is null)
        {
            throw new InvalidOperationException("Trò chơi không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            throw new InvalidOperationException("Vui lòng cấu hình nguồn tài nguyên.");
        }

        if (string.IsNullOrWhiteSpace(targetRoot))
        {
            throw new InvalidOperationException("Vui lòng cấu hình thư mục đích tài nguyên.");
        }

        if (maxBytesPerSecond.HasValue && maxBytesPerSecond.Value < 0)
        {
            throw new InvalidOperationException("Giá trị giới hạn băng thông không hợp lệ.");
        }

        if (TryGetHttpRootUri(sourceRoot, out var sourceRootUri))
        {
            return await SyncGameFromHttpAsync(
                    game,
                    sourceRootUri,
                    targetRoot,
                    progress,
                    maxBytesPerSecond,
                    waitIfPausedAsync,
                    syncMode,
                    cancellationToken,
                    getMaxBytesPerSecond)
                .ConfigureAwait(false);
        }

        return await SyncGameFromDirectoryAsync(
                game,
                sourceRoot,
                targetRoot,
                progress,
                maxBytesPerSecond,
                waitIfPausedAsync,
                syncMode,
                cancellationToken,
                getMaxBytesPerSecond)
            .ConfigureAwait(false);
    }

    private static async Task<ResourceSyncResult> SyncGameFromDirectoryAsync(
        GameRecord game,
        string sourceRoot,
        string targetRoot,
        IProgress<UpdateProgressInfo>? progress,
        long? maxBytesPerSecond,
        Func<CancellationToken, ValueTask>? waitIfPausedAsync,
        ResourceSyncMode syncMode,
        CancellationToken cancellationToken,
        Func<long?>? getMaxBytesPerSecond)
    {
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
            // Delta-friendly ordering: copy smaller files first so games become runnable sooner.
            .OrderBy(info => info.Length)
            .ThenBy(info => info.FullName, StringComparer.OrdinalIgnoreCase)
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
        var limiter = maxBytesPerSecond.GetValueOrDefault() > 0 || getMaxBytesPerSecond is not null
            ? new TransferRateLimiter()
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
                    maxBytesPerSecond,
                    getMaxBytesPerSecond,
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

        var finalElapsed = transferStopwatch.Elapsed;
        var finalCopiedMb = processedBytes / 1024d / 1024d;
        var finalSpeedMbPerSecond = finalElapsed.TotalSeconds <= 0
            ? 0d
            : finalCopiedMb / finalElapsed.TotalSeconds;
        progress?.Report(UpdateProgressInfo.Create(
            100,
            "Hoàn tất đồng bộ tài nguyên.",
            totalBytes,
            processedBytes,
            finalSpeedMbPerSecond));

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
            progress?.Report(UpdateProgressInfo.Create(
                percent,
                message,
                totalBytes,
                processedBytes,
                speedMbPerSecond));
        }
    }

    private async Task<ResourceSyncResult> SyncGameFromHttpAsync(
        GameRecord game,
        Uri sourceRootUri,
        string targetRoot,
        IProgress<UpdateProgressInfo>? progress,
        long? maxBytesPerSecond,
        Func<CancellationToken, ValueTask>? waitIfPausedAsync,
        ResourceSyncMode syncMode,
        CancellationToken cancellationToken,
        Func<long?>? getMaxBytesPerSecond)
    {
        var normalizedTargetRoot = Path.GetFullPath(targetRoot);
        var normalizedInstallPath = Path.GetFullPath(game.InstallPath);
        var relativeGamePath = ResolveRelativeGamePath(game, normalizedInstallPath, normalizedTargetRoot);
        var sourceGameUri = BuildSourceUri(sourceRootUri, relativeGamePath, asDirectory: true);

        Directory.CreateDirectory(normalizedInstallPath);

        var sourceFiles = await EnumerateHttpFilesAsync(sourceGameUri, cancellationToken).ConfigureAwait(false);
        if (sourceFiles.Count == 0)
        {
            throw new InvalidOperationException($"Không tìm thấy tệp trong nguồn HTTP của {game.Name}:{Environment.NewLine}{sourceGameUri}");
        }

        var copied = 0;
        var skipped = 0;
        var downloadedBytes = 0L;
        var transferStopwatch = Stopwatch.StartNew();
        var lastProgressReport = TimeSpan.Zero;
        var limiter = maxBytesPerSecond.GetValueOrDefault() > 0 || getMaxBytesPerSecond is not null
            ? new TransferRateLimiter()
            : null;
        var totalFiles = sourceFiles.Count;
        var knownTotalBytes = 0L;
        var knownSizeFileCount = 0;

        for (var index = 0; index < sourceFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WaitIfPausedAsync(waitIfPausedAsync, cancellationToken).ConfigureAwait(false);

            var sourceFile = sourceFiles[index];
            var targetFile = ResolveSafePath(normalizedInstallPath, sourceFile.RelativePath);
            var targetInfo = new FileInfo(targetFile);

            if (targetInfo.Exists && syncMode == ResourceSyncMode.MissingOnly)
            {
                skipped++;
                RegisterKnownFileSize(targetInfo.Length);
                TryReportProgress(sourceFile.RelativePath, index + 1, currentFileProgress: 1d, force: true);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            EnsureWritable(targetFile);

            var currentFileDownloadedBytes = 0L;
            long? currentFileTotalBytes = null;
            DateTime? ifModifiedSinceUtc = targetInfo.Exists && syncMode == ResourceSyncMode.Incremental
                ? targetInfo.LastWriteTimeUtc
                : null;

            var downloadResult = await DownloadHttpFileAsync(
                    sourceFile.Uri,
                    targetFile,
                    ifModifiedSinceUtc,
                    limiter,
                    maxBytesPerSecond,
                    getMaxBytesPerSecond,
                    (bytesCopied, totalBytes) =>
                    {
                        currentFileDownloadedBytes += bytesCopied;
                        downloadedBytes += bytesCopied;
                        if (totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            currentFileTotalBytes = totalBytes.Value;
                        }

                        var fileTotal = currentFileTotalBytes.GetValueOrDefault();
                        var fileProgress = fileTotal > 0
                            ? Math.Clamp(currentFileDownloadedBytes / (double)fileTotal, 0d, 1d)
                            : 0d;

                        TryReportProgress(sourceFile.RelativePath, index, fileProgress, force: false, currentFileTotalBytes);
                    },
                    waitIfPausedAsync,
                    cancellationToken)
                .ConfigureAwait(false);

            if (downloadResult.Outcome == HttpDownloadOutcome.NotModified)
            {
                skipped++;
                RegisterKnownFileSize(targetInfo.Exists ? targetInfo.Length : downloadResult.TotalBytes);
                TryReportProgress(sourceFile.RelativePath, index + 1, currentFileProgress: 1d, force: true);
                continue;
            }

            if (downloadResult.LastModifiedUtc.HasValue)
            {
                File.SetLastWriteTimeUtc(targetFile, downloadResult.LastModifiedUtc.Value);
            }

            var downloadedFileSize = downloadResult.TotalBytes;
            if (!downloadedFileSize.HasValue || downloadedFileSize.Value <= 0)
            {
                downloadedFileSize = new FileInfo(targetFile).Length;
            }

            RegisterKnownFileSize(downloadedFileSize);
            copied++;
            TryReportProgress(sourceFile.RelativePath, index + 1, currentFileProgress: 1d, force: true);
        }

        var finalElapsed = transferStopwatch.Elapsed;
        var finalDownloadedMb = downloadedBytes / 1024d / 1024d;
        var finalSpeedMbPerSecond = finalElapsed.TotalSeconds <= 0
            ? 0d
            : finalDownloadedMb / finalElapsed.TotalSeconds;
        progress?.Report(UpdateProgressInfo.Create(
            100,
            "Hoàn tất đồng bộ tài nguyên.",
            totalBytes: EstimateTotalBytes(),
            processedBytes: downloadedBytes,
            speedMbps: finalSpeedMbPerSecond));

        return new ResourceSyncResult
        {
            SourcePath = sourceGameUri.AbsoluteUri,
            TargetPath = normalizedInstallPath,
            TotalFiles = totalFiles,
            CopiedFiles = copied,
            SkippedFiles = skipped
        };

        void TryReportProgress(
            string relativePath,
            int completedFiles,
            double currentFileProgress,
            bool force,
            long? currentFileTotalBytesHint = null)
        {
            var elapsed = transferStopwatch.Elapsed;
            if (!force && elapsed - lastProgressReport < TimeSpan.FromMilliseconds(250))
            {
                return;
            }

            lastProgressReport = elapsed;

            var normalizedCompleted = Math.Clamp(completedFiles, 0, totalFiles);
            var fileShare = totalFiles <= 0 ? 1d : 1d / totalFiles;
            var progressValue = normalizedCompleted * fileShare;
            if (normalizedCompleted < totalFiles)
            {
                progressValue += Math.Clamp(currentFileProgress, 0d, 1d) * fileShare;
            }

            var percent = (int)Math.Round(Math.Clamp(progressValue * 100d, 0d, 100d));
            var downloadedMb = downloadedBytes / 1024d / 1024d;
            var speedMbPerSecond = elapsed.TotalSeconds <= 0
                ? 0
                : downloadedMb / elapsed.TotalSeconds;
            var currentFileDisplayIndex = Math.Clamp(normalizedCompleted + (normalizedCompleted < totalFiles ? 1 : 0), 1, totalFiles);
            var message = $"Đang đồng bộ {relativePath} ({currentFileDisplayIndex}/{totalFiles}, {downloadedMb:N1} MB, {speedMbPerSecond:N1} MB/s)";
            progress?.Report(UpdateProgressInfo.Create(
                percent,
                message,
                totalBytes: EstimateTotalBytes(currentFileTotalBytesHint),
                processedBytes: downloadedBytes,
                speedMbps: speedMbPerSecond));
        }

        void RegisterKnownFileSize(long? fileSizeBytes)
        {
            if (!fileSizeBytes.HasValue || fileSizeBytes.Value <= 0)
            {
                return;
            }

            knownTotalBytes += fileSizeBytes.Value;
            knownSizeFileCount++;
        }

        long? EstimateTotalBytes(long? currentFileSize = null)
        {
            var effectiveKnownBytes = knownTotalBytes;
            var effectiveKnownCount = knownSizeFileCount;

            if (currentFileSize.HasValue && currentFileSize.Value > 0)
            {
                effectiveKnownBytes += currentFileSize.Value;
                effectiveKnownCount++;
            }

            if (effectiveKnownCount <= 0 || effectiveKnownBytes <= 0 || totalFiles <= 0)
            {
                return null;
            }

            var averageBytesPerFile = effectiveKnownBytes / (double)effectiveKnownCount;
            var estimatedTotalBytes = (long)Math.Round(averageBytesPerFile * totalFiles);
            return Math.Max(effectiveKnownBytes, estimatedTotalBytes);
        }
    }

    private static async Task<HttpDownloadResult> DownloadHttpFileAsync(
        Uri fileUri,
        string targetPath,
        DateTime? ifModifiedSinceUtc,
        TransferRateLimiter? limiter,
        long? maxBytesPerSecond,
        Func<long?>? getMaxBytesPerSecond,
        Action<int, long?> onBytesCopied,
        Func<CancellationToken, ValueTask>? waitIfPausedAsync,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, fileUri);
        if (ifModifiedSinceUtc.HasValue)
        {
            request.Headers.IfModifiedSince = ifModifiedSinceUtc.Value;
        }

        using var response = await HttpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new HttpDownloadResult(HttpDownloadOutcome.NotModified, 0, ifModifiedSinceUtc, null);
        }

        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        var lastModifiedUtc = response.Content.Headers.LastModified?.UtcDateTime;
        var buffer = new byte[BufferSize];

        await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var targetStream = new FileStream(
            targetPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        long downloadedBytes = 0;
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

            downloadedBytes += bytesRead;
            onBytesCopied(bytesRead, contentLength);

            if (limiter is not null)
            {
                var currentLimitBytesPerSecond = ResolveCurrentLimitBytesPerSecond(maxBytesPerSecond, getMaxBytesPerSecond);
                await limiter.DelayIfNeededAsync(bytesRead, currentLimitBytesPerSecond, cancellationToken).ConfigureAwait(false);
            }
        }

        return new HttpDownloadResult(HttpDownloadOutcome.Downloaded, downloadedBytes, lastModifiedUtc, contentLength);
    }

    private async Task<IReadOnlyList<HttpSourceFile>> EnumerateHttpFilesAsync(Uri sourceGameUri, CancellationToken cancellationToken)
    {
        var root = EnsureTrailingSlash(sourceGameUri);
        var pending = new Queue<Uri>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<HttpSourceFile>();

        pending.Enqueue(root);
        visited.Add(root.AbsoluteUri);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDirectory = pending.Dequeue();
            var entries = await GetDirectoryItemsAsync(currentDirectory, cancellationToken).ConfigureAwait(false);

            foreach (var entry in entries)
            {
                if (!IsSameOrigin(root, entry.Uri))
                {
                    continue;
                }

                if (entry.IsDirectory)
                {
                    var normalizedDirectory = EnsureTrailingSlash(entry.Uri);
                    if (!IsSubPath(root, normalizedDirectory))
                    {
                        continue;
                    }

                    if (visited.Add(normalizedDirectory.AbsoluteUri))
                    {
                        pending.Enqueue(normalizedDirectory);
                    }

                    continue;
                }

                if (!TryBuildRelativePath(root, entry.Uri, out var relativePath))
                {
                    continue;
                }

                files.Add(new HttpSourceFile(entry.Uri, relativePath));
            }
        }

        return files
            .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryBuildRelativePath(Uri rootUri, Uri fileUri, out string relativePath)
    {
        relativePath = string.Empty;

        var normalizedRootPath = Uri.UnescapeDataString(EnsureTrailingSlash(rootUri).AbsolutePath);
        var decodedFilePath = Uri.UnescapeDataString(fileUri.AbsolutePath);

        if (!decodedFilePath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = decodedFilePath.Substring(normalizedRootPath.Length).Trim('/');
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return false;
        }

        relativePath = remainder.Replace('/', Path.DirectorySeparatorChar);
        return true;
    }

    private static bool IsSubPath(Uri rootUri, Uri candidateUri)
    {
        var rootPath = Uri.UnescapeDataString(EnsureTrailingSlash(rootUri).AbsolutePath);
        var candidatePath = Uri.UnescapeDataString(EnsureTrailingSlash(candidateUri).AbsolutePath);
        return candidatePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameOrigin(Uri first, Uri second)
    {
        return string.Equals(first.Scheme, second.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(first.Host, second.Host, StringComparison.OrdinalIgnoreCase) &&
               first.Port == second.Port;
    }

    private async Task<IReadOnlyList<HttpDirectoryItem>> GetDirectoryItemsAsync(Uri directoryUri, CancellationToken cancellationToken)
    {
        using var response = await HttpClient
            .GetAsync(EnsureTrailingSlash(directoryUri), HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<HttpDirectoryItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AnchorHrefRegex.Matches(html))
        {
            var hrefRaw = WebUtility.HtmlDecode(match.Groups["href"].Value).Trim();
            if (string.IsNullOrWhiteSpace(hrefRaw))
            {
                continue;
            }

            if (hrefRaw.StartsWith('#') ||
                hrefRaw.StartsWith('?') ||
                hrefRaw.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                hrefRaw.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (hrefRaw == "/" ||
                hrefRaw == "." ||
                hrefRaw == "./" ||
                hrefRaw == ".." ||
                hrefRaw == "../")
            {
                continue;
            }

            if (!Uri.TryCreate(directoryUri, hrefRaw, out var itemUri))
            {
                continue;
            }

            var isDirectory = hrefRaw.EndsWith("/", StringComparison.Ordinal) ||
                              itemUri.AbsolutePath.EndsWith("/", StringComparison.Ordinal);
            var normalizedUri = isDirectory ? EnsureTrailingSlash(itemUri) : itemUri;
            if (!seen.Add(normalizedUri.AbsoluteUri))
            {
                continue;
            }

            var name = ExtractNameFromHref(hrefRaw, normalizedUri);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            items.Add(new HttpDirectoryItem(normalizedUri, name, isDirectory));
        }

        return items;
    }

    private static string ExtractNameFromHref(string hrefRaw, Uri resolvedUri)
    {
        var withoutQuery = hrefRaw.Split('?', '#')[0].Trim();
        withoutQuery = withoutQuery.Trim('/');
        if (!string.IsNullOrWhiteSpace(withoutQuery))
        {
            var lastSegment = withoutQuery.Split('/').LastOrDefault();
            if (!string.IsNullOrWhiteSpace(lastSegment))
            {
                return Uri.UnescapeDataString(lastSegment);
            }
        }

        var segment = resolvedUri.Segments.LastOrDefault();
        if (string.IsNullOrWhiteSpace(segment))
        {
            return string.Empty;
        }

        return Uri.UnescapeDataString(segment.Trim('/'));
    }

    private static bool TryGetHttpRootUri(string sourceRoot, out Uri rootUri)
    {
        rootUri = default!;
        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            return false;
        }

        if (!Uri.TryCreate(sourceRoot.Trim(), UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (candidate.Scheme is not ("http" or "https"))
        {
            return false;
        }

        rootUri = EnsureTrailingSlash(candidate);
        return true;
    }

    private static Uri BuildSourceUri(Uri sourceRootUri, string relativePath, bool asDirectory)
    {
        var root = EnsureTrailingSlash(sourceRootUri);
        var segments = relativePath
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.EscapeDataString);
        var encodedRelativePath = string.Join("/", segments);
        if (asDirectory && !string.IsNullOrWhiteSpace(encodedRelativePath))
        {
            encodedRelativePath += "/";
        }

        return new Uri(root, encodedRelativePath);
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var absoluteUri = uri.AbsoluteUri;
        if (absoluteUri.EndsWith("/", StringComparison.Ordinal))
        {
            return uri;
        }

        return new Uri($"{absoluteUri}/");
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GameUpdater", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        return client;
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string targetPath,
        TransferRateLimiter? limiter,
        long? maxBytesPerSecond,
        Func<long?>? getMaxBytesPerSecond,
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
                var currentLimitBytesPerSecond = ResolveCurrentLimitBytesPerSecond(maxBytesPerSecond, getMaxBytesPerSecond);
                await limiter.DelayIfNeededAsync(bytesRead, currentLimitBytesPerSecond, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static long ResolveCurrentLimitBytesPerSecond(long? configuredLimit, Func<long?>? getDynamicLimit)
    {
        var dynamicValue = getDynamicLimit?.Invoke() ?? 0L;
        if (dynamicValue > 0)
        {
            return dynamicValue;
        }

        return configuredLimit.GetValueOrDefault() > 0
            ? configuredLimit.GetValueOrDefault()
            : 0L;
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
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private double _targetElapsedSeconds;

        public async ValueTask DelayIfNeededAsync(int bytesTransferred, long bytesPerSecond, CancellationToken cancellationToken)
        {
            if (bytesPerSecond <= 0 || bytesTransferred <= 0)
            {
                return;
            }

            _targetElapsedSeconds += bytesTransferred / (double)bytesPerSecond;
            var actualSeconds = _stopwatch.Elapsed.TotalSeconds;
            var delayMilliseconds = (int)Math.Ceiling((_targetElapsedSeconds - actualSeconds) * 1000d);
            if (delayMilliseconds > 0)
            {
                await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed record HttpSourceFile(Uri Uri, string RelativePath);

    private sealed record HttpDirectoryItem(Uri Uri, string Name, bool IsDirectory);

    private readonly record struct HttpDownloadResult(
        HttpDownloadOutcome Outcome,
        long DownloadedBytes,
        DateTime? LastModifiedUtc,
        long? TotalBytes);

    private enum HttpDownloadOutcome
    {
        Downloaded,
        NotModified
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
