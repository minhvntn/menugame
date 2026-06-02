using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using GameUpdater.Core.Abstractions;
using GameUpdater.Core.Services;
using GameUpdater.Shared.Localization;
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
    private static readonly Color ButtonBorderColor = Color.FromArgb(203, 213, 225);
    private const int ButtonHorizontalPadding = 12;
    private const int ButtonVerticalPadding = 6;
    private static readonly List<Control> StyledButtons = new();
    private static readonly Dictionary<Control, bool> StyledButtonPrimaryStates = new();
    private static readonly Dictionary<Control, Color> StyledButtonTargetColors = new();

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
    private readonly GameUpdater.WinForms.Controls.ModernComboBox _gamesViewModeComboBox = new();
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
    private readonly GameUpdater.WinForms.Controls.ModernComboBox _updateSourceKindComboBox = new();
    private readonly GameUpdater.WinForms.Controls.ModernComboBox _updateGameComboBox = new();
    private readonly GameUpdater.WinForms.Controls.ModernComboBox _fontSizeComboBox = new();
    private readonly TextBox _clientWallpaperPathTextBox = new();
    private readonly TextBox _clientCafeNameTextBox = new();
    private readonly TextBox _clientBannerMessageTextBox = new();
    private readonly TextBox _clientThemeAccentColorTextBox = new();
    private readonly GameUpdater.WinForms.Controls.ModernComboBox _clientThemeFontComboBox = new();
    private readonly TextBox _clientStatusFolderTextBox = new();
    private readonly NumericUpDown _clientHeartbeatIntervalNumeric = new();
    private readonly NumericUpDown _dashboardRefreshIntervalNumeric = new();
    private readonly GameUpdater.WinForms.Controls.ModernCheckBox _enableClientCloseAppHotKeyCheckBox = new();
    private readonly GameUpdater.WinForms.Controls.ModernCheckBox _enableClientFullscreenKioskCheckBox = new();
    private readonly GameUpdater.WinForms.Controls.ModernButton _browseClientWallpaperButton = new();
    private readonly GameUpdater.WinForms.Controls.ModernButton _clearClientWallpaperButton = new();
    private readonly GameUpdater.WinForms.Controls.ModernButton _saveSettingsButton = new();
    private readonly GameUpdater.WinForms.Controls.ModernCheckBox _backupCheckBox = new();
    
    // Original summary label kept for backward compatibility if needed, but not docked
    private readonly Label _resourceSummaryLabel = new();
    
    // New Stat Card Labels
    private readonly Label _statDisplayCountLabel = new();
    private readonly Label _statDownloadedLabel = new();
    private readonly Label _statMissingLabel = new();
    private readonly Label _statSizeLabel = new();
    private readonly Label _statSourceOkLabel = new();
    private readonly Label _statTargetOkLabel = new();
    private readonly ProgressBar _statDiskProgressBar = new();
    private readonly Label _statDiskProgressLabel = new();
    private readonly TextBox _resourceSourceRootTextBox = new();
    private readonly FlowLayoutPanel _targetsContainer = new();
    private readonly Button _addTargetDriveButton = new();
    private readonly NumericUpDown _resourceBandwidthLimitNumeric = new();
    private readonly ProgressBar _updateProgressBar = new();
    private readonly GameUpdater.WinForms.Controls.ModernButton _applyUpdateButton = new();
    private readonly GameUpdater.WinForms.Controls.ModernButton _browseSourceButton = new();
    private readonly TreeView _resourceTree = new();
    private readonly TabControl _resourceWorkspaceTabControl = new HiddenHeadersTabControl();
    private SplitContainer? _resourcesSplitContainer;
    private readonly GameUpdater.WinForms.Controls.ModernButton _browseResourceSourceButton = new();

    private readonly GameUpdater.WinForms.Controls.ModernButton _saveResourceSettingsButton = new();
    private readonly GameUpdater.WinForms.Controls.ModernButton _checkResourceHealthButton = new();
    private readonly GameUpdater.WinForms.Controls.ModernButton _syncSelectedResourceButton = new();

    private readonly BindingList<DownloadMonitorRow> _downloadMonitorRows = new();
    private readonly List<ResourceGameRow> _allResourceRows = new();
    private readonly Dictionary<string, string> _gameSizeDisplayCache = new(StringComparer.Ordinal);
    private readonly Dictionary<DownloadMonitorRow, ResourceSyncTaskControl> _activeResourceSyncControls = new();
    private readonly ContextMenuStrip _resourcesContextMenu = new();
    private readonly ToolStripMenuItem _downloadSelectedResourcesMenuItem = new(I18n.Server.ResourceContextDownloadSelected);
    private readonly ToolStripMenuItem _pauseSelectedResourcesMenuItem = new(I18n.Server.ResourceContextPauseSelected);
    private readonly ToolStripMenuItem _resumeSelectedResourcesMenuItem = new(I18n.Server.ResourceContextResumeSelected);
    private readonly ToolStripMenuItem _stopSelectedResourcesMenuItem = new(I18n.Server.ResourceContextStopSelected);
    private readonly ToolStripMenuItem _setResourceBandwidthMenuItem = new(I18n.Server.ResourceContextSetBandwidth);
    private readonly ToolStripMenuItem _retrySelectedResourcesMenuItem = new(I18n.Server.ResourceContextRetryFromIdc);
    private readonly List<ToolStripMenuItem> _resourceBandwidthPresetMenuItems = new();
    private readonly ToolStripMenuItem _syncMissingFromIdcMenuItem = new(I18n.Server.ResourceContextSyncMissingFromIdc);
    private readonly ContextMenuStrip _gamesContextMenu = new();
    private readonly ToolStripMenuItem _addGameMenuItem = new(I18n.Server.GamesContextAdd);
    private readonly ToolStripMenuItem _editGameMenuItem = new(I18n.Server.GamesContextEdit);
    private readonly ToolStripMenuItem _deleteGameMenuItem = new(I18n.Server.GamesContextDelete);
    private readonly ToolStripMenuItem _viewManifestMenuItem = new(I18n.Server.GamesContextViewManifest);
    private readonly ToolStripMenuItem _scanManifestGameMenuItem = new(I18n.Server.GamesContextScanManifest);
    private readonly ToolStripMenuItem _moveTopGameMenuItem = new(I18n.Server.GamesContextMoveTop);
    private readonly ToolStripMenuItem _moveUpGameMenuItem = new(I18n.Server.GamesContextMoveUp);
    private readonly ToolStripMenuItem _moveDownGameMenuItem = new(I18n.Server.GamesContextMoveDown);
    private readonly ToolStripMenuItem _markHotGameMenuItem = new(I18n.Server.GamesContextMarkHot);
    private readonly ToolStripMenuItem _unmarkHotGameMenuItem = new(I18n.Server.GamesContextUnmarkHot);
    private readonly ContextMenuStrip _downloadMonitorContextMenu = new();
    private readonly ToolStripMenuItem _pauseDownloadMenuItem = new(I18n.Server.DownloadContextPause);
    private readonly ToolStripMenuItem _resumeDownloadMenuItem = new(I18n.Server.DownloadContextResume);
    private readonly ToolStripMenuItem _pauseAllDownloadsMenuItem = new(I18n.Server.DownloadContextPauseAll);
    private readonly ToolStripMenuItem _resumeAllDownloadsMenuItem = new(I18n.Server.DownloadContextResumeAll);
    private readonly ToolStripMenuItem _stopDownloadMenuItem = new(I18n.Server.DownloadContextStop);
    private readonly ToolStripMenuItem _setDownloadBandwidthMenuItem = new(I18n.Server.DownloadContextSetBandwidth);
    private readonly ToolStripMenuItem _retryDownloadFromIdcMenuItem = new(I18n.Server.DownloadContextRetryFromIdc);
    private readonly ToolStripMenuItem _removeDownloadMenuItem = new(I18n.Server.DownloadContextRemoveRow);
    private readonly ToolStripMenuItem _removeFinishedDownloadsMenuItem = new(I18n.Server.DownloadContextRemoveFinished);
    private readonly List<ToolStripMenuItem> _downloadBandwidthPresetMenuItems = new();
    private bool _downloadMonitorContextMenuInitialized;
    private bool _resourcesContextMenuInitialized;
    private bool _gamesContextMenuInitialized;

    private string _autoCatalogPath = string.Empty;
    private string _resourceSourceRootPath = @"E:\GameOnlineIDC";
    private string _resourceTargetRootPath = @"E:\GameOnline";
    private int _resourceBandwidthLimitMbps;
    private string _clientWindowsWallpaperPath = string.Empty;
    private string _clientCafeDisplayName = I18n.Server.DefaultClientCafeName;
    private string _clientBannerMessage = string.Empty;
    private string _clientThemeAccentColor = I18n.Server.DefaultThemeAccent;
    private string _clientThemeFontFamily = I18n.Server.DefaultThemeFontFamily;
    private string _clientStatusFolderPath = string.Empty;
    private int _clientHeartbeatIntervalSeconds = 45;
    private int _dashboardRefreshIntervalSeconds = 15;
    private bool _enableClientCloseApplicationHotKey = true;
    private bool _enableClientFullscreenKioskMode;
    private UiFontSizeMode _uiFontSizeMode = UiFontSizeMode.Big;
    private bool _isUpdatingFontSizeSelection;
    private readonly string _settingsFilePath;
    private ResourceFilterKind _currentResourceFilter = ResourceFilterKind.All;
    private DateTime _serverDashboardStartedAtUtc = DateTime.UtcNow;
    private TimeSpan _lastServerCpuTotalProcessorTime;
    private DateTime _lastServerCpuSampleUtc = DateTime.UtcNow;
    private long _lastServerNetworkBytesSent;
    private long _lastServerNetworkBytesReceived;
    private DateTime _lastServerNetworkSampleUtc = DateTime.UtcNow;
    private int _clientDashboardRefreshInProgress;

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

        Text = I18n.Server.MainWindowTitle;
        Width = 1570;
        Height = 950;
        StartPosition = FormStartPosition.CenterScreen;

        if (File.Exists("app.ico"))
        {
            this.Icon = new Icon("app.ico");
        }

        _gamesBinding.CurrentChanged += GamesBinding_CurrentChanged;
        _downloadMonitorBinding.DataSource = _downloadMonitorRows;
        _clientDashboardRefreshTimer.Interval = ToTimerIntervalMilliseconds(_dashboardRefreshIntervalSeconds, 15);
        _clientDashboardRefreshTimer.Tick += async (_, _) => await RefreshClientDashboardAsync();
        _serverDashboardRefreshTimer.Interval = ToTimerIntervalMilliseconds(_dashboardRefreshIntervalSeconds, 15);
        _serverDashboardRefreshTimer.Tick += (_, _) => RefreshServerDashboard();

        BuildLayout();
        ApplyUiFontSize(_uiFontSizeMode);
    }

    private void ApplyRuntimeIntervals()
    {
        _clientHeartbeatIntervalSeconds = NormalizeClientHeartbeatIntervalSeconds(_clientHeartbeatIntervalSeconds);
        _dashboardRefreshIntervalSeconds = NormalizeDashboardRefreshIntervalSeconds(_dashboardRefreshIntervalSeconds);
        _clientDashboardRefreshTimer.Interval = ToTimerIntervalMilliseconds(_dashboardRefreshIntervalSeconds, 15);
        _serverDashboardRefreshTimer.Interval = ToTimerIntervalMilliseconds(_dashboardRefreshIntervalSeconds, 15);
    }

    private static int NormalizeClientHeartbeatIntervalSeconds(int seconds)
    {
        return Math.Clamp(seconds, 5, 300);
    }

    private static int NormalizeDashboardRefreshIntervalSeconds(int seconds)
    {
        return Math.Clamp(seconds, 3, 300);
    }

    private static int ToTimerIntervalMilliseconds(int seconds, int fallbackSeconds)
    {
        var normalizedSeconds = Math.Clamp(seconds > 0 ? seconds : fallbackSeconds, 1, 3600);
        return normalizedSeconds * 1000;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyResourcesSplitDistance();
        await LoadUiSettingsAsync();
        await ReloadAllAsync();
        ApplyResourcesSplitDistance();
        await RefreshClientDashboardAsync(forceNetworkProbe: true);
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
        _gameSizeDisplayCache.Clear();
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
            MessageBox.Show(this, exception.Message, I18n.Common.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            onFinally?.Invoke();
        }
    }

    private void ToggleGameControls(bool enabled)
    {
        _scanManifestGameMenuItem.Enabled = enabled;
        _moveTopGameMenuItem.Enabled = enabled;
        _moveUpGameMenuItem.Enabled = enabled;
        _moveDownGameMenuItem.Enabled = enabled;
        _markHotGameMenuItem.Enabled = enabled;
        _unmarkHotGameMenuItem.Enabled = enabled;
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

        _resourceSourceRootTextBox.Enabled = true;
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
        MessageBox.Show(this, message, I18n.Common.InfoTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
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




