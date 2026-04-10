namespace GameUpdater.Shared.Models;

public sealed class UpdateRequest
{
    public GameRecord Game { get; set; } = new();

    public string SourcePath { get; set; } = string.Empty;

    public string TargetVersion { get; set; } = string.Empty;

    public UpdateSourceKind SourceKind { get; set; } = UpdateSourceKind.Folder;

    public bool CreateBackup { get; set; } = true;
}

