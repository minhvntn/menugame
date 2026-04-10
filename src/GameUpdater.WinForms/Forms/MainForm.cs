using System.ComponentModel;
using System.Text;
using System.Text.Json;
using GameUpdater.Core.Abstractions;
using GameUpdater.Core.Services;
using GameUpdater.Shared.Models;

namespace GameUpdater.WinForms.Forms;

public sealed class MainForm : Form
{
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
    private readonly TextBox _manifestTextBox = new();
    private readonly TextBox _updateSourceTextBox = new();
    private readonly TextBox _updateVersionTextBox = new();
    private readonly TextBox _updateOutputTextBox = new();
    private readonly ComboBox _updateSourceKindComboBox = new();
    private readonly ComboBox _updateGameComboBox = new();
    private readonly CheckBox _backupCheckBox = new();
    private readonly Label _selectedGameLabel = new();
    private readonly Label _selectedPathLabel = new();
    private readonly Label _resourceSummaryLabel = new();
    private readonly TextBox _resourceSourceRootTextBox = new();
    private readonly TextBox _resourceTargetRootTextBox = new();
    private readonly NumericUpDown _resourceBandwidthLimitNumeric = new();
    private readonly ProgressBar _updateProgressBar = new();
    private readonly Button _applyUpdateButton = new();
    private readonly Button _browseSourceButton = new();
    private readonly Button _scanManifestButton = new();
    private readonly TreeView _resourceTree = new();
    private readonly Button _browseResourceSourceButton = new();
    private readonly Button _browseResourceTargetButton = new();
    private readonly Button _saveResourceSettingsButton = new();
    private readonly Button _syncSelectedResourceButton = new();
    private readonly Button _syncAllResourcesButton = new();

    private readonly BindingList<DownloadMonitorRow> _downloadMonitorRows = new();
    private readonly List<ResourceGameRow> _allResourceRows = new();
    private readonly Dictionary<DownloadMonitorRow, ResourceSyncTaskControl> _activeResourceSyncControls = new();
    private readonly ContextMenuStrip _resourcesContextMenu = new();
    private readonly ToolStripMenuItem _syncMissingFromIdcMenuItem = new("Đồng bộ file thiếu từ IDC");
    private readonly ContextMenuStrip _downloadMonitorContextMenu = new();
    private readonly ToolStripMenuItem _pauseDownloadMenuItem = new("Tạm dừng");
    private readonly ToolStripMenuItem _resumeDownloadMenuItem = new("Tiếp tục");
    private readonly ToolStripMenuItem _stopDownloadMenuItem = new("Dừng tải");
    private readonly ToolStripMenuItem _removeDownloadMenuItem = new("Xóa dòng");
    private readonly ToolStripMenuItem _removeFinishedDownloadsMenuItem = new("Xóa tác vụ đã xong");
    private bool _downloadMonitorContextMenuInitialized;
    private bool _resourcesContextMenuInitialized;

