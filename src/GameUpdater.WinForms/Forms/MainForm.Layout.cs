using GameUpdater.WinForms.Extensions;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using GameUpdater.Core.Abstractions;
using GameUpdater.Core.Services;
using GameUpdater.Shared.Localization;
using GameUpdater.Shared.Models;
using GameUpdater.WinForms.Controls;

namespace GameUpdater.WinForms.Forms;

public sealed partial class MainForm
{
    private GameRecord? SelectedGame => _gamesBinding.Current as GameRecord;

    private void UpdateResourceSourceRootPathFromUi()
    {
        var paths = new System.Collections.Generic.List<string>();
        foreach (Control c in _sourcesContainer.Controls)
        {
            if (c is TableLayoutPanel tlp && tlp.Controls.Count > 1 && tlp.Controls[1] is IconTextBox tb)
            {
                paths.Add(tb.Input.Text.Trim());
            }
        }
        _resourceSourceRootPath = string.Join(";", paths.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private void AddSourceRowUi(string path)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            Padding = this.ScalePadding(0, 0, 0, 6),
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));

        int count = _sourcesContainer.Controls.OfType<TableLayoutPanel>().Count() + 1;
        var label = new Label { Text = $"Nguồn IDC {count}", Font = new Font("Segoe UI", 10.5f), ForeColor = Color.FromArgb(30,30,40), AutoSize = true, Anchor = AnchorStyles.Left };
        
        var inputWrapper = new IconTextBox(DrawGlobeIcon);
        inputWrapper.Dock = DockStyle.Fill;
        inputWrapper.Margin = this.ScalePadding(0, 0, 10, 0);
        inputWrapper.Input.Text = path;
        if (string.IsNullOrEmpty(path)) inputWrapper.Input.PlaceholderText = $"Nhập URL nguồn IDC {count}";
        inputWrapper.Input.TextChanged += (_, _) => UpdateResourceRootsFromInputs();

