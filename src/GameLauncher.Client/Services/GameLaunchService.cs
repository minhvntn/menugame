using System.Diagnostics;
using GameLauncher.Client.Models;

namespace GameLauncher.Client.Services;

public sealed class GameLaunchService
{
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

        Process.Start(startInfo);
    }
}
