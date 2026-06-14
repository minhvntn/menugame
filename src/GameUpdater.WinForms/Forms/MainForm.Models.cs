using System.Runtime.InteropServices;
using GameUpdater.Shared.Models;

namespace GameUpdater.WinForms.Forms;

public sealed partial class MainForm
{
    private enum ResourceFilterKind
    {
        All,
        Missing,
        Downloaded,
        DownloadMonitor
    }

    private sealed class ResourceGameRow
    {
        public int Id { get; init; }

        public int? ManagedGameId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public string SourceKey { get; init; } = string.Empty;

        public string SourceRoot { get; init; } = string.Empty;

        public string SourceStatus { get; init; } = string.Empty;

        public string SourcePath { get; init; } = string.Empty;

        public string DownloadStatus { get; set; } = string.Empty;

        public string HealthStatus { get; set; } = string.Empty;

        public string DownloadSpeedDisplay { get; set; } = "-";

        public string RunStatus { get; set; } = string.Empty;

        public string FileCountDisplay { get; init; } = "-";

        public string VersionLocal { get; set; } = string.Empty;
        
        public string VersionIdc { get; set; } = string.Empty;

        public int ProgressPercent { get; set; }

        public double? SizeLocalGb { get; set; }

        public double? SizeIdcGb { get; set; }

        public double? SizeMissingGb { get; set; }

        public string SizeLocalGbDisplay => SizeLocalGb.HasValue && SizeLocalGb.Value > 0 ? $"{SizeLocalGb.Value:N2} GB" : "-";
        
        public string SizeIdcGbDisplay => SizeIdcGb.HasValue && SizeIdcGb.Value > 0 ? $"{SizeIdcGb.Value:N2} GB" : "-";
        
        public string SizeMissingGbDisplay => SizeMissingGb.HasValue && SizeMissingGb.Value > 0 ? $"{SizeMissingGb.Value:N2} GB" : "-";
        
        // Wait, the screenshot shows "330 MB" if it's less than 1GB.
        public string SizeMissingDisplay => FormatSizeDynamic(SizeMissingGb);

        public DateTime? LastUpdatedAt { get; init; }

        public string InstallPath { get; init; } = string.Empty;

        public bool IsDownloaded { get; set; }

        public bool IsManaged { get; init; }

        public bool HasSource { get; init; }

        public long? RequiredAdditionalBytes { get; set; }

        private static string FormatSizeDynamic(double? sizeGb)
        {
            if (!sizeGb.HasValue || sizeGb.Value <= 0) return "-";
            if (sizeGb.Value < 1.0)
            {
                var mb = sizeGb.Value * 1024;
                return $"{mb:N0} MB";
            }
            return $"{sizeGb.Value:N2} GB";
        }
    }

    private sealed class SourceFolderEntry
    {
        public string Key { get; init; } = string.Empty;

        public string SourceRoot { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string FullPath { get; init; } = string.Empty;
    }

    private sealed class ResourceSyncTaskControl : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        private volatile bool _isPaused;
        private long _bandwidthLimitBytesPerSecond;

        public CancellationToken CancellationToken => _cancellation.Token;

        public bool IsPaused => _isPaused;

        public long BandwidthLimitBytesPerSecond => Interlocked.Read(ref _bandwidthLimitBytesPerSecond);

        public int BandwidthLimitMbps
        {
            get
            {
                var bytesPerSecond = BandwidthLimitBytesPerSecond;
                return bytesPerSecond <= 0 ? 0 : (int)Math.Max(1, bytesPerSecond / (1024L * 1024L));
            }
        }

        public void Pause()
        {
            _isPaused = true;
        }

        public void Resume()
        {
            _isPaused = false;
        }

        public void Cancel()
        {
            _cancellation.Cancel();
        }

        public void SetBandwidthLimitMbps(int mbps)
        {
            var normalizedMbps = Math.Clamp(mbps, 0, 10000);
            var bytesPerSecond = normalizedMbps <= 0 ? 0L : normalizedMbps * 1024L * 1024L;
            Interlocked.Exchange(ref _bandwidthLimitBytesPerSecond, bytesPerSecond);
        }

        public async ValueTask WaitIfPausedAsync(CancellationToken cancellationToken)
        {
            while (_isPaused)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            _cancellation.Dispose();
        }
    }

    private sealed class DownloadMonitorRow
    {
        public int SerialNumber { get; set; }

        public DateTime StartedAt { get; set; }

        public string GameName { get; set; } = string.Empty;

