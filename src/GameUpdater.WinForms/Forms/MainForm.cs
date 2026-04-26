using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using GameUpdater.Core.Abstractions;
using GameUpdater.Core.Services;
using GameUpdater.Shared.Models;

namespace GameUpdater.WinForms.Forms;

public sealed partial class MainForm : Form
{
    private const string DownloadProgressColumnName = "downloadProgressColumn";
    private static readonly Color AccentColor = Color.FromArgb(37, 99, 235);
    private static readonly Color AccentHoverColor = Color.FromArgb(29, 78, 216);
    private static readonly Color SecondaryButtonColor = Color.FromArgb(241, 245, 249);
    private static readonly Color SecondaryButtonHoverColor = Color.FromArgb(226, 232, 240);
    private static readonly Color SecondaryButtonTextColor = Color.FromArgb(15, 23, 42);
    private static readonly List<Button> StyledButtons = new();

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
    private readonly BindingSource _clientStatusBinding = new();

    private readonly DataGridView _gamesGrid = new();
    private readonly FlowLayoutPanel _gamesGridPanel = new();
    private readonly ComboBox _gamesViewModeComboBox = new();
    private readonly Button _moveTopButton = new();
    private readonly Button _moveUpButton = new();
    private readonly Button _moveDownButton = new();
    private readonly DataGridView _resourcesGrid = new();
    private readonly DataGridView _downloadMonitorGrid = new();
    private readonly DataGridView _logsGrid = new();
    private readonly DataGridView _clientStatusGrid = new();
    private readonly Label _clientDashboardSummaryLabel = new();
    private readonly Label _clientDashboardGameStatsLabel = new();
    private readonly System.Windows.Forms.Timer _clientDashboardRefreshTimer = new();
    private readonly System.Windows.Forms.Timer _serverDashboardRefreshTimer = new();
    private readonly Label _serverDashboardSummaryLabel = new();
    private readonly Label _serverDashboardCpuLabel = new();
    private readonly Label _serverDashboardMemoryLabel = new();
    private readonly Label _serverDashboardDiskLabel = new();
    private readonly Label _serverDashboardNetworkLabel = new();
    private readonly Label _serverDashboardStorageLabel = new();
    private readonly Label _serverDashboardServiceLabel = new();
    private readonly Label _serverDashboardRecommendationLabel = new();
    private readonly ProgressBar _serverCpuProgressBar = new();
    private readonly ProgressBar _serverMemoryProgressBar = new();
    private readonly ProgressBar _serverDiskProgressBar = new();
    private readonly TextBox _updateSourceTextBox = new();
    private readonly TextBox _updateVersionTextBox = new();
    private readonly TextBox _updateOutputTextBox = new();
    private readonly ComboBox _updateSourceKindComboBox = new();
    private readonly ComboBox _updateGameComboBox = new();
    private readonly ComboBox _fontSizeComboBox = new();
    private readonly TextBox _clientWallpaperPathTextBox = new();
    private readonly TextBox _clientCafeNameTextBox = new();
    private readonly TextBox _clientBannerMessageTextBox = new();
    private readonly TextBox _clientThemeAccentColorTextBox = new();
    private readonly TextBox _clientStatusFolderTextBox = new();
    private readonly CheckBox _enableClientCloseAppHotKeyCheckBox = new();
    private readonly CheckBox _enableClientFullscreenKioskCheckBox = new();
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
    private readonly Button _checkResourceHealthButton = new();
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
    private readonly ToolStripMenuItem _markHotGameMenuItem = new("Danh dau Hot game");
    private readonly ToolStripMenuItem _unmarkHotGameMenuItem = new("Bo Hot game");
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
    private string _clientCafeDisplayName = "Cyber Game";
    private string _clientBannerMessage = string.Empty;
    private string _clientThemeAccentColor = "#38BDF8";
    private string _clientStatusFolderPath = string.Empty;
    private bool _enableClientCloseApplicationHotKey = true;
    private bool _enableClientFullscreenKioskMode;
    private UiFontSizeMode _uiFontSizeMode = UiFontSizeMode.Normal;
    private bool _isUpdatingFontSizeSelection;
    private readonly string _settingsFilePath;
    private ResourceFilterKind _currentResourceFilter = ResourceFilterKind.All;
    private DateTime _serverDashboardStartedAtUtc = DateTime.UtcNow;
    private TimeSpan _lastServerCpuTotalProcessorTime;
    private DateTime _lastServerCpuSampleUtc = DateTime.UtcNow;
    private long _lastServerNetworkBytesSent;
    private long _lastServerNetworkBytesReceived;
    private DateTime _lastServerNetworkSampleUtc = DateTime.UtcNow;

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

        if (File.Exists("app.ico"))
        {
            this.Icon = new Icon("app.ico");
        }

        _gamesBinding.CurrentChanged += GamesBinding_CurrentChanged;
        _downloadMonitorBinding.DataSource = _downloadMonitorRows;
        _clientDashboardRefreshTimer.Interval = 15_000;
        _clientDashboardRefreshTimer.Tick += (_, _) => RefreshClientDashboard();
        _serverDashboardRefreshTimer.Interval = 15_000;
        _serverDashboardRefreshTimer.Tick += (_, _) => RefreshServerDashboard();

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
        RefreshClientDashboard();
        RefreshServerDashboard();
        _clientDashboardRefreshTimer.Start();
        _serverDashboardRefreshTimer.Start();
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
        RefreshGamesGridPanel();
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
        // Resource sync tasks run in the background and can be stopped/paused from the monitor grid.
        // Keep the tab interactive even while a task is running; otherwise a cancelled/stopped task can
        // leave the operator unable to adjust settings or start another action until the async cleanup ends.
        _saveResourceSettingsButton.Enabled = true;
        _checkResourceHealthButton.Enabled = true;
        _syncSelectedResourceButton.Enabled = enabled;
        _browseResourceSourceButton.Enabled = true;
        _browseResourceTargetButton.Enabled = true;
        _resourceSourceRootTextBox.Enabled = true;
        _resourceTargetRootTextBox.Enabled = true;
        _resourceBandwidthLimitNumeric.Enabled = true;
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


}