    private string _autoCatalogPath = string.Empty;
    private string _resourceSourceRootPath = @"E:\GameOnlineIDC";
    private string _resourceTargetRootPath = @"E:\GameOnline";
    private int _resourceBandwidthLimitMbps;
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
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadUiSettingsAsync();
        await ReloadAllAsync();
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
        RebuildResourceRows(games);

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
        SeedDownloadMonitorRows(logs);
    }

    private async Task RefreshSelectedGameDetailsAsync()
    {
        if (SelectedGame is null)
        {
            _selectedGameLabel.Text = "Trò chơi đang chọn: chưa có";
            _selectedPathLabel.Text = "Đường dẫn cài đặt: -";
            _manifestTextBox.Text = "Thêm trò chơi và bấm Quét manifest để xem chi tiết tệp.";
            _updateVersionTextBox.Text = string.Empty;
            return;
        }

        _selectedGameLabel.Text = $"Trò chơi đang chọn: {SelectedGame.Name} ({SelectedGame.Version})";
        _selectedPathLabel.Text = $"Đường dẫn cài đặt: {SelectedGame.InstallPath}";
        _updateVersionTextBox.Text = SelectedGame.Version;

        try
        {
            _manifestTextBox.Text = await _gameService.GetManifestPreviewAsync(SelectedGame);
        }
        catch (Exception exception)
        {
            _manifestTextBox.Text = $"Không thể tải manifest.{Environment.NewLine}{Environment.NewLine}{exception.Message}";
        }
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

        Controls.Add(tabs);
    }

    private TabPage BuildGamesTab()
    {
        var page = new TabPage("Trò chơi");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));

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

        toolbar.Controls.Add(CreateButton("Thêm trò chơi", AddGameButton_Click));
        toolbar.Controls.Add(CreateButton("Sửa trò chơi", EditGameButton_Click));
        toolbar.Controls.Add(CreateButton("Xóa trò chơi", DeleteGameButton_Click));

        _scanManifestButton.Text = "Quét manifest";
        _scanManifestButton.AutoSize = true;
        _scanManifestButton.Click += ScanManifestButton_Click;
        toolbar.Controls.Add(_scanManifestButton);

        toolbar.Controls.Add(CreateButton("Xuất danh mục client", ExportCatalogButton_Click));
        toolbar.Controls.Add(CreateButton("Làm mới", RefreshButton_Click));

        ConfigureGamesGrid();
        leftPanel.Controls.Add(toolbar, 0, 0);
        leftPanel.Controls.Add(_gamesGrid, 0, 1);

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            Padding = new Padding(8)
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _selectedGameLabel.Dock = DockStyle.Fill;
        _selectedGameLabel.TextAlign = ContentAlignment.MiddleLeft;
        _selectedGameLabel.Text = "Trò chơi đang chọn: chưa có";

        _selectedPathLabel.Dock = DockStyle.Fill;
        _selectedPathLabel.TextAlign = ContentAlignment.MiddleLeft;
        _selectedPathLabel.Text = "Đường dẫn cài đặt: -";

        var manifestLabel = new Label
        {
            Text = "Xem trước manifest",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _manifestTextBox.Dock = DockStyle.Fill;
        _manifestTextBox.Multiline = true;
        _manifestTextBox.ScrollBars = ScrollBars.Both;
        _manifestTextBox.Font = new Font("Consolas", 10);
        _manifestTextBox.ReadOnly = true;

        rightPanel.Controls.Add(_selectedGameLabel, 0, 0);
        rightPanel.Controls.Add(_selectedPathLabel, 0, 1);
        rightPanel.Controls.Add(manifestLabel, 0, 2);
        rightPanel.Controls.Add(_manifestTextBox, 0, 3);

        root.Controls.Add(leftPanel, 0, 0);
        root.Controls.Add(rightPanel, 1, 0);

        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildResourcesTab()
    {
        var page = new TabPage("Tài nguyên");

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 260
        };

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

        _syncAllResourcesButton.Text = "Tải tất cả trò chơi";
        _syncAllResourcesButton.AutoSize = true;
        _syncAllResourcesButton.Click += SyncAllResourcesButton_Click;

        actionsRow.Controls.Add(_saveResourceSettingsButton);
        actionsRow.Controls.Add(_syncSelectedResourceButton);
        actionsRow.Controls.Add(_syncAllResourcesButton);

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
    if (_resourceSyncService is not null)
    {
        if (_resourcesGrid.Visible == false)
        {
            ShowInfo("Vui lòng chuyển sang danh sách tài nguyên để chọn trò chơi.");
            return;
        }

        if (_resourcesGrid.CurrentRow?.DataBoundItem is not ResourceGameRow selectedRow)
        {
            ShowInfo("Vui lòng chọn trò chơi cần tải.");
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
                gameId = await SyncResourceRowAsync(selectedRow);
                await AutoExportCatalogAsync();
            }
            finally
            {
                await ReloadAllAsync(gameId ?? SelectedGame?.Id);
            }
        }, () => ToggleResourceSyncControls(true));
        return;
    }

        if (_resourcesGrid.Visible == false)
        {
            ShowInfo("Vui lòng chuyển sang danh sách tài nguyên để chọn trò chơi.");
            return;
        }

        if (_resourcesGrid.CurrentRow?.DataBoundItem is not ResourceGameRow row)
        {
            ShowInfo("Vui lòng chọn trò chơi cần tải.");
            return;
        }

        var game = FindGameById(row.Id);
        if (game is null)
        {
            ShowInfo("Không tìm thấy trò chơi trong danh sách quản lý.");
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ToggleResourceSyncControls(false);
            UpdateResourceRootsFromInputs();
            await SaveUiSettingsAsync();
            await SyncGameFromResourceAsync(game);
            await ReloadAllAsync(game.Id);
        }, () => ToggleResourceSyncControls(true));
    }

