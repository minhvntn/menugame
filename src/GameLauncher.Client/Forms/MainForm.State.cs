using GameLauncher.Client.Controls;
using GameLauncher.Client.Models;
using GameLauncher.Client.Services;
using GameUpdater.Shared.Localization;
using GameUpdater.Shared.Models;

namespace GameLauncher.Client.Forms;

public sealed partial class MainForm
{
    private List<LauncherGameRow> _allRows = new();
    private readonly List<GameCardControl> _hotCards = new();
    private readonly List<GameCardControl> _normalCards = new();
    private string _catalogPath = string.Empty;
    private string _currentGameName = string.Empty;
    private string _currentGameExecutablePath = string.Empty;

    private async Task LoadCatalogOnStartupAsync()
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            var settings = await _settingsService.LoadAsync();
            _catalogPath = ResolveCatalogPathWithPriority(settings.CatalogPath);
            await LoadCatalogAsync();
        });
    }

    private async Task LoadCatalogAsync()
    {
        if (string.IsNullOrWhiteSpace(_catalogPath))
        {
            throw new InvalidOperationException(I18n.Launcher.MissingCatalogPath);
        }

        var catalog = await _catalogService.LoadCatalogAsync(_catalogPath);
        _allRows = CatalogReaderService.BuildRows(catalog).ToList();
        await ApplyServerPolicyAsync(catalog.ClientPolicy);
        await SaveLauncherSettingsAsync();
        InitializeCards();
        WriteClientStatusSafe();
        _statusHeartbeatTimer.Start();
    }

    private void InitializeCards()
    {
        _hotCardsPanel.SuspendLayout();
        _normalCardsPanel.SuspendLayout();

        foreach (Control control in _hotCardsPanel.Controls)
        {
            control.Dispose();
        }

        foreach (Control control in _normalCardsPanel.Controls)
        {
            control.Dispose();
        }

        _hotCardsPanel.Controls.Clear();
        _normalCardsPanel.Controls.Clear();
        _hotCards.Clear();
        _normalCards.Clear();

        var hotRows = _allRows
            .Where(r => r.IsHot)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var hotControls = hotRows
            .Select(row => new GameCardControl(row, PlayGame, isHotRow: true, ThemeFontFamily))
            .ToArray();
        _hotCards.AddRange(hotControls);
        _hotCardsPanel.Controls.AddRange(hotControls);

        var normalRows = _allRows
            .Where(r => !r.IsHot)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var normalControls = normalRows
            .Select(row => new GameCardControl(row, PlayGame, isHotRow: false, ThemeFontFamily))
            .ToArray();
        _normalCards.AddRange(normalControls);
        _normalCardsPanel.Controls.AddRange(normalControls);

        _hotCardsPanel.ResumeLayout();
        _normalCardsPanel.ResumeLayout();
    }

    private void PlayGame(LauncherGameRow row)
    {
        _ = ExecuteWithErrorHandlingAsync(async () =>
        {
            var process = _launchService.Launch(row);
            _currentGameName = row.Name;
            _currentGameExecutablePath = string.IsNullOrWhiteSpace(_launchService.LastLaunchedExecutablePath)
                ? row.ResolvedExecutablePath
                : _launchService.LastLaunchedExecutablePath;
            WriteClientStatusSafe();
            _ = Task.Run(() =>
            {
                try
                {
                    process.WaitForExit();
                }
                catch
                {
                    // Ignore process tracking failures.
                }

                _currentGameName = string.Empty;
                _currentGameExecutablePath = string.Empty;
                WriteClientStatusSafe();
            });
            SendLauncherToDesktop();
            await Task.CompletedTask;
        });
    }

    private async Task SaveLauncherSettingsAsync()
    {
        await _settingsService.SaveAsync(new LauncherSettings
        {
            CatalogPath = _catalogPath,
            BackgroundImagePath = string.Empty
        });
    }

    private static string ResolveCatalogPathWithPriority(string? configuredCatalogPath)
    {
        var sameFolderJson = Path.Combine(AppContext.BaseDirectory, "games.catalog.json");
        var sameFolderLegacy = Path.Combine(AppContext.BaseDirectory, "games.catalog");
        var trimmedConfiguredPath = configuredCatalogPath?.Trim() ?? string.Empty;

        var candidates = new List<string>
        {
            sameFolderJson,
            sameFolderLegacy
        };

        if (!string.IsNullOrWhiteSpace(trimmedConfiguredPath))
        {
            candidates.Add(trimmedConfiguredPath);
        }

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "..", "games.catalog.json"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "..", "server", "games.catalog.json"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "..", "..", "server", "games.catalog.json"));

        foreach (var candidate in candidates)
        {
            try
            {
                if (candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }

                var fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch
            {
                // Skip invalid candidate path and continue.
            }
        }

        if (!string.IsNullOrWhiteSpace(trimmedConfiguredPath))
        {
            return trimmedConfiguredPath;
        }

        return Path.GetFullPath(sameFolderJson);
    }
}


