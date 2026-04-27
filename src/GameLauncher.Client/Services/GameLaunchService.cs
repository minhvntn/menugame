using System.Diagnostics;
using GameLauncher.Client.Models;

namespace GameLauncher.Client.Services;

public sealed class GameLaunchService
{
    private static readonly string[] ExcludedExecutableKeywords =
    [
        "unins",
        "uninstall",
        "setup",
        "installer",
        "vc_redist",
        "redist",
        "dxsetup",
        "crash",
        "report",
        "benchmark",
        "patcher",
        "updater",
        "configtool"
    ];

    private readonly object _syncRoot = new();
    private Process? _lastLaunchedProcess;
    private string _lastExecutablePath = string.Empty;

    public string LastLaunchedExecutablePath
    {
        get
        {
            lock (_syncRoot)
            {
                return _lastExecutablePath;
            }
        }
    }

    public Process Launch(LauncherGameRow row)
    {
        var executablePath = ResolveExecutablePathWithPrecheck(row);

        var workingDirectory = Directory.Exists(row.InstallPath)
            ? Path.GetFullPath(row.InstallPath)
            : Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = row.LaunchArguments ?? string.Empty,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };

        var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"Không thể khởi chạy trò chơi {row.Name}.");
        }

        lock (_syncRoot)
        {
            try
            {
                _lastLaunchedProcess?.Dispose();
            }
            catch
            {
                // Ignore disposal failure and keep latest process.
            }

            _lastLaunchedProcess = process;
            _lastExecutablePath = Path.GetFullPath(executablePath);
        }

        return process;
    }

    public bool TryCloseLastLaunchedApplication(out string message)
    {
        Process? process;
        string executablePath;
        lock (_syncRoot)
        {
            process = _lastLaunchedProcess;
            executablePath = _lastExecutablePath;
        }

        if (process is not null)
        {
            try
            {
                if (!process.HasExited && TryCloseProcess(process))
                {
                    lock (_syncRoot)
                    {
                        _lastLaunchedProcess = null;
                    }

                    message = $"Đã đóng ứng dụng chạy từ: {Path.GetFileName(process.ProcessName)}";
                    return true;
                }
            }
            catch
            {
                // Fallback to path-based detection.
            }
        }

        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            var closedCount = TryCloseProcessesByExecutablePath(executablePath);
            if (closedCount > 0)
            {
                lock (_syncRoot)
                {
                    _lastLaunchedProcess = null;
                }

                message = $"Đã đóng {closedCount} tiến trình từ launcher.";
                return true;
            }
        }

        message = "Không có ứng dụng do launcher mở đang chạy.";
        return false;
    }

    private static string ResolveExecutablePathWithPrecheck(LauncherGameRow row)
    {
        if (row is null)
        {
            throw new InvalidOperationException("Dữ liệu trò chơi không hợp lệ.");
        }

        var directCandidates = BuildDirectCandidates(row).ToList();
        foreach (var candidate in directCandidates)
        {
            if (TryNormalizeExecutablePath(candidate, out var normalizedCandidate))
            {
                return normalizedCandidate;
            }
        }

        if (string.IsNullOrWhiteSpace(row.InstallPath))
        {
            throw new InvalidOperationException($"Trò chơi {row.Name} chưa cấu hình đường dẫn cài đặt.");
        }

        var installRoot = Path.GetFullPath(row.InstallPath);
        if (!Directory.Exists(installRoot))
        {
            throw new DirectoryNotFoundException($"Không tìm thấy thư mục cài đặt của {row.Name}: {installRoot}");
        }

        var fallbackExecutable = TryFindFallbackExecutable(row, installRoot);
        if (!string.IsNullOrWhiteSpace(fallbackExecutable))
        {
            return fallbackExecutable;
        }

        var expectedPath = directCandidates.FirstOrDefault() ?? Path.Combine(installRoot, row.LaunchRelativePath ?? string.Empty);
        throw new FileNotFoundException(
            $"Không tìm thấy tệp chạy của {row.Name}. Vui lòng đồng bộ lại game hoặc cập nhật LaunchRelativePath.",
            expectedPath);
    }

    private static IEnumerable<string> BuildDirectCandidates(LauncherGameRow row)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(row.ResolvedExecutablePath) && unique.Add(row.ResolvedExecutablePath))
        {
            yield return row.ResolvedExecutablePath;
        }

        var launchRelativePath = row.LaunchRelativePath?.Trim();
        if (string.IsNullOrWhiteSpace(launchRelativePath))
        {
            yield break;
        }

        if (Path.IsPathRooted(launchRelativePath))
        {
            if (unique.Add(launchRelativePath))
            {
                yield return launchRelativePath;
            }

            yield break;
        }

        if (string.IsNullOrWhiteSpace(row.InstallPath))
        {
            yield break;
        }

        var combined = Path.Combine(row.InstallPath, launchRelativePath);
        if (unique.Add(combined))
        {
            yield return combined;
        }
    }

    private static bool TryNormalizeExecutablePath(string candidatePath, out string executablePath)
    {
        executablePath = string.Empty;
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(candidatePath);
            if (!string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!File.Exists(fullPath))
            {
                return false;
            }

            executablePath = fullPath;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string TryFindFallbackExecutable(LauncherGameRow row, string installRoot)
    {
        var expectedName = Path.GetFileNameWithoutExtension(row.LaunchRelativePath ?? string.Empty).Trim();
        var normalizedGameName = NormalizeKey(row.Name);
        var candidates = new List<ExecutableCandidate>();

        foreach (var executablePath in EnumerateExecutableFilesSafe(installRoot))
        {
            if (!TryNormalizeExecutablePath(executablePath, out var normalizedPath))
            {
                continue;
            }

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(normalizedPath);
            if (string.IsNullOrWhiteSpace(nameWithoutExtension) || IsExcludedExecutableName(nameWithoutExtension))
            {
                continue;
            }

            var info = new FileInfo(normalizedPath);
            if (!info.Exists)
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(installRoot, normalizedPath);
            var depth = relativePath
                .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
                .Length;

            var score = ComputeCandidateScore(relativePath, nameWithoutExtension, expectedName, normalizedGameName, depth, info.Length);
            candidates.Add(new ExecutableCandidate(normalizedPath, score, depth, info.Length));
        }

        var bestCandidate = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Depth)
            .ThenByDescending(candidate => candidate.Length)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return bestCandidate?.Path ?? string.Empty;
    }

    private static int ComputeCandidateScore(
        string relativePath,
        string executableName,
        string expectedName,
        string normalizedGameName,
        int depth,
        long fileLength)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(expectedName))
        {
            if (string.Equals(executableName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                score += 4000;
            }
            else if (executableName.Contains(expectedName, StringComparison.OrdinalIgnoreCase))
            {
                score += 1800;
            }
        }

        var normalizedExecutableName = NormalizeKey(executableName);
        if (!string.IsNullOrWhiteSpace(normalizedGameName))
        {
            if (string.Equals(normalizedExecutableName, normalizedGameName, StringComparison.OrdinalIgnoreCase))
            {
                score += 2500;
            }
            else if (normalizedExecutableName.Contains(normalizedGameName, StringComparison.OrdinalIgnoreCase) ||
                     normalizedGameName.Contains(normalizedExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                score += 1200;
            }
        }

        if (relativePath.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains("\\binaries\\", StringComparison.OrdinalIgnoreCase))
        {
            score += 200;
        }

        var sizeInMb = fileLength / (1024d * 1024d);
        score += (int)Math.Min(sizeInMb, 250d);
        score -= depth * 10;

        return score;
    }

    private static IEnumerable<string> EnumerateExecutableFilesSafe(string installRoot)
    {
        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Push(installRoot);
        visited.Add(Path.GetFullPath(installRoot));

        var yieldedCount = 0;
        const int maxFilesToInspect = 10_000;

        while (pending.Count > 0)
        {
            var currentDirectory = pending.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(currentDirectory, "*.exe", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
                yieldedCount++;
                if (yieldedCount >= maxFilesToInspect)
                {
                    yield break;
                }
            }

            IEnumerable<string> childDirectories;
            try
            {
                childDirectories = Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                var normalizedChild = Path.GetFullPath(childDirectory);
                if (visited.Add(normalizedChild))
                {
                    pending.Push(normalizedChild);
                }
            }
        }
    }

    private static bool IsExcludedExecutableName(string executableName)
    {
        foreach (var keyword in ExcludedExecutableKeywords)
        {
            if (executableName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();

        return new string(chars);
    }

    private static int TryCloseProcessesByExecutablePath(string executablePath)
    {
        var targetPath = Path.GetFullPath(executablePath);
        var processName = Path.GetFileNameWithoutExtension(targetPath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return 0;
        }

        var closedCount = 0;
        foreach (var candidate in Process.GetProcessesByName(processName))
        {
            using (candidate)
            {
                if (candidate.HasExited)
                {
                    continue;
                }

                var candidatePath = GetProcessExecutablePath(candidate);
                if (!string.Equals(candidatePath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryCloseProcess(candidate))
                {
                    closedCount++;
                }
            }
        }

        return closedCount;
    }

    private static bool TryCloseProcess(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return true;
            }

            if (process.CloseMainWindow() && process.WaitForExit(2000))
            {
                return true;
            }
        }
        catch
        {
            // Continue with force kill below.
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }

            return process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static string GetProcessExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName is { Length: > 0 } path
                ? Path.GetFullPath(path)
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed record ExecutableCandidate(string Path, int Score, int Depth, long Length);
}
