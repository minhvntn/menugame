namespace GameUpdater.Shared.Models;

public sealed class UpdateProgressInfo
{
    public int Percent { get; init; }

    public string Message { get; init; } = string.Empty;

    public static UpdateProgressInfo Create(int percent, string message)
    {
        return new UpdateProgressInfo
        {
            Percent = Math.Clamp(percent, 0, 100),
            Message = message
        };
    }
}

