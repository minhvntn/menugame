using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Management;
using LibreHardwareMonitor.Hardware;
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
                    IpAddress = ResolvePreferredIpv4Address(),
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
        PopulateHardwareMetrics(status);
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

    private static void PopulateHardwareMetrics(LauncherClientStatus status)
    {
        TryReadHardwareMonitorMetrics(status);
        if (status.CpuTemperatureCelsius <= 0)
        {
            var wmiTemperature = TryReadWmiTemperature();
            if (wmiTemperature > 0)
            {
                status.CpuTemperatureCelsius = wmiTemperature;
            }
        }
    }

    private static bool TryReadHardwareMonitorMetrics(LauncherClientStatus status)
    {
        Computer? computer = null;
        try
        {
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
            };
            computer.Open();

            var metrics = new HardwareMetrics();
            foreach (var hardware in computer.Hardware)
            {
                CollectHardwareMetrics(hardware, metrics);
            }

            ApplyHardwareMetrics(status, metrics);
            return metrics.HasAnyValue;
        }
        catch
        {
            // Hardware sensor access varies by chipset, driver, and permissions.
            return false;
        }
        finally
        {
            try
            {
                computer?.Close();
            }
            catch
            {
                // Ignore shutdown failures from hardware sensor providers.
            }
        }
    }

    private static void CollectHardwareMetrics(IHardware hardware, HardwareMetrics metrics)
    {
        try
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
            {
                CollectHardwareMetrics(subHardware, metrics);
            }

            var isCpu = hardware.HardwareType == HardwareType.Cpu;
            var isGpu = hardware.HardwareType is HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia;

            if (isCpu && string.IsNullOrWhiteSpace(metrics.CpuName))
            {
                metrics.CpuName = hardware.Name;
            }
            else if (isGpu && string.IsNullOrWhiteSpace(metrics.GpuName))
            {
                metrics.GpuName = hardware.Name;
            }

            foreach (var sensor in hardware.Sensors)
            {
                if (!sensor.Value.HasValue)
                {
                    continue;
                }

                var value = sensor.Value.Value;
                if (isCpu)
                {
                    CaptureCpuSensor(metrics, sensor, value);
                }
                else if (isGpu)
                {
                    CaptureGpuSensor(metrics, sensor, value);
                }
                else if (sensor.SensorType == SensorType.Temperature &&
                         value is > 0 and < 125 &&
                         IsLikelyCpuTemperatureSensor(sensor.Name))
                {
                    metrics.CpuTemperatureCelsius = Math.Max(metrics.CpuTemperatureCelsius, value);
                }
            }
        }
        catch
        {
            // Ignore one unreadable hardware node and keep scanning the others.
        }
    }

    private static bool IsLikelyCpuTemperatureSensor(string? sensorName)
    {
        if (string.IsNullOrWhiteSpace(sensorName))
        {
            return false;
        }

        return sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
               sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
               sensorName.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
               sensorName.Contains("Tdie", StringComparison.OrdinalIgnoreCase);
    }

    private static void CaptureCpuSensor(HardwareMetrics metrics, ISensor sensor, float value)
    {
        if (sensor.SensorType == SensorType.Temperature && value is > 0 and < 125)
        {
            metrics.CpuTemperatureCelsius = Math.Max(metrics.CpuTemperatureCelsius, value);
            return;
        }

        if (sensor.SensorType == SensorType.Load && value is >= 0 and <= 100)
        {
            if (sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
                metrics.CpuLoadPercent <= 0)
            {
                metrics.CpuLoadPercent = value;
            }
            return;
        }

        if (sensor.SensorType == SensorType.Clock && value > metrics.CpuClockMhz)
        {
            metrics.CpuClockMhz = value;
            return;
        }

        if (sensor.SensorType == SensorType.Power && value > 0 &&
            (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
             sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)))
        {
            metrics.CpuPowerDrawWatt = Math.Max(metrics.CpuPowerDrawWatt, value);
        }
    }

    private static void CaptureGpuSensor(HardwareMetrics metrics, ISensor sensor, float value)
    {
        if (sensor.SensorType == SensorType.Temperature && value is > 0 and < 125)
        {
            metrics.GpuTemperatureCelsius = Math.Max(metrics.GpuTemperatureCelsius, value);
            return;
        }

        if (sensor.SensorType == SensorType.Load && value is >= 0 and <= 100)
        {
            if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                metrics.GpuLoadPercent <= 0)
            {
                metrics.GpuLoadPercent = value;
            }
            return;
        }

        if (sensor.SensorType == SensorType.Fan && value > metrics.GpuFanRpm)
        {
            metrics.GpuFanRpm = value;
            return;
        }

        if (sensor.SensorType == SensorType.Power && value > 0)
        {
            metrics.GpuPowerDrawWatt = Math.Max(metrics.GpuPowerDrawWatt, value);
        }
    }

    private static void ApplyHardwareMetrics(LauncherClientStatus status, HardwareMetrics metrics)
    {
        status.CpuName = metrics.CpuName;
        status.GpuName = metrics.GpuName;
        status.CpuTemperatureCelsius = RoundPositive(metrics.CpuTemperatureCelsius);
        status.CpuLoadPercent = RoundPositive(metrics.CpuLoadPercent);
        status.CpuClockMhz = RoundPositive(metrics.CpuClockMhz);
        status.CpuPowerDrawWatt = RoundPositive(metrics.CpuPowerDrawWatt);
        status.GpuTemperatureCelsius = RoundPositive(metrics.GpuTemperatureCelsius);
        status.GpuLoadPercent = RoundPositive(metrics.GpuLoadPercent);
        status.GpuFanRpm = RoundPositive(metrics.GpuFanRpm);
        status.GpuPowerDrawWatt = RoundPositive(metrics.GpuPowerDrawWatt);
    }

    private static double RoundPositive(double value)
    {
        return value <= 0 ? 0 : Math.Round(value, 1);
    }

    private static double TryReadWmiTemperature()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

            var temperatures = new List<double>();
            foreach (var item in searcher.Get().Cast<ManagementObject>())
            {
                if (item["CurrentTemperature"] is not uint rawTemperature)
                {
                    continue;
                }

                var celsius = (rawTemperature / 10d) - 273.15d;
                if (celsius is > 0 and < 125)
                {
                    temperatures.Add(celsius);
                }
            }

            if (temperatures.Count > 0)
            {
                return Math.Round(temperatures.Max(), 1);
            }
        }
        catch
        {
            // Some client machines do not expose thermal sensors through WMI.
        }

        return 0;
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

    private static string ResolvePreferredIpv4Address()
    {
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var ipProperties = adapter.GetIPProperties();
                foreach (var unicastAddress in ipProperties.UnicastAddresses)
                {
                    if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    var address = unicastAddress.Address.ToString();
                    if (string.IsNullOrWhiteSpace(address))
                    {
                        continue;
                    }

                    // Skip APIPA (169.254.x.x) which is usually not routable in LAN.
                    if (address.StartsWith("169.254.", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    return address;
                }
            }
        }
        catch
        {
            // Ignore adapter inspection failures.
        }

        return string.Empty;
    }

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

    private sealed class HardwareMetrics
    {
        public string CpuName { get; set; } = string.Empty;

        public string GpuName { get; set; } = string.Empty;

        public double CpuTemperatureCelsius { get; set; }

        public double CpuLoadPercent { get; set; }

        public double CpuClockMhz { get; set; }

        public double GpuTemperatureCelsius { get; set; }

        public double GpuLoadPercent { get; set; }

        public double CpuPowerDrawWatt { get; set; }

        public double GpuPowerDrawWatt { get; set; }

        public double GpuFanRpm { get; set; }

        public bool HasAnyValue =>
            !string.IsNullOrWhiteSpace(CpuName) ||
            !string.IsNullOrWhiteSpace(GpuName) ||
            CpuTemperatureCelsius > 0 ||
            CpuLoadPercent > 0 ||
            CpuClockMhz > 0 ||
            GpuTemperatureCelsius > 0 ||
            GpuLoadPercent > 0 ||
            GpuFanRpm > 0;
    }
}
