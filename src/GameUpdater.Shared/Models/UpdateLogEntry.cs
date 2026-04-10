namespace GameUpdater.Shared.Models;

public sealed class UpdateLogEntry
{
    public int Id { get; set; }

    public int? GameId { get; set; }

    public string GameName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
