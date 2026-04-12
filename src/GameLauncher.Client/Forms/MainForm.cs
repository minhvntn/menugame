using Microsoft.Win32;
using System.Net.Http;
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
    private static readonly HttpClient WallpaperHttpClient = new();

    private readonly SettingsService _settingsService;
    private readonly CatalogReaderService _catalogService;
    private readonly GameLaunchService _launchService;

    private readonly TextBox _searchTextBox = new();
    private readonly ComboBox _categoryComboBox = new();
    private readonly Label _summaryLabel = new();
    private readonly FlowLayoutPanel _cardsPanel = new();

    private List<LauncherGameRow> _allRows = new();
    private string _catalogPath = string.Empty;
    private string _backgroundImagePath = string.Empty;
    private bool _enableCloseAppHotKeyFromServer = true;
    private Image? _cardsBackgroundImage;
    private bool _isCloseAppHotKeyRegistered;

    public MainForm(
        SettingsService settingsService,
        CatalogReaderService catalogService,
        GameLaunchService launchService)
    {
        _settingsService = settingsService;
        _catalogService = catalogService;
        _launchService = launchService;

        Text = "Menu trò chơi";
        Width = 1260;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 247, 250);

        BuildLayout();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        EnsureStartupWithWindows();
        await LoadCatalogOnStartupAsync();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateCloseAppHotKeyRegistration();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cardsBackgroundImage?.Dispose();
        _cardsBackgroundImage = null;
        base.OnFormClosed(e);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterCloseAppHotKey();
        base.OnHandleDestroyed(e);
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
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

        var titleLabel = new Label
        {
            Text = "Tìm trò chơi",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };

        _searchTextBox.Dock = DockStyle.Fill;
        _searchTextBox.PlaceholderText = "Nhập tên trò chơi...";
        _searchTextBox.TextChanged += (_, _) => ApplyFilter();

        _categoryComboBox.Dock = DockStyle.Fill;
        _categoryComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _categoryComboBox.SelectedIndexChanged += (_, _) => ApplyFilter();

        var refreshButton = new Button
        {
            Text = "Làm mới",
            Dock = DockStyle.Fill
        };
        refreshButton.Click += RefreshButton_Click;

        var reloadButton = new Button
        {
            Text = "Tải lại",
            Dock = DockStyle.Fill
        };
        reloadButton.Click += ReloadCatalogButton_Click;

        var chooseBackgroundButton = new Button
        {
            Text = "Chọn nền",
            Dock = DockStyle.Fill
        };
        chooseBackgroundButton.Click += ChooseBackgroundButton_Click;

        var clearBackgroundButton = new Button
        {
            Text = "Xóa nền",
            Dock = DockStyle.Fill
        };
        clearBackgroundButton.Click += ClearBackgroundButton_Click;

        topBar.Controls.Add(titleLabel, 0, 0);
        topBar.Controls.Add(_searchTextBox, 1, 0);
        topBar.Controls.Add(_categoryComboBox, 2, 0);
        topBar.Controls.Add(refreshButton, 3, 0);
        topBar.Controls.Add(reloadButton, 4, 0);
        topBar.Controls.Add(chooseBackgroundButton, 5, 0);
        topBar.Controls.Add(clearBackgroundButton, 6, 0);

        _cardsPanel.Dock = DockStyle.Fill;
        _cardsPanel.AutoScroll = true;
        _cardsPanel.WrapContents = true;
        _cardsPanel.FlowDirection = FlowDirection.LeftToRight;
        _cardsPanel.Padding = new Padding(4);
        _cardsPanel.BackgroundImageLayout = ImageLayout.Zoom;

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _summaryLabel.Text = "Đang tải danh sách trò chơi... (Ctrl + Alt + K: đóng game đang chạy)";

        root.Controls.Add(topBar, 0, 0);
        root.Controls.Add(_cardsPanel, 0, 1);
        root.Controls.Add(_summaryLabel, 0, 2);

        Controls.Add(root);
    }

    private async Task LoadCatalogOnStartupAsync()
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            var settings = await _settingsService.LoadAsync();
            _catalogPath = ResolveCatalogPathWithPriority(settings.CatalogPath);
            _backgroundImagePath = settings.BackgroundImagePath;

            ApplyBackgroundImage(_backgroundImagePath);
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

        BuildCategoryFilter();
        ApplyFilter();
    }

    private void BuildCategoryFilter()
    {
        var previous = _categoryComboBox.SelectedItem?.ToString() ?? "Tất cả nhóm";
        var categories = _allRows
            .Select(row => row.Category)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        categories.Insert(0, "Tất cả nhóm");

        _categoryComboBox.DataSource = null;
        _categoryComboBox.DataSource = categories;

        _categoryComboBox.SelectedItem = categories.Contains(previous, StringComparer.OrdinalIgnoreCase)
            ? categories.First(item => string.Equals(item, previous, StringComparison.OrdinalIgnoreCase))
            : "Tất cả nhóm";
    }

    private void ApplyFilter()
    {
        var keyword = _searchTextBox.Text.Trim();
        var category = _categoryComboBox.SelectedItem?.ToString() ?? "Tất cả nhóm";

        var query = _allRows.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(row => row.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(category, "Tất cả nhóm", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(row => string.Equals(row.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RenderCards(filtered);

        var readyCount = filtered.Count(row => string.Equals(row.Status, "Sẵn sàng", StringComparison.OrdinalIgnoreCase));
        var hotKeyStatus = _enableCloseAppHotKeyFromServer ? "Hotkey đóng app: Bật" : "Hotkey đóng app: Tắt";
        _summaryLabel.Text = $"Hiển thị {filtered.Count}/{_allRows.Count} trò chơi. Sẵn sàng: {readyCount}. {hotKeyStatus}.";
    }

    private void RenderCards(IReadOnlyList<LauncherGameRow> rows)
    {
        _cardsPanel.SuspendLayout();

        foreach (Control control in _cardsPanel.Controls)
        {
            control.Dispose();
        }

        _cardsPanel.Controls.Clear();

        foreach (var row in rows)
        {
            var card = new GameCardControl(row, PlayGame);
            _cardsPanel.Controls.Add(card);
        }

        _cardsPanel.ResumeLayout();
    }

    private void PlayGame(LauncherGameRow row)
    {
        _ = ExecuteWithErrorHandlingAsync(async () =>
        {
            _launchService.Launch(row);
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

    private async void ChooseBackgroundButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Chọn hình nền client",
            Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Tất cả tệp (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ApplyBackgroundImage(dialog.FileName);
            await SaveLauncherSettingsAsync();
        });
    }

    private async void ClearBackgroundButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            ApplyBackgroundImage(string.Empty);
            await SaveLauncherSettingsAsync();
        });
    }

    private void ApplyBackgroundImage(string? imagePath)
    {
        _cardsBackgroundImage?.Dispose();
        _cardsBackgroundImage = null;
        _cardsPanel.BackgroundImage = null;

        var normalizedPath = imagePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            _backgroundImagePath = string.Empty;
            return;
        }

        if (!File.Exists(normalizedPath))
        {
            _backgroundImagePath = string.Empty;
            return;
        }

        try
        {
            _cardsBackgroundImage = LoadImageWithoutLock(normalizedPath);
            _cardsPanel.BackgroundImage = _cardsBackgroundImage;
            _backgroundImagePath = normalizedPath;
        }
        catch
        {
            _cardsBackgroundImage?.Dispose();
            _cardsBackgroundImage = null;
            _cardsPanel.BackgroundImage = null;
            _backgroundImagePath = string.Empty;
        }
    }

    private static Image LoadImageWithoutLock(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private async Task SaveLauncherSettingsAsync()
    {
        await _settingsService.SaveAsync(new LauncherSettings
        {
            CatalogPath = _catalogPath,
            BackgroundImagePath = _backgroundImagePath
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

        var wallpaperPath = effectivePolicy.ClientWindowsWallpaperPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(wallpaperPath))
        {
            return;
        }

        var resolvedWallpaperPath = ResolvePolicyWallpaperPath(wallpaperPath, _catalogPath);
        if (!await TrySetWindowsWallpaperAsync(resolvedWallpaperPath))
        {
            _summaryLabel.Text = $"Không thể áp dụng hình nền Windows từ server: {wallpaperPath}";
        }
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
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return false;
        }

        if (!File.Exists(imagePath))
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
            _summaryLabel.Text = "Server đã tắt tính năng đóng ứng dụng bằng phím tắt.";
            return;
        }

        if (_launchService.TryCloseLastLaunchedApplication(out var message))
        {
            _summaryLabel.Text = $"{message} (Ctrl + Alt + K)";
            return;
        }

        _summaryLabel.Text = message;
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

    private async void ReloadCatalogButton_Click(object? sender, EventArgs e)
    {
        await ExecuteWithErrorHandlingAsync(LoadCatalogAsync);
    }

    private void RefreshButton_Click(object? sender, EventArgs e)
    {
        ApplyFilter();
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
            _summaryLabel.Text = "Không thể tải menu trò chơi. Vui lòng liên hệ kỹ thuật.";
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);
}
