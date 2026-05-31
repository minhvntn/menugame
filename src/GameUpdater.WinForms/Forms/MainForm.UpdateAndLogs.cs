using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Drawing.Drawing2D;
using GameUpdater.Core.Abstractions;
using GameUpdater.Core.Services;
using GameUpdater.Shared.Localization;
using GameUpdater.Shared.Models;
using GameUpdater.WinForms.Controls;

namespace GameUpdater.WinForms.Forms;

public sealed partial class MainForm
{    private TabPage BuildUpdateTab()
    {
        var page = new TabPage(I18n.Server.UpdateTab);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 8,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        for (var rowIndex = 0; rowIndex < 7; rowIndex++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
            new() { Kind = UpdateSourceKind.Folder, Name = I18n.Server.UpdateSourceFolder },
            new() { Kind = UpdateSourceKind.Zip, Name = I18n.Server.UpdateSourceZip }
        };
        _updateSourceKindComboBox.DataSource = sourceOptions;
        _updateSourceKindComboBox.DisplayMember = nameof(UpdateSourceOption.Name);
        _updateSourceKindComboBox.ValueMember = nameof(UpdateSourceOption.Kind);

        _updateSourceTextBox.Dock = DockStyle.Fill;

        _browseSourceButton.Text = I18n.Common.SelectButton;
        _browseSourceButton.Dock = DockStyle.Fill;
        _browseSourceButton.Click += BrowseSourceButton_Click;
        _browseSourceButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary;
        _browseSourceButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Folder;
        StyleButton(_browseSourceButton);

        _updateVersionTextBox.Dock = DockStyle.Fill;

        _backupCheckBox.Text = I18n.Server.BackupBeforeUpdate;
        _backupCheckBox.Checked = true;
        _backupCheckBox.Dock = DockStyle.Fill;

        _updateProgressBar.Dock = DockStyle.Fill;

        _applyUpdateButton.Text = I18n.Server.StartUpdateButton;
        _applyUpdateButton.Dock = DockStyle.Fill;
        _applyUpdateButton.Click += ApplyUpdateButton_Click;
        _applyUpdateButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.PrimaryBlue;
        _applyUpdateButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Refresh;
        StyleButton(_applyUpdateButton, primary: true);

        _updateOutputTextBox.Dock = DockStyle.Fill;
        _updateOutputTextBox.Multiline = true;
        _updateOutputTextBox.ScrollBars = ScrollBars.Vertical;
        _updateOutputTextBox.Font = new Font("Consolas", 10);
        _updateOutputTextBox.ReadOnly = true;

        root.Controls.Add(CreateFieldLabel(I18n.Server.FieldGame), 0, 0);
        root.Controls.Add(_updateGameComboBox, 1, 0);
        root.SetColumnSpan(_updateGameComboBox, 2);

        root.Controls.Add(CreateFieldLabel(I18n.Server.FieldSourceType), 0, 1);
        root.Controls.Add(_updateSourceKindComboBox, 1, 1);
        root.SetColumnSpan(_updateSourceKindComboBox, 2);

        root.Controls.Add(CreateFieldLabel(I18n.Server.FieldUpdateSource), 0, 2);
        root.Controls.Add(_updateSourceTextBox, 1, 2);
        root.Controls.Add(_browseSourceButton, 2, 2);

        root.Controls.Add(CreateFieldLabel(I18n.Server.FieldVersion), 0, 3);
        root.Controls.Add(_updateVersionTextBox, 1, 3);
        root.SetColumnSpan(_updateVersionTextBox, 2);

        root.Controls.Add(CreateFieldLabel(I18n.Server.FieldOptions), 0, 4);
        root.Controls.Add(_backupCheckBox, 1, 4);
        root.SetColumnSpan(_backupCheckBox, 2);

        root.Controls.Add(CreateFieldLabel(I18n.Server.FieldProgress), 0, 5);
        root.Controls.Add(_updateProgressBar, 1, 5);
        root.SetColumnSpan(_updateProgressBar, 2);

        root.Controls.Add(CreateFieldLabel(I18n.Server.FieldActions), 0, 6);
        root.Controls.Add(_applyUpdateButton, 1, 6);
        root.SetColumnSpan(_applyUpdateButton, 2);

        root.Controls.Add(CreateFieldLabel(I18n.Server.FieldLogs), 0, 7);
        root.Controls.Add(_updateOutputTextBox, 1, 7);
        root.SetColumnSpan(_updateOutputTextBox, 2);

        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildLogsTab()
    {
        var page = new TabPage(I18n.Server.LogsTab);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        
        var refreshBtn = new GameUpdater.WinForms.Controls.ModernButton
        {
            Text = I18n.Server.LogsRefresh,
            ColorType = GameUpdater.WinForms.Controls.ButtonColorType.PrimaryBlue,
            IconType = GameUpdater.WinForms.Controls.ButtonIconType.Refresh,
            AutoSize = true
        };
        refreshBtn.Click += RefreshLogsButton_Click;

        var exportBtn = new GameUpdater.WinForms.Controls.ModernButton
        {
            Text = I18n.Common.CsvButton,
            ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Green,
            IconType = GameUpdater.WinForms.Controls.ButtonIconType.Export,
            AutoSize = true
        };
        exportBtn.Click += ExportLogsCsvButton_Click;

        var deleteBtn = new GameUpdater.WinForms.Controls.ModernButton
        {
            Text = I18n.Server.LogsDelete,
            ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Red,
            IconType = GameUpdater.WinForms.Controls.ButtonIconType.Delete,
            AutoSize = true
        };
        deleteBtn.Click += ClearLogsButton_Click;

        toolbar.Controls.Add(refreshBtn);
        toolbar.Controls.Add(exportBtn);
        toolbar.Controls.Add(deleteBtn);

        ConfigureLogsGrid();
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(_logsGrid, 0, 1);

        page.Controls.Add(root);
        return page;
    }

    private async void ClearLogsButton_Click(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(this, I18n.Server.LogsDeleteConfirm, I18n.Common.ConfirmTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            await _logRepository.ClearAllAsync();
            await LoadLogsAsync();
            MessageBox.Show(this, I18n.Server.LogsDeleteSuccess, I18n.Common.InfoTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private TabPage BuildSettingsTab()
    {
        var page = new TabPage(I18n.Server.SettingsTab);
        page.BackColor = Color.FromArgb(248, 250, 252); // slate-50

        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        page.Controls.Add(scrollPanel);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(24, 24, 24, 24),
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // White rounded card panel
        var cardPanel = new CardPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(24, 24, 24, 24),
            Margin = new Padding(0, 0, 0, 16),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        cardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header
        cardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Separator
        cardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Settings table
        cardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Checkboxes
        cardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Info bar

        // 1. Header Panel
        var headerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            Height = 44,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var iconPanel = new Panel
        {
            Width = 38,
            Height = 38,
            BackColor = Color.FromArgb(238, 242, 255), // indigo-50
            Margin = new Padding(0, 2, 10, 2)
        };
        iconPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, iconPanel.Width - 1, iconPanel.Height - 1);
            using (var path = GetRoundedRectPath(rect, 8))
            using (var brush = new SolidBrush(Color.FromArgb(238, 242, 255)))
            {
                g.FillPath(brush, path);
            }
            using (var pen = new Pen(Color.FromArgb(99, 102, 241), 2.2f)) // indigo-500
            {
                int cx = iconPanel.Width / 2;
                int cy = iconPanel.Height / 2;
                g.DrawEllipse(pen, cx - 5, cy - 5, 10, 10);
                for (int angle = 0; angle < 360; angle += 45)
                {
                    double rad = angle * Math.PI / 180.0;
                    float x1 = (float)(cx + 5 * Math.Cos(rad));
                    float y1 = (float)(cy + 5 * Math.Sin(rad));
                    float x2 = (float)(cx + 8 * Math.Cos(rad));
                    float y2 = (float)(cy + 8 * Math.Sin(rad));
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }
        };

        var titleContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        var titleLabel = new Label
        {
            Text = "Thiết lập quản lý",
            Font = new Font(page.Font.FontFamily, 13f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42), // slate-900
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };
        var descLabel = new Label
        {
            Text = "Cấu hình các thông số cơ bản của phần mềm",
            Font = new Font(page.Font.FontFamily, 9.2f, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139), // slate-500
            AutoSize = true,
            Margin = Padding.Empty
        };
        titleContainer.Controls.Add(titleLabel);
        titleContainer.Controls.Add(descLabel);

        headerPanel.Controls.Add(iconPanel, 0, 0);
        headerPanel.Controls.Add(titleContainer, 1, 0);

        // 2. Separator
        var separator = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = Color.FromArgb(226, 232, 240), // slate-200
            Margin = new Padding(0, 14, 0, 18)
        };

        // 3. Settings table
        var settingsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 9,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 9; i++)
        {
            settingsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        // 0. FontSize
        settingsPanel.Controls.Add(CreateFieldLabel("Cỡ chữ giao diện"), 0, 0);
        
        _fontSizeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _fontSizeComboBox.DisplayMember = nameof(FontSizeOption.Name);
        _fontSizeComboBox.ValueMember = nameof(FontSizeOption.Mode);
        _fontSizeComboBox.DataSource = new List<FontSizeOption>
        {
            new() { Mode = UiFontSizeMode.VerySmall, Name = "Rất nhỏ" },
            new() { Mode = UiFontSizeMode.Small, Name = "Nhỏ" },
            new() { Mode = UiFontSizeMode.Normal, Name = "Bình thường" },
            new() { Mode = UiFontSizeMode.Big, Name = "Lớn" },
            new() { Mode = UiFontSizeMode.VeryBig, Name = "Rất lớn" }
        };
        SetFontSizeSelection(_uiFontSizeMode);
        _fontSizeComboBox.SelectedIndexChanged -= FontSizeComboBox_SelectedIndexChanged;
        _fontSizeComboBox.SelectedIndexChanged += FontSizeComboBox_SelectedIndexChanged;
        
        _fontSizeComboBox.Dock = DockStyle.Fill;
        _fontSizeComboBox.Margin = new Padding(0, 10, 0, 10);
        settingsPanel.Controls.Add(_fontSizeComboBox, 1, 0);

        // 1. Cafe name
        settingsPanel.Controls.Add(CreateFieldLabel(I18n.Server.SettingCafeName), 0, 1);
        _clientCafeNameTextBox.Dock = DockStyle.Fill;
        _clientCafeNameTextBox.Text = _clientCafeDisplayName;
        _clientCafeNameTextBox.TextChanged -= ClientCafeNameTextBox_TextChanged;
        _clientCafeNameTextBox.TextChanged += ClientCafeNameTextBox_TextChanged;
        _clientCafeNameTextBox.Margin = new Padding(0, 10, 0, 10);
        settingsPanel.Controls.Add(_clientCafeNameTextBox, 1, 1);

        // 2. Banner msg
        settingsPanel.Controls.Add(CreateFieldLabel(I18n.Server.SettingBanner), 0, 2);
        _clientBannerMessageTextBox.Dock = DockStyle.Fill;
        _clientBannerMessageTextBox.Text = _clientBannerMessage;
        _clientBannerMessageTextBox.TextChanged -= ClientBannerMessageTextBox_TextChanged;
        _clientBannerMessageTextBox.TextChanged += ClientBannerMessageTextBox_TextChanged;
        _clientBannerMessageTextBox.Margin = new Padding(0, 10, 0, 10);
        settingsPanel.Controls.Add(_clientBannerMessageTextBox, 1, 2);

        // 3. Theme color with preview
        settingsPanel.Controls.Add(CreateFieldLabel(I18n.Server.SettingThemeColor), 0, 3);

        var colorInputLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 10, 0, 10),
            Height = 32,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        colorInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
        colorInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        colorInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        colorInputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var colorPreview = new Panel
        {
            Width = 24,
            Height = 24,
            BackColor = Color.FromArgb(52, 52, 52),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 4, 8, 4)
        };

        var colorPickerBtn = new GameUpdater.WinForms.Controls.ModernButton
        {
            Text = "",
            ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary,
            IconType = GameUpdater.WinForms.Controls.ButtonIconType.Edit,
            Width = 32,
            Height = 32,
            Margin = new Padding(8, 0, 0, 0)
        };

        _clientThemeAccentColorTextBox.Dock = DockStyle.Fill;
        _clientThemeAccentColorTextBox.Text = _clientThemeAccentColor;
        _clientThemeAccentColorTextBox.TextChanged -= ClientThemeAccentColorTextBox_TextChanged;
        _clientThemeAccentColorTextBox.TextChanged += ClientThemeAccentColorTextBox_TextChanged;
        _clientThemeAccentColorTextBox.TextChanged += (s, e) =>
        {
            try
            {
                var hex = _clientThemeAccentColorTextBox.Text.Trim();
                if (hex.StartsWith("#") && (hex.Length == 7 || hex.Length == 4))
                {
                    colorPreview.BackColor = ColorTranslator.FromHtml(hex);
                }
            }
            catch { }
        };

        colorPickerBtn.Click += (s, e) =>
        {
            using var colorDiag = new ColorDialog
            {
                Color = colorPreview.BackColor,
                FullOpen = true
            };
            if (colorDiag.ShowDialog(this) == DialogResult.OK)
            {
                _clientThemeAccentColorTextBox.Text = $"#{colorDiag.Color.R:X2}{colorDiag.Color.G:X2}{colorDiag.Color.B:X2}";
            }
        };

        try
        {
            var initialHex = _clientThemeAccentColorTextBox.Text.Trim();
            if (initialHex.StartsWith("#") && (initialHex.Length == 7 || initialHex.Length == 4))
            {
                colorPreview.BackColor = ColorTranslator.FromHtml(initialHex);
            }
        }
        catch { }

        colorInputLayout.Controls.Add(colorPreview, 0, 0);
        colorInputLayout.Controls.Add(_clientThemeAccentColorTextBox, 1, 0);
        colorInputLayout.Controls.Add(colorPickerBtn, 2, 0);
        settingsPanel.Controls.Add(colorInputLayout, 1, 3);

        // 4. Font Row
        settingsPanel.Controls.Add(CreateFieldLabel(I18n.Server.SettingThemeFont), 0, 4);
        _clientThemeFontComboBox.Dock = DockStyle.Fill;
        _clientThemeFontComboBox.Margin = new Padding(0, 10, 0, 10);
        _clientThemeFontComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _clientThemeFontComboBox.Items.Clear();
        _clientThemeFontComboBox.Items.AddRange(new object[] { "Segoe UI", "Cambria", "sans-serif", "Tahoma", "Roboto", "Helvetica", "Arial", "Calibri", "Open Sans", "Quicksand", "Peace Sans" });
        if (_clientThemeFontComboBox.Items.Contains(_clientThemeFontFamily))
        {
            _clientThemeFontComboBox.SelectedItem = _clientThemeFontFamily;
        }
        else
        {
            _clientThemeFontComboBox.SelectedItem = I18n.Server.DefaultThemeFontFamily;
        }
        _clientThemeFontComboBox.SelectedIndexChanged -= ClientThemeFontComboBox_SelectedIndexChanged;
        _clientThemeFontComboBox.SelectedIndexChanged += ClientThemeFontComboBox_SelectedIndexChanged;
        settingsPanel.Controls.Add(_clientThemeFontComboBox, 1, 4);

        // 5. Wallpaper
        settingsPanel.Controls.Add(CreateFieldLabel(I18n.Server.SettingWallpaper), 0, 5);

        _browseClientWallpaperButton.Text = I18n.Common.SelectButton;
        _browseClientWallpaperButton.Click -= BrowseClientWallpaperButton_Click;
        _browseClientWallpaperButton.Click += BrowseClientWallpaperButton_Click;
        _browseClientWallpaperButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary;
        _browseClientWallpaperButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Folder;
        StyleButton(_browseClientWallpaperButton);

        _clearClientWallpaperButton.Text = I18n.Common.DeleteButton;
        _clearClientWallpaperButton.Click -= ClearClientWallpaperButton_Click;
        _clearClientWallpaperButton.Click += ClearClientWallpaperButton_Click;
        _clearClientWallpaperButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Red;
        _clearClientWallpaperButton.IsPrimary = true;
        StyleButton(_clearClientWallpaperButton, primary: true);

        _clientWallpaperPathTextBox.Dock = DockStyle.Fill;
        _clientWallpaperPathTextBox.Text = _clientWindowsWallpaperPath;
        _clientWallpaperPathTextBox.TextChanged -= ClientWallpaperPathTextBox_TextChanged;
        _clientWallpaperPathTextBox.TextChanged += ClientWallpaperPathTextBox_TextChanged;

        var wallpaperInputLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 10, 0, 10),
            Height = 32,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        wallpaperInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        wallpaperInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        wallpaperInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        wallpaperInputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _browseClientWallpaperButton.Margin = new Padding(8, 0, 0, 0);
        _clearClientWallpaperButton.Margin = new Padding(8, 0, 0, 0);