        var browseBtn = new IconButton 
        { 
            DrawIcon = DrawDotsIcon,
            Margin = this.ScalePadding(0, 0, 10, 0),
            Anchor = AnchorStyles.None 
        };
        browseBtn.MouseClick += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog();
            if (!string.IsNullOrWhiteSpace(inputWrapper.Input.Text))
                dialog.SelectedPath = inputWrapper.Input.Text;
            if (dialog.ShowDialog() == DialogResult.OK)
                inputWrapper.Input.Text = dialog.SelectedPath;
        };

        var removeBtn = new IconButton 
        { 
            DrawIcon = DrawTrashIcon,
            NormalColor = Color.FromArgb(254, 242, 242),
            HoverColor = Color.FromArgb(254, 226, 226),
            PressedColor = Color.FromArgb(252, 165, 165),
            IconNormalColor = Color.FromArgb(239, 68, 68),
            IconHoverColor = Color.FromArgb(220, 38, 38),
            BorderColor = Color.FromArgb(254, 202, 202),
            Margin = this.ScalePadding(0), 
            Anchor = AnchorStyles.None 
        };
        removeBtn.MouseClick += (_, _) =>
        {
            _sourcesContainer.Controls.Remove(row);
            UpdateResourceRootsFromInputs();
            
            // Update labels
            int i = 1;
            foreach (Control c in _sourcesContainer.Controls)
            {
                if (c is TableLayoutPanel tlp && tlp.Controls.Count > 0 && tlp.Controls[0] is Label lbl)
                {
                    lbl.Text = $"Nguồn IDC {i}";
                    if (tlp.Controls.Count > 1 && tlp.Controls[1] is IconTextBox tb && string.IsNullOrEmpty(tb.Input.Text))
                    {
                        tb.Input.PlaceholderText = $"Nhập URL nguồn IDC {i}";
                    }
                    i++;
                }
            }
        };

        row.Controls.Add(label, 0, 0);
        row.Controls.Add(inputWrapper, 1, 0);
        row.Controls.Add(browseBtn, 2, 0);
        row.Controls.Add(removeBtn, 3, 0);

        _sourcesContainer.Controls.Add(row);
    }

    private void BuildSourcesUi()
    {
        _sourcesContainer.Controls.Clear();
        var paths = GetConfiguredResourceSourceRoots().ToList();
            
        if (paths.Count == 0) paths.Add("");

        foreach (var path in paths)
        {
            AddSourceRowUi(path);
        }

        var addBtn = new GameUpdater.WinForms.Controls.ModernButton { Text = "+  Thêm nguồn IDC", Size = this.ScaleSize(180, 36), Margin = this.ScalePadding(180, 0, 0, 0), CornerRadius = 6, ColorType = GameUpdater.WinForms.Controls.ButtonColorType.DashedPurple, Font = new Font("Segoe UI", 10.5f) };
        addBtn.Click += (_, _) => 
        {
            _sourcesContainer.Controls.Remove(addBtn);
            AddSourceRowUi("");
            _sourcesContainer.Controls.Add(addBtn);
            UpdateResourceRootsFromInputs();
        };
        _sourcesContainer.Controls.Add(addBtn);
    }

    private void UpdateResourceTargetRootPathFromUi()
    {
        var paths = new System.Collections.Generic.List<string>();
        foreach (Control c in _targetsContainer.Controls)
        {
            if (c is TableLayoutPanel tlp && tlp.Controls.Count > 1 && tlp.Controls[1] is IconTextBox tb)
            {
                paths.Add(tb.Input.Text.Trim());
            }
        }
        _resourceTargetRootPath = string.Join(";", paths.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private void AddTargetRowUi(string path)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            Padding = this.ScalePadding(0, 0, 0, 6),
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));

        int count = _targetsContainer.Controls.OfType<TableLayoutPanel>().Count() + 1;
        var label = new Label { Text = $"Đích máy chủ ổ cứng {count}", Font = new Font("Segoe UI", 10.5f), ForeColor = Color.FromArgb(30,30,40), AutoSize = true, Anchor = AnchorStyles.Left };
        
        var inputWrapper = new IconTextBox(DrawFolderIcon);
        inputWrapper.Dock = DockStyle.Fill;
        inputWrapper.Margin = this.ScalePadding(0, 0, 10, 0);
        inputWrapper.Input.Text = path;
        if (string.IsNullOrEmpty(path)) inputWrapper.Input.PlaceholderText = $"Chọn ổ cứng đích {count}";
        inputWrapper.Input.TextChanged += (_, _) => UpdateResourceTargetRootPathFromUi();

        var browseBtn = new IconButton 
        { 
            DrawIcon = DrawDotsIcon,
            Margin = this.ScalePadding(0, 0, 10, 0),
            Anchor = AnchorStyles.None 
        };
        browseBtn.MouseClick += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog();
            if (!string.IsNullOrWhiteSpace(inputWrapper.Input.Text))
                dialog.SelectedPath = inputWrapper.Input.Text;
            if (dialog.ShowDialog() == DialogResult.OK)
                inputWrapper.Input.Text = dialog.SelectedPath;
        };

        var removeBtn = new IconButton 
        { 
            DrawIcon = DrawTrashIcon,
            NormalColor = Color.FromArgb(254, 242, 242),
            HoverColor = Color.FromArgb(254, 226, 226),
            PressedColor = Color.FromArgb(252, 165, 165),
            IconNormalColor = Color.FromArgb(239, 68, 68),
            IconHoverColor = Color.FromArgb(220, 38, 38),
            BorderColor = Color.FromArgb(254, 202, 202),
            Margin = this.ScalePadding(0), 
            Anchor = AnchorStyles.None 
        };
        removeBtn.MouseClick += (_, _) =>
        {
            _targetsContainer.Controls.Remove(row);
            UpdateResourceTargetRootPathFromUi();
            
            // Update labels
            int i = 1;
            foreach (Control c in _targetsContainer.Controls)
            {
                if (c is TableLayoutPanel tlp && tlp.Controls.Count > 0 && tlp.Controls[0] is Label lbl)
                {
                    lbl.Text = $"Đích máy chủ ổ cứng {i}";
                    if (tlp.Controls.Count > 1 && tlp.Controls[1] is IconTextBox tb && string.IsNullOrEmpty(tb.Input.Text))
                    {
                        tb.Input.PlaceholderText = $"Chọn ổ cứng đích {i}";
                    }
                    i++;
                }
            }
        };

        row.Controls.Add(label, 0, 0);
        row.Controls.Add(inputWrapper, 1, 0);
        row.Controls.Add(browseBtn, 2, 0);
        row.Controls.Add(removeBtn, 3, 0);

        _targetsContainer.Controls.Add(row);
    }

    private void BuildTargetsUi()
    {
        _targetsContainer.Controls.Clear();
        var paths = _resourceTargetRootPath.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .ToList();
            
        if (paths.Count == 0) paths.Add("");

        foreach (var path in paths)
        {
            AddTargetRowUi(path);
        }

        var addBtn = new GameUpdater.WinForms.Controls.ModernButton { Text = "+  Thêm đích máy chủ", Size = this.ScaleSize(180, 36), Margin = this.ScalePadding(180, 0, 0, 0), CornerRadius = 6, ColorType = GameUpdater.WinForms.Controls.ButtonColorType.DashedPurple, Font = new Font("Segoe UI", 10.5f) };
        addBtn.Click += (_, _) => 
        {
            _targetsContainer.Controls.Remove(addBtn);
            AddTargetRowUi("");
            _targetsContainer.Controls.Add(addBtn);
            UpdateResourceTargetRootPathFromUi();
        };
        _targetsContainer.Controls.Add(addBtn);
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var tabs = new HiddenHeadersTabControl
        {
            Dock = DockStyle.Fill,
            Padding = this.ScalePoint(16, 9)
        };

        tabs.TabPages.Add(BuildGamesTab());
        tabs.TabPages.Add(BuildClientDashboardTab());
        tabs.TabPages.Add(BuildServerDashboardTab());
        tabs.TabPages.Add(BuildResourcesTab());
        // Temporarily hide the "Cập nhật" tab on server app.
        tabs.TabPages.Add(BuildLogsTab());
        tabs.TabPages.Add(BuildSettingsTab());

        foreach (TabPage page in tabs.TabPages)
        {
            if (page.Text != I18n.Server.ServerTab)
            {
                page.BackColor = Color.FromArgb(248, 250, 252); // slate 50
            }
        }

        var tabHeaderPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = this.ScalePadding(12, 3, 12, 0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        // Removed tabHeaderPanel bottom border paint event

        var tabButtons = new List<ModernTabButton>();
        var tabInfo = new[]
        {
            (Text: I18n.Server.GamesTab, IconFile: "tro-choi.png", Tint: Color.FromArgb(99, 102, 241)),
            (Text: I18n.Server.ClientTab, IconFile: "dashboard-client.png", Tint: Color.FromArgb(59, 130, 246)),
            (Text: I18n.Server.ServerTab, IconFile: "dashboard-server.png", Tint: Color.FromArgb(37, 99, 235)),
            (Text: I18n.Server.ResourcesTab, IconFile: "tai-nguyen.png", Tint: Color.FromArgb(16, 185, 129)),
            (Text: I18n.Server.LogsTab, IconFile: "lich-su.png", Tint: Color.FromArgb(245, 158, 11)),
            (Text: I18n.Server.SettingsTab, IconFile: "setting.png", Tint: Color.FromArgb(99, 102, 241))
        };

        for (int i = 0; i < tabInfo.Length; i++)
        {
            var info = tabInfo[i];
            var btnIndex = i;
            var btn = new ModernTabButton
            {
                Text = info.Text,
                TabIcon = TryLoadEmbeddedTabIcon(info.IconFile, this.ScaleSize(20, 20)),
                IconTintColor = info.Tint,
                IsSelected = (i == 0)
            };

            btn.Click += (s, e) =>
            {
                tabs.SelectedIndex = btnIndex;
            };

            tabButtons.Add(btn);
            tabHeaderPanel.Controls.Add(btn);
        }

        tabs.SelectedIndexChanged += (s, e) =>
        {
            for (int i = 0; i < tabButtons.Count; i++)
            {
                tabButtons[i].IsSelected = (i == tabs.SelectedIndex);
                tabButtons[i].Invalidate();
            }
        };

        mainLayout.Controls.Add(tabHeaderPanel, 0, 0);
        mainLayout.Controls.Add(tabs, 0, 1);

        Controls.Add(mainLayout);
    }

    private TabPage BuildGamesTab()
    {
        var page = new TabPage(I18n.Server.GamesTab);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = this.ScalePadding(8),
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };

        _gamesViewModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _gamesViewModeComboBox.Items.AddRange(new object[] { I18n.Server.GamesViewTable, I18n.Server.GamesViewGrid });
        _gamesViewModeComboBox.SelectedIndex = 0;
        _gamesViewModeComboBox.SelectedIndexChanged += GamesViewModeComboBox_SelectedIndexChanged;
        toolbar.Controls.Add(_gamesViewModeComboBox);

        var addBtn = (GameUpdater.WinForms.Controls.ModernButton)CreateButton("Thêm game mới", AddGameButton_Click);
        addBtn.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Purple;
        addBtn.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Add;
        toolbar.Controls.Add(addBtn);

        var editBtn = (GameUpdater.WinForms.Controls.ModernButton)CreateButton("Sửa game", EditGameButton_Click);
        editBtn.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.PrimaryBlue;
        editBtn.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Edit;
        toolbar.Controls.Add(editBtn);

        var deleteBtn = (GameUpdater.WinForms.Controls.ModernButton)CreateButton("Xóa game", DeleteGameButton_Click);
        deleteBtn.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Red;
        deleteBtn.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Delete;
        toolbar.Controls.Add(deleteBtn);

        var exportBtn = (GameUpdater.WinForms.Controls.ModernButton)CreateButton(I18n.Server.ExportClientCatalogButton, ExportCatalogButton_Click);
        exportBtn.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Green;
        exportBtn.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Export;
        toolbar.Controls.Add(exportBtn);

        var refreshBtn = (GameUpdater.WinForms.Controls.ModernButton)CreateButton(I18n.Common.RefreshButton, RefreshButton_Click);
        refreshBtn.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Orange;
        refreshBtn.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Refresh;
        toolbar.Controls.Add(refreshBtn);

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
        var page = new TabPage(I18n.Server.ClientTab);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            Padding = this.ScalePadding(12),
            BackColor = Color.Transparent
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = this.ScalePadding(0, 8, 0, 8),
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };

        _clientDashboardSummaryLabel.AutoSize = true;
        _clientDashboardSummaryLabel.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        _clientDashboardSummaryLabel.Text = I18n.Server.ClientDashboardNoData;
        _clientDashboardSummaryLabel.Margin = this.ScalePadding(0, 6, 18, 0);
        toolbar.Controls.Add(_clientDashboardSummaryLabel);
        toolbar.Controls.Add(CreateButton(I18n.Common.RefreshButton, async (_, _) => await RefreshClientDashboardAsync(forceNetworkProbe: true)));
        toolbar.Controls.Add(CreateButton(I18n.Server.OpenClientStatusFolderButton, OpenClientStatusFolderButton_Click));

        _clientDashboardGameStatsLabel.Dock = DockStyle.Fill;
        _clientDashboardGameStatsLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _clientDashboardGameStatsLabel.Text = I18n.Server.ClientDashboardGameStatsPlaceholder;
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
        var page = new TabPage(I18n.Server.ServerTab);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = this.ScalePadding(16),
            BackColor = Color.FromArgb(14, 18, 32)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = this.ScalePadding(0, 0, 0, 0) };
        
        var refreshBtn = new ModernButton { Text = I18n.Common.RefreshButton, Width = 110, Height = 36, ColorType = ButtonColorType.Secondary, IconType = ButtonIconType.Refresh, Dock = DockStyle.Right };
        refreshBtn.Click += async (s, e) => {
            refreshBtn.Enabled = false;
            await RefreshServerDashboardAsync();
            refreshBtn.Enabled = true;
        };

        _serverDashboardSummaryLabel.Dock = DockStyle.Fill;
        _serverDashboardSummaryLabel.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        _serverDashboardSummaryLabel.ForeColor = DashboardSummaryColor;
        _serverDashboardSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _serverDashboardSummaryLabel.Text = I18n.Server.ServerDashboardLoading;

        headerPanel.Controls.Add(refreshBtn);
        headerPanel.Controls.Add(_serverDashboardSummaryLabel);

        var metricCards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = this.ScalePadding(0, 4, 0, 8),
            BackColor = Color.Transparent
        };
        metricCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        metricCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        metricCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        metricCards.Controls.Add(CreateServerMetricCard(I18n.Server.ServerMetricCpu, _serverDashboardCpuLabel, _serverCpuProgressBar), 0, 0);
        metricCards.Controls.Add(CreateServerMetricCard(I18n.Server.ServerMetricRam, _serverDashboardMemoryLabel, _serverMemoryProgressBar), 1, 0);
        metricCards.Controls.Add(CreateServerMetricCard(I18n.Server.ServerMetricSystemDrive, _serverDashboardDiskLabel, _serverDiskProgressBar), 2, 0);

        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = this.ScalePadding(0, 4, 0, 8),
            BackColor = Color.Transparent
        };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        details.Controls.Add(CreateServerInfoCard(I18n.Server.ServerCardNetwork, _serverDashboardNetworkLabel), 0, 0);
        details.Controls.Add(CreateServerInfoCard(I18n.Server.ServerCardStorage, _serverDashboardStorageLabel), 1, 0);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.Transparent
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottom.Controls.Add(CreateServerInfoCard(I18n.Server.ServerCardServices, _serverDashboardServiceLabel), 0, 0);
        bottom.Controls.Add(CreateServerInfoCard(I18n.Server.ServerCardRecommendation, _serverDashboardRecommendationLabel), 1, 0);

        root.Controls.Add(headerPanel, 0, 0);
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

    private FlowLayoutPanel BuildResourceSummaryCards()
    {
        var cardsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = this.ScalePadding(12, 0, 12, 0),
            Padding = this.ScalePadding(0),
            BackColor = Color.White,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        // 1. Display Count
        var card1 = CreateStatCard("Hiển thị 0/0 trò chơi", "Tổng số game", "🖥️", Color.FromArgb(59, 130, 246), _statDisplayCountLabel);
        // 2. Downloaded
        var card2 = CreateStatCard("Đã tải 0 trò chơi", "Hoàn tất", "✅", Color.FromArgb(16, 185, 129), _statDownloadedLabel);
        // 3. Missing
        var card3 = CreateStatCard("Chưa tải 0 trò chơi", "Cần tải về", "📦", Color.FromArgb(245, 158, 11), _statMissingLabel);
        // 6. Target OK (with progress bar)
        var card6 = CreateStatCard("Ổ game trống", "100/100 GB (100%)", "💿", Color.FromArgb(236, 72, 153), _statTargetOkLabel, true);

        cardsLayout.SizeChanged += (s, e) =>
        {
            cardsLayout.SuspendLayout();
            int w = cardsLayout.ClientSize.Width - cardsLayout.Padding.Horizontal;
            foreach (Control c in cardsLayout.Controls)
            {
                c.Width = w - c.Margin.Horizontal;
            }
            cardsLayout.ResumeLayout();
        };

        cardsLayout.Controls.Add(card1);
        cardsLayout.Controls.Add(card2);
        cardsLayout.Controls.Add(card3);
        cardsLayout.Controls.Add(card6);

        return cardsLayout;
    }

    private GameUpdater.WinForms.Controls.CardPanel CreateStatCard(string title, string subtitle, string iconText, Color iconColor, Label titleLabelRef, bool hasProgress = false)
    {
        var card = new GameUpdater.WinForms.Controls.CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = this.ScalePadding(0, 0, 0, 8),
            Padding = this.ScalePadding(12),
            CardBackColor = Color.White,
            AutoSize = true
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = hasProgress ? 3 : 2,
            ColumnCount = 2,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (hasProgress) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var iconContainer = new Panel
        {
            Width = 36,
            Height = 36,
            Margin = this.ScalePadding(0, 0, 12, 0),
            BackColor = Color.FromArgb(30, iconColor.R, iconColor.G, iconColor.B)
        };
        iconContainer.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(30, iconColor.R, iconColor.G, iconColor.B));
            e.Graphics.FillEllipse(brush, new Rectangle(0, 0, 36, 36));
            TextRenderer.DrawText(e.Graphics, iconText, new Font("Segoe UI Emoji", 14), new Rectangle(0, 0, 36, 36), iconColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        titleLabelRef.Text = title;
        titleLabelRef.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        titleLabelRef.ForeColor = Color.FromArgb(30, 41, 59); // slate-800
        titleLabelRef.AutoSize = true;
        titleLabelRef.Dock = DockStyle.Left;
        titleLabelRef.Margin = this.ScalePadding(0, 2, 0, 0);

        Label subtitleLabel;
        if (hasProgress)
        {
            // For the last card, _statDiskProgressLabel will be the subtitle
            subtitleLabel = _statDiskProgressLabel;
        }
        else
        {
            subtitleLabel = new Label();
        }

        subtitleLabel.Text = subtitle;
        subtitleLabel.Font = new Font("Segoe UI", 8.5f);
        subtitleLabel.ForeColor = Color.FromArgb(100, 116, 139); // slate-500
        subtitleLabel.AutoSize = true;
        subtitleLabel.Dock = DockStyle.Left;
        subtitleLabel.Margin = this.ScalePadding(0, 4, 0, 0);

        layout.Controls.Add(iconContainer, 0, 0);
        layout.SetRowSpan(iconContainer, 2);
        layout.Controls.Add(titleLabelRef, 1, 0);
        layout.Controls.Add(subtitleLabel, 1, 1);

        if (hasProgress)
        {
            _statDiskProgressBar.Dock = DockStyle.Top;
            _statDiskProgressBar.Height = 4;
            _statDiskProgressBar.Margin = this.ScalePadding(0, 8, 0, 0);
            _statDiskProgressBar.Value = 100;
            layout.Controls.Add(_statDiskProgressBar, 0, 2);
            layout.SetColumnSpan(_statDiskProgressBar, 2);
        }

        card.Controls.Add(layout);
        return card;
    }

    private TabPage BuildResourcesTab()
    {
        var page = new TabPage(I18n.Server.ResourcesTab);

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
            RowCount = 4
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var leftToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = this.ScalePadding(8),
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        
        var refreshBtn = new GameUpdater.WinForms.Controls.ModernButton
        {
            Text = "Làm mới",
            ColorType = GameUpdater.WinForms.Controls.ButtonColorType.PrimaryBlue,
            IconType = GameUpdater.WinForms.Controls.ButtonIconType.Refresh,
            AutoSize = true
        };
        refreshBtn.Click += RefreshResourcesButton_Click;
        leftToolbar.Controls.Add(refreshBtn);

        BuildResourceTree();
        _resourceTree.Dock = DockStyle.Fill;
        _resourceTree.BorderStyle = BorderStyle.None;

        var guideCard = new GameUpdater.WinForms.Controls.CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = this.ScalePadding(12),
            Padding = this.ScalePadding(12),
            CardBackColor = Color.FromArgb(248, 250, 252), // slate-50
            AutoSize = true
        };
        var guideLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        guideLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
        guideLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        guideLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        guideLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var iconLabel = new Label
        {
            Text = "ⓘ",
            Font = new Font("Segoe UI", 12),
            ForeColor = Color.FromArgb(37, 99, 235), // blue-600
            AutoSize = true,
            Margin = this.ScalePadding(0)
        };
        var guideTitleLabel = new Label
        {
            Text = "Hướng dẫn",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235), // blue-600
            AutoSize = true,
            Margin = this.ScalePadding(4, 2, 0, 0)
        };
        var descLabel = new Label
        {
            Text = "Chọn một trò chơi để xem chi tiết hoặc thực hiện các thao tác.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(100, 116, 139), // slate-500
            AutoSize = true,
            Margin = this.ScalePadding(0, 8, 0, 0)
        };
        guideLayout.Controls.Add(iconLabel, 0, 0);
        guideLayout.Controls.Add(guideTitleLabel, 1, 0);
        guideLayout.Controls.Add(descLabel, 0, 1);
        guideLayout.SetColumnSpan(descLabel, 2);
        guideCard.Controls.Add(guideLayout);

        leftPanel.Controls.Add(leftToolbar, 0, 0);
        leftPanel.Controls.Add(_resourceTree, 0, 1);
        leftPanel.Controls.Add(BuildResourceSummaryCards(), 0, 2);
        leftPanel.Controls.Add(guideCard, 0, 3);
        split.Panel1.Controls.Add(leftPanel);

        _sourcesContainer.Dock = DockStyle.Fill;
        _sourcesContainer.AutoSize = true;
        _sourcesContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _sourcesContainer.FlowDirection = FlowDirection.TopDown;
        _sourcesContainer.WrapContents = false;
        _sourcesContainer.Padding = this.ScalePadding(0);
        BuildSourcesUi();



        _targetsContainer.Dock = DockStyle.Fill;
        _targetsContainer.AutoSize = true;
        _targetsContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _targetsContainer.FlowDirection = FlowDirection.TopDown;
        _targetsContainer.WrapContents = false;
        _targetsContainer.Padding = this.ScalePadding(0);
        BuildTargetsUi();



        _resourceSummaryLabel.Dock = DockStyle.Fill;
        _resourceSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _resourceSummaryLabel.Text = I18n.Server.ResourceLoading;

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

        var listWorkspaceLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Padding = this.ScalePadding(6, 8, 6, 6)
        };
        listWorkspaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        listWorkspaceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        listWorkspaceLayout.Controls.Add(contentPanel, 0, 0);

        var configWorkspaceLayout = BuildConfigWorkspaceLayout();

        var listTab = new TabPage(I18n.Server.ResourceListTab);
        listTab.Controls.Add(listWorkspaceLayout);

        var configTab = new TabPage(I18n.Server.ResourceConfigTab);
        configTab.Controls.Add(configWorkspaceLayout);

        _resourceWorkspaceTabControl.Dock = DockStyle.Fill;
        _resourceWorkspaceTabControl.Padding = this.ScalePoint(12, 6);
        _resourceWorkspaceTabControl.TabPages.Clear();
        _resourceWorkspaceTabControl.TabPages.Add(listTab);
        _resourceWorkspaceTabControl.TabPages.Add(configTab);
        _resourceWorkspaceTabControl.SelectedIndex = 0;

        var subLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        subLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        subLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        subLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var subHeaderPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = this.ScalePadding(12, 2, 12, 0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        subHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        subHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        subHeaderPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Removed subHeaderPanel bottom border paint event

        var searchBox = new IconTextBox(DrawSearchIcon)
        {
            Width = 280,
            Margin = this.ScalePadding(0, 4, 0, 4)
        };
        searchBox.Input.PlaceholderText = "Tìm kiếm trò chơi...";
        searchBox.Input.TextChanged += (s, e) =>
        {
             // Trigger search filter
             _resourceSearchQuery = searchBox.Input.Text;
             _filterResourceDebounceTimer.Stop();
             _filterResourceDebounceTimer.Start();
        };

        subHeaderPanel.Controls.Add(searchBox, 1, 0);

        subLayout.Controls.Add(subHeaderPanel, 0, 0);
        subLayout.Controls.Add(_resourceWorkspaceTabControl, 0, 1);

        split.Panel2.Controls.Add(subLayout);

        page.Controls.Add(split);
        return page;
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

    private Control BuildConfigWorkspaceLayout()
    {
        var wrapperLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = this.ScalePadding(0),
            BackColor = Color.FromArgb(248, 250, 252) // slate-50
        };
        wrapperLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        wrapperLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var mainCard = new GameUpdater.WinForms.Controls.CardPanel
        {
            Dock = DockStyle.Fill,
            CardBackColor = Color.White,
            Padding = this.ScalePadding(20, 16, 20, 16),
            Margin = this.ScalePadding(0, 0, 0, 16),
            AutoScroll = true
        };

        var mainFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        // Section 1: Nguồn IDC
        mainFlow.Controls.Add(CreateConfigSectionHeader(DrawGlobeIcon, "NGUỒN IDC", Color.FromArgb(88, 50, 228)));
        
        _sourcesContainer.Dock = DockStyle.Top;
        _sourcesContainer.AutoSize = true;
        _sourcesContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _sourcesContainer.FlowDirection = FlowDirection.TopDown;
        _sourcesContainer.WrapContents = false;
        _sourcesContainer.Padding = this.ScalePadding(0, 4, 0, 8);
        _sourcesContainer.Margin = this.ScalePadding(0);
        _sourcesContainer.SizeChanged += (s, e) => {
            _sourcesContainer.SuspendLayout();
            int w = _sourcesContainer.ClientSize.Width - _sourcesContainer.Padding.Horizontal;
            foreach (Control c in _sourcesContainer.Controls) {
                c.Width = w - c.Margin.Horizontal;
            }
            _sourcesContainer.ResumeLayout();
        };
        mainFlow.Controls.Add(_sourcesContainer);
        BuildSourcesUi(); // This will populate _sourcesContainer
        
        // Removed divider1

        // Section 2: Đích Máy Chủ
        mainFlow.Controls.Add(CreateConfigSectionHeader(DrawMonitorIcon, "ĐÍCH MÁY CHỦ", Color.FromArgb(88, 50, 228)));
        
        _targetsContainer.Dock = DockStyle.Top;
        _targetsContainer.AutoSize = true;
        _targetsContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _targetsContainer.FlowDirection = FlowDirection.TopDown;
        _targetsContainer.WrapContents = false;
        _targetsContainer.Padding = this.ScalePadding(0, 4, 0, 8);
        _targetsContainer.Margin = this.ScalePadding(0);
        _targetsContainer.SizeChanged += (s, e) => {
            _targetsContainer.SuspendLayout();
            int w = _targetsContainer.ClientSize.Width - _targetsContainer.Padding.Horizontal;
            foreach (Control c in _targetsContainer.Controls) {
                c.Width = w - c.Margin.Horizontal;
            }
            _targetsContainer.ResumeLayout();
        };
        mainFlow.Controls.Add(_targetsContainer);
        BuildTargetsUi(); // This will populate _targetsContainer
        
        // Removed divider2

        // Section 3: Giới Hạn
        mainFlow.Controls.Add(CreateConfigSectionHeader(DrawSpeedIcon, "GIỚI HẠN", Color.FromArgb(88, 50, 228)));
        var bandwidthRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            Padding = this.ScalePadding(0, 4, 0, 16),
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = this.ScalePadding(0)
        };
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var limitLabel = new Label { Text = "Giới hạn MB/s", Font = new Font("Segoe UI", 10.5f), ForeColor = Color.FromArgb(30,30,40), AutoSize = true, Anchor = AnchorStyles.Left };
        
        var numWrapper = new Panel { BackColor = Color.White, Width = 140, Height = 42 };
        numWrapper.Paint += (s, e) => {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, GameEditorForm.GetRoundedRectPath(new Rectangle(0,0,numWrapper.Width-1,numWrapper.Height-1), 6));
        };
        
        var numClip = new Panel { BackColor = Color.White };
        
        _resourceBandwidthLimitNumeric.BorderStyle = BorderStyle.None;
        _resourceBandwidthLimitNumeric.Minimum = 0;
        _resourceBandwidthLimitNumeric.Maximum = 10000;
        _resourceBandwidthLimitNumeric.DecimalPlaces = 0;
        _resourceBandwidthLimitNumeric.Value = _resourceBandwidthLimitMbps;
        _resourceBandwidthLimitNumeric.Font = new Font("Segoe UI", 10.5f);
        _resourceBandwidthLimitNumeric.ValueChanged += (_, _) => _resourceBandwidthLimitMbps = Decimal.ToInt32(_resourceBandwidthLimitNumeric.Value);
        
        int borderSize = 1;
        numClip.Height = _resourceBandwidthLimitNumeric.Height - (borderSize * 2);
        numClip.Width = 116;
        numClip.Location = this.ScalePoint(12, (numWrapper.Height - numClip.Height) / 2);
        _resourceBandwidthLimitNumeric.Location = this.ScalePoint(-borderSize, -borderSize);
        _resourceBandwidthLimitNumeric.Width = numClip.Width + (borderSize * 2);
        
        numClip.Controls.Add(_resourceBandwidthLimitNumeric);
        numWrapper.Controls.Add(numClip);

        var hintLabel = new Label { Text = "0 = không giới hạn", Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(130,130,140), AutoSize = true, Anchor = AnchorStyles.Left };
        
        bandwidthRow.Controls.Add(limitLabel, 0, 0);
        bandwidthRow.Controls.Add(numWrapper, 1, 0);
        bandwidthRow.Controls.Add(hintLabel, 2, 0);
        mainFlow.Controls.Add(bandwidthRow);

        // Removed divider

        // Actions Row
        var actionsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = this.ScalePadding(0, 0, 0, 16),
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = this.ScalePadding(0)
        };
        
        _saveResourceSettingsButton.Text = "Lưu cấu hình";
        _saveResourceSettingsButton.Click -= SaveResourceSettingsButton_Click;
        _saveResourceSettingsButton.Click += SaveResourceSettingsButton_Click;
        _saveResourceSettingsButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary;
        _saveResourceSettingsButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Save;
        _saveResourceSettingsButton.Size = this.ScaleSize(180, 42);
        
        _checkResourceHealthButton.Text = "Kiểm tra tài nguyên";
        _checkResourceHealthButton.Click -= CheckResourceHealthButton_Click;
        _checkResourceHealthButton.Click += CheckResourceHealthButton_Click;
        _checkResourceHealthButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary;
        _checkResourceHealthButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Refresh;
        _checkResourceHealthButton.Size = this.ScaleSize(180, 42);
        _checkResourceHealthButton.Margin = this.ScalePadding(16, 0, 0, 0);
        
        _syncSelectedResourceButton.Text = "Tải trò chơi đã chọn";
        _syncSelectedResourceButton.Click -= SyncSelectedResourceButton_Click;
        _syncSelectedResourceButton.Click += SyncSelectedResourceButton_Click;
        _syncSelectedResourceButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Purple;
        _syncSelectedResourceButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Refresh;
        _syncSelectedResourceButton.Size = this.ScaleSize(300, 42);
        _syncSelectedResourceButton.Margin = this.ScalePadding(30, 0, 0, 0);

        actionsRow.Controls.Add(_saveResourceSettingsButton);
        actionsRow.Controls.Add(_checkResourceHealthButton);
        actionsRow.Controls.Add(_syncSelectedResourceButton);
        
        mainFlow.Controls.Add(actionsRow);

        mainCard.Controls.Add(mainFlow);
        
        // Info Bar
        var infoBar = new GameUpdater.WinForms.Controls.CardPanel
        {
            Dock = DockStyle.Top,
            CardBackColor = Color.FromArgb(243, 244, 255), // light purple bg
            Padding = this.ScalePadding(16, 12, 16, 12),
            AutoSize = true,
            Margin = this.ScalePadding(0)
        };
        var infoFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
        
        var infoIcon = new Panel { Width = 20, Height = 20, Margin = this.ScalePadding(0, 0, 10, 0) };
        infoIcon.Paint += (s, e) => {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var p = new Pen(Color.FromArgb(88, 50, 228), 1.5f);
            e.Graphics.DrawEllipse(p, 2, 2, 16, 16);
            e.Graphics.DrawLine(p, 10, 6, 10, 7);
            e.Graphics.DrawLine(p, 10, 9, 10, 15);
        };

        var infoText = new Label { Text = "Cấu hình nguồn/đích và giới hạn băng thông tải tài nguyên.", Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(30,30,40), AutoSize = true, Margin = this.ScalePadding(0, 1, 0, 0) };
        infoFlow.Controls.Add(infoIcon);
        infoFlow.Controls.Add(infoText);
        infoBar.Controls.Add(infoFlow);

        wrapperLayout.Controls.Add(mainCard, 0, 0);
        wrapperLayout.Controls.Add(infoBar, 0, 1);

        return wrapperLayout;
    }

    private Control CreateConfigSectionHeader(Action<Graphics, Rectangle, Color> drawIcon, string title, Color iconColor)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = this.ScalePadding(0, 0, 0, 8)
        };

        var iconContainer = new Panel
        {
            Width = 40,
            Height = 40,
            Margin = this.ScalePadding(0, 0, 16, 0),
            BackColor = Color.FromArgb(240, 237, 252) // Soft purple bg
        };
        iconContainer.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(240, 237, 252));
            using var path = GameEditorForm.GetRoundedRectPath(new Rectangle(0, 0, 39, 39), 8);
            e.Graphics.FillPath(brush, path);
            drawIcon(e.Graphics, new Rectangle(10, 10, 20, 20), iconColor);
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30,30,40),
            AutoSize = true,
            Margin = this.ScalePadding(0, 10, 0, 0)
        };

        panel.Controls.Add(iconContainer);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private class IconButton : Panel
    {
        public Action<Graphics, Rectangle, Color> DrawIcon { get; set; } = null!;
        public Color NormalColor { get; set; } = Color.White;
        public Color HoverColor { get; set; } = Color.FromArgb(248, 250, 252);
        public Color PressedColor { get; set; } = Color.FromArgb(241, 245, 249);
        public Color IconNormalColor { get; set; } = Color.FromArgb(148, 163, 184); // slate-400
        public Color IconHoverColor { get; set; } = Color.FromArgb(71, 85, 105);
        public Color BorderColor { get; set; } = Color.FromArgb(226, 232, 240);

        private bool _isHovered;
        private bool _isPressed;

        public IconButton()
        {
            DoubleBuffered = true;
            Size = this.ScaleSize(36, 36);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; _isPressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _isPressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _isPressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Color bg = _isPressed ? PressedColor : (_isHovered ? HoverColor : NormalColor);
            using var brush = new SolidBrush(bg);
            using var path = GameEditorForm.GetRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 6);
            e.Graphics.FillPath(brush, path);
            
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawPath(pen, path);

            if (DrawIcon != null)
            {
                Color iconColor = _isHovered ? IconHoverColor : IconNormalColor;
                DrawIcon(e.Graphics, new Rectangle(10, 10, Width - 20, Height - 20), iconColor);
            }
        }
    }

    private class IconTextBox : Panel
    {
        private TextBox _textBox;
        private Action<Graphics, Rectangle, Color>? _drawIcon;
        private Panel _clipPanel;

        public IconTextBox(Action<Graphics, Rectangle, Color>? drawIcon = null)
        {
            _drawIcon = drawIcon;
            BackColor = Color.White;
            Height = 42;
            
            _clipPanel = new Panel { BackColor = Color.White };
            
            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5f),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30,30,40)
            };
            
            _clipPanel.Controls.Add(_textBox);
            Controls.Add(_clipPanel);
        }
        
        public TextBox Input => _textBox;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_clipPanel != null && _textBox != null)
            {
                _textBox.BorderStyle = BorderStyle.None;
                
                int borderSize = 2;
                _clipPanel.Height = _textBox.Height - (borderSize * 2); 
                
                if (_drawIcon != null)
                {
                    _clipPanel.Width = Width - 50; 
                    _clipPanel.Location = this.ScalePoint(42, (Height - _clipPanel.Height) / 2); 
                }
                else
                {
                    _clipPanel.Width = Width - 24; 
                    _clipPanel.Location = this.ScalePoint(12, (Height - _clipPanel.Height) / 2); 
                }
                
                _textBox.Width = _clipPanel.Width + (borderSize * 2);
                _textBox.Location = this.ScalePoint(-borderSize, -borderSize);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            using var path = GameEditorForm.GetRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 6);
            e.Graphics.DrawPath(pen, path);
            
            if (_drawIcon != null)
            {
                _drawIcon(e.Graphics, new Rectangle(12, (Height - 16) / 2, 16, 16), Color.FromArgb(148, 163, 184)); // slate-400
            }
        }
    }

    private static void DrawDotsIcon(Graphics g, Rectangle rect, Color color)
    {
        using var brush = new SolidBrush(color);
        int d = 4;
        int y = rect.Y + (rect.Height - d) / 2;
        g.FillEllipse(brush, rect.X + rect.Width / 2 - 8, y, d, d);
        g.FillEllipse(brush, rect.X + rect.Width / 2 - d/2, y, d, d);
        g.FillEllipse(brush, rect.X + rect.Width / 2 + 8 - d, y, d, d);
    }

    private static void DrawTrashIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        g.DrawRectangle(pen, rect.X + 2, rect.Y + 4, rect.Width - 4, rect.Height - 4);
        g.DrawLine(pen, rect.X, rect.Y + 4, rect.Right, rect.Y + 4);
        g.DrawLine(pen, rect.X + rect.Width / 3, rect.Y + 4, rect.X + rect.Width / 3, rect.Y + 1);
        g.DrawLine(pen, rect.Right - rect.Width / 3, rect.Y + 4, rect.Right - rect.Width / 3, rect.Y + 1);
        g.DrawLine(pen, rect.X + rect.Width / 3, rect.Y + 1, rect.Right - rect.Width / 3, rect.Y + 1);
    }

    private static void DrawGlobeIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        g.DrawEllipse(pen, rect);
        g.DrawEllipse(pen, rect.X + rect.Width / 4, rect.Y, rect.Width / 2, rect.Height);
        g.DrawLine(pen, rect.X, rect.Y + rect.Height / 2, rect.Right, rect.Y + rect.Height / 2);
    }

    private static void DrawMonitorIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height - 4);
        g.DrawLine(pen, rect.X + rect.Width / 3, rect.Bottom, rect.Right - rect.Width / 3, rect.Bottom);
        g.DrawLine(pen, rect.X + rect.Width / 2, rect.Bottom - 4, rect.X + rect.Width / 2, rect.Bottom);
    }

    private static void DrawSpeedIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        g.DrawArc(pen, rect.X, rect.Y, rect.Width, rect.Height, 180, 180);
        g.DrawLine(pen, rect.X + rect.Width / 2, rect.Bottom - rect.Height / 2, rect.Right - 4, rect.Y + 4);
        g.FillEllipse(new SolidBrush(color), rect.X + rect.Width / 2 - 2, rect.Bottom - rect.Height / 2 - 2, 4, 4);
    }

    private static void DrawSearchIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, 2f);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.DrawEllipse(pen, rect.X, rect.Y, rect.Width - 6, rect.Height - 6);
        g.DrawLine(pen, rect.X + rect.Width - 6, rect.Y + rect.Height - 6, rect.Right - 1, rect.Bottom - 1);
    }

    private static void DrawFolderIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        using var path = GameEditorForm.GetRoundedRectPath(new Rectangle(rect.X, rect.Y + 4, rect.Width, rect.Height - 4), 2);
        g.DrawPath(pen, path);
        g.DrawLine(pen, rect.X, rect.Y + 8, rect.Right, rect.Y + 8);
        g.DrawLine(pen, rect.X, rect.Y + 4, rect.X + 4, rect.Y);
        g.DrawLine(pen, rect.X + 4, rect.Y, rect.X + rect.Width/2 - 2, rect.Y);
        g.DrawLine(pen, rect.X + rect.Width/2 - 2, rect.Y, rect.X + rect.Width/2 + 2, rect.Y + 4);
    }

}

