using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using GameUpdater.Core.Abstractions;
using GameUpdater.Core.Services;
using GameUpdater.Shared.Models;

namespace GameUpdater.WinForms.Forms;

public sealed partial class MainForm
{
    private void RefreshServerDashboard()
    {
        try
        {
            var now = DateTime.UtcNow;
            var cpuPercent = GetServerCpuUsagePercent(now);
            var memory = GetServerMemorySnapshot();
            var systemDrive = GetDriveSnapshot(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            var network = GetServerNetworkSnapshot(now);
            var storage = GetServerStorageSnapshot();
            var runningTasks = _downloadMonitorRows.Count(row =>
                string.Equals(row.Status, "Đang tải", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.Status, "Tạm dừng", StringComparison.OrdinalIgnoreCase));
            var clientRows = LoadClientDashboardRows();
            var onlineClients = clientRows.Count(row => row.IsOnline);
            var playingClients = clientRows.Count(row => row.IsPlaying);
            var process = Process.GetCurrentProcess();
            var uptime = DateTime.UtcNow - _serverDashboardStartedAtUtc;

            _serverDashboardSummaryLabel.Text = $"{Environment.MachineName} • uptime {FormatDuration(uptime)} • {onlineClients} client online • {playingClients} đang chơi • {DateTime.Now:HH:mm:ss}";
            _serverDashboardCpuLabel.Text = $"{cpuPercent:0.#}% • {Environment.ProcessorCount} logical CPU";
            _serverDashboardMemoryLabel.Text = $"{memory.UsedGb:0.#}/{memory.TotalGb:0.#} GB ({memory.Percent:0.#}%)";
            _serverDashboardDiskLabel.Text = systemDrive.TotalGb <= 0
                ? "-"
                : $"{systemDrive.UsedPercent:0.#}% • trống {systemDrive.FreeGb:0.#}/{systemDrive.TotalGb:0.#} GB";
            _serverDashboardNetworkLabel.Text = $"Download: {network.ReceivedKbps:0.#} KB/s\nUpload: {network.SentKbps:0.#} KB/s\nAdapter active: {network.ActiveAdapters}";
            _serverDashboardStorageLabel.Text = storage;
            _serverDashboardServiceLabel.Text = $"Game: {_gamesBinding.Count}\nClient online: {onlineClients}\nTác vụ tải/đồng bộ: {runningTasks}\nRAM app: {process.WorkingSet64 / 1024d / 1024d:0.#} MB";
            _serverDashboardRecommendationLabel.Text = BuildServerRecommendations(cpuPercent, memory.Percent, systemDrive.UsedPercent, runningTasks, onlineClients);

            _serverCpuProgressBar.Value = ClampPercent(cpuPercent);
            _serverMemoryProgressBar.Value = ClampPercent(memory.Percent);
            _serverDiskProgressBar.Value = ClampPercent(systemDrive.UsedPercent);

            // Dynamic color coding: green → amber → red based on usage.
            _serverCpuProgressBar.ForeColor = GetUsageColor(cpuPercent);
            _serverMemoryProgressBar.ForeColor = GetUsageColor(memory.Percent);
            _serverDiskProgressBar.ForeColor = GetUsageColor(systemDrive.UsedPercent);
            _serverDashboardCpuLabel.ForeColor = GetUsageColor(cpuPercent);
            _serverDashboardMemoryLabel.ForeColor = GetUsageColor(memory.Percent);
            _serverDashboardDiskLabel.ForeColor = GetUsageColor(systemDrive.UsedPercent);
        }
        catch (Exception exception)
        {
            _serverDashboardSummaryLabel.Text = $"Không đọc được thông tin server: {exception.Message}";
        }
    }

    private double GetServerCpuUsagePercent(DateTime now)
    {
        var process = Process.GetCurrentProcess();
        var totalProcessorTime = process.TotalProcessorTime;
        var elapsedMs = Math.Max(1, (now - _lastServerCpuSampleUtc).TotalMilliseconds);
        var cpuUsedMs = Math.Max(0, (totalProcessorTime - _lastServerCpuTotalProcessorTime).TotalMilliseconds);
        _lastServerCpuTotalProcessorTime = totalProcessorTime;
        _lastServerCpuSampleUtc = now;
        return Math.Min(100, cpuUsedMs / elapsedMs / Math.Max(1, Environment.ProcessorCount) * 100d);
    }

    private static (double TotalGb, double UsedGb, double Percent) GetServerMemorySnapshot()
    {
        var memoryStatus = new MemoryStatusEx();
        memoryStatus.Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MemoryStatusEx>();
        if (!GlobalMemoryStatusEx(ref memoryStatus) || memoryStatus.TotalPhys == 0)
        {
            return (0, 0, 0);
        }

        var totalGb = BytesToGb(memoryStatus.TotalPhys);
        var usedGb = BytesToGb(memoryStatus.TotalPhys - memoryStatus.AvailPhys);
        return (totalGb, usedGb, usedGb / totalGb * 100d);
    }

    private static (double TotalGb, double FreeGb, double UsedPercent) GetDriveSnapshot(string driveRoot)
    {
        var drive = new DriveInfo(driveRoot);
        if (!drive.IsReady || drive.TotalSize <= 0)
        {
            return (0, 0, 0);
        }

        var totalGb = BytesToGb((ulong)drive.TotalSize);
        var freeGb = BytesToGb((ulong)drive.AvailableFreeSpace);
        return (totalGb, freeGb, (drive.TotalSize - drive.AvailableFreeSpace) * 100d / drive.TotalSize);
    }

    private (double SentKbps, double ReceivedKbps, int ActiveAdapters) GetServerNetworkSnapshot(DateTime now)
    {
        long sent = 0;
        long received = 0;
        var activeAdapters = 0;
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var stats = adapter.GetIPv4Statistics();
            sent += stats.BytesSent;
            received += stats.BytesReceived;
            activeAdapters++;
        }

        var elapsedSeconds = Math.Max(1, (now - _lastServerNetworkSampleUtc).TotalSeconds);
        var sentKbps = (_lastServerNetworkBytesSent > 0 || _lastServerNetworkBytesReceived > 0)
            ? Math.Max(0, (sent - _lastServerNetworkBytesSent) / 1024d / elapsedSeconds)
            : 0;
        var receivedKbps = (_lastServerNetworkBytesSent > 0 || _lastServerNetworkBytesReceived > 0)
            ? Math.Max(0, (received - _lastServerNetworkBytesReceived) / 1024d / elapsedSeconds)
            : 0;

        _lastServerNetworkBytesSent = sent;
        _lastServerNetworkBytesReceived = received;
        _lastServerNetworkSampleUtc = now;
        return (sentKbps, receivedKbps, activeAdapters);
    }

