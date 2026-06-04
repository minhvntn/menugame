namespace GameUpdater.Shared.Models;

public sealed class LauncherClientStatus
{
    public string MachineName { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public string CurrentGameName { get; set; } = string.Empty;

    public string CurrentGameExecutablePath { get; set; } = string.Empty;

    public DateTime LastSeenUtc { get; set; }

    public double TotalMemoryGb { get; set; }

    public double UsedMemoryGb { get; set; }

    public double MemoryUsagePercent { get; set; }

    public double CpuTemperatureCelsius { get; set; }

    public string CpuName { get; set; } = string.Empty;

    public string GpuName { get; set; } = string.Empty;

    public double CpuLoadPercent { get; set; }

    public double CpuClockMhz { get; set; }

    public double GpuTemperatureCelsius { get; set; }

    public double GpuLoadPercent { get; set; }

    public double CpuPowerDrawWatt { get; set; }

    public double GpuPowerDrawWatt { get; set; }

    public double GpuFanRpm { get; set; }

    public DateTime ClientStartedAtUtc { get; set; }

    public long UptimeSeconds { get; set; }

    public double NetworkSentKbps { get; set; }

    public double NetworkReceivedKbps { get; set; }

    public bool IsPlaying => !string.IsNullOrWhiteSpace(CurrentGameName);
}