public class HiddenHeadersTabControl : TabControl
{
    public HiddenHeadersTabControl()
    {
        SizeMode = TabSizeMode.Fixed;
        ItemSize = this.ScaleSize(0, 1);
    }
}

public class ModernTabButton : Control
{
    public bool IsSecondary { get; set; }
    public Color IconTintColor { get; set; } = Color.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            UpdateSize();
            Invalidate();
        }
    }

    private Image? _tabIcon;
    public Image? TabIcon
    {
        get => _tabIcon;
        set
        {
            _tabIcon = value;
            UpdateSize();
            Invalidate();
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        UpdateSize();
        Invalidate();
    }

    public ModernTabButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        TabStop = false;
        Cursor = Cursors.Hand;
        Margin = this.ScalePadding(2, 4, 2, 4); // Margin to give a card floating look
        Padding = this.ScalePadding(0);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateSize();
    }

    private void UpdateSize()
    {
        using var font = new Font(Font.FontFamily, Font.Size, IsSelected ? FontStyle.Bold : FontStyle.Regular);
        int iconSize = IsSecondary ? 16 : 20;
        int spacing = IsSecondary ? 6 : 8;
        
        var measuredText = TextRenderer.MeasureText(
            Text,
            font,
            this.ScaleSize(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        int totalWidth = (TabIcon != null ? iconSize : 0) + (TabIcon != null && !string.IsNullOrEmpty(Text) ? spacing : 0) + measuredText.Width + (IsSecondary ? 28 : 36);
        int totalHeight = Math.Max(IsSecondary ? 40 : 52, measuredText.Height + (IsSecondary ? 12 : 16));
        this.Size = this.ScaleSize(totalWidth, totalHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // Find parent's actual opaque background color
        Control? p = Parent;
        Color parentBgColor = Color.FromArgb(248, 250, 252);
        while (p != null)
        {
            if (p.BackColor != Color.Transparent && p.BackColor != Color.Empty)
            {
                parentBgColor = p.BackColor;
                break;
            }
            p = p.Parent;
        }

        // Draw flat background
        Color bgColor = parentBgColor;
        if (ClientRectangle.Contains(PointToClient(Cursor.Position)))
        {
            bgColor = Color.FromArgb(241, 245, 249); // slate-100 hover
        }

        using (var brush = new SolidBrush(bgColor))
        {
            g.FillRectangle(brush, ClientRectangle);
        }

        // Colors
        Color textColor = IsSelected ? Color.FromArgb(99, 102, 241) : Color.FromArgb(71, 85, 105);
        using var font = new Font(Font.FontFamily, Font.Size, IsSelected ? FontStyle.Bold : FontStyle.Regular);

        // Icon and Text layout
        int iconSize = IsSecondary ? 16 : 20;
        int spacing = IsSecondary ? 6 : 8;
        
        var measuredText = TextRenderer.MeasureText(
            Text,
            font,
            this.ScaleSize(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        int totalWidth = (TabIcon != null ? iconSize : 0) + (TabIcon != null && !string.IsNullOrEmpty(Text) ? spacing : 0) + measuredText.Width;
        int startX = (Width - totalWidth) / 2;

        if (TabIcon != null)
        {
            int iconY = (Height - iconSize) / 2;
            
            // Colorize the icon to the tint color if specified, or active text color if selected
            Color tintColor = IsSelected ? Color.FromArgb(99, 102, 241) : Color.FromArgb(71, 85, 105);
            if (IconTintColor != Color.Empty)
            {
                tintColor = IconTintColor;
            }

            using (var colorized = ColorizeImage(TabIcon, tintColor))
            {
                g.DrawImage(colorized, new Rectangle(startX, iconY, iconSize, iconSize));
            }
            startX += iconSize + spacing;
        }

        int textY = (Height - measuredText.Height) / 2;
        var textRect = new Rectangle(startX, textY, measuredText.Width + 4, measuredText.Height);

        TextRenderer.DrawText(
            g,
            Text,
            font,
            textRect,
            textColor,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        // Bottom accent bar for selected - full-width underline
        if (IsSelected)
        {
            using (var accentBrush = new SolidBrush(Color.FromArgb(99, 102, 241)))
            {
                int barHeight = IsSecondary ? 2 : 3;
                g.FillRectangle(accentBrush, 0, Height - barHeight, Width, barHeight);
            }
        }
    }

    private static Image ColorizeImage(Image original, Color color)
    {
        var bitmap = new Bitmap(original.Width, original.Height);
        using (var g = Graphics.FromImage(bitmap))
        {
            float r = color.R / 255f;
            float gVal = color.G / 255f;
            float b = color.B / 255f;

            var colorMatrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
            {
                new float[] {0, 0, 0, 0, 0},
                new float[] {0, 0, 0, 0, 0},
                new float[] {0, 0, 0, 0, 0},
                new float[] {0, 0, 0, 1, 0}, // Keep transparency
                new float[] {r, gVal, b, 0, 1} // Shift color values
            });

            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                attributes.SetColorMatrix(colorMatrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
                g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height), 0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
            }
        }
        return bitmap;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Invalidate();
    }
}
