using System.IO.Compression;
using GameUpdater.Shared.Models;

namespace GameUpdater.Core.Services;

public sealed class BackupService
{
    private readonly AppEnvironment _appEnvironment;

    public BackupService(AppEnvironment appEnvironment)
    {
        _appEnvironment = appEnvironment;
    }

    public Task<string> CreateBackupFromFolderAsync(GameRecord game, string sourceFolder, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(sourceFolder))
            {
                throw new DirectoryNotFoundException($"Không tìm thấy thư mục bản vá: {sourceFolder}");
            }

            var relativePaths = Directory
                .EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories)
                .Select(filePath => Path.GetRelativePath(sourceFolder, filePath));

            return BackupExistingFiles(game, relativePaths, cancellationToken);
        }, cancellationToken);
    }

    public Task<string> CreateBackupFromZipAsync(GameRecord game, string zipPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException("Không tìm thấy tệp zip nguồn.", zipPath);
            }

            using var archive = ZipFile.OpenRead(zipPath);
            var relativePaths = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .Select(entry => entry.FullName.Replace('/', Path.DirectorySeparatorChar));

            return BackupExistingFiles(game, relativePaths, cancellationToken);
        }, cancellationToken);
    }

    private string BackupExistingFiles(GameRecord game, IEnumerable<string> relativePaths, CancellationToken cancellationToken)
    {
        var backupRoot = Path.Combine(
            _appEnvironment.BackupDirectory,
            ToSafeFolderName(game.Name),
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));

        Directory.CreateDirectory(backupRoot);

        foreach (var relativePath in relativePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedRelativePath = NormalizeRelativePath(relativePath);
            var targetPath = ResolveSafePath(game.InstallPath, normalizedRelativePath);

            if (!File.Exists(targetPath))
            {
                continue;
            }

            var backupFilePath = ResolveSafePath(backupRoot, normalizedRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);
            File.Copy(targetPath, backupFilePath, overwrite: true);
        }

        return backupRoot;
    }

    private static string ResolveSafePath(string root, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var candidatePath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));

        if (!candidatePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Phát hiện đường dẫn không an toàn: {relativePath}");
        }

        return candidatePath;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
    }

    private static string ToSafeFolderName(string value)
    {
        return string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    }
}
