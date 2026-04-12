using System.ComponentModel;
using System.Text;
using System.Text.Json;
using GameUpdater.Core.Abstractions;
using GameUpdater.Core.Services;
using GameUpdater.Shared.Models;

namespace GameUpdater.WinForms.Forms;

public sealed class MainForm : Form
{
    private const string DownloadProgressColumnName = "downloadProgressColumn";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly GameService _gameService;
    private readonly UpdateService _updateService;
    private readonly CatalogService _catalogService;
    private readonly ResourceSyncService _resourceSyncService;
    private readonly ILogRepository _logRepository;

    private readonly BindingSource _gamesBinding = new();
    private readonly BindingSource _logsBinding = new();
    private readonly BindingSource _resourcesBinding = new();
    private readonly BindingSource _downloadMonitorBinding = new();

    private readonly DataGridView _gamesGrid = new();
    private readonly DataGridView _resourcesGrid = new();
    private readonly DataGridView _downloadMonitorGrid = new();
    private readonly DataGridView _logsGrid = new();
    private readonly TextBox _updateSourceTextBox = new();
    private readonly TextBox _updateVersionTextBox = new();
    private readonly TextBox _updateOutputTextBox = new();
    private readonly ComboBox _updateSourceKindComboBox = new();
    private readonly ComboBox _updateGameComboBox = new();
    private readonly ComboBox _fontSizeComboBox = new();
    private readonly TextBox _clientWallpaperPathTextBox = new();
    private readonly CheckBox _enableClientCloseAppHotKeyCheckBox = new();
    private readonly Button _browseClientWallpaperButton = new();
    private readonly Button _clearClientWallpaperButton = new();
    private readonly Button _saveSettingsButton = new();
    private readonly CheckBox _backupCheckBox = new();
    private readonly Label _resourceSummaryLabel = new();
    private readonly TextBox _resourceSourceRootTextBox = new();
    private readonly TextBox _resourceTargetRootTextBox = new();
    private readonly NumericUpDown _resourceBandwidthLimitNumeric = new();
    private readonly ProgressBar _updateProgressBar = new();
    private readonly Button _applyUpdateButton = new();
    private readonly Button _browseSourceButton = new();
    private readonly Button _scanManifestButton = new();
    private readonly TreeView _resourceTree = new();
    private SplitContainer? _resourcesSplitContainer;
    private readonly Button _browseResourceSourceButton = new();
    private readonly Button _browseResourceTargetButton = new();
    private readonly Button _saveResourceSettingsButton = new();
    private readonly Button _syncSelectedResourceButton = new();

    private readonly BindingList<DownloadMonitorRow> _downloadMonitorRows = new();
    private readonly List<ResourceGameRow> _allResourceRows = new();
    private readonly Dictionary<DownloadMonitorRow, ResourceSyncTaskControl> _activeResourceSyncControls = new();
    private readonly ContextMenuStrip _resourcesContextMenu = new();
    private readonly ToolStripMenuItem _downloadSelectedResourcesMenuItem = new("Tải mục đã chọn");
    private readonly ToolStripMenuItem _pauseSelectedResourcesMenuItem = new("Tạm dừng tải");
    private readonly ToolStripMenuItem _resumeSelectedResourcesMenuItem = new("Tiếp tục tải");
    private readonly ToolStripMenuItem _stopSelectedResourcesMenuItem = new("Dừng tải");
    private readonly ToolStripMenuItem _setResourceBandwidthMenuItem = new("Giới hạn băng thông");
    private readonly ToolStripMenuItem _retrySelectedResourcesMenuItem = new("Tải lại từ IDC");
    private readonly List<ToolStripMenuItem> _resourceBandwidthPresetMenuItems = new();
    private readonly ToolStripMenuItem _syncMissingFromIdcMenuItem = new("Đồng bộ file thiếu từ IDC");
    private readonly ContextMenuStrip _gamesContextMenu = new();
    private readonly ToolStripMenuItem _addGameMenuItem = new("Thêm");
    private readonly ToolStripMenuItem _editGameMenuItem = new("Sửa");
    private readonly ToolStripMenuItem _deleteGameMenuItem = new("Xóa");
    private readonly ToolStripMenuItem _viewManifestMenuItem = new("Xem manifest");
    private readonly ContextMenuStrip _downloadMonitorContextMenu = new();
    private readonly ToolStripMenuItem _pauseDownloadMenuItem = new("Tạm dừng");
    private readonly ToolStripMenuItem _resumeDownloadMenuItem = new("Tiếp tục");
    private readonly ToolStripMenuItem _pauseAllDownloadsMenuItem = new("Tạm dừng tất cả");
    private readonly ToolStripMenuItem _resumeAllDownloadsMenuItem = new("Tiếp tục tất cả");
    private readonly ToolStripMenuItem _stopDownloadMenuItem = new("Dừng tải");
    private readonly ToolStripMenuItem _setDownloadBandwidthMenuItem = new("Giới hạn băng thông");
    private readonly ToolStripMenuItem _retryDownloadFromIdcMenuItem = new("Tải lại từ IDC");
    private readonly ToolStripMenuItem _removeDownloadMenuItem = new("Xóa dòng");
    private readonly ToolStripMenuItem _removeFinishedDownloadsMenuItem = new("Xóa tác vụ đã xong");
    private readonly List<ToolStripMenuItem> _downloadBandwidthPresetMenuItems = new();
    private bool _downloadMonitorContextMenuInitialized;
    private bool _resourcesContextMenuInitialized;
    private bool _gamesContextMenuInitialized;

    private string _autoCatalogPath = string.Empty;
    private string _resourceSourceRootPath = @"E:\GameOnlineIDC";
    private string _resourceTargetRootPath = @"E:\GameOnline";
    private int _resourceBandwidthLimitMbps;
    private string _clientWindowsWallpaperPath = string.Empty;
    private bool _enableClientCloseApplicationHotKey = true;
    private UiFontSizeMode _uiFontSizeMode = UiFontSizeMode.Normal;
    private bool _isUpdatingFontSizeSelection;
    private readonly string _settingsFilePath;
    private ResourceFilterKind _currentResourceFilter = ResourceFilterKind.All;

    public MainForm(
        GameService gameService,
        UpdateService updateService,
        ResourceSyncService resourceSyncService,
        CatalogService catalogService,
        ILogRepository logRepository)
    {
        _gameService = gameService;
        _updateService = updateService;
        _resourceSyncService = resourceSyncService;
        _catalogService = catalogService;
        _logRepository = logRepository;

        _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "data", "server.ui.settings.json");
        _autoCatalogPath = Path.Combine(AppContext.BaseDirectory, "games.catalog.json");

        Text = "Quản lý cập nhật trò chơi";
        Width = 1280;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;

        _gamesBinding.CurrentChanged += GamesBinding_CurrentChanged;
        _downloadMonitorBinding.DataSource = _downloadMonitorRows;

        BuildLayout();
        ApplyUiFontSize(_uiFontSizeMode);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyResourcesSplitDistance();
        await LoadUiSettingsAsync();
        await ReloadAllAsync();
        ApplyResourcesSplitDistance();
    }

    private async void GamesBinding_CurrentChanged(object? sender, EventArgs e)
    {
        await RefreshSelectedGameDetailsAsync();
    }

    private async Task ReloadAllAsync(int? selectedGameId = null)
    {
        await LoadGamesAsync(selectedGameId);
        await LoadLogsAsync();
        await RefreshSelectedGameDetailsAsync();
    }

    private async Task LoadGamesAsync(int? selectedGameId = null)
    {
        var games = (await _gameService.GetGamesAsync()).ToList();
        _gamesBinding.DataSource = games;
        await RebuildResourceRowsAsync(games);

        if (games.Count == 0)
        {
            return;
        }

        if (selectedGameId.HasValue)
        {
            var matchIndex = games.FindIndex(game => game.Id == selectedGameId.Value);
            if (matchIndex >= 0)
            {
                _gamesBinding.Position = matchIndex;
            }
        }
    }

    private async Task LoadLogsAsync()
    {
        var logs = (await _logRepository.GetRecentAsync()).ToList();
        _logsBinding.DataSource = logs;
    }

    private Task RefreshSelectedGameDetailsAsync()
    {
        if (SelectedGame is null)
        {
            _updateVersionTextBox.Text = string.Empty;
            return Task.CompletedTask;
        }

        _updateVersionTextBox.Text = SelectedGame.Version;
        return Task.CompletedTask;
    }

    private GameRecord? SelectedGame => _gamesBinding.Current as GameRecord;

    private void BuildLayout()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill
        };

        tabs.TabPages.Add(BuildGamesTab());
        tabs.TabPages.Add(BuildResourcesTab());
        tabs.TabPages.Add(BuildUpdateTab());
        tabs.TabPages.Add(BuildLogsTab());
        tabs.TabPages.Add(BuildSettingsTab());

        Controls.Add(tabs);
    }

    private TabPage BuildGamesTab()
    {
        var page = new TabPage("Trò chơi");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            WrapContents = false
        };


        _scanManifestButton.Text = "Quét manifest";
        _scanManifestButton.AutoSize = true;
        _scanManifestButton.Click += ScanManifestButton_Click;
        toolbar.Controls.Add(_scanManifestButton);

        toolbar.Controls.Add(CreateButton("Xuất danh mục client", ExportCatalogButton_Click));
        toolbar.Controls.Add(CreateButton("Làm mới", RefreshButton_Click));

        ConfigureGamesGrid();
        EnsureGamesContextMenu();
        leftPanel.Controls.Add(toolbar, 0, 0);
        leftPanel.Controls.Add(_gamesGrid, 0, 1);

        root.Controls.Add(leftPanel, 0, 0);

        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildResourcesTab()
    {
        var page = new TabPage("Tài nguyên");

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            SplitterWidth = 6
        };
        _resourcesSplitContainer = split;
        split.SizeChanged += (_, _) => ApplyResourcesSplitDistance();

        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var leftToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            WrapContents = false
        };
        leftToolbar.Controls.Add(CreateButton("Làm mới tài nguyên", RefreshResourcesButton_Click));

        BuildResourceTree();
        _resourceTree.Dock = DockStyle.Fill;

        leftPanel.Controls.Add(leftToolbar, 0, 0);
        leftPanel.Controls.Add(_resourceTree, 0, 1);
        split.Panel1.Controls.Add(leftPanel);

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 6
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _resourceSourceRootTextBox.Dock = DockStyle.Fill;
        _resourceSourceRootTextBox.Text = _resourceSourceRootPath;
        _resourceSourceRootTextBox.TextChanged += (_, _) => _resourceSourceRootPath = _resourceSourceRootTextBox.Text.Trim();
        _resourceTargetRootTextBox.Dock = DockStyle.Fill;
        _resourceTargetRootTextBox.Text = _resourceTargetRootPath;
        _resourceTargetRootTextBox.TextChanged += (_, _) => _resourceTargetRootPath = _resourceTargetRootTextBox.Text.Trim();

        _resourceBandwidthLimitNumeric.Dock = DockStyle.Left;
        _resourceBandwidthLimitNumeric.Width = 120;
        _resourceBandwidthLimitNumeric.Minimum = 0;
        _resourceBandwidthLimitNumeric.Maximum = 10000;
        _resourceBandwidthLimitNumeric.DecimalPlaces = 0;
        _resourceBandwidthLimitNumeric.Value = _resourceBandwidthLimitMbps;
        _resourceBandwidthLimitNumeric.ValueChanged += (_, _) => _resourceBandwidthLimitMbps = Decimal.ToInt32(_resourceBandwidthLimitNumeric.Value);

        _browseResourceSourceButton.Text = "...";
        _browseResourceSourceButton.Width = 36;
        _browseResourceSourceButton.Click += BrowseResourceSourceButton_Click;

        _browseResourceTargetButton.Text = "...";
        _browseResourceTargetButton.Width = 36;
        _browseResourceTargetButton.Click += BrowseResourceTargetButton_Click;

        var sourceRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 4, 0, 0)
        };
        sourceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        sourceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sourceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        sourceRow.Controls.Add(CreateFieldLabel("Nguồn IDC"), 0, 0);
        sourceRow.Controls.Add(_resourceSourceRootTextBox, 1, 0);
        sourceRow.Controls.Add(_browseResourceSourceButton, 2, 0);

        var targetRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 2, 0, 0)
        };
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        targetRow.Controls.Add(CreateFieldLabel("Đích máy chủ"), 0, 0);
        targetRow.Controls.Add(_resourceTargetRootTextBox, 1, 0);
        targetRow.Controls.Add(_browseResourceTargetButton, 2, 0);

        var bandwidthRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 2, 0, 0)
        };
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bandwidthRow.Controls.Add(CreateFieldLabel("Giới hạn MB/s"), 0, 0);
        bandwidthRow.Controls.Add(_resourceBandwidthLimitNumeric, 1, 0);
        bandwidthRow.Controls.Add(CreateFieldLabel("0 = không giới hạn"), 2, 0);

        var actionsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 4, 0, 0),
            WrapContents = false
        };
        _saveResourceSettingsButton.Text = "Lưu cấu hình";
        _saveResourceSettingsButton.AutoSize = true;
        _saveResourceSettingsButton.Click += SaveResourceSettingsButton_Click;

        _syncSelectedResourceButton.Text = "Tải trò chơi đã chọn";
        _syncSelectedResourceButton.AutoSize = true;
        _syncSelectedResourceButton.Click += SyncSelectedResourceButton_Click;


        actionsRow.Controls.Add(_saveResourceSettingsButton);
        actionsRow.Controls.Add(_syncSelectedResourceButton);

        _resourceSummaryLabel.Dock = DockStyle.Fill;
        _resourceSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _resourceSummaryLabel.Text = "Đang tải dữ liệu tài nguyên...";

        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill
        };

        ConfigureResourcesGrid();
        ConfigureDownloadMonitorGrid();
        EnsureResourcesContextMenu();
        EnsureDownloadMonitorContextMenu();

        _resourcesGrid.Dock = DockStyle.Fill;
        _downloadMonitorGrid.Dock = DockStyle.Fill;
        _downloadMonitorGrid.Visible = false;

        contentPanel.Controls.Add(_downloadMonitorGrid);
        contentPanel.Controls.Add(_resourcesGrid);

        rightPanel.Controls.Add(sourceRow, 0, 0);
        rightPanel.Controls.Add(targetRow, 0, 1);
        rightPanel.Controls.Add(bandwidthRow, 0, 2);
        rightPanel.Controls.Add(actionsRow, 0, 3);
        rightPanel.Controls.Add(_resourceSummaryLabel, 0, 4);
        rightPanel.Controls.Add(contentPanel, 0, 5);
        split.Panel2.Controls.Add(rightPanel);

        page.Controls.Add(split);
        return page;
    }

    private void BuildResourceTree()
    {
        _resourceTree.AfterSelect -= ResourceTree_AfterSelect;
        _resourceTree.Nodes.Clear();

        var resourceRoot = new TreeNode("Tải tài nguyên")
        {
            Tag = ResourceFilterKind.All
        };
        resourceRoot.Nodes.Add(new TreeNode("Trò chơi chưa tải")
        {
            Tag = ResourceFilterKind.Missing
        });
        resourceRoot.Nodes.Add(new TreeNode("Trò chơi đã tải")
        {
            Tag = ResourceFilterKind.Downloaded
        });

        var monitorRoot = new TreeNode("Trung tâm giám sát")
        {
            Tag = ResourceFilterKind.DownloadMonitor
        };
        monitorRoot.Nodes.Add(new TreeNode("Tải xuống máy chủ")
        {
            Tag = ResourceFilterKind.DownloadMonitor
        });

        _resourceTree.Nodes.Add(resourceRoot);
        _resourceTree.Nodes.Add(monitorRoot);
        _resourceTree.ExpandAll();

        _resourceTree.AfterSelect += ResourceTree_AfterSelect;
        _resourceTree.SelectedNode = resourceRoot;
    }

    private void ResourceTree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is ResourceFilterKind filterKind)
        {
            ApplyResourceFilter(filterKind);
        }
    }

    private async void RefreshResourcesButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            UpdateResourceRootsFromInputs();
            await ReloadAllAsync(SelectedGame?.Id);
        });
    }

    private void BrowseResourceSourceButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Chọn thư mục nguồn tài nguyên (IDC).",
            UseDescriptionForTitle = true,
            SelectedPath = _resourceSourceRootTextBox.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _resourceSourceRootTextBox.Text = dialog.SelectedPath;
        }
    }

    private void BrowseResourceTargetButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Chọn thư mục đích trên máy chủ.",
            UseDescriptionForTitle = true,
            SelectedPath = _resourceTargetRootTextBox.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _resourceTargetRootTextBox.Text = dialog.SelectedPath;
        }
    }

    private async void SaveResourceSettingsButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            UpdateResourceRootsFromInputs();
            await SaveUiSettingsAsync();
            await ReloadAllAsync(SelectedGame?.Id);
            ShowInfo("Đã lưu cấu hình nguồn/đích tài nguyên.");
        });
    }

