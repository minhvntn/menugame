using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using GameUpdater.Core.Abstractions;
using GameUpdater.Core.Services;
using GameUpdater.Shared.Localization;
using GameUpdater.Shared.Models;

namespace GameUpdater.WinForms.Forms;

public sealed partial class MainForm
{
    private GameRecord? SelectedGame => _gamesBinding.Current as GameRecord;

    private void UpdateResourceTargetRootPathFromUi()
    {
        var paths = new List<string>();
        foreach (Control c in _targetsContainer.Controls)
        {
            if (c is TableLayoutPanel tlp && tlp.Controls.Count > 1 && tlp.Controls[1] is TextBox tb)
            {
                paths.Add(tb.Text.Trim());
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
            Padding = new Padding(0, 2, 0, 0),
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));

        int count = _targetsContainer.Controls.OfType<TableLayoutPanel>().Count() + 1;
        var label = CreateFieldLabel($"Đích máy chủ ổ cứng {count}");
        var textBox = new TextBox { Dock = DockStyle.Fill, Text = path };
        
        textBox.TextChanged += (_, _) => UpdateResourceTargetRootPathFromUi();

        var browseBtn = new GameUpdater.WinForms.Controls.ModernButton { Text = "...", Size = new Size(30, 25), Margin = new Padding(0, 0, 5, 0), CornerRadius = 4, ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary };
        browseBtn.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog();
            if (!string.IsNullOrWhiteSpace(textBox.Text))
                dialog.SelectedPath = textBox.Text;
            if (dialog.ShowDialog() == DialogResult.OK)
                textBox.Text = dialog.SelectedPath;
        };

        var removeBtn = new GameUpdater.WinForms.Controls.ModernButton { Text = "X", Size = new Size(30, 25), Margin = new Padding(0), CornerRadius = 4, ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Red };
        removeBtn.Click += (_, _) =>
        {
            _targetsContainer.Controls.Remove(row);
            UpdateResourceTargetRootPathFromUi();
            
            // Update labels
            int i = 1;
            foreach (Control c in _targetsContainer.Controls)
            {
                if (c is TableLayoutPanel tlp && tlp.Controls.Count > 0 && tlp.Controls[0] is Label lbl)
                {
                    lbl.Text = $"Đích máy chủ ổ cứng {i++}";
                }
            }
        };

        row.Controls.Add(label, 0, 0);
        row.Controls.Add(textBox, 1, 0);
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

