using System.Text;
using System.Text.Json;
using GameLauncher.Client.Models;

namespace GameLauncher.Client.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;

    public SettingsService(string appBasePath)
    {
        _settingsFilePath = Path.Combine(appBasePath, "launcher.settings.json");
    }

    public async Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new LauncherSettings();
        }

        var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken);
        return JsonSerializer.Deserialize<LauncherSettings>(json, JsonOptions) ?? new LauncherSettings();
    }

    public async Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(_settingsFilePath, json, Encoding.UTF8, cancellationToken);
    }
}

