namespace GameUpdater.Shared.Models;

public sealed class ManifestFileEntry
{
    public string RelativePath { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public DateTime LastWriteTimeUtc { get; set; }
}