        var addBtn = new GameUpdater.WinForms.Controls.ModernButton { Text = "Thêm đích máy chủ", Size = new Size(150, 25), Margin = new Padding(180, 5, 0, 0), CornerRadius = 4, ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary };
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
            Padding = new Point(16, 9)
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
            Padding = new Padding(12, 3, 12, 0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        tabHeaderPanel.Paint += (s, e) =>
        {
            using var linePen = new Pen(Color.FromArgb(226, 232, 240), 1f);
            e.Graphics.DrawLine(linePen, 0, tabHeaderPanel.Height - 1, tabHeaderPanel.Width, tabHeaderPanel.Height - 1);
        };

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
                TabIcon = TryLoadEmbeddedTabIcon(info.IconFile, new Size(20, 20)),
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
            Padding = new Padding(8),
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
            Padding = new Padding(12),
            BackColor = Color.Transparent
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 8),
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };

        _clientDashboardSummaryLabel.AutoSize = true;
        _clientDashboardSummaryLabel.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        _clientDashboardSummaryLabel.Text = I18n.Server.ClientDashboardNoData;
        _clientDashboardSummaryLabel.Margin = new Padding(0, 6, 18, 0);
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
        _serverDashboardSummaryLabel.Text = I18n.Server.ServerDashboardLoading;

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
        metricCards.Controls.Add(CreateServerMetricCard(I18n.Server.ServerMetricCpu, _serverDashboardCpuLabel, _serverCpuProgressBar), 0, 0);
        metricCards.Controls.Add(CreateServerMetricCard(I18n.Server.ServerMetricRam, _serverDashboardMemoryLabel, _serverMemoryProgressBar), 1, 0);
        metricCards.Controls.Add(CreateServerMetricCard(I18n.Server.ServerMetricSystemDrive, _serverDashboardDiskLabel, _serverDiskProgressBar), 2, 0);

        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(0, 4, 0, 8),
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

    private TableLayoutPanel BuildResourceSummaryCards()
    {
        var cardsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 6,
            Padding = new Padding(0, 0, 0, 8),
            BackColor = Color.FromArgb(248, 250, 252) // slate-50
        };
        cardsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        for (int i = 0; i < 6; i++)
        {
            cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));
        }

        // 1. Display Count
        var card1 = CreateStatCard("Hiển thị 0/0 trò chơi", "Tổng số game", "🖥️", Color.FromArgb(59, 130, 246), _statDisplayCountLabel);
        // 2. Downloaded
        var card2 = CreateStatCard("Đã tải 0 trò chơi", "Hoàn tất", "✅", Color.FromArgb(16, 185, 129), _statDownloadedLabel);
        // 3. Missing
        var card3 = CreateStatCard("Chưa tải 0 trò chơi", "Cần tải về", "📦", Color.FromArgb(245, 158, 11), _statMissingLabel);
        // 4. Size Needed
        var card4 = CreateStatCard("Cần thêm 0.0 GB", "Dung lượng tải", "💾", Color.FromArgb(139, 92, 246), _statSizeLabel);
        // 5. Source OK
        var card5 = CreateStatCard("Nguồn IDC OK", "Trạng thái", "🌐", Color.FromArgb(99, 102, 241), _statSourceOkLabel);
        // 6. Target OK (with progress bar)
        var card6 = CreateStatCard("Ổ game trống", "100/100 GB (100%)", "💿", Color.FromArgb(236, 72, 153), _statTargetOkLabel, true);

        cardsLayout.Controls.Add(card1, 0, 0);
        cardsLayout.Controls.Add(card2, 1, 0);
        cardsLayout.Controls.Add(card3, 2, 0);
        cardsLayout.Controls.Add(card4, 3, 0);
        cardsLayout.Controls.Add(card5, 4, 0);
        cardsLayout.Controls.Add(card6, 5, 0);

        return cardsLayout;
    }

    private GameUpdater.WinForms.Controls.CardPanel CreateStatCard(string title, string subtitle, string iconText, Color iconColor, Label titleLabelRef, bool hasProgress = false)
    {
        var card = new GameUpdater.WinForms.Controls.CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(12),
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
            Margin = new Padding(0, 0, 12, 0),
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
        titleLabelRef.Margin = new Padding(0, 2, 0, 0);

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
        subtitleLabel.Margin = new Padding(0, 4, 0, 0);

        layout.Controls.Add(iconContainer, 0, 0);
        layout.SetRowSpan(iconContainer, 2);
        layout.Controls.Add(titleLabelRef, 1, 0);
        layout.Controls.Add(subtitleLabel, 1, 1);

        if (hasProgress)
        {
            _statDiskProgressBar.Dock = DockStyle.Top;
            _statDiskProgressBar.Height = 4;
            _statDiskProgressBar.Margin = new Padding(0, 8, 0, 0);
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
            RowCount = 3
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var leftToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
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
            Margin = new Padding(12),
            Padding = new Padding(12),
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
            Margin = new Padding(0)
        };
        var guideTitleLabel = new Label
        {
            Text = "Hướng dẫn",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235), // blue-600
            AutoSize = true,
            Margin = new Padding(4, 2, 0, 0)
        };
        var descLabel = new Label
        {
            Text = "Chọn một trò chơi để xem chi tiết hoặc thực hiện các thao tác.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(100, 116, 139), // slate-500
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };
        guideLayout.Controls.Add(iconLabel, 0, 0);
        guideLayout.Controls.Add(guideTitleLabel, 1, 0);
        guideLayout.Controls.Add(descLabel, 0, 1);
        guideLayout.SetColumnSpan(descLabel, 2);
        guideCard.Controls.Add(guideLayout);

        leftPanel.Controls.Add(leftToolbar, 0, 0);
        leftPanel.Controls.Add(_resourceTree, 0, 1);
        leftPanel.Controls.Add(guideCard, 0, 2);
        split.Panel1.Controls.Add(leftPanel);

        _resourceSourceRootTextBox.Dock = DockStyle.Fill;
        _resourceSourceRootTextBox.Text = _resourceSourceRootPath;
        _resourceSourceRootTextBox.TextChanged += (_, _) => _resourceSourceRootPath = _resourceSourceRootTextBox.Text.Trim();

        _resourceBandwidthLimitNumeric.Dock = DockStyle.Left;
        _resourceBandwidthLimitNumeric.Width = 120;
        _resourceBandwidthLimitNumeric.Minimum = 0;
        _resourceBandwidthLimitNumeric.Maximum = 10000;
        _resourceBandwidthLimitNumeric.DecimalPlaces = 0;
        _resourceBandwidthLimitNumeric.Value = _resourceBandwidthLimitMbps;
        _resourceBandwidthLimitNumeric.ValueChanged += (_, _) => _resourceBandwidthLimitMbps = Decimal.ToInt32(_resourceBandwidthLimitNumeric.Value);

        _browseResourceSourceButton.Text = "...";
        _browseResourceSourceButton.Click += BrowseResourceSourceButton_Click;
        StyleButton(_browseResourceSourceButton);


        var sourceRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 4, 0, 0),
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        sourceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        sourceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sourceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        sourceRow.Controls.Add(CreateFieldLabel(I18n.Server.ResourceSourceLabel), 0, 0);
        sourceRow.Controls.Add(_resourceSourceRootTextBox, 1, 0);
        sourceRow.Controls.Add(_browseResourceSourceButton, 2, 0);

        _targetsContainer.Dock = DockStyle.Fill;
        _targetsContainer.AutoSize = true;
        _targetsContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _targetsContainer.FlowDirection = FlowDirection.TopDown;
        _targetsContainer.WrapContents = false;
        _targetsContainer.Padding = new Padding(0);
        BuildTargetsUi();

        var bandwidthRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 2, 0, 0),
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bandwidthRow.Controls.Add(CreateFieldLabel(I18n.Server.ResourceBandwidthLabel), 0, 0);
        bandwidthRow.Controls.Add(_resourceBandwidthLimitNumeric, 1, 0);
        bandwidthRow.Controls.Add(CreateFieldLabel(I18n.Server.ResourceBandwidthHint), 2, 0);

        var actionsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 2, 0, 0),
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };
        _saveResourceSettingsButton.Text = I18n.Server.ResourceSaveConfig;
        _saveResourceSettingsButton.Click += SaveResourceSettingsButton_Click;
        _saveResourceSettingsButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary;
        _saveResourceSettingsButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Save;
        StyleButton(_saveResourceSettingsButton);

        _checkResourceHealthButton.Text = I18n.Server.ResourceHealthCheck;
        _checkResourceHealthButton.Click += CheckResourceHealthButton_Click;
        _checkResourceHealthButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary;
        _checkResourceHealthButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Refresh;
        StyleButton(_checkResourceHealthButton);

        _syncSelectedResourceButton.Text = I18n.Server.ResourceSyncSelected;
        _syncSelectedResourceButton.Click += SyncSelectedResourceButton_Click;
        _syncSelectedResourceButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.PrimaryBlue;
        _syncSelectedResourceButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Refresh;
        StyleButton(_syncSelectedResourceButton, primary: true);


        actionsRow.Controls.Add(_saveResourceSettingsButton);
        actionsRow.Controls.Add(_checkResourceHealthButton);
        actionsRow.Controls.Add(_syncSelectedResourceButton);

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
            RowCount = 2,
            Padding = new Padding(6, 8, 6, 6)
        };
        listWorkspaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        listWorkspaceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100f));
        listWorkspaceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        listWorkspaceLayout.Controls.Add(BuildResourceSummaryCards(), 0, 0);
        listWorkspaceLayout.Controls.Add(contentPanel, 0, 1);

        var configWorkspaceLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            Padding = new Padding(8)
        };
        configWorkspaceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        configWorkspaceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        configWorkspaceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        configWorkspaceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        configWorkspaceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        configWorkspaceLayout.Controls.Add(sourceRow, 0, 0);
        configWorkspaceLayout.Controls.Add(_targetsContainer, 0, 1);
        configWorkspaceLayout.Controls.Add(bandwidthRow, 0, 2);
        configWorkspaceLayout.Controls.Add(actionsRow, 0, 3);
        configWorkspaceLayout.Controls.Add(
            new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Text = I18n.Server.ResourceConfigHint
            },
            0,
            4);

        var listTab = new TabPage(I18n.Server.ResourceListTab);
        listTab.Controls.Add(listWorkspaceLayout);

        var configTab = new TabPage(I18n.Server.ResourceConfigTab);
        configTab.Controls.Add(configWorkspaceLayout);

        _resourceWorkspaceTabControl.Dock = DockStyle.Fill;
        _resourceWorkspaceTabControl.Padding = new Point(12, 6);
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
            Padding = new Padding(12, 2, 12, 0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        subHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        subHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        subHeaderPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        subHeaderPanel.Paint += (s, e) =>
        {
            using var linePen = new Pen(Color.FromArgb(226, 232, 240), 1f);
            e.Graphics.DrawLine(linePen, 0, subHeaderPanel.Height - 1, subHeaderPanel.Width, subHeaderPanel.Height - 1);
        };

        var tabsFlowPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0)
        };

        var subTabButtons = new List<ModernTabButton>();
        var subTabInfo = new[]
        {
            (Text: I18n.Server.ResourceListTab, IconFile: "tro-choi.png", Tint: Color.FromArgb(99, 102, 241)),
            (Text: I18n.Server.ResourceConfigTab, IconFile: "setting.png", Tint: Color.FromArgb(99, 102, 241))
        };

        for (int i = 0; i < subTabInfo.Length; i++)
        {
            var info = subTabInfo[i];
            var btnIndex = i;
            var btn = new ModernTabButton
            {
                Text = info.Text,
                TabIcon = TryLoadEmbeddedTabIcon(info.IconFile, new Size(16, 16)),
                IconTintColor = info.Tint,
                IsSecondary = true,
                IsSelected = (i == 0)
            };

            btn.Click += (s, e) =>
            {
                _resourceWorkspaceTabControl.SelectedIndex = btnIndex;
            };

            subTabButtons.Add(btn);
            tabsFlowPanel.Controls.Add(btn);
        }
        
        var searchBox = new TextBox
        {
            PlaceholderText = "Tìm kiếm trò chơi...",
            Width = 200,
            Margin = new Padding(0, 10, 0, 0),
            Font = new Font("Segoe UI", 9.5f)
        };
        searchBox.TextChanged += (s, e) =>
        {
             // Trigger search filter
             _resourceSearchQuery = searchBox.Text;
             _filterResourceDebounceTimer.Stop();
             _filterResourceDebounceTimer.Start();
        };

        subHeaderPanel.Controls.Add(tabsFlowPanel, 0, 0);
        subHeaderPanel.Controls.Add(searchBox, 1, 0);

        _resourceWorkspaceTabControl.SelectedIndexChanged += (s, e) =>
        {
            for (int i = 0; i < subTabButtons.Count; i++)
            {
                subTabButtons[i].IsSelected = (i == _resourceWorkspaceTabControl.SelectedIndex);
                subTabButtons[i].Invalidate();
            }
        };

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

}

public class HiddenHeadersTabControl : TabControl
{
    public HiddenHeadersTabControl()
    {
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(0, 1);
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
        Margin = new Padding(2, 4, 2, 4); // Margin to give a card floating look
        Padding = new Padding(0);
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
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        int totalWidth = (TabIcon != null ? iconSize : 0) + (TabIcon != null && !string.IsNullOrEmpty(Text) ? spacing : 0) + measuredText.Width + (IsSecondary ? 28 : 36);
        int totalHeight = Math.Max(IsSecondary ? 40 : 52, measuredText.Height + (IsSecondary ? 12 : 16));
        this.Size = new Size(totalWidth, totalHeight);
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
            new Size(int.MaxValue, int.MaxValue),
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
