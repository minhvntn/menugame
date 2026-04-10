using System.Diagnostics;
using GameLauncher.Client.Controls;
using GameLauncher.Client.Models;
using GameLauncher.Client.Services;

namespace GameLauncher.Client.Forms;

public sealed class MainForm : Form
{
    private readonly SettingsService _settingsService;
    private readonly CatalogReaderService _catalogService;
    private readonly GameLaunchService _launchService;

    private readonly TextBox _searchTextBox = new();
    private readonly ComboBox _categoryComboBox = new();
    private readonly Label _summaryLabel = new();
    private readonly FlowLayoutPanel _cardsPanel = new();

    private List<LauncherGameRow> _allRows = new();
    private string _catalogPath = string.Empty;

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
        await LoadCatalogOnStartupAsync();
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
            ColumnCount = 5
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

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

        topBar.Controls.Add(titleLabel, 0, 0);
        topBar.Controls.Add(_searchTextBox, 1, 0);
        topBar.Controls.Add(_categoryComboBox, 2, 0);
        topBar.Controls.Add(refreshButton, 3, 0);
        topBar.Controls.Add(reloadButton, 4, 0);

        _cardsPanel.Dock = DockStyle.Fill;
        _cardsPanel.AutoScroll = true;
        _cardsPanel.WrapContents = true;
        _cardsPanel.FlowDirection = FlowDirection.LeftToRight;
        _cardsPanel.Padding = new Padding(4);

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _summaryLabel.Text = "Đang tải danh sách trò chơi...";

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
            _catalogPath = settings.CatalogPath;

            if (string.IsNullOrWhiteSpace(_catalogPath))
            {
                _catalogPath = Path.Combine(AppContext.BaseDirectory, "games.catalog.json");
            }

            await LoadCatalogAsync();
        });
    }

    private async Task LoadCatalogAsync()
    {
        if (string.IsNullOrWhiteSpace(_catalogPath))
        {
            throw new InvalidOperationException("Chưa cấu hình đường dẫn danh mục trò chơi.");
        }

        _allRows = (await _catalogService.LoadCatalogRowsAsync(_catalogPath)).ToList();

        await _settingsService.SaveAsync(new LauncherSettings
        {
            CatalogPath = _catalogPath
        });

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
        _summaryLabel.Text = $"Hiển thị {filtered.Count}/{_allRows.Count} trò chơi. Sẵn sàng: {readyCount}.";
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
            var card = new GameCardControl(row, PlayGame, OpenGameFolder);
            _cardsPanel.Controls.Add(card);
        }

        _cardsPanel.ResumeLayout();
    }

    private void PlayGame(LauncherGameRow row)
    {
        _ = ExecuteWithErrorHandlingAsync(async () =>
        {
            _launchService.Launch(row);
            await Task.CompletedTask;
        });
    }

    private void OpenGameFolder(LauncherGameRow row)
    {
        _ = ExecuteWithErrorHandlingAsync(async () =>
        {
            if (!Directory.Exists(row.InstallPath))
            {
                throw new DirectoryNotFoundException($"Không tìm thấy thư mục trò chơi: {row.InstallPath}");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{row.InstallPath}\"",
                UseShellExecute = true
            });

            await Task.CompletedTask;
        });
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
}