private async void SyncSelectedResourceButton_Click(object? sender, EventArgs e)
{
    if (_resourcesGrid.Visible == false)
    {
        ShowInfo("Vui lòng chuyển sang danh sách tài nguyên để chọn trò chơi.");
        return;
    }

    var selectedRows = GetSelectedOrCurrentResourceRows()
        .Where(row => row.HasSource)
        .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (selectedRows.Count == 0)
    {
        ShowInfo("Vui lòng chọn trò chơi có nguồn IDC để tải.");
        return;
    }

    await RunResourceSyncForRowsAsync(selectedRows, ResourceSyncMode.Incremental);
}

    private async Task RunResourceSyncForRowsAsync(
        IReadOnlyList<ResourceGameRow> rows,
        ResourceSyncMode syncMode)
    {
        if (rows.Count == 0)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ToggleResourceSyncControls(false);
            UpdateResourceRootsFromInputs();
            await SaveUiSettingsAsync();

            int? selectedGameId = SelectedGame?.Id;
            foreach (var row in rows)
            {
                if (FindActiveMonitorRowForResource(row) is not null)
                {
                    AppendUpdateMessage($"Bỏ qua {row.Name}: đang có tác vụ tải chạy.");
                    continue;
                }

                try
                {
                    var gameId = await SyncResourceRowAsync(row, syncMode);
                    if (gameId.HasValue)
                    {
                        selectedGameId = gameId.Value;
                    }
                }
                catch (OperationCanceledException)
                {
                    AppendUpdateMessage($"Đã dừng tác vụ tải tài nguyên của {row.Name} theo yêu cầu.");
                    break;
                }
            }

            await AutoExportCatalogAsync();
            await ReloadAllAsync(selectedGameId);
        }, () => ToggleResourceSyncControls(true));
    }

    private async Task SyncGameFromResourceLegacyAsync(GameRecord game)
    {
        var monitorRow = StartDownloadMonitor(game.Name, game.Id > 0 ? game.Id : null, resourceKey: ResolveSourceKeyForGame(game));
        var syncControl = new ResourceSyncTaskControl();
        var syncMode = ResourceSyncMode.Incremental;
        var actionName = "Đồng bộ tài nguyên";

        try
        {
            var progress = new Progress<UpdateProgressInfo>(info =>
            {
                if (syncControl.IsPaused)
                {
                    return;
                }
                if (syncControl.IsPaused)
                {
                    return;
                }

                UpdateDownloadMonitor(monitorRow, info.Percent, "Đang tải", info.Message, info);
            });

            var result = await _resourceSyncService.SyncGameAsync(
                game,
                _resourceSourceRootPath,
                _resourceTargetRootPath,
                progress);

            var successMessage = syncMode == ResourceSyncMode.MissingOnly
                ? $"Đồng bộ file thiếu {game.Name}: sao chép {result.CopiedFiles}/{result.TotalFiles} tệp."
                : $"Đã tải {game.Name}: sao chép {result.CopiedFiles}/{result.TotalFiles} tệp từ {result.SourcePath} về {result.TargetPath}.";

            UpdateDownloadMonitor(monitorRow, 100, "Hoàn tất", successMessage);
            AppendUpdateMessage(successMessage);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = actionName,
                Status = "Thành công",
                Message = successMessage,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception exception)
        {
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Thất bại", exception.Message);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = "Đồng bộ tài nguyên",
                Status = "Thất bại",
                Message = exception.Message,
                CreatedAt = DateTime.UtcNow
            });

            throw;
        }
    }

    private async Task SyncGameFromResourceAsync(GameRecord game, ResourceSyncMode syncMode = ResourceSyncMode.Incremental)
    {
        var monitorRow = StartDownloadMonitor(game.Name, game.Id > 0 ? game.Id : null, resourceKey: ResolveSourceKeyForGame(game));
        var syncControl = new ResourceSyncTaskControl();
        syncControl.SetBandwidthLimitMbps(_resourceBandwidthLimitMbps);
        RegisterResourceSyncToken(monitorRow, syncControl);
        var actionName = syncMode == ResourceSyncMode.MissingOnly ? "Đồng bộ file thiếu IDC" : "Đồng bộ tài nguyên";

        try
        {
            var progress = new Progress<UpdateProgressInfo>(info =>
            {
                if (syncControl.IsPaused)
                {
                    return;
                }

                UpdateDownloadMonitor(monitorRow, info.Percent, "Đang tải", info.Message, info);
            });

            var result = await Task.Run(
                () => _resourceSyncService.SyncGameAsync(
                    game,
                    _resourceSourceRootPath,
                    _resourceTargetRootPath,
                    progress,
                    maxBytesPerSecond: null,
                    waitIfPausedAsync: syncControl.WaitIfPausedAsync,
                    syncMode: syncMode,
                    cancellationToken: syncControl.CancellationToken,
                    getMaxBytesPerSecond: () => syncControl.BandwidthLimitBytesPerSecond),
                syncControl.CancellationToken);

            var successMessage = syncMode == ResourceSyncMode.MissingOnly
                ? $"Đồng bộ file thiếu {game.Name}: sao chép {result.CopiedFiles}/{result.TotalFiles} tệp."
                : $"Đã tải {game.Name}: sao chép {result.CopiedFiles}/{result.TotalFiles} tệp từ {result.SourcePath} về {result.TargetPath}.";

            UpdateDownloadMonitor(monitorRow, 100, "Hoàn tất", successMessage);
            AppendUpdateMessage(successMessage);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = actionName,
                Status = "Thành công",
                Message = successMessage,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (OperationCanceledException)
        {
            var canceledMessage = $"Đã dừng tải {game.Name} theo yêu cầu.";
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Đã dừng", canceledMessage);
            AppendUpdateMessage(canceledMessage);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = actionName,
                Status = "Đã dừng",
                Message = canceledMessage,
                CreatedAt = DateTime.UtcNow
            });

            throw;
        }
        catch (Exception exception)
        {
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Thất bại", exception.Message);

            await _logRepository.AddAsync(new UpdateLogEntry
            {
                GameId = game.Id > 0 ? game.Id : null,
                GameName = game.Name,
                Action = actionName,
                Status = "Thất bại",
                Message = exception.Message,
                CreatedAt = DateTime.UtcNow
            });

            throw;
        }
        finally
        {
            UnregisterResourceSyncToken(monitorRow);
        }
    }

    private GameRecord? FindGameById(int gameId)
    {
        var games = (_gamesBinding.DataSource as IEnumerable<GameRecord>)?.ToList();
        return games?.FirstOrDefault(game => game.Id == gameId);
    }

    private GameRecord? FindGameByInstallPath(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(installPath);
        var games = (_gamesBinding.DataSource as IEnumerable<GameRecord>)?.ToList();
        return games?.FirstOrDefault(game =>
        {
            if (string.IsNullOrWhiteSpace(game.InstallPath))
            {
                return false;
            }

            var gameInstallPath = Path.GetFullPath(game.InstallPath);
            return string.Equals(gameInstallPath, fullPath, StringComparison.OrdinalIgnoreCase);
        });
    }

    private GameRecord BuildTransientGameRecordFromResourceRow(ResourceGameRow row)
    {
        return new GameRecord
        {
            Id = 0,
            Name = row.Name,
            Category = string.IsNullOrWhiteSpace(row.Category) ? "IDC" : row.Category,
            InstallPath = row.InstallPath,
            Version = "1.0.0",
            LaunchRelativePath = string.Empty,
            LaunchArguments = string.Empty,
            Notes = "Tạo tự động từ nguồn IDC"
        };
    }

    private async Task<int?> SyncResourceRowAsync(ResourceGameRow row, ResourceSyncMode syncMode = ResourceSyncMode.Incremental)
    {
        if (!await ConfirmDiskSpaceForResourceSyncAsync(row))
        {
            AppendUpdateMessage($"Bỏ qua tải {row.Name}: không đủ dung lượng trống.");
            return null;
        }

        var existingGame = row.ManagedGameId.HasValue
            ? FindGameById(row.ManagedGameId.Value)
            : FindGameByInstallPath(row.InstallPath);

        var game = existingGame ?? BuildTransientGameRecordFromResourceRow(row);
        await SyncGameFromResourceAsync(game, syncMode);
        return await EnsureManagedGameRegistrationAsync(game, row);
    }

    private async Task<bool> ConfirmDiskSpaceForResourceSyncAsync(ResourceGameRow row)
    {
        if (!row.HasSource ||
            string.IsNullOrWhiteSpace(row.SourcePath) ||
            string.IsNullOrWhiteSpace(row.InstallPath) ||
            !Directory.Exists(row.SourcePath))
        {
            return true;
        }

        var estimate = await Task.Run(() => TryEstimateRequiredDiskSpace(row.SourcePath, row.InstallPath));
        if (estimate is null)
        {
            return true;
        }

        var (requiredAdditionalBytes, availableBytes) = estimate.Value;
        const long reserveBytes = 1L * 1024 * 1024 * 1024; // 1 GB safety margin.
        if (availableBytes >= requiredAdditionalBytes + reserveBytes)
        {
            return true;
        }

        var requiredGb = requiredAdditionalBytes / 1024d / 1024d / 1024d;
        var availableGb = availableBytes / 1024d / 1024d / 1024d;
        var reserveGb = reserveBytes / 1024d / 1024d / 1024d;

        var result = MessageBox.Show(
            this,
            $"Dung lượng trống có thể không đủ để tải {row.Name}.{Environment.NewLine}" +
            $"Cần thêm khoảng: {requiredGb:N2} GB{Environment.NewLine}" +
            $"Đang trống: {availableGb:N2} GB (khuyến nghị dự phòng {reserveGb:N0} GB).{Environment.NewLine}{Environment.NewLine}" +
            "Bạn có muốn tiếp tục không?",
            "Cảnh báo dung lượng",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        return result == DialogResult.Yes;
    }

    private static (long RequiredAdditionalBytes, long AvailableBytes)? TryEstimateRequiredDiskSpace(string sourcePath, string targetPath)
    {
        try
        {
            if (!Directory.Exists(sourcePath))
            {
                return null;
            }

            var sourceBytes = CalculateDirectorySizeSafe(sourcePath);
            var targetBytes = Directory.Exists(targetPath) ? CalculateDirectorySizeSafe(targetPath) : 0L;
            var requiredAdditionalBytes = Math.Max(0L, sourceBytes - targetBytes);

            var fullTargetPath = Path.GetFullPath(targetPath);
            var root = Path.GetPathRoot(fullTargetPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var drive = new DriveInfo(root);
            return (requiredAdditionalBytes, drive.AvailableFreeSpace);
        }
        catch
        {
            return null;
        }
    }

    private static long CalculateDirectorySizeSafe(string path)
    {
        var total = 0L;
        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // Ignore individual file access errors.
                    }
                }
            }
            catch
            {
                // Ignore folder access errors.
            }

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    stack.Push(directory);
                }
            }
            catch
            {
                // Ignore folder access errors.
            }
        }

        return total;
    }

    private async Task<int?> EnsureManagedGameRegistrationAsync(GameRecord game, ResourceGameRow row)
    {
        var launchRelativePath = FindPreferredLaunchRelativePath(game.InstallPath, row.Name);
        if (!string.IsNullOrWhiteSpace(launchRelativePath))
        {
            game.LaunchRelativePath = launchRelativePath;
        }

        if (string.IsNullOrWhiteSpace(game.Version))
        {
            game.Version = "1.0.0";
        }

        var gameId = await _gameService.SaveGameAsync(game);
        game.Id = gameId;

        try
        {
            await _gameService.ScanGameAsync(game);
        }
        catch
        {
            // Ignore manifest scan failure to avoid blocking sync flow.
        }

        return gameId;
    }

    private void UpdateResourceRootsFromInputs()
    {
        _resourceSourceRootPath = _resourceSourceRootTextBox.Text.Trim();
        _resourceTargetRootPath = _resourceTargetRootTextBox.Text.Trim();
        _resourceBandwidthLimitMbps = Decimal.ToInt32(_resourceBandwidthLimitNumeric.Value);

        if (string.IsNullOrWhiteSpace(_resourceSourceRootPath))
        {
            throw new InvalidOperationException("Vui lòng nhập nguồn IDC (thư mục local/UNC hoặc URL HTTP/HTTPS).");
        }

        if (string.IsNullOrWhiteSpace(_resourceTargetRootPath))
        {
            throw new InvalidOperationException("Vui lòng nhập thư mục đích máy chủ.");
        }
    }

    private void ConfigureResourcesGrid()
    {
        _resourcesGrid.AutoGenerateColumns = false;
        _resourcesGrid.AllowUserToAddRows = false;
        _resourcesGrid.AllowUserToDeleteRows = false;
        _resourcesGrid.MultiSelect = true;
        _resourcesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _resourcesGrid.ReadOnly = true;
        _resourcesGrid.RowHeadersVisible = false;
        _resourcesGrid.DataSource = _resourcesBinding;

        _resourcesGrid.Columns.Add(CreateTextColumn("ID", nameof(ResourceGameRow.Id), 70));
        _resourcesGrid.Columns.Add(CreateTextColumn("Tên trò chơi", nameof(ResourceGameRow.Name), 180));
        _resourcesGrid.Columns.Add(CreateTextColumn("Nhóm", nameof(ResourceGameRow.Category), 120));
        _resourcesGrid.Columns.Add(CreateTextColumn("Nguồn IDC", nameof(ResourceGameRow.SourceStatus), 110));
        _resourcesGrid.Columns.Add(CreateTextColumn("Trạng thái tải", nameof(ResourceGameRow.DownloadStatus), 160));
        _resourcesGrid.Columns.Add(CreateTextColumn("Tốc độ", nameof(ResourceGameRow.DownloadSpeedDisplay), 100));
        _resourcesGrid.Columns.Add(CreateTextColumn("Trạng thái chạy", nameof(ResourceGameRow.RunStatus), 130));
        _resourcesGrid.Columns.Add(CreateTextColumn("Số tệp", nameof(ResourceGameRow.FileCountDisplay), 90));
        _resourcesGrid.Columns.Add(CreateTextColumn("Kích thước (GB)", nameof(ResourceGameRow.SizeGbDisplay), 110));
        _resourcesGrid.Columns.Add(CreateTextColumn("Cập nhật gần nhất", nameof(ResourceGameRow.LastUpdatedAt), 150, "yyyy-MM-dd HH:mm:ss"));
        _resourcesGrid.Columns.Add(CreateTextColumn("Đường dẫn nguồn", nameof(ResourceGameRow.SourcePath), 260));
        _resourcesGrid.Columns.Add(CreateTextColumn("Đường dẫn cài đặt", nameof(ResourceGameRow.InstallPath), 400, fill: true));
    }

    private void EnsureResourcesContextMenu()
    {
        if (_resourcesContextMenuInitialized)
        {
            return;
        }

        _resourcesContextMenuInitialized = true;
        EnsureResourceBandwidthPresetMenuItems();

        _resourcesContextMenu.Items.Add(_downloadSelectedResourcesMenuItem);
        _resourcesContextMenu.Items.Add(new ToolStripSeparator());
        _resourcesContextMenu.Items.Add(_pauseSelectedResourcesMenuItem);
        _resourcesContextMenu.Items.Add(_resumeSelectedResourcesMenuItem);
        _resourcesContextMenu.Items.Add(_stopSelectedResourcesMenuItem);
        _resourcesContextMenu.Items.Add(_setResourceBandwidthMenuItem);
        _resourcesContextMenu.Items.Add(_retrySelectedResourcesMenuItem);
        _resourcesContextMenu.Items.Add(new ToolStripSeparator());
        _resourcesContextMenu.Items.Add(_syncMissingFromIdcMenuItem);

        _resourcesContextMenu.Opening += ResourcesContextMenu_Opening;
        _downloadSelectedResourcesMenuItem.Click += DownloadSelectedResourcesMenuItem_Click;
        _pauseSelectedResourcesMenuItem.Click += PauseSelectedResourcesMenuItem_Click;
        _resumeSelectedResourcesMenuItem.Click += ResumeSelectedResourcesMenuItem_Click;
        _stopSelectedResourcesMenuItem.Click += StopSelectedResourcesMenuItem_Click;
        _retrySelectedResourcesMenuItem.Click += RetrySelectedResourcesMenuItem_Click;
        _syncMissingFromIdcMenuItem.Click += SyncMissingFromIdcMenuItem_Click;

        _resourcesGrid.ContextMenuStrip = _resourcesContextMenu;
        _resourcesGrid.MouseDown += ResourcesGrid_MouseDown;
    }

    private void EnsureResourceBandwidthPresetMenuItems()
    {
        if (_resourceBandwidthPresetMenuItems.Count > 0)
        {
            return;
        }

        for (var mbps = 1; mbps <= 10; mbps++)
        {
            var item = new ToolStripMenuItem($"{mbps} MB/s")
            {
                Tag = mbps
            };

            item.Click += ResourceBandwidthPresetMenuItem_Click;
            _resourceBandwidthPresetMenuItems.Add(item);
            _setResourceBandwidthMenuItem.DropDownItems.Add(item);
        }
    }

    private void ResourcesGrid_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || !_resourcesGrid.Visible)
        {
            return;
        }

        var hit = _resourcesGrid.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0 || hit.RowIndex >= _resourcesGrid.Rows.Count)
        {
            return;
        }

        var row = _resourcesGrid.Rows[hit.RowIndex];
        if (!row.Selected)
        {
            _resourcesGrid.ClearSelection();
            row.Selected = true;
        }

        _resourcesGrid.CurrentCell = row.Cells[0];
    }

    private void ResourcesContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var selectedRows = GetSelectedOrCurrentResourceRows();
        if (selectedRows.Count == 0)
        {
            _downloadSelectedResourcesMenuItem.Enabled = false;
            _pauseSelectedResourcesMenuItem.Enabled = false;
            _resumeSelectedResourcesMenuItem.Enabled = false;
            _stopSelectedResourcesMenuItem.Enabled = false;
            _setResourceBandwidthMenuItem.Enabled = false;
            _retrySelectedResourcesMenuItem.Enabled = false;
            _syncMissingFromIdcMenuItem.Enabled = false;
            SetCheckedResourceBandwidthPreset(-1);
            return;
        }

        var selectedWithSourceCount = selectedRows.Count(row => row.HasSource);
        var selectedTasks = GetSelectedActiveResourceTasks(selectedRows);
        var hasRunning = selectedTasks.Any(item => !item.Control.IsPaused);
        var hasPaused = selectedTasks.Any(item => item.Control.IsPaused);
        var hasAnyTask = selectedTasks.Count > 0;
        var canRetry = selectedRows.Any(row =>
            row.HasSource &&
            FindLatestMonitorRowForResource(row) is { } monitorRow &&
            !IsResourceSyncRunning(monitorRow) &&
            IsRetryableMonitorStatus(monitorRow.Status));
        var canSyncMissing = selectedRows.Any(row =>
            row.HasSource &&
            row.IsDownloaded &&
            !string.IsNullOrWhiteSpace(row.InstallPath));

        _downloadSelectedResourcesMenuItem.Enabled = selectedWithSourceCount > 0;
        _pauseSelectedResourcesMenuItem.Enabled = hasRunning;
        _resumeSelectedResourcesMenuItem.Enabled = hasPaused;
        _stopSelectedResourcesMenuItem.Enabled = hasAnyTask;
        _setResourceBandwidthMenuItem.Enabled = hasAnyTask;
        _retrySelectedResourcesMenuItem.Enabled = canRetry;
        _syncMissingFromIdcMenuItem.Enabled = canSyncMissing;

        var selectedBandwidths = selectedTasks
            .Select(item => item.Control.BandwidthLimitMbps)
            .Distinct()
            .ToList();
        var unifiedBandwidth = selectedBandwidths.Count == 1 ? selectedBandwidths[0] : -1;
        SetCheckedResourceBandwidthPreset(unifiedBandwidth);
    }

    private async void DownloadSelectedResourcesMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedRows = GetSelectedOrCurrentResourceRows()
            .Where(row => row.HasSource)
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedRows.Count == 0)
        {
            ShowInfo("Không có trò chơi có nguồn IDC để tải.");
            return;
        }

        await RunResourceSyncForRowsAsync(selectedRows, ResourceSyncMode.Incremental);
    }

    private void PauseSelectedResourcesMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedTasks = GetSelectedActiveResourceTasks();
        var paused = 0;
        foreach (var (monitorRow, control) in selectedTasks)
        {
            if (control.IsPaused)
            {
                continue;
            }

            control.Pause();
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Tạm dừng", "Đã tạm dừng theo yêu cầu từ danh sách tài nguyên.");
            paused++;
        }

        if (paused == 0)
        {
            ShowInfo("Không có tác vụ đang chạy để tạm dừng.");
        }
    }

    private void ResumeSelectedResourcesMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedTasks = GetSelectedActiveResourceTasks();
        var resumed = 0;
        foreach (var (monitorRow, control) in selectedTasks)
        {
            if (!control.IsPaused)
            {
                continue;
            }

            control.Resume();
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Đang tải", "Đã tiếp tục theo yêu cầu từ danh sách tài nguyên.");
            resumed++;
        }

        if (resumed == 0)
        {
            ShowInfo("Không có tác vụ tạm dừng để tiếp tục.");
        }
    }

    private void StopSelectedResourcesMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedTasks = GetSelectedActiveResourceTasks();
        var stopped = 0;
        foreach (var (monitorRow, control) in selectedTasks)
        {
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Đang dừng", "Đang gửi yêu cầu dừng từ danh sách tài nguyên...");
            control.Cancel();
            stopped++;
        }

        if (stopped == 0)
        {
            ShowInfo("Không có tác vụ đang chạy để dừng.");
        }
    }

    private async void RetrySelectedResourcesMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedRows = GetSelectedOrCurrentResourceRows()
            .Where(row =>
                row.HasSource &&
                FindLatestMonitorRowForResource(row) is { } monitorRow &&
                !IsResourceSyncRunning(monitorRow) &&
                IsRetryableMonitorStatus(monitorRow.Status))
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedRows.Count == 0)
        {
            ShowInfo("Không có mục phù hợp để tải lại.");
            return;
        }

        await RunResourceSyncForRowsAsync(selectedRows, ResourceSyncMode.Incremental);
    }

    private void ResourceBandwidthPresetMenuItem_Click(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem { Tag: int mbps })
        {
            return;
        }

        var selectedTasks = GetSelectedActiveResourceTasks();
        if (selectedTasks.Count == 0)
        {
            ShowInfo("Không có tác vụ đang chạy để đặt băng thông.");
            return;
        }

        foreach (var (monitorRow, control) in selectedTasks)
        {
            control.SetBandwidthLimitMbps(mbps);
            UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, monitorRow.Status, $"Đã đặt giới hạn băng thông: {mbps} MB/s.");
        }

        SetCheckedResourceBandwidthPreset(mbps);
    }

    private void SetCheckedResourceBandwidthPreset(int mbps)
    {
        foreach (var item in _resourceBandwidthPresetMenuItems)
        {
            item.Checked = item.Tag is int value && value == mbps;
        }
    }

    private async void SyncMissingFromIdcMenuItem_Click(object? sender, EventArgs e)
    {
        var selectedRows = GetSelectedOrCurrentResourceRows()
            .Where(row =>
                row.HasSource &&
                row.IsDownloaded &&
                !string.IsNullOrWhiteSpace(row.InstallPath))
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedRows.Count == 0)
        {
            ShowInfo("Không có trò chơi phù hợp để đồng bộ file thiếu.");
            return;
        }

        await RunResourceSyncForRowsAsync(selectedRows, ResourceSyncMode.MissingOnly);
    }

    private IReadOnlyList<ResourceGameRow> GetSelectedResourceRows()
    {
        return _resourcesGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .OrderBy(row => row.Index)
            .Select(row => row.DataBoundItem as ResourceGameRow)
            .Where(row => row is not null)
            .Cast<ResourceGameRow>()
            .ToList();
    }

    private IReadOnlyList<ResourceGameRow> GetSelectedOrCurrentResourceRows()
    {
        var selectedRows = GetSelectedResourceRows();
        if (selectedRows.Count > 0)
        {
            return selectedRows;
        }

        if (_resourcesGrid.CurrentRow?.DataBoundItem is ResourceGameRow currentRow)
        {
            return new[] { currentRow };
        }

        return Array.Empty<ResourceGameRow>();
    }

    private IReadOnlyList<(DownloadMonitorRow MonitorRow, ResourceSyncTaskControl Control)> GetSelectedActiveResourceTasks(
        IReadOnlyList<ResourceGameRow>? selectedRows = null)
    {
        var rows = selectedRows ?? GetSelectedOrCurrentResourceRows();
        var result = new List<(DownloadMonitorRow MonitorRow, ResourceSyncTaskControl Control)>();
        var seen = new HashSet<DownloadMonitorRow>();

        foreach (var resourceRow in rows)
        {
            var monitorRow = FindActiveMonitorRowForResource(resourceRow);
            if (monitorRow is null || !seen.Add(monitorRow))
            {
                continue;
            }

            if (TryGetResourceSyncToken(monitorRow, out var control))
            {
                result.Add((monitorRow, control));
            }
        }

        return result;
    }

    private DownloadMonitorRow? FindActiveMonitorRowForResource(ResourceGameRow resourceRow)
    {
        return _downloadMonitorRows
            .Where(row => IsResourceSyncRunning(row))
            .Where(row =>
                (!string.IsNullOrWhiteSpace(resourceRow.SourceKey) &&
                 string.Equals(row.ResourceKey, resourceRow.SourceKey, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(row.GameName, resourceRow.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(row => row.UpdatedAt)
            .FirstOrDefault();
    }

    private DownloadMonitorRow? FindLatestMonitorRowForResource(ResourceGameRow resourceRow)
    {
        return _downloadMonitorRows
            .Where(row =>
                (!string.IsNullOrWhiteSpace(resourceRow.SourceKey) &&
                 string.Equals(row.ResourceKey, resourceRow.SourceKey, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(row.GameName, resourceRow.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(row => row.UpdatedAt)
            .FirstOrDefault();
    }

    private static bool IsRetryableMonitorStatus(string status)
    {
        return string.Equals(status, "Thất bại", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Đã dừng", StringComparison.OrdinalIgnoreCase);
    }

    private void ConfigureDownloadMonitorGrid()
    {
        _downloadMonitorGrid.AutoGenerateColumns = false;
        _downloadMonitorGrid.AllowUserToAddRows = false;
        _downloadMonitorGrid.AllowUserToDeleteRows = false;
        _downloadMonitorGrid.MultiSelect = false;
        _downloadMonitorGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _downloadMonitorGrid.ReadOnly = true;
        _downloadMonitorGrid.RowHeadersVisible = false;
        _downloadMonitorGrid.DataSource = _downloadMonitorBinding;

        _downloadMonitorGrid.Columns.Add(CreateTextColumn("STT", nameof(DownloadMonitorRow.SerialNumber), 50));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Game ID", nameof(DownloadMonitorRow.GameIdDisplay), 80));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Tên Game", nameof(DownloadMonitorRow.GameName), 190));
        var progressColumn = new DataGridViewTextBoxColumn
        {
            Name = DownloadProgressColumnName,
            HeaderText = "Tiến trình",
            DataPropertyName = nameof(DownloadMonitorRow.ProgressPercent),
            Width = 100
        };
        _downloadMonitorGrid.Columns.Add(progressColumn);
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Trạng thái", nameof(DownloadMonitorRow.Status), 110));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Dung lượng (GB)", nameof(DownloadMonitorRow.TotalSizeGbDisplay), 115));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Còn lại (MB)", nameof(DownloadMonitorRow.RemainingMbDisplay), 115));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Thời gian còn lại", nameof(DownloadMonitorRow.RemainingTimeDisplay), 125));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Tốc độ (MB/S)", nameof(DownloadMonitorRow.SpeedMbpsDisplay), 100));
        _downloadMonitorGrid.CellPainting -= DownloadMonitorGrid_CellPainting;
        _downloadMonitorGrid.CellPainting += DownloadMonitorGrid_CellPainting;
    }

    private async Task RebuildResourceRowsAsync(IReadOnlyList<GameRecord> games)
    {
        _allResourceRows.Clear();
        var sourceFolders = await GetSourceFolderEntriesAsync();
        var sourceFoldersByKey = sourceFolders
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var game in games.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            _allResourceRows.Add(CreateResourceRow(game, sourceFoldersByKey));
        }

        var existingSourceKeys = new HashSet<string>(
            _allResourceRows
                .Where(row => !string.IsNullOrWhiteSpace(row.SourceKey))
                .Select(row => row.SourceKey),
            StringComparer.OrdinalIgnoreCase);

        foreach (var sourceFolder in sourceFolders)
        {
            if (existingSourceKeys.Contains(sourceFolder.Key))
            {
                continue;
            }

            _allResourceRows.Add(CreateSourceOnlyResourceRow(sourceFolder));
        }

        await RefreshResourceCompletionStatesAsync(games);

        _allResourceRows.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));

        ApplyResourceFilter(_currentResourceFilter);
    }

    private ResourceGameRow CreateResourceRow(GameRecord game, IReadOnlyDictionary<string, SourceFolderEntry> sourceFoldersByKey)
    {
        var sourceKey = ResolveSourceKeyForGame(game);
        var sourcePath = ResolveSourcePathForGame(game);
        var sourceExists = sourceFoldersByKey.ContainsKey(sourceKey);

        if (!sourceExists && !IsHttpSourceRootConfigured() && !string.IsNullOrWhiteSpace(sourcePath))
        {
            sourceExists = Directory.Exists(sourcePath);
        }

        if (sourceFoldersByKey.TryGetValue(sourceKey, out var sourceFolder) &&
            !string.IsNullOrWhiteSpace(sourceFolder.FullPath))
        {
            sourcePath = sourceFolder.FullPath;
        }

        var hasDownloadedData = HasAnyFileSystemEntry(game.InstallPath);
        var launchPath = ResolveLaunchPath(game);
        var runReady = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
        var manifest = TryLoadManifest(game);

        long? totalBytes = null;
        int? fileCount = null;
        if (manifest is not null)
        {
            totalBytes = manifest.Files.Sum(file => file.Size);
            fileCount = manifest.Files.Count;
        }

        return new ResourceGameRow
        {
            Id = game.Id,
            ManagedGameId = game.Id,
            Name = game.Name,
            Category = game.Category,
            SourceKey = sourceKey,
            SourcePath = sourcePath,
            SourceStatus = sourceExists ? "Có nguồn" : "Thiếu nguồn",
            InstallPath = game.InstallPath,
            LastUpdatedAt = game.LastUpdatedAt,
            IsDownloaded = hasDownloadedData,
            IsManaged = true,
            HasSource = sourceExists,
            DownloadStatus = hasDownloadedData ? "Đã tải" : "Chưa tải",
            DownloadSpeedDisplay = "-",
            RunStatus = runReady ? "Sẵn sàng chạy" : "Thiếu tệp chạy",
            FileCountDisplay = fileCount?.ToString("N0") ?? "-",
            SizeGbDisplay = totalBytes.HasValue ? (totalBytes.Value / 1024d / 1024d / 1024d).ToString("N2") : "-"
        };
    }

    private string ResolveSourcePathForGame(GameRecord game)
    {
        var sourceKey = ResolveSourceKeyForGame(game);
        return ResolveSourcePathForKey(sourceKey);
    }

    private string ResolveSourceKeyForGame(GameRecord game)
    {
        if (string.IsNullOrWhiteSpace(game.InstallPath))
        {
            return game.Name;
        }

        try
        {
            var normalizedTargetRoot = Path.GetFullPath(_resourceTargetRootPath);
            var normalizedInstallPath = Path.GetFullPath(game.InstallPath);

            var normalizedRoot = normalizedTargetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedInstallWithSlash = normalizedInstallPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            string relativePath;
            if (normalizedInstallWithSlash.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = Path.GetRelativePath(normalizedTargetRoot, normalizedInstallPath);
            }
            else
            {
                relativePath = Path.GetFileName(normalizedInstallPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                relativePath = game.Name;
            }

            return relativePath;
        }
        catch
        {
            return game.Name;
        }
    }

    private string ResolveSourcePathForKey(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) || string.IsNullOrWhiteSpace(_resourceSourceRootPath))
        {
            return string.Empty;
        }

        if (IsHttpSourceRootConfigured())
        {
            try
            {
                if (!Uri.TryCreate(_resourceSourceRootPath.Trim(), UriKind.Absolute, out var sourceRootUri))
                {
                    return string.Empty;
                }

                var rootUri = sourceRootUri.AbsoluteUri.EndsWith('/')
                    ? sourceRootUri
                    : new Uri($"{sourceRootUri.AbsoluteUri}/");
                var encodedSegments = sourceKey
                    .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(Uri.EscapeDataString);
                var relativePath = string.Join("/", encodedSegments);
                var combined = new Uri(rootUri, relativePath);
                return combined.AbsoluteUri;
            }
            catch
            {
                return string.Empty;
            }
        }

        try
        {
            return Path.GetFullPath(Path.Combine(_resourceSourceRootPath, sourceKey));
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ResolveTargetPathForSourceKey(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return _resourceTargetRootPath;
        }

        try
        {
            return Path.GetFullPath(Path.Combine(_resourceTargetRootPath, sourceKey));
        }
        catch
        {
            return Path.Combine(_resourceTargetRootPath, sourceKey);
        }
    }

    private bool IsHttpSourceRootConfigured()
    {
        return _resourceSyncService.IsHttpSourceRoot(_resourceSourceRootPath);
    }

    private async Task<IReadOnlyList<SourceFolderEntry>> GetSourceFolderEntriesAsync()
    {
        if (string.IsNullOrWhiteSpace(_resourceSourceRootPath))
        {
            return Array.Empty<SourceFolderEntry>();
        }

        if (IsHttpSourceRootConfigured())
        {
            IReadOnlyList<string> sourceKeys;
            try
            {
                sourceKeys = await _resourceSyncService.GetHttpTopLevelDirectoryKeysAsync(_resourceSourceRootPath);
            }
            catch
            {
                return Array.Empty<SourceFolderEntry>();
            }

            return sourceKeys
                .Select(sourceKey => new SourceFolderEntry
                {
                    Key = sourceKey,
                    Name = sourceKey,
                    FullPath = ResolveSourcePathForKey(sourceKey)
                })
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        string sourceRootPath;
        try
        {
            sourceRootPath = Path.GetFullPath(_resourceSourceRootPath);
        }
        catch
        {
            return Array.Empty<SourceFolderEntry>();
        }

        if (!Directory.Exists(sourceRootPath))
        {
            return Array.Empty<SourceFolderEntry>();
        }

        var result = new List<SourceFolderEntry>();
        foreach (var directory in Directory.EnumerateDirectories(sourceRootPath, "*", SearchOption.TopDirectoryOnly))
        {
            var folderName = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(folderName))
            {
                continue;
            }

            result.Add(new SourceFolderEntry
            {
                Key = folderName,
                Name = folderName,
                FullPath = directory
            });
        }

        return result.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private ResourceGameRow CreateSourceOnlyResourceRow(SourceFolderEntry sourceFolder)
    {
        var targetPath = ResolveTargetPathForSourceKey(sourceFolder.Key);
        var hasDownloadedData = HasAnyFileSystemEntry(targetPath);
        var launchPath = FindPreferredExecutablePath(targetPath, sourceFolder.Name);
        var runReady = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);

        return new ResourceGameRow
        {
            Id = 0,
            ManagedGameId = null,
            Name = sourceFolder.Name,
            Category = "IDC",
            SourceKey = sourceFolder.Key,
            SourceStatus = "Có nguồn",
            SourcePath = sourceFolder.FullPath,
            DownloadStatus = hasDownloadedData ? "Đã tải" : "Chưa tải",
            DownloadSpeedDisplay = "-",
            RunStatus = runReady ? "Sẵn sàng chạy" : "Chưa cấu hình tệp chạy",
            FileCountDisplay = "-",
            SizeGbDisplay = "-",
            LastUpdatedAt = null,
            InstallPath = targetPath,
            IsDownloaded = hasDownloadedData,
            IsManaged = false,
            HasSource = true
        };
    }

    private async Task RefreshResourceCompletionStatesAsync(IReadOnlyList<GameRecord> games)
    {
        var gamesById = games
            .GroupBy(game => game.Id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var row in _allResourceRows)
        {
            var monitorRow = FindActiveMonitorRowForResource(row);
            if (monitorRow is not null && IsResourceSyncRunning(monitorRow))
            {
                continue;
            }

            var isDownloaded = await DetermineResourceDownloadedStateAsync(row, gamesById);
            row.IsDownloaded = isDownloaded;
            row.DownloadStatus = isDownloaded ? "Đã tải" : "Chưa tải";
            row.DownloadSpeedDisplay = "-";
            row.RunStatus = isDownloaded
                ? GetRunStatusAfterSync(row)
                : (row.IsManaged ? "Thiếu tệp chạy" : "Chưa cấu hình tệp chạy");
        }
    }

    private async Task<bool> DetermineResourceDownloadedStateAsync(
        ResourceGameRow row,
        IReadOnlyDictionary<int, GameRecord> gamesById)
    {
        if (!HasAnyFileSystemEntry(row.InstallPath))
        {
            return false;
        }

        if (row.ManagedGameId.HasValue &&
            gamesById.TryGetValue(row.ManagedGameId.Value, out var managedGame) &&
            TryCheckDownloadedByManifest(managedGame, row.InstallPath, out var isCompleteFromManifest))
        {
            return isCompleteFromManifest;
        }

        if (!row.HasSource || string.IsNullOrWhiteSpace(row.SourcePath))
        {
            return true;
        }

        try
        {
            return await _resourceSyncService.IsSourceMirroredToTargetAsync(row.SourcePath, row.InstallPath);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCheckDownloadedByManifest(GameRecord game, string installPath, out bool isComplete)
    {
        isComplete = false;
        var manifest = TryLoadManifest(game);
        if (manifest is null || manifest.Files.Count == 0)
        {
            return false;
        }

        string normalizedInstallRoot;
        try
        {
            normalizedInstallRoot = Path.GetFullPath(installPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
        }
        catch
        {
            return true;
        }

        foreach (var file in manifest.Files)
        {
            string targetPath;
            try
            {
                targetPath = Path.GetFullPath(Path.Combine(installPath, file.RelativePath));
            }
            catch
            {
                return true;
            }

            if (!targetPath.StartsWith(normalizedInstallRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!File.Exists(targetPath))
            {
                return true;
            }

            long actualSize;
            try
            {
                actualSize = new FileInfo(targetPath).Length;
            }
            catch
            {
                return true;
            }

            if (actualSize != file.Size)
            {
                return true;
            }
        }

        isComplete = true;
        return true;
    }

    private static string FindPreferredExecutablePath(string installPath, string preferredName)
    {
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
        {
            return string.Empty;
        }

        var preferredFileName = string.IsNullOrWhiteSpace(preferredName)
            ? string.Empty
            : preferredName.Trim() + ".exe";

        if (!string.IsNullOrWhiteSpace(preferredFileName))
        {
            var exactPath = Path.Combine(installPath, preferredFileName);
            if (File.Exists(exactPath))
            {
                return exactPath;
            }
        }

        var rootExecutables = Directory
            .EnumerateFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rootPreferred = rootExecutables.FirstOrDefault(path =>
            string.Equals(Path.GetFileName(path), preferredFileName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(rootPreferred))
        {
            return rootPreferred;
        }

        var rootFirst = rootExecutables.FirstOrDefault(path =>
            !Path.GetFileName(path).Contains("unins", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(rootFirst))
        {
            return rootFirst;
        }

        return Directory
            .EnumerateFiles(installPath, "*.exe", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(path => !Path.GetFileName(path).Contains("unins", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
    }

    private static string FindPreferredLaunchRelativePath(string installPath, string preferredName)
    {
        var executablePath = FindPreferredExecutablePath(installPath, preferredName);
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return string.Empty;
        }

        return Path.GetRelativePath(installPath, executablePath);
    }

    private static string ResolveLaunchPath(GameRecord game)
    {
        if (string.IsNullOrWhiteSpace(game.LaunchRelativePath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(game.LaunchRelativePath))
        {
            return Path.GetFullPath(game.LaunchRelativePath);
        }

        if (string.IsNullOrWhiteSpace(game.InstallPath))
        {
            return string.Empty;
        }

        return Path.GetFullPath(Path.Combine(game.InstallPath, game.LaunchRelativePath));
    }

    private static bool HasAnyFileSystemEntry(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }

        try
        {
            using var enumerator = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
            return enumerator.MoveNext();
        }
        catch
        {
            return false;
        }
    }

    private static string GetManifestPath(GameRecord game)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "manifests",
            $"{game.Id:0000}-{ToSafeFileName(game.Name)}.manifest.json");
    }

    private static string ToSafeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        return builder.ToString().Trim().ToLowerInvariant();
    }

    private static GameManifest? TryLoadManifest(GameRecord game)
    {
        try
        {
            var path = GetManifestPath(game);
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GameManifest>(json, ManifestJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void ApplyResourceFilter(ResourceFilterKind filterKind)
    {
        _currentResourceFilter = filterKind;

        if (filterKind == ResourceFilterKind.DownloadMonitor)
        {
            _resourcesGrid.Visible = false;
            _downloadMonitorGrid.Visible = true;
            _downloadMonitorGrid.BringToFront();
            UpdateDownloadSummary();
            return;
        }

        RefreshResourceRowsFromFileSystem();

        _downloadMonitorGrid.Visible = false;
        _resourcesGrid.Visible = true;
        _resourcesGrid.BringToFront();

        var filtered = filterKind switch
        {
            ResourceFilterKind.Missing => _allResourceRows.Where(row => row.HasSource && !row.IsDownloaded).ToList(),
            ResourceFilterKind.Downloaded => _allResourceRows.Where(row => row.IsDownloaded).ToList(),
            _ => _allResourceRows.ToList()
        };

        _resourcesBinding.DataSource = filtered;
        UpdateResourceSummary(filtered);
    }

    private void RefreshResourceRowsFromFileSystem()
    {
        foreach (var row in _allResourceRows)
        {
            var hasDownloadedData = HasAnyFileSystemEntry(row.InstallPath);
            if (!hasDownloadedData)
            {
                row.IsDownloaded = false;
            }

            var activeMonitor = FindActiveMonitorRowForResource(row);
            var isSyncRunning = activeMonitor is not null && IsResourceSyncRunning(activeMonitor);
            if (isSyncRunning)
            {
                continue;
            }

            row.DownloadStatus = row.IsDownloaded ? "Đã tải" : "Chưa tải";
            row.DownloadSpeedDisplay = "-";
            row.RunStatus = row.IsDownloaded
                ? GetRunStatusAfterSync(row)
                : (row.IsManaged ? "Thiếu tệp chạy" : "Chưa cấu hình tệp chạy");
        }
    }

    private void UpdateResourceSummary(IReadOnlyList<ResourceGameRow> filteredRows)
    {
        var total = _allResourceRows.Count;
        var downloaded = _allResourceRows.Count(row => row.IsDownloaded);
        var missing = total - downloaded;
        _resourceSummaryLabel.Text = $"Hiển thị {filteredRows.Count}/{total} trò chơi. Đã tải: {downloaded}. Chưa tải: {missing}.";
    }

    private void UpdateDownloadSummary()
    {
        var total = _downloadMonitorRows.Count;
        var running = _downloadMonitorRows.Count(row => string.Equals(row.Status, "Đang tải", StringComparison.OrdinalIgnoreCase));
        var paused = _downloadMonitorRows.Count(row => string.Equals(row.Status, "Tạm dừng", StringComparison.OrdinalIgnoreCase));
        var stopping = _downloadMonitorRows.Count(row => string.Equals(row.Status, "Đang dừng", StringComparison.OrdinalIgnoreCase));
        var failed = _downloadMonitorRows.Count(row => string.Equals(row.Status, "Thất bại", StringComparison.OrdinalIgnoreCase));
        _resourceSummaryLabel.Text = $"Giám sát tải xuống máy chủ: tổng {total} tác vụ, đang tải {running}, tạm dừng {paused}, đang dừng {stopping}, thất bại {failed}.";
    }

    private TabPage BuildUpdateTab()
    {
        var page = new TabPage("Cập nhật");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 8,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        for (var rowIndex = 0; rowIndex < 7; rowIndex++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        }

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _updateGameComboBox.Dock = DockStyle.Fill;
        _updateGameComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _updateGameComboBox.DataSource = _gamesBinding;
        _updateGameComboBox.DisplayMember = nameof(GameRecord.Name);

        _updateSourceKindComboBox.Dock = DockStyle.Fill;
        _updateSourceKindComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        var sourceOptions = new List<UpdateSourceOption>
        {
            new() { Kind = UpdateSourceKind.Folder, Name = "Thư mục" },
            new() { Kind = UpdateSourceKind.Zip, Name = "Tệp ZIP" }
        };
        _updateSourceKindComboBox.DataSource = sourceOptions;
        _updateSourceKindComboBox.DisplayMember = nameof(UpdateSourceOption.Name);
        _updateSourceKindComboBox.ValueMember = nameof(UpdateSourceOption.Kind);

        _updateSourceTextBox.Dock = DockStyle.Fill;

        _browseSourceButton.Text = "Chọn";
        _browseSourceButton.Dock = DockStyle.Fill;
        _browseSourceButton.Click += BrowseSourceButton_Click;

        _updateVersionTextBox.Dock = DockStyle.Fill;

        _backupCheckBox.Text = "Sao lưu trước khi cập nhật";
        _backupCheckBox.Checked = true;
        _backupCheckBox.Dock = DockStyle.Fill;

        _updateProgressBar.Dock = DockStyle.Fill;

        _applyUpdateButton.Text = "Bắt đầu cập nhật";
        _applyUpdateButton.Dock = DockStyle.Fill;
        _applyUpdateButton.Click += ApplyUpdateButton_Click;

        _updateOutputTextBox.Dock = DockStyle.Fill;
        _updateOutputTextBox.Multiline = true;
        _updateOutputTextBox.ScrollBars = ScrollBars.Vertical;
        _updateOutputTextBox.Font = new Font("Consolas", 10);
        _updateOutputTextBox.ReadOnly = true;

        root.Controls.Add(CreateFieldLabel("Trò chơi"), 0, 0);
        root.Controls.Add(_updateGameComboBox, 1, 0);
        root.SetColumnSpan(_updateGameComboBox, 2);

        root.Controls.Add(CreateFieldLabel("Loại nguồn"), 0, 1);
        root.Controls.Add(_updateSourceKindComboBox, 1, 1);
        root.SetColumnSpan(_updateSourceKindComboBox, 2);

        root.Controls.Add(CreateFieldLabel("Nguồn cập nhật"), 0, 2);
        root.Controls.Add(_updateSourceTextBox, 1, 2);
        root.Controls.Add(_browseSourceButton, 2, 2);

        root.Controls.Add(CreateFieldLabel("Phiên bản"), 0, 3);
        root.Controls.Add(_updateVersionTextBox, 1, 3);
        root.SetColumnSpan(_updateVersionTextBox, 2);

        root.Controls.Add(CreateFieldLabel("Tùy chọn"), 0, 4);
        root.Controls.Add(_backupCheckBox, 1, 4);
        root.SetColumnSpan(_backupCheckBox, 2);

        root.Controls.Add(CreateFieldLabel("Tiến trình"), 0, 5);
        root.Controls.Add(_updateProgressBar, 1, 5);
        root.SetColumnSpan(_updateProgressBar, 2);

        root.Controls.Add(CreateFieldLabel("Thao tác"), 0, 6);
        root.Controls.Add(_applyUpdateButton, 1, 6);
        root.SetColumnSpan(_applyUpdateButton, 2);

        root.Controls.Add(CreateFieldLabel("Nhật ký"), 0, 7);
        root.Controls.Add(_updateOutputTextBox, 1, 7);
        root.SetColumnSpan(_updateOutputTextBox, 2);

        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildLogsTab()
    {
        var page = new TabPage("Lịch sử");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            WrapContents = false
        };
        toolbar.Controls.Add(CreateButton("Làm mới lịch sử", RefreshLogsButton_Click));
        toolbar.Controls.Add(CreateButton("Xuất CSV", ExportLogsCsvButton_Click));

        ConfigureLogsGrid();
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(_logsGrid, 0, 1);

        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildSettingsTab()
    {
        var page = new TabPage("Setting");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var settingsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1
        };
        settingsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        settingsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        settingsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        settingsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var fontSizeRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        InitializeFontSizeSelector(fontSizeRow);

        _browseClientWallpaperButton.Text = "Chọn";
        _browseClientWallpaperButton.Width = 80;
        _browseClientWallpaperButton.Click += BrowseClientWallpaperButton_Click;

        _clearClientWallpaperButton.Text = "Xóa";
        _clearClientWallpaperButton.Width = 70;
        _clearClientWallpaperButton.Click += ClearClientWallpaperButton_Click;

        _clientWallpaperPathTextBox.Dock = DockStyle.Fill;
        _clientWallpaperPathTextBox.Text = _clientWindowsWallpaperPath;

        var wallpaperRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4
        };
        wallpaperRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        wallpaperRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        wallpaperRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        wallpaperRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        wallpaperRow.Controls.Add(CreateFieldLabel("Hình nền Windows máy trạm"), 0, 0);
        wallpaperRow.Controls.Add(_clientWallpaperPathTextBox, 1, 0);
        wallpaperRow.Controls.Add(_browseClientWallpaperButton, 2, 0);
        wallpaperRow.Controls.Add(_clearClientWallpaperButton, 3, 0);

        _enableClientCloseAppHotKeyCheckBox.Dock = DockStyle.Fill;
        _enableClientCloseAppHotKeyCheckBox.Text = "Cho phép máy trạm đóng ứng dụng bằng Ctrl + Alt + K";
        _enableClientCloseAppHotKeyCheckBox.Checked = _enableClientCloseApplicationHotKey;

        var hintLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Lưu ý: chính sách client được xuất kèm theo games.catalog.json. Đường dẫn hình nền nên dùng local/UNC mà client truy cập được.",
            TextAlign = ContentAlignment.TopLeft
        };

        settingsPanel.Controls.Add(fontSizeRow, 0, 0);
        settingsPanel.Controls.Add(wallpaperRow, 0, 1);
        settingsPanel.Controls.Add(_enableClientCloseAppHotKeyCheckBox, 0, 2);
        settingsPanel.Controls.Add(hintLabel, 0, 3);

        var actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        _saveSettingsButton.Text = "Lưu thiết lập";
        _saveSettingsButton.AutoSize = true;
        _saveSettingsButton.Click += SaveSettingsButton_Click;
        actionsPanel.Controls.Add(_saveSettingsButton);

        root.Controls.Add(settingsPanel, 0, 0);
        root.Controls.Add(actionsPanel, 0, 1);

        page.Controls.Add(root);
        return page;
    }

    private void ConfigureGamesGrid()
    {
        _gamesGrid.Dock = DockStyle.Fill;
        _gamesGrid.AutoGenerateColumns = false;
        _gamesGrid.AllowUserToAddRows = false;
        _gamesGrid.AllowUserToDeleteRows = false;
        _gamesGrid.MultiSelect = false;
        _gamesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gamesGrid.ReadOnly = true;
        _gamesGrid.RowHeadersVisible = false;
        _gamesGrid.DataSource = _gamesBinding;

        _gamesGrid.Columns.Add(CreateTextColumn("Tên trò chơi", nameof(GameRecord.Name), 180));
        _gamesGrid.Columns.Add(CreateTextColumn("Nhóm", nameof(GameRecord.Category), 120));
        _gamesGrid.Columns.Add(CreateTextColumn("Phiên bản", nameof(GameRecord.Version), 90));
        _gamesGrid.Columns.Add(CreateTextColumn("Tệp chạy", nameof(GameRecord.LaunchRelativePath), 220));
        _gamesGrid.Columns.Add(CreateTextColumn("Đường dẫn cài đặt", nameof(GameRecord.InstallPath), 320));
        _gamesGrid.Columns.Add(CreateTextColumn("Quét gần nhất", nameof(GameRecord.LastScannedAt), 140, "yyyy-MM-dd HH:mm:ss"));
        _gamesGrid.Columns.Add(CreateTextColumn("Cập nhật gần nhất", nameof(GameRecord.LastUpdatedAt), 140, "yyyy-MM-dd HH:mm:ss"));
    }

    private void EnsureGamesContextMenu()
    {
        if (_gamesContextMenuInitialized)
        {
            return;
        }

        _gamesContextMenuInitialized = true;
        _gamesContextMenu.Items.Add(_addGameMenuItem);
        _gamesContextMenu.Items.Add(_deleteGameMenuItem);
        _gamesContextMenu.Items.Add(_editGameMenuItem);
        _gamesContextMenu.Items.Add(new ToolStripSeparator());
        _gamesContextMenu.Items.Add(_viewManifestMenuItem);
        _gamesContextMenu.Opening += GamesContextMenu_Opening;
        _addGameMenuItem.Click += AddGameButton_Click;
        _editGameMenuItem.Click += EditGameButton_Click;
        _deleteGameMenuItem.Click += DeleteGameButton_Click;
        _viewManifestMenuItem.Click += ViewManifestMenuItem_Click;

        _gamesGrid.ContextMenuStrip = _gamesContextMenu;
        _gamesGrid.MouseDown += GamesGrid_MouseDown;
    }

    private void GamesGrid_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var hit = _gamesGrid.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0 || hit.RowIndex >= _gamesGrid.Rows.Count)
        {
            return;
        }

        _gamesGrid.ClearSelection();
        var row = _gamesGrid.Rows[hit.RowIndex];
        row.Selected = true;
        _gamesGrid.CurrentCell = row.Cells[0];
        _gamesBinding.Position = row.Index;
    }

    private void GamesContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var hasSelectedGame = SelectedGame is not null;
        _addGameMenuItem.Enabled = true;
        _editGameMenuItem.Enabled = hasSelectedGame;
        _deleteGameMenuItem.Enabled = hasSelectedGame;
        _viewManifestMenuItem.Enabled = hasSelectedGame;
    }

    private async void ViewManifestMenuItem_Click(object? sender, EventArgs e)
    {
        if (SelectedGame is null)
        {
            return;
        }

        var game = SelectedGame;
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            var manifestPreview = await _gameService.GetManifestPreviewAsync(game);
            ShowManifestDialog(game.Name, manifestPreview);
        });
    }

    private void ShowManifestDialog(string gameName, string manifestText)
    {
        using var dialog = new Form
        {
            Text = $"Manifest - {gameName}",
            Width = 900,
            Height = 700,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = true
        };

        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", Math.Max(10f, GetUiFontSize(_uiFontSizeMode))),
            ReadOnly = true,
            WordWrap = false,
            Text = manifestText
        };

        dialog.Controls.Add(textBox);
        dialog.ShowDialog(this);
    }

    private void ConfigureLogsGrid()
    {
        _logsGrid.Dock = DockStyle.Fill;
        _logsGrid.AutoGenerateColumns = false;
        _logsGrid.AllowUserToAddRows = false;
        _logsGrid.AllowUserToDeleteRows = false;
        _logsGrid.MultiSelect = false;
        _logsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _logsGrid.ReadOnly = true;
        _logsGrid.RowHeadersVisible = false;
        _logsGrid.DataSource = _logsBinding;

        _logsGrid.Columns.Add(CreateTextColumn("Thời gian", nameof(UpdateLogEntry.CreatedAt), 150, "yyyy-MM-dd HH:mm:ss"));
        _logsGrid.Columns.Add(CreateTextColumn("Trò chơi", nameof(UpdateLogEntry.GameName), 160));
        _logsGrid.Columns.Add(CreateTextColumn("Hành động", nameof(UpdateLogEntry.Action), 120));
        _logsGrid.Columns.Add(CreateTextColumn("Trạng thái", nameof(UpdateLogEntry.Status), 90));
        _logsGrid.Columns.Add(CreateTextColumn("Nội dung", nameof(UpdateLogEntry.Message), 600, fill: true));
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string header, string propertyName, int width, string? format = null, bool fill = false)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = propertyName,
            Width = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None
        };

        if (!string.IsNullOrWhiteSpace(format))
        {
            column.DefaultCellStyle.Format = format;
            column.DefaultCellStyle.NullValue = string.Empty;
        }

        return column;
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private void ApplyResourcesSplitDistance()
    {
        if (_resourcesSplitContainer is null || _resourcesSplitContainer.Width <= 0)
        {
            return;
        }

        var split = _resourcesSplitContainer;
        var hardMax = Math.Max(0, split.Width - 1);
        var preferredLeft = 220;
        var minLeft = 120;
        var reserveRight = 360;

        var target = Math.Min(preferredLeft, Math.Max(minLeft, split.Width - reserveRight));
        target = Math.Clamp(target, 0, hardMax);

        if (split.SplitterDistance != target)
        {
            split.SplitterDistance = target;
        }
    }

    private void InitializeFontSizeSelector(FlowLayoutPanel toolbar)
    {
        var label = new Label
        {
            Text = "Cỡ chữ giao diện",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 5, 8, 0)
        };

        _fontSizeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _fontSizeComboBox.Width = 140;
        _fontSizeComboBox.Margin = new Padding(0, 2, 0, 0);
        _fontSizeComboBox.DisplayMember = nameof(FontSizeOption.Name);
        _fontSizeComboBox.ValueMember = nameof(FontSizeOption.Mode);
        _fontSizeComboBox.DataSource = new List<FontSizeOption>
        {
            new() { Mode = UiFontSizeMode.Normal, Name = "Bình thường" },
            new() { Mode = UiFontSizeMode.Big, Name = "Lớn" },
            new() { Mode = UiFontSizeMode.VeryBig, Name = "Rất lớn" }
        };

        SetFontSizeSelection(_uiFontSizeMode);
        _fontSizeComboBox.SelectedIndexChanged += FontSizeComboBox_SelectedIndexChanged;

        toolbar.Controls.Add(label);
        toolbar.Controls.Add(_fontSizeComboBox);
    }

    private async void FontSizeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingFontSizeSelection || _fontSizeComboBox.SelectedValue is not UiFontSizeMode mode)
        {
            return;
        }

        if (mode == _uiFontSizeMode)
        {
            return;
        }

        ApplyUiFontSize(mode);
        await ExecuteWithErrorHandlingAsync(SaveUiSettingsAsync);
    }

    private void SetFontSizeSelection(UiFontSizeMode mode)
    {
        if (_fontSizeComboBox.DataSource is null)
        {
            return;
        }

        _isUpdatingFontSizeSelection = true;
        try
        {
            _fontSizeComboBox.SelectedValue = mode;
        }
        finally
        {
            _isUpdatingFontSizeSelection = false;
        }
    }

    private void ApplyUiFontSize(UiFontSizeMode mode)
    {
        _uiFontSizeMode = mode;
        var uiFontSize = GetUiFontSize(mode);
        var uiFont = new Font("Segoe UI", uiFontSize, FontStyle.Regular);

        SuspendLayout();
        try
        {
            Font = uiFont;
            ApplyDataGridFont(_gamesGrid, uiFont);
            ApplyDataGridFont(_resourcesGrid, uiFont);
            ApplyDataGridFont(_downloadMonitorGrid, uiFont);
            ApplyDataGridFont(_logsGrid, uiFont);

            _updateOutputTextBox.Font = new Font("Consolas", Math.Max(10f, uiFontSize), FontStyle.Regular);
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    private static void ApplyDataGridFont(DataGridView grid, Font uiFont)
    {
        grid.Font = uiFont;
        grid.ColumnHeadersDefaultCellStyle.Font = uiFont;
        grid.DefaultCellStyle.Font = uiFont;
        grid.RowTemplate.Height = Math.Max(24, (int)Math.Ceiling(uiFont.Size * 2.2f));
        grid.ColumnHeadersHeight = Math.Max(28, (int)Math.Ceiling(uiFont.Size * 2.5f));
    }

    private static float GetUiFontSize(UiFontSizeMode mode)
    {
        return mode switch
        {
            UiFontSizeMode.Big => 11f,
            UiFontSizeMode.VeryBig => 13f,
            _ => 9f
        };
    }

    private static Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true
        };
        button.Click += onClick;
        return button;
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private async void AddGameButton_Click(object? sender, EventArgs e)
    {
        using var editor = new GameEditorForm();
        if (editor.ShowDialog(this) != DialogResult.OK || editor.EditedGame is null)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            var gameId = await _gameService.SaveGameAsync(editor.EditedGame);
            await AutoExportCatalogAsync();
            await ReloadAllAsync(gameId);
        });
    }

    private async void EditGameButton_Click(object? sender, EventArgs e)
    {
        if (SelectedGame is null)
        {
            ShowInfo("Vui lòng chọn trò chơi trước.");
            return;
        }

        using var editor = new GameEditorForm(SelectedGame);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.EditedGame is null)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            var gameId = await _gameService.SaveGameAsync(editor.EditedGame);
            await AutoExportCatalogAsync();
            await ReloadAllAsync(gameId);
        });
    }

    private async void DeleteGameButton_Click(object? sender, EventArgs e)
    {
        if (SelectedGame is null)
        {
            ShowInfo("Vui lòng chọn trò chơi trước.");
            return;
        }

        var game = SelectedGame;
        var result = MessageBox.Show(
            this,
            $"Bạn có chắc muốn xóa {game.Name} khỏi danh sách quản lý? Dữ liệu trò chơi trên ổ đĩa sẽ không bị xóa.",
            "Xác nhận xóa",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            await _gameService.DeleteGameAsync(game);
            await AutoExportCatalogAsync();
            await ReloadAllAsync();
        });
    }

    private async void ScanManifestButton_Click(object? sender, EventArgs e)
    {
        if (SelectedGame is null)
        {
            ShowInfo("Vui lòng chọn trò chơi trước.");
            return;
        }

        var game = SelectedGame;
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ToggleGameControls(false);
            await _gameService.ScanGameAsync(game);
            await AutoExportCatalogAsync();
            await ReloadAllAsync(game.Id);
        }, () => ToggleGameControls(true));
    }

    private async void RefreshButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(() => ReloadAllAsync());
    }

    private async void ExportCatalogButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Tệp JSON (*.json)|*.json",
            Title = "Xuất danh mục trò chơi cho client",
            FileName = Path.GetFileName(_autoCatalogPath)
        };

        var initialDirectory = Path.GetDirectoryName(_autoCatalogPath);
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            _autoCatalogPath = dialog.FileName;
            await _catalogService.ExportToFileAsync(_autoCatalogPath, BuildClientPolicy());
            await SaveUiSettingsAsync();
            MessageBox.Show(this, $"Đã xuất danh mục:{Environment.NewLine}{_autoCatalogPath}", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void BrowseClientWallpaperButton_Click(object? sender, EventArgs e)
    {
        using var openDialog = new OpenFileDialog
        {
            Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Tất cả tệp (*.*)|*.*",
            CheckFileExists = true,
            Title = "Chọn hình nền Windows cho client"
        };

        if (openDialog.ShowDialog(this) == DialogResult.OK)
        {
            _clientWallpaperPathTextBox.Text = openDialog.FileName;
        }
    }

    private void ClearClientWallpaperButton_Click(object? sender, EventArgs e)
    {
        _clientWallpaperPathTextBox.Text = string.Empty;
    }

    private async void SaveSettingsButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            _clientWindowsWallpaperPath = _clientWallpaperPathTextBox.Text.Trim();
            _enableClientCloseApplicationHotKey = _enableClientCloseAppHotKeyCheckBox.Checked;
            await SaveUiSettingsAsync();
            await AutoExportCatalogAsync();
            ShowInfo("Đã lưu thiết lập và đồng bộ catalog cho client.");
        });
    }

    private async void RefreshLogsButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(LoadLogsAsync);
    }

    private async void ExportLogsCsvButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Tệp CSV (*.csv)|*.csv",
            Title = "Xuất lịch sử cập nhật",
            FileName = $"update-logs-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            var logs = (await _logRepository.GetRecentAsync())
                .OrderByDescending(item => item.CreatedAt)
                .ToList();

            var builder = new StringBuilder();
            builder.AppendLine("Thời gian,Trò chơi,Hành động,Trạng thái,Nội dung");
            foreach (var log in logs)
            {
                builder.AppendLine(string.Join(",",
                    EscapeCsv(log.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                    EscapeCsv(log.GameName),
                    EscapeCsv(log.Action),
                    EscapeCsv(log.Status),
                    EscapeCsv(log.Message)));
            }

            await File.WriteAllTextAsync(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
            ShowInfo($"Đã xuất CSV: {dialog.FileName}");
        });
    }

    private void BrowseSourceButton_Click(object? sender, EventArgs e)
    {
        if (CurrentSourceKind == UpdateSourceKind.Folder)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Chọn thư mục bản vá để chép vào thư mục trò chơi.",
                UseDescriptionForTitle = true,
                SelectedPath = _updateSourceTextBox.Text
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _updateSourceTextBox.Text = dialog.SelectedPath;
            }
        }
        else
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Tệp ZIP (*.zip)|*.zip",
                CheckFileExists = true,
                InitialDirectory = File.Exists(_updateSourceTextBox.Text)
                    ? Path.GetDirectoryName(_updateSourceTextBox.Text)
                    : string.Empty
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _updateSourceTextBox.Text = dialog.FileName;
            }
        }
    }

    private async void ApplyUpdateButton_Click(object? sender, EventArgs e)
    {
        if (SelectedGame is null)
        {
            ShowInfo("Vui lòng chọn trò chơi trước.");
            return;
        }

        var game = SelectedGame;
        var request = new UpdateRequest
        {
            Game = game,
            SourceKind = CurrentSourceKind,
            SourcePath = _updateSourceTextBox.Text.Trim(),
            TargetVersion = _updateVersionTextBox.Text.Trim(),
            CreateBackup = _backupCheckBox.Checked
        };

        var monitorRow = StartDownloadMonitor(game.Name, game.Id, resourceKey: null);

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ToggleUpdateControls(false);
            _updateOutputTextBox.Clear();
            _updateProgressBar.Value = 0;
            AppendUpdateMessage($"Bắt đầu cập nhật {game.Name}.");

            try
            {
                var progress = new Progress<UpdateProgressInfo>(info =>
                {
                    _updateProgressBar.Value = Math.Clamp(info.Percent, 0, 100);
                    AppendUpdateMessage(info.Message);
                    UpdateDownloadMonitor(monitorRow, info.Percent, "Đang tải", info.Message, info);
                });

                var backupPath = await _updateService.ApplyUpdateAsync(request, progress);
                if (!string.IsNullOrWhiteSpace(backupPath))
                {
                    AppendUpdateMessage($"Đã lưu bản sao lưu: {backupPath}");
                }

                UpdateDownloadMonitor(monitorRow, 100, "Hoàn tất", "Cập nhật hoàn tất.");
                await AutoExportCatalogAsync();
                await ReloadAllAsync(game.Id);
            }
            catch (Exception exception)
            {
                UpdateDownloadMonitor(monitorRow, monitorRow.ProgressPercent, "Thất bại", exception.Message);
                throw;
            }
        }, () => ToggleUpdateControls(true));
    }

    private DownloadMonitorRow StartDownloadMonitor(string gameName, int? gameId = null, string? resourceKey = null)
    {
        var row = new DownloadMonitorRow
        {
            StartedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            GameName = gameName,
            GameId = gameId,
            GameIdDisplay = gameId.HasValue && gameId.Value > 0 ? gameId.Value.ToString() : "-",
            ResourceKey = resourceKey ?? string.Empty,
            ProgressPercent = 0,
            ProgressDisplay = "0.0%",
            Status = "Đang tải",
            Message = "Khởi tạo tác vụ cập nhật.",
            TotalSizeGbDisplay = "-",
            RemainingMbDisplay = "-",
            RemainingTimeDisplay = "-",
            SpeedMbpsDisplay = "-"
        };

        _downloadMonitorRows.Insert(0, row);
        RefreshDownloadMonitorSerialNumbers();
        _downloadMonitorBinding.ResetBindings(false);

        if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
        {
            UpdateDownloadSummary();
        }

        SyncResourceRowFromMonitor(row);
        return row;
    }

    private void UpdateDownloadMonitor(
        DownloadMonitorRow row,
        int progressPercent,
        string status,
        string message,
        UpdateProgressInfo? progressInfo = null)
    {
        row.ProgressPercent = Math.Clamp(progressPercent, 0, 100);
        row.Status = status;
        row.Message = message;
        row.UpdatedAt = DateTime.Now;
        UpdateDownloadMonitorDerivedColumns(row, progressInfo);
        _downloadMonitorBinding.ResetBindings(false);

        if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
        {
            UpdateDownloadSummary();
        }

        SyncResourceRowFromMonitor(row);

        if (IsAutoRemovableCompletedStatus(status))
        {
            ScheduleAutoRemoveCompletedRow(row);
        }
        else
        {
            row.AutoRemoveScheduled = false;
        }
    }

    private void UpdateDownloadMonitorDerivedColumns(DownloadMonitorRow row, UpdateProgressInfo? progressInfo)
    {
        if (progressInfo?.TotalBytes is long totalBytes && totalBytes > 0)
        {
            row.TotalBytes = totalBytes;
        }

        if (progressInfo?.ProcessedBytes is long processedBytes && processedBytes >= 0)
        {
            row.ProcessedBytes = row.TotalBytes.HasValue
                ? Math.Clamp(processedBytes, 0L, row.TotalBytes.Value)
                : processedBytes;
        }

        if (progressInfo?.SpeedMbps is double speedMbps && speedMbps >= 0)
        {
            row.SpeedMbps = speedMbps;
        }
        else if (!string.Equals(row.Status, "Đang tải", StringComparison.OrdinalIgnoreCase))
        {
            row.SpeedMbps = null;
        }

        if (IsAutoRemovableCompletedStatus(row.Status) && row.TotalBytes.HasValue)
        {
            row.ProcessedBytes = row.TotalBytes.Value;
        }

        var precisePercent = row.TotalBytes.HasValue && row.TotalBytes.Value > 0
            ? (row.ProcessedBytes * 100d) / row.TotalBytes.Value
            : row.ProgressPercent;
        row.ProgressDisplay = $"{Math.Clamp(precisePercent, 0d, 100d):N1}%";

        row.TotalSizeGbDisplay = row.TotalBytes.HasValue && row.TotalBytes.Value > 0
            ? (row.TotalBytes.Value / 1024d / 1024d / 1024d).ToString("N2")
            : "-";

        if (row.TotalBytes.HasValue && row.TotalBytes.Value > 0)
        {
            var remainingBytes = Math.Max(0L, row.TotalBytes.Value - row.ProcessedBytes);
            var remainingMb = remainingBytes / 1024d / 1024d;
            row.RemainingMbDisplay = remainingMb.ToString("N2");

            if (remainingBytes <= 0)
            {
                row.RemainingTimeDisplay = "00:00:00";
            }
            else if (row.SpeedMbps.HasValue && row.SpeedMbps.Value > 0.05d)
            {
                var etaSeconds = remainingMb / row.SpeedMbps.Value;
                row.RemainingTimeDisplay = TimeSpan.FromSeconds(Math.Max(0d, etaSeconds)).ToString(@"hh\:mm\:ss");
            }
            else
            {
                row.RemainingTimeDisplay = "-";
            }
        }
        else
        {
            row.RemainingMbDisplay = "-";
            row.RemainingTimeDisplay = "-";
        }

        row.SpeedMbpsDisplay = row.SpeedMbps.HasValue && row.SpeedMbps.Value > 0
            ? row.SpeedMbps.Value.ToString("N1")
            : "-";
    }

    private void RefreshDownloadMonitorSerialNumbers()
    {
        for (var index = 0; index < _downloadMonitorRows.Count; index++)
        {
            _downloadMonitorRows[index].SerialNumber = index + 1;
        }
    }

    private async void ScheduleAutoRemoveCompletedRow(DownloadMonitorRow row)
    {
        if (row.AutoRemoveScheduled)
        {
            return;
        }

        row.AutoRemoveScheduled = true;

        const int maxChecks = 20;
        for (var attempt = 0; attempt < maxChecks; attempt++)
        {
            await Task.Delay(500);

            if (!_downloadMonitorRows.Contains(row))
            {
                row.AutoRemoveScheduled = false;
                return;
            }

            if (!IsAutoRemovableCompletedStatus(row.Status))
            {
                row.AutoRemoveScheduled = false;
                return;
            }

            if (IsResourceSyncRunning(row))
            {
                continue;
            }

            _downloadMonitorRows.Remove(row);
            RefreshDownloadMonitorSerialNumbers();
            _downloadMonitorBinding.ResetBindings(false);

            if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
            {
                UpdateDownloadSummary();
            }

            row.AutoRemoveScheduled = false;
            return;
        }

        row.AutoRemoveScheduled = false;
    }

    private static bool IsAutoRemovableCompletedStatus(string status)
    {
        return string.Equals(status, "Hoàn tất", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Thành công", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "HoÃ n táº¥t", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "ThÃ nh cÃ´ng", StringComparison.OrdinalIgnoreCase);
    }

    private void SyncResourceRowFromMonitor(DownloadMonitorRow monitorRow)
    {
        var resourceRow = FindResourceRowForMonitor(monitorRow);
        if (resourceRow is null)
        {
            return;
        }

        var previousDownloaded = resourceRow.IsDownloaded;

        if (string.Equals(monitorRow.Status, "Đang tải", StringComparison.OrdinalIgnoreCase))
        {
            resourceRow.DownloadStatus = $"Đang tải {monitorRow.ProgressPercent}%";
            resourceRow.DownloadSpeedDisplay = ExtractDownloadSpeedDisplay(monitorRow.Message);
        }
        else if (string.Equals(monitorRow.Status, "Tạm dừng", StringComparison.OrdinalIgnoreCase))
        {
            resourceRow.DownloadStatus = $"Tạm dừng {monitorRow.ProgressPercent}%";
            resourceRow.DownloadSpeedDisplay = "-";
        }
        else if (string.Equals(monitorRow.Status, "Đang dừng", StringComparison.OrdinalIgnoreCase))
        {
            resourceRow.DownloadStatus = $"Đang dừng {monitorRow.ProgressPercent}%";
            resourceRow.DownloadSpeedDisplay = "-";
        }
        else if (IsAutoRemovableCompletedStatus(monitorRow.Status))
        {
            resourceRow.IsDownloaded = true;
            resourceRow.DownloadStatus = "Đã tải";
            resourceRow.DownloadSpeedDisplay = "-";
            resourceRow.RunStatus = GetRunStatusAfterSync(resourceRow);
        }
        else if (string.Equals(monitorRow.Status, "Đã dừng", StringComparison.OrdinalIgnoreCase))
        {
            resourceRow.DownloadStatus = resourceRow.IsDownloaded ? "Đã tải" : "Chưa tải";
            resourceRow.DownloadSpeedDisplay = "-";
        }
        else if (string.Equals(monitorRow.Status, "Thất bại", StringComparison.OrdinalIgnoreCase))
        {
            resourceRow.DownloadStatus = resourceRow.IsDownloaded ? "Đã tải" : "Lỗi tải";
            resourceRow.DownloadSpeedDisplay = "-";
        }

        if (previousDownloaded != resourceRow.IsDownloaded)
        {
            ApplyResourceFilter(_currentResourceFilter);
            return;
        }

        _resourcesBinding.ResetBindings(false);
    }

    private static string ExtractDownloadSpeedDisplay(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "-";
        }

        var marker = "MB/s";
        var markerIndex = message.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex <= 0)
        {
            return "-";
        }

        var start = markerIndex - 1;
        while (start >= 0)
        {
            var character = message[start];
            if (char.IsDigit(character) || character == '.' || character == ',' || char.IsWhiteSpace(character))
            {
                start--;
                continue;
            }

            break;
        }

        start++;
        if (start >= markerIndex)
        {
            return "-";
        }

        var numberPart = message.Substring(start, markerIndex - start).Trim();
        if (string.IsNullOrWhiteSpace(numberPart))
        {
            return "-";
        }

        return $"{numberPart} MB/s";
    }

    private ResourceGameRow? FindResourceRowForMonitor(DownloadMonitorRow monitorRow)
    {
        if (!string.IsNullOrWhiteSpace(monitorRow.ResourceKey))
        {
            var bySourceKey = _allResourceRows.FirstOrDefault(row =>
                string.Equals(row.SourceKey, monitorRow.ResourceKey, StringComparison.OrdinalIgnoreCase));
            if (bySourceKey is not null)
            {
                return bySourceKey;
            }
        }

        return _allResourceRows.FirstOrDefault(row =>
            string.Equals(row.Name, monitorRow.GameName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetRunStatusAfterSync(ResourceGameRow row)
    {
        var launchPath = FindPreferredExecutablePath(row.InstallPath, row.Name);
        var runReady = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
        if (runReady)
        {
            return "Sẵn sàng chạy";
        }

        return row.IsManaged ? "Thiếu tệp chạy" : "Chưa cấu hình tệp chạy";
    }

    private void EnsureDownloadMonitorContextMenu()
    {
        if (_downloadMonitorContextMenuInitialized)
        {
            return;
        }

        _downloadMonitorContextMenuInitialized = true;
        EnsureDownloadBandwidthPresetMenuItems();

        _downloadMonitorContextMenu.Items.Add(_pauseDownloadMenuItem);
        _downloadMonitorContextMenu.Items.Add(_resumeDownloadMenuItem);
        _downloadMonitorContextMenu.Items.Add(_pauseAllDownloadsMenuItem);
        _downloadMonitorContextMenu.Items.Add(_resumeAllDownloadsMenuItem);
        _downloadMonitorContextMenu.Items.Add(_stopDownloadMenuItem);
        _downloadMonitorContextMenu.Items.Add(_setDownloadBandwidthMenuItem);
        _downloadMonitorContextMenu.Items.Add(_retryDownloadFromIdcMenuItem);
        _downloadMonitorContextMenu.Items.Add(_removeDownloadMenuItem);
        _downloadMonitorContextMenu.Items.Add(new ToolStripSeparator());
        _downloadMonitorContextMenu.Items.Add(_removeFinishedDownloadsMenuItem);

        _downloadMonitorContextMenu.Opening += DownloadMonitorContextMenu_Opening;
        _pauseDownloadMenuItem.Click += PauseDownloadMenuItem_Click;
        _resumeDownloadMenuItem.Click += ResumeDownloadMenuItem_Click;
        _pauseAllDownloadsMenuItem.Click += PauseAllDownloadsMenuItem_Click;
        _resumeAllDownloadsMenuItem.Click += ResumeAllDownloadsMenuItem_Click;
        _stopDownloadMenuItem.Click += StopDownloadMenuItem_Click;
        _retryDownloadFromIdcMenuItem.Click += RetryDownloadFromIdcMenuItem_Click;
        _removeDownloadMenuItem.Click += RemoveDownloadMenuItem_Click;
        _removeFinishedDownloadsMenuItem.Click += RemoveFinishedDownloadsMenuItem_Click;

        _downloadMonitorGrid.ContextMenuStrip = _downloadMonitorContextMenu;
        _downloadMonitorGrid.MouseDown += DownloadMonitorGrid_MouseDown;
    }

    private void EnsureDownloadBandwidthPresetMenuItems()
    {
        if (_downloadBandwidthPresetMenuItems.Count > 0)
        {
            return;
        }

        for (var mbps = 1; mbps <= 10; mbps++)
        {
            var item = new ToolStripMenuItem($"{mbps} MB/s")
            {
                Tag = mbps
            };

            item.Click += DownloadBandwidthPresetMenuItem_Click;
            _downloadBandwidthPresetMenuItems.Add(item);
            _setDownloadBandwidthMenuItem.DropDownItems.Add(item);
        }
    }

    private void DownloadMonitorGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        var column = _downloadMonitorGrid.Columns[e.ColumnIndex];
        if (!string.Equals(column.Name, DownloadProgressColumnName, StringComparison.Ordinal))
        {
            return;
        }

        e.Handled = true;
        e.PaintBackground(e.CellBounds, true);

        var graphics = e.Graphics;
        if (graphics is null)
        {
            return;
        }

        var row = _downloadMonitorGrid.Rows[e.RowIndex].DataBoundItem as DownloadMonitorRow;
        var percent = Math.Clamp(row?.ProgressPercent ?? 0, 0, 100);
        var progressText = row?.ProgressDisplay ?? $"{percent:0.0}%";

        var barBounds = new Rectangle(
            e.CellBounds.X + 4,
            e.CellBounds.Y + 4,
            Math.Max(1, e.CellBounds.Width - 8),
            Math.Max(1, e.CellBounds.Height - 8));

        using (var borderPen = new Pen(Color.Silver))
        {
            graphics.DrawRectangle(borderPen, barBounds);
        }

        if (percent > 0)
        {
            var fillWidth = (int)Math.Round((barBounds.Width - 1) * (percent / 100d));
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(
                    barBounds.X + 1,
                    barBounds.Y + 1,
                    Math.Max(1, fillWidth),
                    Math.Max(1, barBounds.Height - 1));

                using var fillBrush = new SolidBrush(Color.FromArgb(64, 196, 99));
                graphics.FillRectangle(fillBrush, fillRect);
            }
        }

        var textColor = _downloadMonitorGrid.Rows[e.RowIndex].Selected ? Color.White : Color.Black;
        var textFont = e.CellStyle?.Font ?? _downloadMonitorGrid.Font;
        TextRenderer.DrawText(
            graphics,
            progressText,
            textFont,
            barBounds,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        e.Paint(e.CellBounds, DataGridViewPaintParts.Border);
    }

    private void DownloadMonitorGrid_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var hit = _downloadMonitorGrid.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0 || hit.RowIndex >= _downloadMonitorGrid.Rows.Count)
        {
            return;
        }

        _downloadMonitorGrid.ClearSelection();
        var row = _downloadMonitorGrid.Rows[hit.RowIndex];
        row.Selected = true;
        _downloadMonitorGrid.CurrentCell = row.Cells[0];
    }

    private void DownloadMonitorContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var hasRunningTasks = _activeResourceSyncControls.Values.Any(control => !control.IsPaused);
        var hasPausedTasks = _activeResourceSyncControls.Values.Any(control => control.IsPaused);
        _pauseAllDownloadsMenuItem.Enabled = hasRunningTasks;
        _resumeAllDownloadsMenuItem.Enabled = hasPausedTasks;

        var selected = GetSelectedDownloadMonitorRow();
        if (selected is null)
        {
            _pauseDownloadMenuItem.Enabled = false;
            _resumeDownloadMenuItem.Enabled = false;
            _stopDownloadMenuItem.Enabled = false;
            _setDownloadBandwidthMenuItem.Enabled = false;
            _retryDownloadFromIdcMenuItem.Enabled = false;
            _removeDownloadMenuItem.Enabled = false;
            _removeFinishedDownloadsMenuItem.Enabled = _downloadMonitorRows.Count > 0;
            SetCheckedBandwidthPreset(0);
            return;
        }

        var isRunning = IsResourceSyncRunning(selected);
        var isPaused = IsResourceSyncPaused(selected);
        var selectedControl = TryGetResourceSyncToken(selected, out var syncControl) ? syncControl : null;
        var retryCandidate = !isRunning &&
                             string.Equals(selected.Status, "Thất bại", StringComparison.OrdinalIgnoreCase) &&
                             FindResourceRowForMonitor(selected) is { HasSource: true };
        _pauseDownloadMenuItem.Enabled = isRunning && !isPaused;
        _resumeDownloadMenuItem.Enabled = isRunning && isPaused;
        _stopDownloadMenuItem.Enabled = isRunning;
        _setDownloadBandwidthMenuItem.Enabled = isRunning && selectedControl is not null;
        _retryDownloadFromIdcMenuItem.Enabled = retryCandidate;
        _removeDownloadMenuItem.Enabled = !isRunning;
        _removeFinishedDownloadsMenuItem.Enabled = _downloadMonitorRows.Any(row => !IsResourceSyncRunning(row));
        SetCheckedBandwidthPreset(selectedControl?.BandwidthLimitMbps ?? 0);
    }

    private void PauseDownloadMenuItem_Click(object? sender, EventArgs e)
    {
        var row = GetSelectedDownloadMonitorRow();
        if (row is null)
        {
            return;
        }

        if (!TryGetResourceSyncToken(row, out var syncControl))
        {
            ShowInfo("Tác vụ này không còn chạy.");
            return;
        }

        syncControl.Pause();
        UpdateDownloadMonitor(row, row.ProgressPercent, "Tạm dừng", "Tác vụ đã tạm dừng.");
    }

    private void ResumeDownloadMenuItem_Click(object? sender, EventArgs e)
    {
        var row = GetSelectedDownloadMonitorRow();
        if (row is null)
        {
            return;
        }

        if (!TryGetResourceSyncToken(row, out var syncControl))
        {
            ShowInfo("Tác vụ này không còn chạy.");
            return;
        }

        syncControl.Resume();
        UpdateDownloadMonitor(row, row.ProgressPercent, "Đang tải", "Tác vụ đã tiếp tục.");
    }

    private void PauseAllDownloadsMenuItem_Click(object? sender, EventArgs e)
    {
        var runningRows = _activeResourceSyncControls
            .Where(item => !item.Value.IsPaused)
            .Select(item => item.Key)
            .ToList();

        if (runningRows.Count == 0)
        {
            return;
        }

        foreach (var row in runningRows)
        {
            if (!TryGetResourceSyncToken(row, out var syncControl))
            {
                continue;
            }

            syncControl.Pause();
            UpdateDownloadMonitor(row, row.ProgressPercent, "Tạm dừng", "Đã tạm dừng theo yêu cầu hàng loạt.");
        }
    }

    private void ResumeAllDownloadsMenuItem_Click(object? sender, EventArgs e)
    {
        var pausedRows = _activeResourceSyncControls
            .Where(item => item.Value.IsPaused)
            .Select(item => item.Key)
            .ToList();

        if (pausedRows.Count == 0)
        {
            return;
        }

        foreach (var row in pausedRows)
        {
            if (!TryGetResourceSyncToken(row, out var syncControl))
            {
                continue;
            }

            syncControl.Resume();
            UpdateDownloadMonitor(row, row.ProgressPercent, "Đang tải", "Đã tiếp tục theo yêu cầu hàng loạt.");
        }
    }

    private void DownloadBandwidthPresetMenuItem_Click(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem { Tag: int mbps })
        {
            return;
        }

        var row = GetSelectedDownloadMonitorRow();
        if (row is null)
        {
            return;
        }

        if (!TryGetResourceSyncToken(row, out var syncControl))
        {
            ShowInfo("Tác vụ này không còn chạy.");
            return;
        }

        syncControl.SetBandwidthLimitMbps(mbps);
        SetCheckedBandwidthPreset(mbps);
        UpdateDownloadMonitor(row, row.ProgressPercent, row.Status, $"Đã đặt giới hạn băng thông: {mbps} MB/s.");
    }

    private void SetCheckedBandwidthPreset(int mbps)
    {
        foreach (var item in _downloadBandwidthPresetMenuItems)
        {
            item.Checked = item.Tag is int value && value == mbps;
        }
    }

    private void StopDownloadMenuItem_Click(object? sender, EventArgs e)
    {
        var row = GetSelectedDownloadMonitorRow();
        if (row is null)
        {
            return;
        }

        if (!TryGetResourceSyncToken(row, out var syncControl))
        {
            ShowInfo("Tác vụ này không còn chạy.");
            return;
        }

        UpdateDownloadMonitor(row, row.ProgressPercent, "Đang dừng", "Đang gửi yêu cầu dừng...");
        syncControl.Cancel();
    }

    private async void RetryDownloadFromIdcMenuItem_Click(object? sender, EventArgs e)
    {
        var monitorRow = GetSelectedDownloadMonitorRow();
        if (monitorRow is null)
        {
            return;
        }

        if (IsResourceSyncRunning(monitorRow))
        {
            ShowInfo("Tác vụ đang chạy, không thể tải lại.");
            return;
        }

        var resourceRow = FindResourceRowForMonitor(monitorRow);
        if (resourceRow is null || !resourceRow.HasSource)
        {
            ShowInfo("Không tìm thấy nguồn IDC để tải lại tác vụ này.");
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ToggleResourceSyncControls(false);
            UpdateResourceRootsFromInputs();
            await SaveUiSettingsAsync();

            int? gameId = null;
            try
            {
                gameId = await SyncResourceRowAsync(resourceRow);
                await AutoExportCatalogAsync();
            }
            finally
            {
                await ReloadAllAsync(gameId ?? SelectedGame?.Id);
            }
        }, () => ToggleResourceSyncControls(true));
    }

    private void RemoveDownloadMenuItem_Click(object? sender, EventArgs e)
    {
        var row = GetSelectedDownloadMonitorRow();
        if (row is null)
        {
            return;
        }

        if (IsResourceSyncRunning(row))
        {
            ShowInfo("Vui lòng dừng tác vụ trước khi xóa dòng.");
            return;
        }

        _downloadMonitorRows.Remove(row);
        RefreshDownloadMonitorSerialNumbers();
        _downloadMonitorBinding.ResetBindings(false);

        if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
        {
            UpdateDownloadSummary();
        }
    }

    private void RemoveFinishedDownloadsMenuItem_Click(object? sender, EventArgs e)
    {
        var removableRows = _downloadMonitorRows
            .Where(row => !IsResourceSyncRunning(row))
            .ToList();

        if (removableRows.Count == 0)
        {
            return;
        }

        foreach (var row in removableRows)
        {
            _downloadMonitorRows.Remove(row);
        }

        RefreshDownloadMonitorSerialNumbers();
        _downloadMonitorBinding.ResetBindings(false);

        if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
        {
            UpdateDownloadSummary();
        }
    }

    private DownloadMonitorRow? GetSelectedDownloadMonitorRow()
    {
        return _downloadMonitorGrid.CurrentRow?.DataBoundItem as DownloadMonitorRow;
    }

    private void RegisterResourceSyncToken(DownloadMonitorRow row, ResourceSyncTaskControl syncControl)
    {
        _activeResourceSyncControls[row] = syncControl;
    }

    private void UnregisterResourceSyncToken(DownloadMonitorRow row)
    {
        if (_activeResourceSyncControls.TryGetValue(row, out var syncControl))
        {
            _activeResourceSyncControls.Remove(row);
            syncControl.Dispose();
        }
    }

    private bool TryGetResourceSyncToken(DownloadMonitorRow row, out ResourceSyncTaskControl syncControl)
    {
        return _activeResourceSyncControls.TryGetValue(row, out syncControl!);
    }

    private bool IsResourceSyncRunning(DownloadMonitorRow row)
    {
        return _activeResourceSyncControls.ContainsKey(row);
    }

    private bool IsResourceSyncPaused(DownloadMonitorRow row)
    {
        return _activeResourceSyncControls.TryGetValue(row, out var syncControl) && syncControl.IsPaused;
    }

    private void SeedDownloadMonitorRows(IReadOnlyList<UpdateLogEntry> logs)
    {
        if (_activeResourceSyncControls.Count > 0 ||
            _downloadMonitorRows.Any(row =>
                string.Equals(row.Status, "Đang tải", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.Status, "Tạm dừng", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.Status, "Đang dừng", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _downloadMonitorRows.Clear();

        var recentUpdateLogs = logs
            .Where(log =>
                log.Action.Contains("Cập nhật", StringComparison.OrdinalIgnoreCase) ||
                log.Action.Contains("Đồng bộ", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(log => log.CreatedAt)
            .Take(30);

        foreach (var log in recentUpdateLogs)
        {
            var row = new DownloadMonitorRow
            {
                StartedAt = log.CreatedAt.ToLocalTime(),
                UpdatedAt = log.CreatedAt.ToLocalTime(),
                GameName = log.GameName,
                ProgressPercent = string.Equals(log.Status, "Thành công", StringComparison.OrdinalIgnoreCase) ? 100 : 0,
                Status = log.Status,
                Message = log.Message,
                GameIdDisplay = "-",
                ProgressDisplay = string.Equals(log.Status, "Thành công", StringComparison.OrdinalIgnoreCase) ? "100.0%" : "0.0%",
                TotalSizeGbDisplay = "-",
                RemainingMbDisplay = "-",
                RemainingTimeDisplay = "-",
                SpeedMbpsDisplay = "-"
            };

            UpdateDownloadMonitorDerivedColumns(row, null);
            _downloadMonitorRows.Add(row);
        }

        RefreshDownloadMonitorSerialNumbers();
        _downloadMonitorBinding.ResetBindings(false);

        if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
        {
            UpdateDownloadSummary();
        }
    }

    private async Task ExecuteWithErrorHandlingAsync(Func<Task> action, Action? onFinally = null)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // User requested to stop a running task.
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            onFinally?.Invoke();
        }
    }

    private void ToggleGameControls(bool enabled)
    {
        _scanManifestButton.Enabled = enabled;
        _gamesGrid.Enabled = enabled;
    }

    private void ToggleUpdateControls(bool enabled)
    {
        _applyUpdateButton.Enabled = enabled;
        _browseSourceButton.Enabled = enabled;
        _updateSourceKindComboBox.Enabled = enabled;
        _updateGameComboBox.Enabled = enabled;
        _backupCheckBox.Enabled = enabled;
    }

    private void ToggleResourceSyncControls(bool enabled)
    {
        _saveResourceSettingsButton.Enabled = enabled;
        _syncSelectedResourceButton.Enabled = enabled;
        _browseResourceSourceButton.Enabled = enabled;
        _browseResourceTargetButton.Enabled = enabled;
        _resourceSourceRootTextBox.Enabled = enabled;
        _resourceTargetRootTextBox.Enabled = enabled;
        _resourceBandwidthLimitNumeric.Enabled = enabled;
        _resourceTree.Enabled = true;
        _resourcesGrid.Enabled = true;
        _downloadMonitorGrid.Enabled = true;
    }

    private void AppendUpdateMessage(string message)
    {
        _updateOutputTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private UpdateSourceKind CurrentSourceKind =>
        _updateSourceKindComboBox.SelectedItem is UpdateSourceOption option ? option.Kind : UpdateSourceKind.Folder;

    private void ShowInfo(string message)
    {
        MessageBox.Show(this, message, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task AutoExportCatalogAsync()
    {
        if (string.IsNullOrWhiteSpace(_autoCatalogPath))
        {
            return;
        }

        try
        {
            await _catalogService.ExportToFileAsync(_autoCatalogPath, BuildClientPolicy());
        }
        catch (Exception exception)
        {
            AppendUpdateMessage($"Cảnh báo: tự xuất danh mục thất bại - {exception.Message}");
        }
    }

    private LauncherClientPolicy BuildClientPolicy()
    {
        return new LauncherClientPolicy
        {
            ClientWindowsWallpaperPath = _clientWindowsWallpaperPath.Trim(),
            EnableCloseRunningApplicationHotKey = _enableClientCloseApplicationHotKey
        };
    }

    private async Task LoadUiSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<ServerUiSettings>(json);
            if (settings is not null)
            {
                if (!string.IsNullOrWhiteSpace(settings.ClientCatalogPath))
                {
                    _autoCatalogPath = settings.ClientCatalogPath;
                }

                if (!string.IsNullOrWhiteSpace(settings.ResourceSourceRootPath))
                {
                    _resourceSourceRootPath = settings.ResourceSourceRootPath;
                }

                if (!string.IsNullOrWhiteSpace(settings.ResourceTargetRootPath))
                {
                    _resourceTargetRootPath = settings.ResourceTargetRootPath;
                }

                _resourceBandwidthLimitMbps = Math.Max(0, settings.ResourceBandwidthLimitMbps);
                _clientWindowsWallpaperPath = settings.ClientWindowsWallpaperPath?.Trim() ?? string.Empty;
                _enableClientCloseApplicationHotKey = settings.EnableClientCloseApplicationHotKey;

                if (!string.IsNullOrWhiteSpace(settings.UiFontSizeMode) &&
                    Enum.TryParse<UiFontSizeMode>(settings.UiFontSizeMode, true, out var parsedFontSizeMode))
                {
                    _uiFontSizeMode = parsedFontSizeMode;
                }
            }

            _resourceSourceRootTextBox.Text = _resourceSourceRootPath;
            _resourceTargetRootTextBox.Text = _resourceTargetRootPath;
            _resourceBandwidthLimitNumeric.Value = Math.Min(_resourceBandwidthLimitNumeric.Maximum, _resourceBandwidthLimitMbps);
            _clientWallpaperPathTextBox.Text = _clientWindowsWallpaperPath;
            _enableClientCloseAppHotKeyCheckBox.Checked = _enableClientCloseApplicationHotKey;
            SetFontSizeSelection(_uiFontSizeMode);
            ApplyUiFontSize(_uiFontSizeMode);
        }
        catch
        {
            // Keep default path when settings file is invalid.
        }
    }

    private async Task SaveUiSettingsAsync()
    {
        var settings = new ServerUiSettings
        {
            ClientCatalogPath = _autoCatalogPath,
            ResourceSourceRootPath = _resourceSourceRootPath,
            ResourceTargetRootPath = _resourceTargetRootPath,
            ResourceBandwidthLimitMbps = _resourceBandwidthLimitMbps,
            ClientWindowsWallpaperPath = _clientWindowsWallpaperPath,
            EnableClientCloseApplicationHotKey = _enableClientCloseApplicationHotKey,
            UiFontSizeMode = _uiFontSizeMode.ToString()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsFilePath, json, Encoding.UTF8);
    }

    private enum ResourceFilterKind
    {
        All,
        Missing,
        Downloaded,
        DownloadMonitor
    }

    private sealed class ResourceGameRow
    {
        public int Id { get; init; }

        public int? ManagedGameId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public string SourceKey { get; init; } = string.Empty;

        public string SourceStatus { get; init; } = string.Empty;

        public string SourcePath { get; init; } = string.Empty;

        public string DownloadStatus { get; set; } = string.Empty;

        public string DownloadSpeedDisplay { get; set; } = "-";

        public string RunStatus { get; set; } = string.Empty;

        public string FileCountDisplay { get; init; } = "-";

        public string SizeGbDisplay { get; init; } = "-";

        public DateTime? LastUpdatedAt { get; init; }

        public string InstallPath { get; init; } = string.Empty;

        public bool IsDownloaded { get; set; }

        public bool IsManaged { get; init; }

        public bool HasSource { get; init; }
    }

    private sealed class SourceFolderEntry
    {
        public string Key { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string FullPath { get; init; } = string.Empty;
    }

    private sealed class ResourceSyncTaskControl : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        private volatile bool _isPaused;
        private long _bandwidthLimitBytesPerSecond;

        public CancellationToken CancellationToken => _cancellation.Token;

        public bool IsPaused => _isPaused;

        public long BandwidthLimitBytesPerSecond => Interlocked.Read(ref _bandwidthLimitBytesPerSecond);

        public int BandwidthLimitMbps
        {
            get
            {
                var bytesPerSecond = BandwidthLimitBytesPerSecond;
                return bytesPerSecond <= 0 ? 0 : (int)Math.Max(1, bytesPerSecond / (1024L * 1024L));
            }
        }

        public void Pause()
        {
            _isPaused = true;
        }

        public void Resume()
        {
            _isPaused = false;
        }

        public void Cancel()
        {
            _cancellation.Cancel();
        }

        public void SetBandwidthLimitMbps(int mbps)
        {
            var normalizedMbps = Math.Clamp(mbps, 0, 10000);
            var bytesPerSecond = normalizedMbps <= 0 ? 0L : normalizedMbps * 1024L * 1024L;
            Interlocked.Exchange(ref _bandwidthLimitBytesPerSecond, bytesPerSecond);
        }

        public async ValueTask WaitIfPausedAsync(CancellationToken cancellationToken)
        {
            while (_isPaused)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            _cancellation.Dispose();
        }
    }

    private sealed class DownloadMonitorRow
    {
        public int SerialNumber { get; set; }

        public DateTime StartedAt { get; set; }

        public string GameName { get; set; } = string.Empty;

        public int? GameId { get; set; }

        public string GameIdDisplay { get; set; } = "-";

        public string ResourceKey { get; set; } = string.Empty;

        public int ProgressPercent { get; set; }

        public string ProgressDisplay { get; set; } = "0.0%";

        public string Status { get; set; } = string.Empty;

        public long? TotalBytes { get; set; }

        public long ProcessedBytes { get; set; }

        public double? SpeedMbps { get; set; }

        public string TotalSizeGbDisplay { get; set; } = "-";

        public string RemainingMbDisplay { get; set; } = "-";

        public string RemainingTimeDisplay { get; set; } = "-";

        public string SpeedMbpsDisplay { get; set; } = "-";

        public DateTime UpdatedAt { get; set; }

        public string Message { get; set; } = string.Empty;

        public bool AutoRemoveScheduled { get; set; }
    }

    private sealed class UpdateSourceOption
    {
        public UpdateSourceKind Kind { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    private enum UiFontSizeMode
    {
        Normal,
        Big,
        VeryBig
    }

    private sealed class FontSizeOption
    {
        public UiFontSizeMode Mode { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    private sealed class ServerUiSettings
    {
        public string ClientCatalogPath { get; set; } = string.Empty;

        public string ResourceSourceRootPath { get; set; } = string.Empty;

        public string ResourceTargetRootPath { get; set; } = string.Empty;

        public int ResourceBandwidthLimitMbps { get; set; }

        public string ClientWindowsWallpaperPath { get; set; } = string.Empty;

        public bool EnableClientCloseApplicationHotKey { get; set; } = true;

        public string UiFontSizeMode { get; set; } = string.Empty;
    }
}
