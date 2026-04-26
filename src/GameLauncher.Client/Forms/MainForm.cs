using Microsoft.Win32;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using GameLauncher.Client.Controls;
using GameLauncher.Client.Models;
using GameLauncher.Client.Services;
using GameUpdater.Shared.Models;

namespace GameLauncher.Client.Forms;

public sealed class MainForm : Form
{
    private const int CloseLaunchedAppHotKeyId = 0x4B01;
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint SpiSetDeskWallpaper = 0x0014;
    private const uint SpifUpdateIniFile = 0x01;
    private const uint SpifSendWinIniChange = 0x02;
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupRegistryValueName = "GameLauncher.Client";
    private const string CafeDisplayName = "Cyber Game";

    private static readonly HttpClient WallpaperHttpClient = new();
    private static readonly Color HeaderBackColor = Color.FromArgb(33, 56, 74);
    private static readonly Color BodyBackColor = Color.FromArgb(24, 39, 54);

    private readonly SettingsService _settingsService;
    private readonly CatalogReaderService _catalogService;
    private readonly GameLaunchService _launchService;

    private readonly Label _headerSectionLabel = new();
    private readonly Label _cafeNameLabel = new();
    private readonly Label _bannerMessageLabel = new();
    private readonly FlowLayoutPanel _hotCardsPanel = new();
    private readonly FlowLayoutPanel _normalCardsPanel = new();
    private readonly System.Windows.Forms.Timer _statusHeartbeatTimer = new();

    private List<LauncherGameRow> _allRows = new();
    private string _catalogPath = string.Empty;
    private string _currentGameName = string.Empty;
    private string _currentGameExecutablePath = string.Empty;
    private bool _enableCloseAppHotKeyFromServer = true;
    private bool _isCloseAppHotKeyRegistered;
    private Image? _headerLogoImage;
    private long _lastNetworkBytesSent;
    private long _lastNetworkBytesReceived;
    private DateTime _lastNetworkSampleUtc = DateTime.UtcNow;
    private readonly DateTime _clientStartedAtUtc = DateTime.UtcNow;

    public MainForm(
        SettingsService settingsService,
        CatalogReaderService catalogService,
        GameLaunchService launchService)
    {
        _settingsService = settingsService;
        _catalogService = catalogService;
        _launchService = launchService;

        Text = "Menu trò chơi";
        Width = 1320;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(236, 241, 246);
        MinimumSize = new Size(1000, 680);

        if (File.Exists("app.ico"))
        {
            Icon = new Icon("app.ico");
        }

        BuildLayout();

        _statusHeartbeatTimer.Interval = 45_000;
        _statusHeartbeatTimer.Tick += (_, _) => WriteClientStatusSafe();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ = Task.Run(EnsureStartupWithWindows);
        await LoadCatalogOnStartupAsync();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateCloseAppHotKeyRegistration();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterCloseAppHotKey();
        base.OnHandleDestroyed(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _statusHeartbeatTimer.Stop();
        WriteClientStatusSafe(clearPlayingGame: true);
        _headerLogoImage?.Dispose();
        _headerLogoImage = null;
        base.OnFormClosed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey && m.WParam.ToInt32() == CloseLaunchedAppHotKeyId)
        {
            HandleCloseRunningApplicationHotKey();
            return;
        }

        base.WndProc(ref m);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        root.Controls.Add(BuildHeaderPanel(), 0, 0);
        root.Controls.Add(BuildBodyPanel(), 0, 1);
        root.Controls.Add(BuildBottomNotificationPanel(), 0, 2);

        Controls.Add(root);
    }

