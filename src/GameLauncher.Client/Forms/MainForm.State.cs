using GameLauncher.Client.Controls;
using GameLauncher.Client.Models;
using GameLauncher.Client.Services;
using GameUpdater.Shared.Localization;
using GameUpdater.Shared.Models;

namespace GameLauncher.Client.Forms;

public sealed partial class MainForm
{
    private static readonly string DefaultCategoryLabel = I18n.Launcher.DefaultCategory;
    private const string HotCategoryLabel = "Hot";
    private static readonly string[] SidebarCategoryLabels =
    {
        "Online",
        "Offline",
        "Tools"
    };

    private List<LauncherGameRow> _allRows = new();
    private readonly List<GameCardControl> _hotCards = new();
    private readonly List<GameCardControl> _normalCards = new();
    private string _catalogPath = string.Empty;
    private string _currentGameName = string.Empty;
    private string _currentGameExecutablePath = string.Empty;
    private string _selectedCategory = DefaultCategoryLabel;
    private bool _sortAscending = true;

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
        BuildCategoryButtons(_allRows
            .Select(row => row.Category?.Trim() ?? string.Empty)
            .Where(category => !string.IsNullOrWhiteSpace(category)));
        ApplyFiltersAndRenderCards();
    }

    private void BuildCategoryButtons(IEnumerable<string> categories)
    {
        _categoryListPanel.SuspendLayout();

        foreach (Control control in _categoryListPanel.Controls)
        {
            control.Dispose();
        }

        _categoryListPanel.Controls.Clear();
        _categoryButtons.Clear();

        var categoryList = new List<string> { DefaultCategoryLabel };
        categoryList.AddRange(SidebarCategoryLabels);

        foreach (var category in categoryList)
        {
            var button = CreateCategoryButton(category);
            button.Click += (_, _) =>
            {
                _selectedCategory = category;
                UpdateCategoryButtonStyles();
                ApplyFiltersAndRenderCards();
            };
            _categoryButtons[category] = button;
            _categoryListPanel.Controls.Add(button);
        }

        _selectedCategory = _categoryButtons.ContainsKey(_selectedCategory)
            ? _selectedCategory
            : DefaultCategoryLabel;
        UpdateCategoryButtonStyles();
        _categoryListPanel.ResumeLayout();
    }

    private void UpdateCategoryButtonStyles()
    {
        foreach (var (category, button) in _categoryButtons)
        {
            button.Invalidate();
        }
    }

    private void ToggleSortOrder()
    {
        _sortAscending = !_sortAscending;
        ApplyFiltersAndRenderCards();
    }

    private void ApplyFiltersAndRenderCards()
    {
        _hotCardsPanel.SuspendLayout();
        _normalCardsPanel.SuspendLayout();

        ClearAndDisposePanelControls(_hotCardsPanel);
        ClearAndDisposePanelControls(_normalCardsPanel);
        _hotCards.Clear();
        _normalCards.Clear();

        var filteredRows = _allRows
            .Where(row => MatchesCategory(row))
            .Where(row => MatchesSearch(row))
            .ToList();

        var hotRows = SortRows(filteredRows.Where(row => row.IsHot)).ToList();
        var normalRows = SortRows(filteredRows.Where(row => !row.IsHot)).ToList();

        var hotControls = hotRows
            .Select(row => new GameCardControl(row, PlayGame, isHotRow: true, ThemeFontFamily))
            .ToArray();
        var normalControls = normalRows
            .Select(row => new GameCardControl(row, PlayGame, isHotRow: false, ThemeFontFamily))
            .ToArray();

        _hotCards.AddRange(hotControls);
        _normalCards.AddRange(normalControls);
        
        _slideTimer.Stop();
        _slideTargetLeft = 0;
        _slideStartLeft = 0;
        _slideProgress = 0f;
        _hotCardsPanel.Left = 0;
        
        _hotCardsPanel.Controls.AddRange(hotControls);
        _normalCardsPanel.Controls.AddRange(normalControls);

        if (_hotCardsPanel.Controls.Count == 0)
        {
            _hotCardsPanel.Controls.Add(CreateEmptyStateLabel("Khong co game noi bat."));
        }

        UpdateCarouselButtonsVisibility();

        if (_normalCardsPanel.Controls.Count == 0)
        {
            _normalCardsPanel.Controls.Add(CreateEmptyStateLabel("Khong co game phu hop."));
        }

        var sortText = _sortAscending 
            ? "S\u1eafp x\u1ebfp: A \u2192 Z  \u02C4" 
            : "S\u1eafp x\u1ebfp: Z \u2192 A  \u02C5";
        _hotSortLabel.Text = sortText;
        _allSortLabel.Text = sortText;

        _hotCardsPanel.ResumeLayout();
        _normalCardsPanel.ResumeLayout();
    }

    private IEnumerable<LauncherGameRow> SortRows(IEnumerable<LauncherGameRow> rows)
    {
        // Keep sorting stable by sort order after the user-visible name sort.
        return _sortAscending
            ? rows.OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.SortOrder)
            : rows.OrderByDescending(row => row.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.SortOrder);
    }

    private bool MatchesCategory(LauncherGameRow row)
    {
        if (string.Equals(_selectedCategory, DefaultCategoryLabel, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(_selectedCategory, HotCategoryLabel, StringComparison.OrdinalIgnoreCase))
        {
            return row.IsHot;
        }

        if (string.Equals(_selectedCategory, "Tools", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(row.Category, "Tools", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(row.Category, "Tool", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(row.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesSearch(LauncherGameRow row)
    {
        var query = _searchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               (row.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private Label CreateEmptyStateLabel(string message)
    {
        return new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(161, 194, 245),
            Font = new Font("Segoe UI", 10f, FontStyle.Italic),
            Text = message,
            Margin = new Padding(8, 8, 8, 8)
        };
    }

    private static void ClearAndDisposePanelControls(FlowLayoutPanel panel)
    {
        foreach (Control control in panel.Controls)
        {
            control.Dispose();
        }

        panel.Controls.Clear();
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
