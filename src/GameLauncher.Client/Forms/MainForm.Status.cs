using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using GameUpdater.Shared.Models;

namespace GameLauncher.Client.Forms;

public sealed partial class MainForm
{
    private long _lastNetworkBytesSent;
    private long _lastNetworkBytesReceived;
    private DateTime _lastNetworkSampleUtc = DateTime.UtcNow;
    private readonly DateTime _clientStartedAtUtc = DateTime.UtcNow;
    private readonly System.Windows.Forms.Timer _statusHeartbeatTimer = new();

    private void WriteClientStatusSafe(bool clearPlayingGame = false)
    {
        var currentGameName = clearPlayingGame ? string.Empty : _currentGameName;
        var currentExecutable = clearPlayingGame ? string.Empty : _currentGameExecutablePath;
        var startedAt = _clientStartedAtUtc;

        Task.Run(() =>
        {
            try
            {
                var folder = ResolveClientStatusFolder();
                if (string.IsNullOrWhiteSpace(folder))
                {
                    return;
                }

                Directory.CreateDirectory(folder);
                var status = new LauncherClientStatus
                {
                    MachineName = Environment.MachineName,
                    UserName = Environment.UserName,
                    IpAddress = string.Empty,
                    CurrentGameName = currentGameName,
                    CurrentGameExecutablePath = currentExecutable,
                    LastSeenUtc = DateTime.UtcNow,
                    ClientStartedAtUtc = startedAt,
                    UptimeSeconds = Math.Max(0, (long)(DateTime.UtcNow - startedAt).TotalSeconds)
                };
                PopulateSystemMetrics(status);

                var filePath = Path.Combine(folder, $"{SanitizeFileName(Environment.MachineName)}.json");
                var json = System.Text.Json.JsonSerializer.Serialize(status, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Status reporting must never block the launcher.
            }
        });
    }

    private void PopulateSystemMetrics(LauncherClientStatus status)
    {
        PopulateMemoryMetrics(status);
        PopulateNetworkMetrics(status);
    }

    private static void PopulateMemoryMetrics(LauncherClientStatus status)
    {
        var memoryStatus = new MemoryStatusEx();
        memoryStatus.Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        if (!GlobalMemoryStatusEx(ref memoryStatus) || memoryStatus.TotalPhys == 0)
        {
            return;
        }

        var totalGb = BytesToGb(memoryStatus.TotalPhys);
        var availableGb = BytesToGb(memoryStatus.AvailPhys);
        var usedGb = Math.Max(0, totalGb - availableGb);
        status.TotalMemoryGb = Math.Round(totalGb, 1);
        status.UsedMemoryGb = Math.Round(usedGb, 1);
        status.MemoryUsagePercent = Math.Round(usedGb / totalGb * 100, 1);
    }

    private void PopulateNetworkMetrics(LauncherClientStatus status)
    {
        var now = DateTime.UtcNow;
        var sent = 0L;
        var received = 0L;
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var stats = adapter.GetIPv4Statistics();
            sent += stats.BytesSent;
            received += stats.BytesReceived;
        }

        var elapsedSeconds = Math.Max(1, (now - _lastNetworkSampleUtc).TotalSeconds);
        if (_lastNetworkBytesSent > 0 || _lastNetworkBytesReceived > 0)
        {
            status.NetworkSentKbps = Math.Round((sent - _lastNetworkBytesSent) / 1024d / elapsedSeconds, 1);
            status.NetworkReceivedKbps = Math.Round((received - _lastNetworkBytesReceived) / 1024d / elapsedSeconds, 1);
        }

        _lastNetworkBytesSent = sent;
        _lastNetworkBytesReceived = received;
        _lastNetworkSampleUtc = now;
    }

    private static double BytesToGb(ulong bytes) => bytes / 1024d / 1024d / 1024d;

    private static double BytesToGb(long bytes) => bytes / 1024d / 1024d / 1024d;

    private string ResolveClientStatusFolder()
    {
        if (string.IsNullOrWhiteSpace(_catalogPath) || _catalogPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var catalogDirectory = Path.GetDirectoryName(Path.GetFullPath(_catalogPath));
        return string.IsNullOrWhiteSpace(catalogDirectory)
            ? string.Empty
            : Path.Combine(catalogDirectory, "client-status");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }
}