private async void SyncAllResourcesButton_Click(object? sender, EventArgs e)
{
    if (_resourceSyncService is not null)
    {
        var rows = _allResourceRows
            .Where(row => row.HasSource)
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rows.Count == 0)
        {
            ShowInfo("Chưa có trò chơi nào để tải.");
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Bạn muốn tải tài nguyên cho toàn bộ {rows.Count} trò chơi?",
            "Xác nhận tải tài nguyên",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ToggleResourceSyncControls(false);
            UpdateResourceRootsFromInputs();
            await SaveUiSettingsAsync();

            foreach (var row in rows)
            {
                try
                {
                    await SyncResourceRowAsync(row);
                }
                catch (OperationCanceledException)
                {
                    AppendUpdateMessage("Đã dừng tác vụ tải tài nguyên theo yêu cầu.");
                    break;
                }
            }

            await AutoExportCatalogAsync();
            await ReloadAllAsync(SelectedGame?.Id);
        }, () => ToggleResourceSyncControls(true));
        return;
    }

        var games = (_gamesBinding.DataSource as IEnumerable<GameRecord>)?.ToList() ?? new List<GameRecord>();
        if (games.Count == 0)
        {
            ShowInfo("Chưa có trò chơi nào để tải.");
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Bạn muốn tải tài nguyên cho toàn bộ {games.Count} trò chơi?",
            "Xác nhận tải tài nguyên",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ToggleResourceSyncControls(false);
            UpdateResourceRootsFromInputs();
            await SaveUiSettingsAsync();

            foreach (var game in games)
            {
                await SyncGameFromResourceAsync(game);
            }

            await ReloadAllAsync(SelectedGame?.Id);
        }, () => ToggleResourceSyncControls(true));
    }

    private async Task SyncGameFromResourceLegacyAsync(GameRecord game)
    {
        var monitorRow = StartDownloadMonitor(game.Name);
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

                UpdateDownloadMonitor(monitorRow, info.Percent, "Đang tải", info.Message);
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
        var monitorRow = StartDownloadMonitor(game.Name);
        var syncControl = new ResourceSyncTaskControl();
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

                UpdateDownloadMonitor(monitorRow, info.Percent, "Đang tải", info.Message);
            });

            var bandwidthLimitBytesPerSecond = _resourceBandwidthLimitMbps <= 0
                ? (long?)null
                : _resourceBandwidthLimitMbps * 1024L * 1024L;

            var result = await Task.Run(
                () => _resourceSyncService.SyncGameAsync(
                    game,
                    _resourceSourceRootPath,
                    _resourceTargetRootPath,
                    progress,
                    bandwidthLimitBytesPerSecond,
                    syncControl.WaitIfPausedAsync,
                    syncMode,
                    syncControl.CancellationToken),
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
        var existingGame = row.ManagedGameId.HasValue
            ? FindGameById(row.ManagedGameId.Value)
            : FindGameByInstallPath(row.InstallPath);

        var game = existingGame ?? BuildTransientGameRecordFromResourceRow(row);
        await SyncGameFromResourceAsync(game, syncMode);
        return await EnsureManagedGameRegistrationAsync(game, row);
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
            throw new InvalidOperationException("Vui lòng nhập thư mục nguồn IDC.");
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
        _resourcesGrid.MultiSelect = false;
        _resourcesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _resourcesGrid.ReadOnly = true;
        _resourcesGrid.RowHeadersVisible = false;
        _resourcesGrid.DataSource = _resourcesBinding;

        _resourcesGrid.Columns.Add(CreateTextColumn("ID", nameof(ResourceGameRow.Id), 70));
        _resourcesGrid.Columns.Add(CreateTextColumn("Tên trò chơi", nameof(ResourceGameRow.Name), 180));
        _resourcesGrid.Columns.Add(CreateTextColumn("Nhóm", nameof(ResourceGameRow.Category), 120));
        _resourcesGrid.Columns.Add(CreateTextColumn("Nguồn IDC", nameof(ResourceGameRow.SourceStatus), 110));
        _resourcesGrid.Columns.Add(CreateTextColumn("Trạng thái tải", nameof(ResourceGameRow.DownloadStatus), 120));
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
        _resourcesContextMenu.Items.Add(_syncMissingFromIdcMenuItem);
        _resourcesContextMenu.Opening += ResourcesContextMenu_Opening;
        _syncMissingFromIdcMenuItem.Click += SyncMissingFromIdcMenuItem_Click;

        _resourcesGrid.ContextMenuStrip = _resourcesContextMenu;
        _resourcesGrid.MouseDown += ResourcesGrid_MouseDown;
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

        _resourcesGrid.ClearSelection();
        var row = _resourcesGrid.Rows[hit.RowIndex];
        row.Selected = true;
        _resourcesGrid.CurrentCell = row.Cells[0];
    }

    private void ResourcesContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var row = GetSelectedResourceRow();
        var canSyncMissing =
            _currentResourceFilter == ResourceFilterKind.Downloaded &&
            row is not null &&
            row.HasSource &&
            row.IsDownloaded &&
            !string.IsNullOrWhiteSpace(row.InstallPath);

        _syncMissingFromIdcMenuItem.Enabled = canSyncMissing;
    }

    private async void SyncMissingFromIdcMenuItem_Click(object? sender, EventArgs e)
    {
        var row = GetSelectedResourceRow();
        if (row is null)
        {
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
                gameId = await SyncResourceRowAsync(row, ResourceSyncMode.MissingOnly);
                await AutoExportCatalogAsync();
            }
            finally
            {
                await ReloadAllAsync(gameId ?? SelectedGame?.Id);
            }
        }, () => ToggleResourceSyncControls(true));
    }

    private ResourceGameRow? GetSelectedResourceRow()
    {
        return _resourcesGrid.CurrentRow?.DataBoundItem as ResourceGameRow;
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

        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Bắt đầu", nameof(DownloadMonitorRow.StartedAt), 135, "yyyy-MM-dd HH:mm:ss"));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Trò chơi", nameof(DownloadMonitorRow.GameName), 180));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Tiến độ (%)", nameof(DownloadMonitorRow.ProgressPercent), 90));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Trạng thái", nameof(DownloadMonitorRow.Status), 100));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Cập nhật lúc", nameof(DownloadMonitorRow.UpdatedAt), 135, "yyyy-MM-dd HH:mm:ss"));
        _downloadMonitorGrid.Columns.Add(CreateTextColumn("Thông điệp", nameof(DownloadMonitorRow.Message), 500, fill: true));
    }

    private void RebuildResourceRows(IReadOnlyList<GameRecord> games)
    {
        _allResourceRows.Clear();
        var sourceFolders = GetSourceFolderEntries();

        foreach (var game in games.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            _allResourceRows.Add(CreateResourceRow(game));
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

        _allResourceRows.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));

        ApplyResourceFilter(_currentResourceFilter);
    }

    private ResourceGameRow CreateResourceRow(GameRecord game)
    {
        var sourceKey = ResolveSourceKeyForGame(game);
        var sourcePath = ResolveSourcePathForGame(game);
        var sourceExists = Directory.Exists(sourcePath);
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

    private IReadOnlyList<SourceFolderEntry> GetSourceFolderEntries()
    {
        if (string.IsNullOrWhiteSpace(_resourceSourceRootPath))
        {
            return Array.Empty<SourceFolderEntry>();
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

        ConfigureLogsGrid();
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(_logsGrid, 0, 1);

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
            _manifestTextBox.Text = "Đang quét tệp và tạo lại manifest...";
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
            await _catalogService.ExportToFileAsync(_autoCatalogPath);
            await SaveUiSettingsAsync();
            MessageBox.Show(this, $"Đã xuất danh mục:{Environment.NewLine}{_autoCatalogPath}", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private async void RefreshLogsButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(LoadLogsAsync);
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

        var monitorRow = StartDownloadMonitor(game.Name);

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
                    UpdateDownloadMonitor(monitorRow, info.Percent, "Đang tải", info.Message);
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

    private DownloadMonitorRow StartDownloadMonitor(string gameName)
    {
        var row = new DownloadMonitorRow
        {
            StartedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            GameName = gameName,
            ProgressPercent = 0,
            Status = "Đang tải",
            Message = "Khởi tạo tác vụ cập nhật."
        };

        _downloadMonitorRows.Insert(0, row);
        _downloadMonitorBinding.ResetBindings(false);

        if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
        {
            UpdateDownloadSummary();
        }

        return row;
    }

    private void UpdateDownloadMonitor(DownloadMonitorRow row, int progressPercent, string status, string message)
    {
        row.ProgressPercent = Math.Clamp(progressPercent, 0, 100);
        row.Status = status;
        row.Message = message;
        row.UpdatedAt = DateTime.Now;
        _downloadMonitorBinding.ResetBindings(false);

        if (_currentResourceFilter == ResourceFilterKind.DownloadMonitor)
        {
            UpdateDownloadSummary();
        }
    }

    private void EnsureDownloadMonitorContextMenu()
    {
        if (_downloadMonitorContextMenuInitialized)
        {
            return;
        }

        _downloadMonitorContextMenuInitialized = true;

        _downloadMonitorContextMenu.Items.Add(_pauseDownloadMenuItem);
        _downloadMonitorContextMenu.Items.Add(_resumeDownloadMenuItem);
        _downloadMonitorContextMenu.Items.Add(_stopDownloadMenuItem);
        _downloadMonitorContextMenu.Items.Add(_removeDownloadMenuItem);
        _downloadMonitorContextMenu.Items.Add(new ToolStripSeparator());
        _downloadMonitorContextMenu.Items.Add(_removeFinishedDownloadsMenuItem);

        _downloadMonitorContextMenu.Opening += DownloadMonitorContextMenu_Opening;
        _pauseDownloadMenuItem.Click += PauseDownloadMenuItem_Click;
        _resumeDownloadMenuItem.Click += ResumeDownloadMenuItem_Click;
        _stopDownloadMenuItem.Click += StopDownloadMenuItem_Click;
        _removeDownloadMenuItem.Click += RemoveDownloadMenuItem_Click;
        _removeFinishedDownloadsMenuItem.Click += RemoveFinishedDownloadsMenuItem_Click;

        _downloadMonitorGrid.ContextMenuStrip = _downloadMonitorContextMenu;
        _downloadMonitorGrid.MouseDown += DownloadMonitorGrid_MouseDown;
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
        var selected = GetSelectedDownloadMonitorRow();
        if (selected is null)
        {
            _pauseDownloadMenuItem.Enabled = false;
            _resumeDownloadMenuItem.Enabled = false;
            _stopDownloadMenuItem.Enabled = false;
            _removeDownloadMenuItem.Enabled = false;
            _removeFinishedDownloadsMenuItem.Enabled = _downloadMonitorRows.Count > 0;
            return;
        }

        var isRunning = IsResourceSyncRunning(selected);
        var isPaused = IsResourceSyncPaused(selected);
        _pauseDownloadMenuItem.Enabled = isRunning && !isPaused;
        _resumeDownloadMenuItem.Enabled = isRunning && isPaused;
        _stopDownloadMenuItem.Enabled = isRunning;
        _removeDownloadMenuItem.Enabled = !isRunning;
        _removeFinishedDownloadsMenuItem.Enabled = _downloadMonitorRows.Any(row => !IsResourceSyncRunning(row));
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
            _downloadMonitorRows.Add(new DownloadMonitorRow
            {
                StartedAt = log.CreatedAt.ToLocalTime(),
                UpdatedAt = log.CreatedAt.ToLocalTime(),
                GameName = log.GameName,
                ProgressPercent = string.Equals(log.Status, "Thành công", StringComparison.OrdinalIgnoreCase) ? 100 : 0,
                Status = log.Status,
                Message = log.Message
            });
        }

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
        _syncAllResourcesButton.Enabled = enabled;
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
            await _catalogService.ExportToFileAsync(_autoCatalogPath);
        }
        catch (Exception exception)
        {
            AppendUpdateMessage($"Cảnh báo: tự xuất danh mục thất bại - {exception.Message}");
        }
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
            }

            _resourceSourceRootTextBox.Text = _resourceSourceRootPath;
            _resourceTargetRootTextBox.Text = _resourceTargetRootPath;
            _resourceBandwidthLimitNumeric.Value = Math.Min(_resourceBandwidthLimitNumeric.Maximum, _resourceBandwidthLimitMbps);
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
            ResourceBandwidthLimitMbps = _resourceBandwidthLimitMbps
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

        public string DownloadStatus { get; init; } = string.Empty;

        public string RunStatus { get; init; } = string.Empty;

        public string FileCountDisplay { get; init; } = "-";

        public string SizeGbDisplay { get; init; } = "-";

        public DateTime? LastUpdatedAt { get; init; }

        public string InstallPath { get; init; } = string.Empty;

        public bool IsDownloaded { get; init; }

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

        public CancellationToken CancellationToken => _cancellation.Token;

        public bool IsPaused => _isPaused;

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
        public DateTime StartedAt { get; set; }

        public string GameName { get; set; } = string.Empty;

        public int ProgressPercent { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    private sealed class UpdateSourceOption
    {
        public UpdateSourceKind Kind { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    private sealed class ServerUiSettings
    {
        public string ClientCatalogPath { get; set; } = string.Empty;

        public string ResourceSourceRootPath { get; set; } = string.Empty;

        public string ResourceTargetRootPath { get; set; } = string.Empty;

        public int ResourceBandwidthLimitMbps { get; set; }
    }
}
