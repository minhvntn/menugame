using GameLauncher.Client.Models;

namespace GameLauncher.Client.Services;

public sealed class GamePrewarmService
{
    private static readonly string[] DataExtensions =
    [
        ".exe",
        ".dll",
        ".pak",
        ".vpk",
        ".ucas",
        ".utoc",
        ".pakchunk",
        ".idx",
        ".bin",
        ".dat"
    ];

    private const int ReadBufferSize = 128 * 1024;
    private const int MaxGamesPerRun = 5;
    private const int MaxFilesPerGame = 96;
    private const long MaxBytesPerGame = 384L * 1024L * 1024L;
    private const int FullReadThresholdBytes = 8 * 1024 * 1024;
    private const int HeadReadBytes = 1024 * 1024;
    private const int TailReadBytes = 256 * 1024;

    public async Task PrewarmHotGamesAsync(
        IReadOnlyList<LauncherGameRow> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var hotRows = rows
            .Where(row => row.IsHot)
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxGamesPerRun)
            .ToList();

        foreach (var row in hotRows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(row.InstallPath))
            {
                continue;
            }

            string installPath;
            try
            {
                installPath = Path.GetFullPath(row.InstallPath);
            }
            catch
            {
                continue;
            }

            if (!Directory.Exists(installPath))
            {
                continue;
            }

            var candidates = CollectCandidateFiles(row, installPath)
                .Take(MaxFilesPerGame)
                .ToList();

            var warmedBytes = 0L;
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (warmedBytes >= MaxBytesPerGame)
                {
                    break;
                }

                try
                {
                    warmedBytes += await TouchFileAsync(candidate, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore single-file warm failures.
                }
            }
        }
    }

    private static IEnumerable<string> CollectCandidateFiles(LauncherGameRow row, string installPath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (TryAddCandidate(row.ResolvedExecutablePath, seen, out var resolvedExecutable))
        {
            yield return resolvedExecutable;
        }

        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        directories.Add(installPath);

        if (!string.IsNullOrWhiteSpace(resolvedExecutable))
        {
            var executableDirectory = Path.GetDirectoryName(resolvedExecutable);
            if (!string.IsNullOrWhiteSpace(executableDirectory) && Directory.Exists(executableDirectory))
            {
                directories.Add(executableDirectory);
            }
        }

        foreach (var folderName in new[] { "bin", "binaries", "game", "data", "content", "paks" })
        {
            var candidateDirectory = Path.Combine(installPath, folderName);
            if (Directory.Exists(candidateDirectory))
            {
                directories.Add(candidateDirectory);
            }
        }

        var rankedFiles = new List<(string Path, int Priority, long Length)>();
        foreach (var directory in directories)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                if (!TryAddCandidate(file, seen, out var normalizedFile))
                {
                    continue;
                }

                var extension = Path.GetExtension(normalizedFile);
                if (!DataExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                FileInfo info;
                try
                {
                    info = new FileInfo(normalizedFile);
                }
                catch
                {
                    continue;
                }

                if (!info.Exists || info.Length <= 0)
                {
                    continue;
                }

                rankedFiles.Add((
                    normalizedFile,
                    GetPriority(normalizedFile, resolvedExecutable),
                    info.Length));
            }
        }

        foreach (var ranked in rankedFiles
                     .OrderBy(item => item.Priority)
                     .ThenBy(item => item.Length)
                     .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            yield return ranked.Path;
        }
    }

    private static bool TryAddCandidate(string? path, ISet<string> seen, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
            return seen.Add(normalizedPath) && File.Exists(normalizedPath);
        }
        catch
        {
            return false;
        }
    }

    private static int GetPriority(string filePath, string? resolvedExecutable)
    {
        if (!string.IsNullOrWhiteSpace(resolvedExecutable) &&
            string.Equals(filePath, resolvedExecutable, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var extension = Path.GetExtension(filePath);
        if (string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }

    private static async Task<long> TouchFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var info = new FileInfo(filePath);
        if (!info.Exists || info.Length <= 0)
        {
            return 0;
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            ReadBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[ReadBufferSize];
        var warmedBytes = 0L;

        if (info.Length <= FullReadThresholdBytes)
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                warmedBytes += read;
            }

            return warmedBytes;
        }

        warmedBytes += await ReadAtMostAsync(stream, buffer, HeadReadBytes, cancellationToken).ConfigureAwait(false);

        var tailOffset = Math.Max(0, info.Length - TailReadBytes);
        if (tailOffset > stream.Position)
        {
            stream.Seek(tailOffset, SeekOrigin.Begin);
            warmedBytes += await ReadAtMostAsync(stream, buffer, TailReadBytes, cancellationToken).ConfigureAwait(false);
        }

        return warmedBytes;
    }

    private static async Task<long> ReadAtMostAsync(
        Stream stream,
        byte[] buffer,
        int bytesToRead,
        CancellationToken cancellationToken)
    {
        var remaining = bytesToRead;
        var total = 0L;

        while (remaining > 0)
        {
            var sliceLength = Math.Min(buffer.Length, remaining);
            var read = await stream.ReadAsync(buffer.AsMemory(0, sliceLength), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            remaining -= read;
            total += read;
        }

        return total;
    }
}
