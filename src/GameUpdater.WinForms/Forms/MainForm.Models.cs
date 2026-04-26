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

        public string SourceStatus { get; init; } = string.Empty;

        public string SourcePath { get; init; } = string.Empty;

        public string DownloadStatus { get; set; } = string.Empty;

        public string HealthStatus { get; set; } = string.Empty;

        public string DownloadSpeedDisplay { get; set; } = "-";

        public string RunStatus { get; set; } = string.Empty;

        public string FileCountDisplay { get; init; } = "-";

        public string SizeGbDisplay { get; init; } = "-";

        public DateTime? LastUpdatedAt { get; init; }

        public string InstallPath { get; init; } = string.Empty;

        public bool IsDownloaded { get; set; }

        public bool IsManaged { get; init; }

        public bool HasSource { get; init; }

        public long? RequiredAdditionalBytes { get; init; }

        public string RequiredAdditionalGbDisplay => RequiredAdditionalBytes.HasValue
            ? (RequiredAdditionalBytes.Value / 1024d / 1024d / 1024d).ToString("N2")
            : "-";
    }

    private sealed class SourceFolderEntry
    {
        public string Key { get; init; } = string.Empty;

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
        private static readonly TimeSpan OnlineThreshold = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan SlowHeartbeatThreshold = TimeSpan.FromMinutes(5);

        public string MachineName { get; init; } = string.Empty;

        public string UserName { get; init; } = string.Empty;

        public string CurrentGameName { get; init; } = string.Empty;

        public string CurrentGameExecutablePath { get; init; } = string.Empty;

        public DateTime LastSeenUtc { get; init; }

        public string SourceFileName { get; init; } = string.Empty;

        public double TotalMemoryGb { get; init; }

        public double UsedMemoryGb { get; init; }

        public double MemoryUsagePercent { get; init; }

        public DateTime ClientStartedAtUtc { get; init; }

        public long UptimeSeconds { get; init; }

        public double NetworkSentKbps { get; init; }

        public double NetworkReceivedKbps { get; init; }

        public bool IsOnline => DateTime.UtcNow - LastSeenUtc <= OnlineThreshold;

        public bool IsSlowHeartbeat => DateTime.UtcNow - LastSeenUtc > OnlineThreshold && DateTime.UtcNow - LastSeenUtc <= SlowHeartbeatThreshold;

        public bool IsPlaying => !string.IsNullOrWhiteSpace(CurrentGameName);

        public string StatusText => IsOnline ? (IsPlaying ? "Đang chơi" : "Online") : (IsSlowHeartbeat ? "Chậm heartbeat" : "Offline");

        public string LastSeenLocalText => LastSeenUtc == default
            ? "-"
            : LastSeenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        public string MemoryText => TotalMemoryGb <= 0
            ? "-"
            : $"{UsedMemoryGb:0.#}/{TotalMemoryGb:0.#}GB ({MemoryUsagePercent:0.#}%)";

        public string UptimeText => UptimeSeconds <= 0
            ? "-"
            : FormatDuration(TimeSpan.FromSeconds(UptimeSeconds));

        public string NetworkText => NetworkSentKbps <= 0 && NetworkReceivedKbps <= 0
            ? "-"
            : $"↓{NetworkReceivedKbps:0.#} KB/s ↑{NetworkSentKbps:0.#} KB/s";

        public static ClientDashboardRow FromStatus(LauncherClientStatus status, string sourceFileName)
        {
            return new ClientDashboardRow
            {
                MachineName = string.IsNullOrWhiteSpace(status.MachineName) ? "Không rõ" : status.MachineName,
                UserName = status.UserName,
                CurrentGameName = status.CurrentGameName,
                CurrentGameExecutablePath = status.CurrentGameExecutablePath,
                LastSeenUtc = status.LastSeenUtc,
                SourceFileName = sourceFileName,
                TotalMemoryGb = status.TotalMemoryGb,
                UsedMemoryGb = status.UsedMemoryGb,
                MemoryUsagePercent = status.MemoryUsagePercent,
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

        public string ClientStatusFolderPath { get; set; } = string.Empty;

        public bool EnableClientCloseApplicationHotKey { get; set; } = true;

        public bool EnableClientFullscreenKioskMode { get; set; }

        public string UiFontSizeMode { get; set; } = string.Empty;
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
