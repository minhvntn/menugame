using System.Diagnostics;
using GameLauncher.Client.Models;

namespace GameLauncher.Client.Services;

public sealed class GameLaunchService
{
    private readonly object _syncRoot = new();
    private Process? _lastLaunchedProcess;
    private string _lastExecutablePath = string.Empty;

    public void Launch(LauncherGameRow row)
    {
        if (string.IsNullOrWhiteSpace(row.ResolvedExecutablePath))
        {
            throw new InvalidOperationException($"Trò chơi {row.Name} chưa cấu hình tệp chạy.");
        }

        if (!File.Exists(row.ResolvedExecutablePath))
        {
            throw new FileNotFoundException("Không tìm thấy tệp chạy trò chơi.", row.ResolvedExecutablePath);
        }

        var workingDirectory = Directory.Exists(row.InstallPath)
            ? row.InstallPath
            : Path.GetDirectoryName(row.ResolvedExecutablePath) ?? AppContext.BaseDirectory;

        var startInfo = new ProcessStartInfo
        {
            FileName = row.ResolvedExecutablePath,
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
            _lastExecutablePath = Path.GetFullPath(row.ResolvedExecutablePath);
        }
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
}
