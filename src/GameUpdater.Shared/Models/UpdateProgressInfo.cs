namespace GameUpdater.Shared.Models;

public sealed class UpdateProgressInfo
{
    public int Percent { get; init; }

    public string Message { get; init; } = string.Empty;

    public long? TotalBytes { get; init; }

    public long? ProcessedBytes { get; init; }

    public double? SpeedMbps { get; init; }

    public static UpdateProgressInfo Create(
        int percent,
        string message,
        long? totalBytes = null,
        long? processedBytes = null,
        double? speedMbps = null)
    {
        return new UpdateProgressInfo
        {
            Percent = Math.Clamp(percent, 0, 100),
            Message = message,
            TotalBytes = totalBytes,
            ProcessedBytes = processedBytes,
            SpeedMbps = speedMbps
        };
    }
}