    private string GetServerStorageSnapshot()
    {
        var catalogDirectory = string.IsNullOrWhiteSpace(_autoCatalogPath)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(Path.GetFullPath(_autoCatalogPath)) ?? AppContext.BaseDirectory;
        var catalogDrive = GetDriveSnapshot(Path.GetPathRoot(catalogDirectory) ?? catalogDirectory);
        var resourceTarget = string.IsNullOrWhiteSpace(_resourceTargetRootPath)
            ? AppContext.BaseDirectory
            : _resourceTargetRootPath;
        var resourceDrive = GetDriveSnapshot(Path.GetPathRoot(Path.GetFullPath(resourceTarget)) ?? resourceTarget);
        var statusFolder = ResolveClientStatusFolder();
        var statusFileCount = Directory.Exists(statusFolder) ? Directory.EnumerateFiles(statusFolder, "*.json").Count() : 0;
        return $"Catalog: {_autoCatalogPath}\nỔ catalog trống: {catalogDrive.FreeGb:0.#}/{catalogDrive.TotalGb:0.#} GB\nỔ game/resource trống: {resourceDrive.FreeGb:0.#}/{resourceDrive.TotalGb:0.#} GB\nStatus files: {statusFileCount}\nFolder status: {statusFolder}";
    }

    private static string BuildServerRecommendations(double cpuPercent, double memoryPercent, double diskPercent, int runningTasks, int onlineClients)
    {
        var tips = new List<string>();
        if (cpuPercent >= 85) tips.Add("CPU cao: giảm số tác vụ đồng bộ/tải cùng lúc.");
        if (memoryPercent >= 85) tips.Add("RAM cao: đóng bớt ứng dụng nền trên server.");
        if (diskPercent >= 90) tips.Add("Ổ hệ thống gần đầy: dọn log/cache hoặc chuyển dữ liệu game sang ổ khác.");
        if (runningTasks > 0) tips.Add($"Đang có {runningTasks} tác vụ tải/đồng bộ, theo dõi tab Tài nguyên.");
        if (onlineClients == 0) tips.Add("Chưa có client online, kiểm tra shared folder client-status hoặc catalog path.");
        return tips.Count == 0 ? "Hệ thống ổn định. Có thể vận hành bình thường." : string.Join("\n", tips);
    }

    private static int ClampPercent(double value) => Math.Max(0, Math.Min(100, (int)Math.Round(value)));

