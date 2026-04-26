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

        _gamesViewModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _gamesViewModeComboBox.Items.AddRange(new object[] { "Dạng bảng", "Dạng lưới" });
        _gamesViewModeComboBox.SelectedIndex = 0;
        _gamesViewModeComboBox.SelectedIndexChanged += GamesViewModeComboBox_SelectedIndexChanged;
        toolbar.Controls.Add(_gamesViewModeComboBox);

        _moveTopButton.Text = "Lên đầu";
        _moveTopButton.Click += async (_, _) => await ReorderSelectedGameAsync(-99999);
        toolbar.Controls.Add(_moveTopButton);

        _moveUpButton.Text = "Lên trên";
        _moveUpButton.Click += async (_, _) => await ReorderSelectedGameAsync(-15);
        toolbar.Controls.Add(_moveUpButton);

        _moveDownButton.Text = "Xuống dưới";
        _moveDownButton.Click += async (_, _) => await ReorderSelectedGameAsync(15);
        toolbar.Controls.Add(_moveDownButton);
        toolbar.Controls.Add(CreateButton("Danh dau Hot", async (_, _) => await SetSelectedGameHotAsync(true)));
        toolbar.Controls.Add(CreateButton("Bo Hot", async (_, _) => await SetSelectedGameHotAsync(false)));

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
        var page = new TabPage("Dashboard máy trạm");
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
        toolbar.Controls.Add(CreateButton("Làm mới", (_, _) => RefreshClientDashboard()));
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

    private TabPage BuildServerDashboardTab()
    {
        var page = new TabPage("Dashboard máy server");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _serverDashboardSummaryLabel.Dock = DockStyle.Fill;
        _serverDashboardSummaryLabel.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        _serverDashboardSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _serverDashboardSummaryLabel.Text = "Đang tải thông tin máy server...";

        var metricCards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 4, 0, 8)
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
            Padding = new Padding(0, 4, 0, 8)
        };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        details.Controls.Add(CreateServerInfoCard("Mạng", _serverDashboardNetworkLabel), 0, 0);
        details.Controls.Add(CreateServerInfoCard("Kho dữ liệu & catalog", _serverDashboardStorageLabel), 1, 0);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
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
        valueLabel.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        valueLabel.Text = "-";
        progressBar.Dock = DockStyle.Bottom;
        progressBar.Height = 18;
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        card.Controls.Add(valueLabel, 0, 1);
        card.Controls.Add(progressBar, 0, 2);
        return card;
    }

    private static Control CreateServerInfoCard(string title, Label valueLabel)
    {
        var card = CreateServerCardBase(title);
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
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
            Padding = new Padding(12),
            Margin = new Padding(4),
            BackColor = Color.FromArgb(245, 248, 252)
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 58, 86),
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

}