        wallpaperInputLayout.Controls.Add(_clientWallpaperPathTextBox, 0, 0);
        wallpaperInputLayout.Controls.Add(_browseClientWallpaperButton, 1, 0);
        wallpaperInputLayout.Controls.Add(_clearClientWallpaperButton, 2, 0);
        settingsPanel.Controls.Add(wallpaperInputLayout, 1, 5);

        // 6. Status folder
        settingsPanel.Controls.Add(CreateFieldLabel(I18n.Server.SettingStatusFolder), 0, 6);
        _clientStatusFolderTextBox.Dock = DockStyle.Fill;
        _clientStatusFolderTextBox.Text = _clientStatusFolderPath;
        _clientStatusFolderTextBox.TextChanged -= ClientStatusFolderTextBox_TextChanged;
        _clientStatusFolderTextBox.TextChanged += ClientStatusFolderTextBox_TextChanged;
        _clientStatusFolderTextBox.Margin = new Padding(0, 10, 0, 10);
        settingsPanel.Controls.Add(_clientStatusFolderTextBox, 1, 6);

        // 7. Heartbeat
        settingsPanel.Controls.Add(CreateFieldLabel("Chu kỳ máy trạm gửi tín hiệu duy trì kết nối báo trạng thái (giây)"), 0, 7);
        _clientHeartbeatIntervalNumeric.Dock = DockStyle.Fill;
        _clientHeartbeatIntervalNumeric.Minimum = 5;
        _clientHeartbeatIntervalNumeric.Maximum = 300;
        _clientHeartbeatIntervalNumeric.DecimalPlaces = 0;
        _clientHeartbeatIntervalNumeric.Value = _clientHeartbeatIntervalSeconds;
        _clientHeartbeatIntervalNumeric.ValueChanged -= HeartbeatIntervalNumeric_ValueChanged;
        _clientHeartbeatIntervalNumeric.ValueChanged += HeartbeatIntervalNumeric_ValueChanged;
        _clientHeartbeatIntervalNumeric.Margin = new Padding(0, 10, 0, 10);
        settingsPanel.Controls.Add(_clientHeartbeatIntervalNumeric, 1, 7);