    private static double BytesToGb(ulong bytes) => bytes / 1024d / 1024d / 1024d;

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1) return $"{(int)duration.TotalDays}d {duration.Hours}h";
        if (duration.TotalHours >= 1) return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        return $"{duration.Minutes}m {duration.Seconds}s";
    }

    private async Task RefreshClientDashboardAsync(bool forceNetworkProbe = false)
    {
        if (Interlocked.Exchange(ref _clientDashboardRefreshInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            var rows = LoadClientDashboardRows();
            if (forceNetworkProbe && rows.Count > 0)
            {
                await ProbeClientReachabilityAsync(rows);
                rows = rows
                    .OrderByDescending(row => row.IsOnline)
                    .ThenByDescending(row => row.IsPlaying)
                    .ThenBy(row => row.MachineName)
                    .ToList();
            }

            _clientStatusBinding.DataSource = rows;

            var onlineCount = rows.Count(row => row.IsOnline);
            var playingCount = rows.Count(row => row.IsPlaying);
            _clientDashboardSummaryLabel.Text = $"Online: {onlineCount} / {rows.Count} máy • Đang chơi: {playingCount} • Cập nhật: {DateTime.Now:HH:mm:ss}";
            _clientDashboardGameStatsLabel.Text = BuildClientDashboardGameStats(rows);
        }
        catch (Exception exception)
        {
            _clientStatusBinding.DataSource = new List<ClientDashboardRow>();
            _clientDashboardSummaryLabel.Text = $"Không đọc được trạng thái máy trạm: {exception.Message}";
            _clientDashboardGameStatsLabel.Text = "Game hot: - • Chơi nhiều nhất: - • Vừa cập nhật: -";
        }
        finally
        {
            Interlocked.Exchange(ref _clientDashboardRefreshInProgress, 0);
        }
    }

    private static async Task ProbeClientReachabilityAsync(IReadOnlyList<ClientDashboardRow> rows)
    {
        var tasks = rows.Select(async row =>
        {
            var target = row.ProbeTarget?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(target) ||
                string.Equals(target, "Không rõ", StringComparison.OrdinalIgnoreCase))
            {
                row.ReachabilityOverride = null;
                return;
            }

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(target, 900).ConfigureAwait(false);
                row.ReachabilityOverride = reply.Status == IPStatus.Success;
            }
            catch
            {
                row.ReachabilityOverride = null;
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private string BuildClientDashboardGameStats(IReadOnlyCollection<ClientDashboardRow> rows)
    {
        var hotGames = _gamesBinding.List.OfType<GameRecord>()
            .Where(game => game.IsHot)
            .Select(game => game.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Take(5)
            .ToList();

        var mostPlayed = rows
            .Where(row => row.IsPlaying && !string.IsNullOrWhiteSpace(row.CurrentGameName))
            .GroupBy(row => row.CurrentGameName)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => $"{group.Key} ({group.Count()})")
            .FirstOrDefault() ?? "-";

        var recentlyUpdated = _gamesBinding.List.OfType<GameRecord>()
            .Where(game => game.LastUpdatedAt.HasValue)
            .OrderByDescending(game => game.LastUpdatedAt)
            .Take(3)
            .Select(game => $"{game.Name} ({game.LastUpdatedAt!.Value.ToLocalTime():dd/MM HH:mm})")
            .ToList();

        return $"Game hot: {(hotGames.Count == 0 ? "-" : string.Join(", ", hotGames))} • Chơi nhiều nhất: {mostPlayed} • Vừa cập nhật: {(recentlyUpdated.Count == 0 ? "-" : string.Join(", ", recentlyUpdated))}";
    }

    private List<ClientDashboardRow> LoadClientDashboardRows()
    {
        var folder = ResolveClientStatusFolder();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return new List<ClientDashboardRow>();
        }

        var rows = new List<ClientDashboardRow>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var status = JsonSerializer.Deserialize<LauncherClientStatus>(json, ManifestJsonOptions);
                if (status is null)
                {
                    continue;
                }

                rows.Add(ClientDashboardRow.FromStatus(status, Path.GetFileName(file)));
            }
            catch
            {
                // Ignore broken status files so one bad client cannot break the dashboard.
            }
        }

        return rows
            .OrderByDescending(row => row.IsOnline)
            .ThenByDescending(row => row.IsPlaying)
            .ThenBy(row => row.MachineName)
            .ToList();
    }

    private string ResolveClientStatusFolder()
    {
        if (!string.IsNullOrWhiteSpace(_clientStatusFolderPath))
        {
            return _clientStatusFolderPath.Trim();
        }

        if (string.IsNullOrWhiteSpace(_autoCatalogPath))
        {
            return string.Empty;
        }

        var catalogDirectory = Path.GetDirectoryName(Path.GetFullPath(_autoCatalogPath));
        return string.IsNullOrWhiteSpace(catalogDirectory)
            ? string.Empty
            : Path.Combine(catalogDirectory, "client-status");
    }

    private void OpenClientStatusFolderButton_Click(object? sender, EventArgs e)
    {
        var folder = ResolveClientStatusFolder();
        if (string.IsNullOrWhiteSpace(folder))
        {
            ShowInfo("Chưa cấu hình thư mục trạng thái client.");
            return;
        }

        Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    private LauncherClientPolicy BuildClientPolicy()
    {
        return new LauncherClientPolicy
        {
            CafeDisplayName = _clientCafeDisplayName.Trim(),
            BannerMessage = _clientBannerMessage.Trim(),
            ThemeAccentColor = _clientThemeAccentColor.Trim(),
            ThemeFontFamily = _clientThemeFontFamily.Trim(),
            ClientWindowsWallpaperPath = _clientWindowsWallpaperPath.Trim(),
            EnableFullscreenKioskMode = _enableClientFullscreenKioskMode,
            EnableCloseRunningApplicationHotKey = _enableClientCloseApplicationHotKey
        };
    }
}
