using GameLauncher.Client.Controls;
using GameLauncher.Client.Models;
using GameLauncher.Client.Services;
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
    private string _currentCategory = "Tất cả";
    private string _lastAppliedCategory = string.Empty;
    private CancellationTokenSource? _prewarmCts;

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
            throw new InvalidOperationException("Chưa cấu hình đường dẫn danh mục trò chơi.");
        }

        var catalog = await _catalogService.LoadCatalogAsync(_catalogPath);
        _allRows = CatalogReaderService.BuildRows(catalog).ToList();
        PopulateCategories();
        await ApplyServerPolicyAsync(catalog.ClientPolicy);
        await SaveLauncherSettingsAsync();
        InitializeCards();
        ApplyFilter(force: true);
        StartBackgroundPrewarm();
        WriteClientStatusSafe();
        _statusHeartbeatTimer.Start();
    }

    private void PopulateCategories()
    {
        _categoriesPanel.SuspendLayout();
        _categoriesPanel.Controls.Clear();

        var categories = new List<string> { "Tất cả" };
        var uniqueCategories = _allRows
            .Select(r => r.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        categories.AddRange(uniqueCategories);

        foreach (var category in categories)
        {
            var btn = new Button
            {
                Text = category,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = category == _currentCategory ? Color.FromArgb(59, 130, 246) : Color.FromArgb(30, 41, 59),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 10f),
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(0, 0, 8, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (_, _) =>
            {
                if (string.Equals(_currentCategory, category, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _currentCategory = category;
                foreach (Button c in _categoriesPanel.Controls)
                {
                    c.BackColor = c.Text == _currentCategory ? Color.FromArgb(59, 130, 246) : Color.FromArgb(30, 41, 59);
                }

                ApplyFilter(force: true);
            };
            _categoriesPanel.Controls.Add(btn);
        }

        _categoriesPanel.ResumeLayout();
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

        _lastAppliedCategory = string.Empty;

        _hotCardsPanel.ResumeLayout();
        _normalCardsPanel.ResumeLayout();
    }

    private void ApplyFilter()
    {
        ApplyFilter(force: false);
    }

    private void ApplyFilter(bool force)
    {
        if (!force &&
            string.Equals(_lastAppliedCategory, _currentCategory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastAppliedCategory = _currentCategory;

        _hotCardsPanel.SuspendLayout();
        _normalCardsPanel.SuspendLayout();

        foreach (var card in _hotCards)
        {
            var isVisible = IsRowVisible(card.Row);
            if (card.Visible != isVisible)
            {
                card.Visible = isVisible;
            }
        }

        foreach (var card in _normalCards)
        {
            var isVisible = IsRowVisible(card.Row);
            if (card.Visible != isVisible)
            {
                card.Visible = isVisible;
            }
        }

        _hotCardsPanel.ResumeLayout();
        _normalCardsPanel.ResumeLayout();
    }

    private bool IsRowVisible(LauncherGameRow row)
    {
        return _currentCategory == "Tất cả" ||
               string.Equals(row.Category, _currentCategory, StringComparison.OrdinalIgnoreCase);
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

    private void StartBackgroundPrewarm()
    {
        CancelBackgroundPrewarm();
        if (_allRows.Count == 0)
        {
            return;
        }

        _prewarmCts = new CancellationTokenSource();
        var rowsSnapshot = _allRows.ToList();
        var cancellationToken = _prewarmCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await _prewarmService.PrewarmHotGamesAsync(rowsSnapshot, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation during shutdown/reload.
            }
            catch
            {
                // Prewarm is best-effort only.
            }
        }, cancellationToken);
    }

    private void CancelBackgroundPrewarm()
    {
        try
        {
            _prewarmCts?.Cancel();
            _prewarmCts?.Dispose();
        }
        catch
        {
            // Ignore cancellation/dispose failures.
        }
        finally
        {
            _prewarmCts = null;
        }
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