        public int? GameId { get; set; }

        public string GameIdDisplay { get; set; } = "-";

        public string ResourceKey { get; set; } = string.Empty;

        public int ProgressPercent { get; set; }

        public string ProgressDisplay { get; set; } = "0.0%";

        public string Status { get; set; } = string.Empty;

        public long? TotalBytes { get; set; }

        public long ProcessedBytes { get; set; }

        public double? SpeedMbps { get; set; }

        public string TotalSizeGbDisplay { get; set; } = "-";

        public string RemainingMbDisplay { get; set; } = "-";

        public string RemainingTimeDisplay { get; set; } = "-";

        public string SpeedMbpsDisplay { get; set; } = "-";

        public DateTime UpdatedAt { get; set; }

        public string Message { get; set; } = string.Empty;

        public bool AutoRemoveScheduled { get; set; }
    }

    private sealed class UpdateSourceOption
    {
        public UpdateSourceKind Kind { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    private enum UiFontSizeMode
    {
        VerySmall,
        Small,
        Normal,
        Big,
        VeryBig
    }

    private sealed class FontSizeOption
    {
        public UiFontSizeMode Mode { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    private sealed class ClientDashboardRow
    {
        private TimeSpan OnlineThreshold => TimeSpan.FromSeconds(Math.Max(30, (HeartbeatIntervalSeconds * 2) + 30));
        private TimeSpan SlowHeartbeatThreshold => TimeSpan.FromSeconds(
            Math.Max(
                OnlineThreshold.TotalSeconds + 60,
                OnlineThreshold.TotalSeconds + (HeartbeatIntervalSeconds * 4)));

        public int HeartbeatIntervalSeconds { get; init; } = 45;

        public string MachineName { get; init; } = string.Empty;

        public string UserName { get; init; } = string.Empty;

        public string CurrentGameName { get; init; } = string.Empty;

        public string CurrentGameExecutablePath { get; init; } = string.Empty;

        public string IpAddress { get; init; } = string.Empty;

        public DateTime LastSeenUtc { get; init; }

        public string SourceFileName { get; init; } = string.Empty;

        public double TotalMemoryGb { get; init; }

        public double UsedMemoryGb { get; init; }

        public double MemoryUsagePercent { get; init; }

        public double CpuTemperatureCelsius { get; init; }

        public string CpuName { get; init; } = string.Empty;

        public string GpuName { get; init; } = string.Empty;

        public double CpuLoadPercent { get; init; }

        public double CpuClockMhz { get; init; }

        public double GpuTemperatureCelsius { get; init; }

        public double GpuLoadPercent { get; init; }

        public double CpuPowerDrawWatt { get; init; }

        public double GpuPowerDrawWatt { get; init; }

        public double GpuFanRpm { get; init; }

        public DateTime ClientStartedAtUtc { get; init; }

        public long UptimeSeconds { get; init; }

        public double NetworkSentKbps { get; init; }

        public double NetworkReceivedKbps { get; init; }

        public bool? ReachabilityOverride { get; set; }

        public string ProbeTarget => string.IsNullOrWhiteSpace(IpAddress)
            ? MachineName
            : IpAddress;

        private bool IsOnlineByHeartbeat => DateTime.UtcNow - LastSeenUtc <= OnlineThreshold;

        public bool IsOnline => ReachabilityOverride ?? IsOnlineByHeartbeat;

        public bool IsSlowHeartbeat => DateTime.UtcNow - LastSeenUtc > OnlineThreshold && DateTime.UtcNow - LastSeenUtc <= SlowHeartbeatThreshold;

        public bool IsPlaying => !string.IsNullOrWhiteSpace(CurrentGameName);

        public string StatusText => IsOnline ? (IsPlaying ? "Đang chơi" : "Online") : (IsSlowHeartbeat ? "Chậm heartbeat" : "Offline");

        public string LastSeenLocalText => LastSeenUtc == default
            ? "-"
            : LastSeenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        public string MemoryText => TotalMemoryGb <= 0
            ? "-"
            : $"{UsedMemoryGb:0.#}/{TotalMemoryGb:0.#}GB ({MemoryUsagePercent:0.#}%)";

        public string CpuTemperatureText => CpuTemperatureCelsius <= 0
            ? "-"
            : $"{CpuTemperatureCelsius:0.#}\u00b0C";

        public string CpuPowerText => CpuPowerDrawWatt <= 0
            ? "-"
            : $"{CpuPowerDrawWatt:0.#}W";

        public string VgaLoadText
        {
            get
            {
                var parts = new List<string>();
                if (GpuTemperatureCelsius > 0)
                {
                    parts.Add($"{GpuTemperatureCelsius:0.#}\u00b0C");
                }

                if (GpuLoadPercent > 0)
                {
                    parts.Add($"{GpuLoadPercent:0.#}%");
                }

                if (GpuFanRpm > 0)
                {
                    parts.Add($"{GpuFanRpm:0} RPM");
                }

                return parts.Count == 0 ? "-" : string.Join(" / ", parts);
            }
        }

        public string CpuLoadText
        {
            get
            {
                var parts = new List<string>();
                if (CpuTemperatureCelsius > 0)
                {
                    parts.Add($"{CpuTemperatureCelsius:0.#}\u00b0C");
                }

                if (CpuLoadPercent > 0)
                {
                    parts.Add($"{CpuLoadPercent:0.#}%");
                }

                if (CpuClockMhz > 0)
                {
                    parts.Add($"{CpuClockMhz / 1000d:0.###}GHz");
                }

                return parts.Count == 0 ? "-" : string.Join(" / ", parts);
            }
        }

        public string UptimeText => UptimeSeconds <= 0
            ? "-"
            : FormatDuration(TimeSpan.FromSeconds(UptimeSeconds));

        public string NetworkText => NetworkSentKbps <= 0 && NetworkReceivedKbps <= 0
            ? "-"
            : $"↓{NetworkReceivedKbps:0.#} KB/s ↑{NetworkSentKbps:0.#} KB/s";

        public static ClientDashboardRow FromStatus(
            LauncherClientStatus status,
            string sourceFileName,
            int heartbeatIntervalSeconds = 45)
        {
            return new ClientDashboardRow
            {
                HeartbeatIntervalSeconds = Math.Clamp(heartbeatIntervalSeconds, 5, 300),
                MachineName = string.IsNullOrWhiteSpace(status.MachineName) ? "Không rõ" : status.MachineName,
                UserName = status.UserName,
                CurrentGameName = status.CurrentGameName,
                CurrentGameExecutablePath = status.CurrentGameExecutablePath,
                IpAddress = status.IpAddress?.Trim() ?? string.Empty,
                LastSeenUtc = status.LastSeenUtc,
                SourceFileName = sourceFileName,
                CpuName = status.CpuName,
                GpuName = status.GpuName,
                TotalMemoryGb = status.TotalMemoryGb,
                UsedMemoryGb = status.UsedMemoryGb,
                MemoryUsagePercent = status.MemoryUsagePercent,
                CpuTemperatureCelsius = status.CpuTemperatureCelsius,
                CpuLoadPercent = status.CpuLoadPercent,
                CpuClockMhz = status.CpuClockMhz,
                CpuPowerDrawWatt = status.CpuPowerDrawWatt,
                GpuTemperatureCelsius = status.GpuTemperatureCelsius,
                GpuLoadPercent = status.GpuLoadPercent,
                GpuPowerDrawWatt = status.GpuPowerDrawWatt,
                GpuFanRpm = status.GpuFanRpm,
                ClientStartedAtUtc = status.ClientStartedAtUtc,
                UptimeSeconds = status.UptimeSeconds,
                NetworkSentKbps = status.NetworkSentKbps,
                NetworkReceivedKbps = status.NetworkReceivedKbps
            };
        }
    }

    private sealed class ServerUiSettings
    {
        public string ClientCatalogPath { get; set; } = string.Empty;

        public string ResourceSourceRootPath { get; set; } = string.Empty;

        public string ResourceTargetRootPath { get; set; } = string.Empty;

        public int ResourceBandwidthLimitMbps { get; set; }

        public string ClientWindowsWallpaperPath { get; set; } = string.Empty;

        public string ClientCafeDisplayName { get; set; } = string.Empty;

        public string ClientBannerMessage { get; set; } = string.Empty;

        public string ClientThemeAccentColor { get; set; } = string.Empty;

        public string ClientThemeFontFamily { get; set; } = string.Empty;

        public string ClientStatusFolderPath { get; set; } = string.Empty;

        public bool EnableClientCloseApplicationHotKey { get; set; } = true;

        public bool EnableClientFullscreenKioskMode { get; set; }

        public string UiFontSizeMode { get; set; } = string.Empty;

        public int ClientHeartbeatIntervalSeconds { get; set; } = 45;

        public int DashboardRefreshIntervalSeconds { get; set; } = 15;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