        // 8. Dashboard refresh
        settingsPanel.Controls.Add(CreateFieldLabel("Chu kỳ tự động tải lại dữ liệu trên trang giám sát quản trị (giây)"), 0, 8);
        _dashboardRefreshIntervalNumeric.Dock = DockStyle.Fill;
        _dashboardRefreshIntervalNumeric.Minimum = 3;
        _dashboardRefreshIntervalNumeric.Maximum = 300;
        _dashboardRefreshIntervalNumeric.DecimalPlaces = 0;
        _dashboardRefreshIntervalNumeric.Value = _dashboardRefreshIntervalSeconds;
        _dashboardRefreshIntervalNumeric.ValueChanged -= DashboardRefreshIntervalNumeric_ValueChanged;
        _dashboardRefreshIntervalNumeric.ValueChanged += DashboardRefreshIntervalNumeric_ValueChanged;
        _dashboardRefreshIntervalNumeric.Margin = new Padding(0, 10, 0, 10);
        settingsPanel.Controls.Add(_dashboardRefreshIntervalNumeric, 1, 8);

        // 4. Checkboxes Panel
        var checkboxesPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 16, 0, 16),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        checkboxesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        checkboxesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // cb1
        checkboxesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // cb1Desc
        checkboxesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // cb2
        checkboxesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // cb2Desc

        _enableClientCloseAppHotKeyCheckBox.AutoSize = true;
        _enableClientCloseAppHotKeyCheckBox.Text = I18n.Server.SettingAllowCloseHotkey;
        _enableClientCloseAppHotKeyCheckBox.Checked = _enableClientCloseApplicationHotKey;
        _enableClientCloseAppHotKeyCheckBox.CheckedChanged -= CloseAppHotKeyCheckBox_CheckedChanged;
        _enableClientCloseAppHotKeyCheckBox.CheckedChanged += CloseAppHotKeyCheckBox_CheckedChanged;
        _enableClientCloseAppHotKeyCheckBox.Margin = new Padding(0, 0, 0, 4);
        
        var cb1Desc = new Label
        {
            Text = "Bật tùy chọn này nếu bạn muốn cho phép máy trạm đóng ứng dụng quản lý.",
            ForeColor = Color.FromArgb(100, 116, 139), // slate-500
            Font = new Font(page.Font.FontFamily, 9.2f, FontStyle.Regular),
            AutoSize = true,
            Margin = new Padding(26, 0, 0, 12)
        };

        _enableClientFullscreenKioskCheckBox.AutoSize = true;
        _enableClientFullscreenKioskCheckBox.Text = I18n.Server.SettingEnableKiosk;
        _enableClientFullscreenKioskCheckBox.Checked = _enableClientFullscreenKioskMode;
        _enableClientFullscreenKioskCheckBox.CheckedChanged -= FullscreenKioskCheckBox_CheckedChanged;
        _enableClientFullscreenKioskCheckBox.CheckedChanged += FullscreenKioskCheckBox_CheckedChanged;
        _enableClientFullscreenKioskCheckBox.Margin = new Padding(0, 0, 0, 4);
        _enableClientFullscreenKioskCheckBox.Visible = false; // Hidden per user request
        
        var cb2Desc = new Label
        {
            Text = "Khi bật, client sẽ chạy toàn màn hình và hạn chế thao tác của người dùng.",
            ForeColor = Color.FromArgb(100, 116, 139), // slate-500
            Font = new Font(page.Font.FontFamily, 9.2f, FontStyle.Regular),
            AutoSize = true,
            Margin = new Padding(26, 0, 0, 0),
            Visible = false // Hidden per user request
        };

        checkboxesPanel.Controls.Add(_enableClientCloseAppHotKeyCheckBox, 0, 0);
        checkboxesPanel.Controls.Add(cb1Desc, 0, 1);
        checkboxesPanel.Controls.Add(_enableClientFullscreenKioskCheckBox, 0, 2);
        checkboxesPanel.Controls.Add(cb2Desc, 0, 3);

        // 5. Info Bar
        var infoBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(12),
            Margin = new Padding(0, 16, 0, 0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        infoBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        infoBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var infoIcon = new Label
        {
            Text = "ℹ",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(59, 130, 246), // blue-500
            AutoSize = true,
            Margin = new Padding(0, 0, 8, 0),
            TextAlign = ContentAlignment.TopLeft
        };

        var infoText = new Label
        {
            Text = I18n.Server.SettingHint,
            Font = new Font(page.Font.FontFamily, 9.2f, FontStyle.Regular),
            ForeColor = Color.FromArgb(30, 64, 175), // blue-800
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        infoBar.Controls.Add(infoIcon, 0, 0);
        infoBar.Controls.Add(infoText, 1, 0);

        infoBar.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, infoBar.Width - 1, infoBar.Height - 1);
            using (var fillBrush = new SolidBrush(Color.FromArgb(239, 246, 255))) // blue-50
            using (var borderPen = new Pen(Color.FromArgb(219, 234, 254), 1.2f)) // blue-100
            using (var path = GetRoundedRectPath(rect, 8))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
            }
        };

        cardLayout.Controls.Add(headerPanel, 0, 0);
        cardLayout.Controls.Add(separator, 0, 1);
        cardLayout.Controls.Add(settingsPanel, 0, 2);
        cardLayout.Controls.Add(checkboxesPanel, 0, 3);
        cardLayout.Controls.Add(infoBar, 0, 4);

        cardPanel.Controls.Add(cardLayout);

        var actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 0)
        };
        _saveSettingsButton.Text = I18n.Server.SaveSettingsButton;
        _saveSettingsButton.Click -= SaveSettingsButton_Click;
        _saveSettingsButton.Click += SaveSettingsButton_Click;
        _saveSettingsButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.PrimaryBlue;
        _saveSettingsButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Save;
        StyleButton(_saveSettingsButton, primary: true);
        actionsPanel.Controls.Add(_saveSettingsButton);

        root.Controls.Add(cardPanel, 0, 0);
        root.Controls.Add(actionsPanel, 0, 1);
        scrollPanel.Controls.Add(root);

        // Apply placeholders
        SetPlaceholder(_clientBannerMessageTextBox, "Nhập nội dung banner hoặc thông báo hiển thị trên client (nếu có)");
        SetPlaceholder(_clientWallpaperPathTextBox, "Chọn hình nền sẽ áp dụng cho Windows máy trạm");
        SetPlaceholder(_clientStatusFolderTextBox, "Ví dụ: D:\\CyberX\\status\\");

        return page;
    }

    private void ClientCafeNameTextBox_TextChanged(object? sender, EventArgs e)
    {
        _clientCafeDisplayName = _clientCafeNameTextBox.Text.Trim();
    }

    private void ClientBannerMessageTextBox_TextChanged(object? sender, EventArgs e)
    {
        _clientBannerMessage = _clientBannerMessageTextBox.Text.Trim();
    }

    private void ClientThemeAccentColorTextBox_TextChanged(object? sender, EventArgs e)
    {
        _clientThemeAccentColor = _clientThemeAccentColorTextBox.Text.Trim();
    }

    private void ClientThemeFontComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _clientThemeFontFamily = _clientThemeFontComboBox.Text;
    }

    private void ClientWallpaperPathTextBox_TextChanged(object? sender, EventArgs e)
    {
        _clientWindowsWallpaperPath = _clientWallpaperPathTextBox.Text.Trim();
    }

    private void ClientStatusFolderTextBox_TextChanged(object? sender, EventArgs e)
    {
        _clientStatusFolderPath = _clientStatusFolderTextBox.Text.Trim();
    }

    private void HeartbeatIntervalNumeric_ValueChanged(object? sender, EventArgs e)
    {
        _clientHeartbeatIntervalSeconds = NormalizeClientHeartbeatIntervalSeconds(Decimal.ToInt32(_clientHeartbeatIntervalNumeric.Value));
    }

    private void DashboardRefreshIntervalNumeric_ValueChanged(object? sender, EventArgs e)
    {
        _dashboardRefreshIntervalSeconds = NormalizeDashboardRefreshIntervalSeconds(Decimal.ToInt32(_dashboardRefreshIntervalNumeric.Value));
    }

    private void CloseAppHotKeyCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        _enableClientCloseApplicationHotKey = _enableClientCloseAppHotKeyCheckBox.Checked;
    }

    private void FullscreenKioskCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        _enableClientFullscreenKioskMode = _enableClientFullscreenKioskCheckBox.Checked;
    }

    private static TableLayoutPanel CreateTextSettingRow(string labelText, TextBox textBox)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(CreateFieldLabel(labelText), 0, 0);
        row.Controls.Add(textBox, 1, 0);
        textBox.Margin = new Padding(0, 6, 0, 6);
        return row;
    }

    private static TableLayoutPanel CreateNumericSettingRow(string labelText, NumericUpDown input)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(CreateFieldLabel(labelText), 0, 0);
        row.Controls.Add(input, 1, 0);
        input.Margin = new Padding(0, 6, 0, 6);
        return row;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string lParam);
    private const int EM_SETCUEBANNER = 0x1501;

    private static void SetPlaceholder(TextBox textBox, string placeholder)
    {
        if (textBox.IsHandleCreated)
        {
            SendMessage(textBox.Handle, EM_SETCUEBANNER, 1, placeholder);
        }
        else
        {
            textBox.HandleCreated += (s, e) =>
            {
                SendMessage(textBox.Handle, EM_SETCUEBANNER, 1, placeholder);
            };
        }
    }

}




