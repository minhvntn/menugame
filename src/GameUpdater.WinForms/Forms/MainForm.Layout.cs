using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using GameUpdater.Core.Abstractions;
using GameUpdater.Core.Services;
using GameUpdater.Shared.Models;

namespace GameUpdater.WinForms.Forms;

public sealed partial class MainForm
{
    private GameRecord? SelectedGame => _gamesBinding.Current as GameRecord;

    private void BuildLayout()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(16, 9)
        };

        tabs.TabPages.Add(BuildGamesTab());
        tabs.TabPages.Add(BuildClientDashboardTab());
        tabs.TabPages.Add(BuildServerDashboardTab());
        tabs.TabPages.Add(BuildResourcesTab());
        tabs.TabPages.Add(BuildUpdateTab());
        tabs.TabPages.Add(BuildLogsTab());
        tabs.TabPages.Add(BuildSettingsTab());
        ConfigureTabIcons(tabs);

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
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            WrapContents = false
        };


        _scanManifestButton.Text = "Quét manifest";
        _scanManifestButton.Click += ScanManifestButton_Click;
        StyleButton(_scanManifestButton);
        toolbar.Controls.Add(_scanManifestButton);

        _gamesViewModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _gamesViewModeComboBox.Items.AddRange(new object[] { "Dạng bảng", "Dạng lưới" });
        _gamesViewModeComboBox.SelectedIndex = 0;
        _gamesViewModeComboBox.SelectedIndexChanged += GamesViewModeComboBox_SelectedIndexChanged;
        toolbar.Controls.Add(_gamesViewModeComboBox);

        _moveTopButton.Text = "Lên đầu";
        _moveTopButton.Click += async (_, _) => await ReorderSelectedGameAsync(-99999);
        StyleButton(_moveTopButton);
        toolbar.Controls.Add(_moveTopButton);

        _moveUpButton.Text = "Lên trên";
        _moveUpButton.Click += async (_, _) => await ReorderSelectedGameAsync(-15);
        StyleButton(_moveUpButton);
        toolbar.Controls.Add(_moveUpButton);

        _moveDownButton.Text = "Xuống dưới";
        _moveDownButton.Click += async (_, _) => await ReorderSelectedGameAsync(15);
        StyleButton(_moveDownButton);
        toolbar.Controls.Add(_moveDownButton);
        toolbar.Controls.Add(CreateButton("Đánh dấu Hot", async (_, _) => await SetSelectedGameHotAsync(true)));
        toolbar.Controls.Add(CreateButton("Bỏ Hot", async (_, _) => await SetSelectedGameHotAsync(false)));

        toolbar.Controls.Add(CreateButton("Xuất Danh Mục Client", ExportCatalogButton_Click));
        toolbar.Controls.Add(CreateButton("Làm mới", RefreshButton_Click));

        ConfigureGamesGrid();
        ConfigureGamesGridPanel();
        EnsureGamesContextMenu();

        var gridContainer = new Panel { Dock = DockStyle.Fill };
        gridContainer.Controls.Add(_gamesGridPanel);
        gridContainer.Controls.Add(_gamesGrid);

        leftPanel.Controls.Add(toolbar, 0, 0);
        leftPanel.Controls.Add(gridContainer, 0, 1);

        root.Controls.Add(leftPanel, 0, 0);

        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildClientDashboardTab()
    {
        var page = new TabPage("Client");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 8),
            WrapContents = false
        };

        _clientDashboardSummaryLabel.AutoSize = true;
        _clientDashboardSummaryLabel.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        _clientDashboardSummaryLabel.Text = "Chưa có dữ liệu máy trạm.";
        _clientDashboardSummaryLabel.Margin = new Padding(0, 6, 18, 0);
        toolbar.Controls.Add(_clientDashboardSummaryLabel);
        toolbar.Controls.Add(CreateButton("Làm mới", async (_, _) => await RefreshClientDashboardAsync(forceNetworkProbe: true)));
        toolbar.Controls.Add(CreateButton("Mở thư mục trạng thái", OpenClientStatusFolderButton_Click));

        _clientDashboardGameStatsLabel.Dock = DockStyle.Fill;
        _clientDashboardGameStatsLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _clientDashboardGameStatsLabel.Text = "Game hot: - • Chơi nhiều nhất: - • Vừa cập nhật: -";
        _clientDashboardGameStatsLabel.TextAlign = ContentAlignment.MiddleLeft;

        ConfigureClientStatusGrid();
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(_clientDashboardGameStatsLabel, 0, 1);
        root.Controls.Add(_clientStatusGrid, 0, 2);

        page.Controls.Add(root);
        return page;
    }

    // Dashboard card color palette - modern elevated dark theme.
    private static readonly Color DashboardCardBackground = Color.FromArgb(24, 30, 50);
    private static readonly Color DashboardCardBorder = Color.FromArgb(45, 55, 80);
    private static readonly Color DashboardTitleColor = Color.FromArgb(0, 200, 255);      // Vivid cyan
    private static readonly Color DashboardValueColor = Color.FromArgb(240, 245, 255);    // Near-white
    private static readonly Color DashboardInfoTextColor = Color.FromArgb(180, 200, 225); // Light steel
    private static readonly Color DashboardSummaryColor = Color.FromArgb(160, 210, 255);  // Soft sky-blue
    private static readonly Color DashboardGoodColor = Color.FromArgb(34, 197, 94);       // Emerald green
    private static readonly Color DashboardWarnColor = Color.FromArgb(250, 204, 21);      // Amber
    private static readonly Color DashboardDangerColor = Color.FromArgb(239, 68, 68);     // Red

    /// <summary>Returns a color based on usage percent: green -> amber -> red.</summary>
    private static Color GetUsageColor(double percent)
    {
        if (percent >= 85) return DashboardDangerColor;
        if (percent >= 65) return DashboardWarnColor;
        return DashboardGoodColor;
    }

    private TabPage BuildServerDashboardTab()
    {
        var page = new TabPage("Server");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(16),
            BackColor = Color.FromArgb(14, 18, 32)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _serverDashboardSummaryLabel.Dock = DockStyle.Fill;
        _serverDashboardSummaryLabel.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        _serverDashboardSummaryLabel.ForeColor = DashboardSummaryColor;
        _serverDashboardSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _serverDashboardSummaryLabel.Text = "Đang tải thông tin máy server...";

        var metricCards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 4, 0, 8),
            BackColor = Color.Transparent
        };
        metricCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        metricCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        metricCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        metricCards.Controls.Add(CreateServerMetricCard("CPU", _serverDashboardCpuLabel, _serverCpuProgressBar), 0, 0);
        metricCards.Controls.Add(CreateServerMetricCard("RAM", _serverDashboardMemoryLabel, _serverMemoryProgressBar), 1, 0);
        metricCards.Controls.Add(CreateServerMetricCard("Ổ hệ thống", _serverDashboardDiskLabel, _serverDiskProgressBar), 2, 0);

        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(0, 4, 0, 8),
            BackColor = Color.Transparent
        };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        details.Controls.Add(CreateServerInfoCard("Mạng", _serverDashboardNetworkLabel), 0, 0);
        details.Controls.Add(CreateServerInfoCard("Kho dữ liệu & catalog", _serverDashboardStorageLabel), 1, 0);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.Transparent
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottom.Controls.Add(CreateServerInfoCard("Dịch vụ đang chạy", _serverDashboardServiceLabel), 0, 0);
        bottom.Controls.Add(CreateServerInfoCard("Khuyến nghị", _serverDashboardRecommendationLabel), 1, 0);

        root.Controls.Add(_serverDashboardSummaryLabel, 0, 0);
        root.Controls.Add(metricCards, 0, 1);
        root.Controls.Add(details, 0, 2);
        root.Controls.Add(bottom, 0, 3);
        page.Controls.Add(root);
        return page;
    }

    private static Control CreateServerMetricCard(string title, Label valueLabel, ProgressBar progressBar)
    {
        var card = CreateServerCardBase(title);
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
        valueLabel.ForeColor = DashboardValueColor;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        valueLabel.Text = "-";
        progressBar.Dock = DockStyle.Bottom;
        progressBar.Height = 10;
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.BackColor = Color.FromArgb(35, 42, 65);
        card.Controls.Add(valueLabel, 0, 1);
        card.Controls.Add(progressBar, 0, 2);
        return card;
    }

    private static Control CreateServerInfoCard(string title, Label valueLabel)
    {
        var card = CreateServerCardBase(title);
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        valueLabel.ForeColor = DashboardInfoTextColor;
        valueLabel.TextAlign = ContentAlignment.TopLeft;
        valueLabel.Text = "-";
        card.Controls.Add(valueLabel, 0, 1);
        return card;
    }

    private static TableLayoutPanel CreateServerCardBase(string title)
    {
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(14),
            Margin = new Padding(5),
            BackColor = DashboardCardBackground
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
            ForeColor = DashboardTitleColor,
            TextAlign = ContentAlignment.MiddleLeft
        };
        card.Controls.Add(titleLabel, 0, 0);
        return card;
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
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
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
            Padding = new Padding(0, 2, 0, 0),
            WrapContents = false
        };
        _saveResourceSettingsButton.Text = "Lưu cấu hình";
        _saveResourceSettingsButton.Click += SaveResourceSettingsButton_Click;
        StyleButton(_saveResourceSettingsButton);

        _checkResourceHealthButton.Text = "Kiểm tra tài nguyên";
        _checkResourceHealthButton.Click += CheckResourceHealthButton_Click;
        StyleButton(_checkResourceHealthButton);

        _syncSelectedResourceButton.Text = "Tải trò chơi đã chọn";
        _syncSelectedResourceButton.Click += SyncSelectedResourceButton_Click;
        StyleButton(_syncSelectedResourceButton, primary: true);


        actionsRow.Controls.Add(_saveResourceSettingsButton);
        actionsRow.Controls.Add(_checkResourceHealthButton);
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

    private void ConfigureTabIcons(TabControl tabs)
    {
        if (tabs.TabPages.Count == 0)
        {
            return;
        }

        var imageList = BuildTabIconImageList();
        if (imageList.Images.Count == 0)
        {
            return;
        }

        tabs.ImageList = imageList;
        tabs.Appearance = TabAppearance.Normal;
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.ItemSize = new Size(120, 84);
        tabs.Padding = new Point(0, 0);
        tabs.BackColor = Color.White;
        tabs.Paint += (_, e) => PaintTabHeaderChrome(tabs, e.Graphics);
        tabs.DrawItem += Tabs_DrawItem;

        var iconKeys = new[]
        {
            "tro-choi.png",
            "dashboard-client.png",
            "dashboard-server.png",
            "tai-nguyen.png",
            "cap-nhap.png",
            "lich-su.png",
            "setting.png"
        };

        for (var index = 0; index < Math.Min(tabs.TabPages.Count, iconKeys.Length); index++)
        {
            var iconKey = iconKeys[index];
            if (imageList.Images.ContainsKey(iconKey))
            {
                tabs.TabPages[index].ImageKey = iconKey;
            }
        }
    }

    private static ImageList BuildTabIconImageList()
    {
        var imageList = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(40, 40)
        };

        var iconFiles = new[]
        {
            "tro-choi.png",
            "dashboard-client.png",
            "dashboard-server.png",
            "tai-nguyen.png",
            "cap-nhap.png",
            "lich-su.png",
            "setting.png"
        };

        foreach (var iconFile in iconFiles)
        {
            var image = TryLoadEmbeddedTabIcon(iconFile, imageList.ImageSize);
            if (image is not null)
            {
                imageList.Images.Add(iconFile, image);
            }
        }

        return imageList;
    }

    private static Image? TryLoadEmbeddedTabIcon(string fileName, Size imageSize)
    {
        var resourceName = $"GameUpdater.WinForms.Resources.{fileName}";
        var assembly = typeof(MainForm).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var sourceImage = Image.FromStream(stream);
        var bitmap = new Bitmap(imageSize.Width, imageSize.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);
        graphics.DrawImage(sourceImage, 0, 0, imageSize.Width, imageSize.Height);
        return bitmap;
    }

    private static void Tabs_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs ||
            e.Index < 0 ||
            e.Index >= tabs.TabPages.Count)
        {
            return;
        }

        var tabPage = tabs.TabPages[e.Index];
        var bounds = e.Bounds;
        var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var backgroundColor = Color.White;
        var textColor = isSelected ? Color.FromArgb(17, 24, 39) : Color.FromArgb(71, 85, 105);

        var paintBounds = Rectangle.Inflate(bounds, 2, 2);
        using var backgroundBrush = new SolidBrush(backgroundColor);
        e.Graphics.FillRectangle(backgroundBrush, paintBounds);

        var iconSize = tabs.ImageList?.ImageSize.Width ?? 40;
        var iconTop = bounds.Top + 6;
        if (!string.IsNullOrWhiteSpace(tabPage.ImageKey) &&
            tabs.ImageList?.Images[tabPage.ImageKey] is Image icon)
        {
            var iconLeft = bounds.Left + (bounds.Width - iconSize) / 2;
            e.Graphics.DrawImage(icon, new Rectangle(iconLeft, iconTop, iconSize, iconSize));
        }

        var textTop = iconTop + iconSize + 2;
        using var tabTextFont = new Font(
            "Segoe UI",
            Math.Max(6f, tabs.Font.Size - 2f),
            tabs.Font.Style);
        var textRect = new Rectangle(
            bounds.Left + 4,
            textTop,
            bounds.Width - 8,
            Math.Max(1, bounds.Bottom - textTop - 2));
        TextRenderer.DrawText(
            e.Graphics,
            tabPage.Text,
            tabTextFont,
            textRect,
            textColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);

        // Intentionally skip focus rectangle to keep tabs visually borderless.
    }

    private static void PaintTabHeaderChrome(TabControl tabs, Graphics graphics)
    {
        if (tabs.TabCount == 0)
        {
            return;
        }

        using var erasePen = new Pen(Color.White, 2f);

        foreach (TabPage page in tabs.TabPages)
        {
            var tabRect = tabs.GetTabRect(tabs.TabPages.IndexOf(page));
            graphics.DrawRectangle(erasePen, tabRect);
        }

        var firstRect = tabs.GetTabRect(0);
        var stripBottomY = firstRect.Bottom + 1;
        graphics.DrawLine(erasePen, 0, stripBottomY, tabs.Width, stripBottomY);
        graphics.DrawLine(erasePen, 0, 0, tabs.Width, 0);
    }

}