    private Control BuildHeaderPanel()
    {
        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = HeaderBackColor,
            Padding = new Padding(10, 6, 10, 6)
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));

        _headerLogoImage = BuildHeaderLogoImage();
        var logoBox = new PictureBox
        {
            Width = 42,
            Height = 42,
            Margin = new Padding(0),
            Image = _headerLogoImage,
            SizeMode = PictureBoxSizeMode.Zoom
        };

        _cafeNameLabel.Text = CafeDisplayName;
        _cafeNameLabel.AutoSize = true;
        _cafeNameLabel.ForeColor = Color.White;
        _cafeNameLabel.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);

        var cafeTextPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(8, 8, 0, 0)
        };
        cafeTextPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        cafeTextPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        cafeTextPanel.Controls.Add(_cafeNameLabel, 0, 0);

        var leftPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        leftPanel.Controls.Add(logoBox);
        leftPanel.Controls.Add(cafeTextPanel);

        _headerSectionLabel.Dock = DockStyle.Fill;
        _headerSectionLabel.TextAlign = ContentAlignment.MiddleCenter;
        _headerSectionLabel.ForeColor = Color.White;
        _headerSectionLabel.Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold);
        _headerSectionLabel.Text = "Online Games";

        var quickActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        quickActions.Controls.Add(CreateHeaderLinkButton("YT", "YouTube", "https://www.youtube.com"));
        quickActions.Controls.Add(CreateHeaderLinkButton("FB", "Facebook", "https://www.facebook.com"));
        quickActions.Controls.Add(CreateHeaderLinkButton("Web", "Website", "https://www.google.com"));

        headerLayout.Controls.Add(leftPanel, 0, 0);
        headerLayout.Controls.Add(_headerSectionLabel, 1, 0);
        headerLayout.Controls.Add(quickActions, 2, 0);

        headerPanel.Controls.Add(headerLayout);
        return headerPanel;
    }

    private Control BuildBodyPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10, 10, 10, 10),
            BackColor = BodyBackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _hotCardsPanel.Dock = DockStyle.Fill;
        _hotCardsPanel.AutoScroll = true;
        _hotCardsPanel.WrapContents = false;
        _hotCardsPanel.FlowDirection = FlowDirection.LeftToRight;
        _hotCardsPanel.Padding = new Padding(8, 8, 8, 8);
        _hotCardsPanel.Margin = new Padding(0, 0, 0, 8);
        _hotCardsPanel.BackColor = Color.FromArgb(20, 33, 47);

        _normalCardsPanel.Dock = DockStyle.Fill;
        _normalCardsPanel.AutoScroll = true;
        _normalCardsPanel.WrapContents = true;
        _normalCardsPanel.FlowDirection = FlowDirection.LeftToRight;
        _normalCardsPanel.Padding = new Padding(8, 8, 8, 8);
        _normalCardsPanel.BackColor = BodyBackColor;

        layout.Controls.Add(_hotCardsPanel, 0, 0);
        layout.Controls.Add(_normalCardsPanel, 0, 1);
        return layout;
    }

    private Control BuildBottomNotificationPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(12, 6, 12, 6)
        };

        _bannerMessageLabel.Dock = DockStyle.Fill;
        _bannerMessageLabel.TextAlign = ContentAlignment.MiddleCenter;
        _bannerMessageLabel.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        _bannerMessageLabel.ForeColor = Color.White;
        _bannerMessageLabel.BackColor = Color.Transparent;
        _bannerMessageLabel.Text = "Chào mừng quý khách";
        _bannerMessageLabel.Visible = true;

        panel.Controls.Add(_bannerMessageLabel);
        return panel;
    }

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
        await ApplyServerPolicyAsync(catalog.ClientPolicy);
        await SaveLauncherSettingsAsync();
        ApplyFilter();
        WriteClientStatusSafe();
        _statusHeartbeatTimer.Start();
    }

    private void ApplyFilter()
    {
        var hotRows = _allRows
            .Where(row => row.IsHot)
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalRows = _allRows
            .Where(row => !row.IsHot)
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RenderCards(_hotCardsPanel, hotRows, isHotRow: true);
        RenderCards(_normalCardsPanel, normalRows, isHotRow: false);
    }

    private void RenderCards(FlowLayoutPanel panel, IReadOnlyList<LauncherGameRow> rows, bool isHotRow)
    {
        panel.SuspendLayout();

        foreach (Control control in panel.Controls)
        {
            control.Dispose();
        }

        panel.Controls.Clear();

        foreach (var row in rows)
        {
            panel.Controls.Add(new GameCardControl(row, PlayGame, isHotRow));
        }

        panel.ResumeLayout();
    }

    private void PlayGame(LauncherGameRow row)
    {
        _ = ExecuteWithErrorHandlingAsync(async () =>
        {
            var process = _launchService.Launch(row);
            _currentGameName = row.Name;
            _currentGameExecutablePath = row.ResolvedExecutablePath;
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

    private void SendLauncherToDesktop()
    {
        if (WindowState != FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Minimized;
        }

        SendToBack();
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

    private async Task ApplyServerPolicyAsync(LauncherClientPolicy? policy)
    {
        var effectivePolicy = policy ?? new LauncherClientPolicy();
        _enableCloseAppHotKeyFromServer = effectivePolicy.EnableCloseRunningApplicationHotKey;
        UpdateCloseAppHotKeyRegistration();
        ApplyBrandingPolicy(effectivePolicy);
        ApplyKioskPolicy(effectivePolicy.EnableFullscreenKioskMode);

        var wallpaperPath = effectivePolicy.ClientWindowsWallpaperPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(wallpaperPath))
        {
            return;
        }

        var resolvedWallpaperPath = ResolvePolicyWallpaperPath(wallpaperPath, _catalogPath);
        await TrySetWindowsWallpaperAsync(resolvedWallpaperPath);
    }

    private void ApplyBrandingPolicy(LauncherClientPolicy policy)
    {
        _cafeNameLabel.Text = string.IsNullOrWhiteSpace(policy.CafeDisplayName)
            ? CafeDisplayName
            : policy.CafeDisplayName.Trim();

        var bannerMessage = policy.BannerMessage?.Trim() ?? string.Empty;
        _bannerMessageLabel.Text = string.IsNullOrWhiteSpace(bannerMessage)
            ? "Chào mừng quý khách"
            : bannerMessage;
        _bannerMessageLabel.Visible = true;

        if (TryParseHtmlColor(policy.ThemeAccentColor, out var accentColor))
        {
            _bannerMessageLabel.BackColor = accentColor;
        }
    }

    private void ApplyKioskPolicy(bool enabled)
    {
        if (enabled)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            return;
        }

        TopMost = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        if (WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Normal;
        }
    }

    private static bool TryParseHtmlColor(string? input, out Color color)
    {
        color = Color.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        try
        {
            color = ColorTranslator.FromHtml(input.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void WriteClientStatusSafe(bool clearPlayingGame = false)
    {
        try
        {
            var folder = ResolveClientStatusFolder();
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
            var status = new LauncherClientStatus
            {
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                IpAddress = string.Empty,
                CurrentGameName = clearPlayingGame ? string.Empty : _currentGameName,
                CurrentGameExecutablePath = clearPlayingGame ? string.Empty : _currentGameExecutablePath,
                LastSeenUtc = DateTime.UtcNow,
                ClientStartedAtUtc = _clientStartedAtUtc,
                UptimeSeconds = Math.Max(0, (long)(DateTime.UtcNow - _clientStartedAtUtc).TotalSeconds)
            };
            PopulateSystemMetrics(status);

            var filePath = Path.Combine(folder, $"{SanitizeFileName(Environment.MachineName)}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(status, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // Status reporting must never block the launcher.
        }
    }

    private void PopulateSystemMetrics(LauncherClientStatus status)
    {
        PopulateMemoryMetrics(status);
        PopulateNetworkMetrics(status);
    }

    private static void PopulateMemoryMetrics(LauncherClientStatus status)
    {
        var memoryStatus = new MemoryStatusEx();
        memoryStatus.Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        if (!GlobalMemoryStatusEx(ref memoryStatus) || memoryStatus.TotalPhys == 0)
        {
            return;
        }

        var totalGb = BytesToGb(memoryStatus.TotalPhys);
        var availableGb = BytesToGb(memoryStatus.AvailPhys);
        var usedGb = Math.Max(0, totalGb - availableGb);
        status.TotalMemoryGb = Math.Round(totalGb, 1);
        status.UsedMemoryGb = Math.Round(usedGb, 1);
        status.MemoryUsagePercent = Math.Round(usedGb / totalGb * 100, 1);
    }

    private void PopulateNetworkMetrics(LauncherClientStatus status)
    {
        var now = DateTime.UtcNow;
        var sent = 0L;
        var received = 0L;
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var stats = adapter.GetIPv4Statistics();
            sent += stats.BytesSent;
            received += stats.BytesReceived;
        }

        var elapsedSeconds = Math.Max(1, (now - _lastNetworkSampleUtc).TotalSeconds);
        if (_lastNetworkBytesSent > 0 || _lastNetworkBytesReceived > 0)
        {
            status.NetworkSentKbps = Math.Round((sent - _lastNetworkBytesSent) / 1024d / elapsedSeconds, 1);
            status.NetworkReceivedKbps = Math.Round((received - _lastNetworkBytesReceived) / 1024d / elapsedSeconds, 1);
        }

        _lastNetworkBytesSent = sent;
        _lastNetworkBytesReceived = received;
        _lastNetworkSampleUtc = now;
    }

    private static double BytesToGb(ulong bytes) => bytes / 1024d / 1024d / 1024d;

    private static double BytesToGb(long bytes) => bytes / 1024d / 1024d / 1024d;

    private string ResolveClientStatusFolder()
    {
        if (string.IsNullOrWhiteSpace(_catalogPath) || _catalogPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var catalogDirectory = Path.GetDirectoryName(Path.GetFullPath(_catalogPath));
        return string.IsNullOrWhiteSpace(catalogDirectory)
            ? string.Empty
            : Path.Combine(catalogDirectory, "client-status");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }

    private void UpdateCloseAppHotKeyRegistration()
    {
        if (_enableCloseAppHotKeyFromServer)
        {
            RegisterCloseAppHotKey();
            return;
        }

        UnregisterCloseAppHotKey();
    }

    private static async Task<bool> TrySetWindowsWallpaperAsync(string imagePathOrUri)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imagePathOrUri))
            {
                return false;
            }

            string localPath;
            if (TryGetHttpUri(imagePathOrUri, out var wallpaperUri))
            {
                localPath = await DownloadWallpaperToCacheAsync(wallpaperUri);
            }
            else
            {
                localPath = Path.GetFullPath(imagePathOrUri);
            }

            return ApplyWindowsWallpaper(localPath);
        }
        catch
        {
            return false;
        }
    }

    private static bool ApplyWindowsWallpaper(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return false;
        }

        return SystemParametersInfo(
            SpiSetDeskWallpaper,
            0,
            imagePath,
            SpifUpdateIniFile | SpifSendWinIniChange);
    }

    private static async Task<string> DownloadWallpaperToCacheAsync(Uri wallpaperUri)
    {
        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameLauncher.Client",
            "wallpaper-cache");

        Directory.CreateDirectory(cacheDirectory);

        var extension = Path.GetExtension(wallpaperUri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        var localPath = Path.Combine(cacheDirectory, $"server-wallpaper{extension}");
        using var response = await WallpaperHttpClient.GetAsync(wallpaperUri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var sourceStream = await response.Content.ReadAsStreamAsync();
        await using var targetStream = new FileStream(
            localPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await sourceStream.CopyToAsync(targetStream);
        await targetStream.FlushAsync();
        return localPath;
    }

    private static string ResolvePolicyWallpaperPath(string configuredWallpaperPath, string catalogPath)
    {
        var wallpaperPath = configuredWallpaperPath.Trim();
        if (string.IsNullOrWhiteSpace(wallpaperPath))
        {
            return string.Empty;
        }

        if (TryGetHttpUri(wallpaperPath, out _))
        {
            return wallpaperPath;
        }

        if (Path.IsPathRooted(wallpaperPath))
        {
            return Path.GetFullPath(wallpaperPath);
        }

        if (TryGetHttpUri(catalogPath, out var catalogUri))
        {
            return new Uri(catalogUri, wallpaperPath).ToString();
        }

        try
        {
            var catalogFullPath = Path.GetFullPath(catalogPath);
            var catalogDirectory = Path.GetDirectoryName(catalogFullPath);
            if (!string.IsNullOrWhiteSpace(catalogDirectory))
            {
                return Path.GetFullPath(Path.Combine(catalogDirectory, wallpaperPath));
            }
        }
        catch
        {
            // Keep fallback behavior below.
        }

        return wallpaperPath;
    }

    private static bool TryGetHttpUri(string input, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (!string.Equals(candidate.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private void HandleCloseRunningApplicationHotKey()
    {
        if (!_enableCloseAppHotKeyFromServer)
        {
            return;
        }

        _launchService.TryCloseLastLaunchedApplication(out _);
    }

    private async Task ExecuteWithErrorHandlingAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EnsureStartupWithWindows()
    {
        try
        {
            var executablePath = Application.ExecutablePath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return;
            }

            var startupValue = $"\"{Path.GetFullPath(executablePath)}\"";
            using var runKey = Registry.CurrentUser.CreateSubKey(StartupRegistryPath, writable: true);
            if (runKey is null)
            {
                return;
            }

            var existingValue = runKey.GetValue(StartupRegistryValueName)?.ToString();
            if (!string.Equals(existingValue, startupValue, StringComparison.Ordinal))
            {
                runKey.SetValue(StartupRegistryValueName, startupValue, RegistryValueKind.String);
            }
        }
        catch
        {
            // Ignore autostart registration failures on restricted environments.
        }
    }

    private void RegisterCloseAppHotKey()
    {
        if (_isCloseAppHotKeyRegistered || !IsHandleCreated)
        {
            return;
        }

        _isCloseAppHotKeyRegistered = RegisterHotKey(Handle, CloseLaunchedAppHotKeyId, ModControl | ModAlt, (uint)Keys.K);
    }

    private void UnregisterCloseAppHotKey()
    {
        if (!_isCloseAppHotKeyRegistered || !IsHandleCreated)
        {
            return;
        }

        UnregisterHotKey(Handle, CloseLaunchedAppHotKeyId);
        _isCloseAppHotKeyRegistered = false;
    }

    private static Button CreateHeaderLinkButton(string text, string tooltip, string url)
    {
        var button = new Button
        {
            Text = text,
            Width = 58,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(6, 2, 0, 0),
            BackColor = Color.FromArgb(237, 245, 251),
            ForeColor = Color.FromArgb(24, 44, 62),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(174, 196, 214);
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) => OpenExternalUrl(url);

        var toolTip = new ToolTip();
        toolTip.SetToolTip(button, tooltip);
        return button;
    }

    private static void OpenExternalUrl(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore inability to open quick links.
        }
    }

    private static Image BuildHeaderLogoImage()
    {
        using var source = SystemIcons.Shield.ToBitmap();
        var size = 36;
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var circleBrush = new SolidBrush(Color.FromArgb(255, 122, 55));
        graphics.FillEllipse(circleBrush, 0, 0, size - 1, size - 1);
        graphics.DrawImage(source, 6, 6, size - 12, size - 12);
        return bitmap;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
