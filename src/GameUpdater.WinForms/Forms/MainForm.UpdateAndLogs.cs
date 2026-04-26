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
{    private TabPage BuildUpdateTab()
    {
        var page = new TabPage("Cập nhật");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 8,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        for (var rowIndex = 0; rowIndex < 7; rowIndex++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
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
            new() { Kind = UpdateSourceKind.Folder, Name = "Thư mục" },
            new() { Kind = UpdateSourceKind.Zip, Name = "Tệp ZIP" }
        };
        _updateSourceKindComboBox.DataSource = sourceOptions;
        _updateSourceKindComboBox.DisplayMember = nameof(UpdateSourceOption.Name);
        _updateSourceKindComboBox.ValueMember = nameof(UpdateSourceOption.Kind);

        _updateSourceTextBox.Dock = DockStyle.Fill;

        _browseSourceButton.Text = "Chọn";
        _browseSourceButton.Dock = DockStyle.Fill;
        _browseSourceButton.Click += BrowseSourceButton_Click;

        _updateVersionTextBox.Dock = DockStyle.Fill;

        _backupCheckBox.Text = "Sao lưu trước khi cập nhật";
        _backupCheckBox.Checked = true;
        _backupCheckBox.Dock = DockStyle.Fill;

        _updateProgressBar.Dock = DockStyle.Fill;

        _applyUpdateButton.Text = "Bắt đầu cập nhật";
        _applyUpdateButton.Dock = DockStyle.Fill;
        _applyUpdateButton.Click += ApplyUpdateButton_Click;

        _updateOutputTextBox.Dock = DockStyle.Fill;
        _updateOutputTextBox.Multiline = true;
        _updateOutputTextBox.ScrollBars = ScrollBars.Vertical;
        _updateOutputTextBox.Font = new Font("Consolas", 10);
        _updateOutputTextBox.ReadOnly = true;

        root.Controls.Add(CreateFieldLabel("Trò chơi"), 0, 0);
        root.Controls.Add(_updateGameComboBox, 1, 0);
        root.SetColumnSpan(_updateGameComboBox, 2);

        root.Controls.Add(CreateFieldLabel("Loại nguồn"), 0, 1);
        root.Controls.Add(_updateSourceKindComboBox, 1, 1);
        root.SetColumnSpan(_updateSourceKindComboBox, 2);

        root.Controls.Add(CreateFieldLabel("Nguồn cập nhật"), 0, 2);
        root.Controls.Add(_updateSourceTextBox, 1, 2);
        root.Controls.Add(_browseSourceButton, 2, 2);

        root.Controls.Add(CreateFieldLabel("Phiên bản"), 0, 3);
        root.Controls.Add(_updateVersionTextBox, 1, 3);
        root.SetColumnSpan(_updateVersionTextBox, 2);

        root.Controls.Add(CreateFieldLabel("Tùy chọn"), 0, 4);
        root.Controls.Add(_backupCheckBox, 1, 4);
        root.SetColumnSpan(_backupCheckBox, 2);

        root.Controls.Add(CreateFieldLabel("Tiến trình"), 0, 5);
        root.Controls.Add(_updateProgressBar, 1, 5);
        root.SetColumnSpan(_updateProgressBar, 2);

        root.Controls.Add(CreateFieldLabel("Thao tác"), 0, 6);
        root.Controls.Add(_applyUpdateButton, 1, 6);
        root.SetColumnSpan(_applyUpdateButton, 2);

        root.Controls.Add(CreateFieldLabel("Nhật ký"), 0, 7);
        root.Controls.Add(_updateOutputTextBox, 1, 7);
        root.SetColumnSpan(_updateOutputTextBox, 2);

        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildLogsTab()
    {
        var page = new TabPage("Lịch sử");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            WrapContents = false
        };
        toolbar.Controls.Add(CreateButton("Làm mới lịch sử", RefreshLogsButton_Click));
        toolbar.Controls.Add(CreateButton("Xuất CSV", ExportLogsCsvButton_Click));
        toolbar.Controls.Add(CreateButton("Xóa lịch sử", ClearLogsButton_Click));

        ConfigureLogsGrid();
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(_logsGrid, 0, 1);

        page.Controls.Add(root);
        return page;
    }

    private async void ClearLogsButton_Click(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(this, "Bạn có chắc chắn muốn xóa toàn bộ lịch sử không?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            await _logRepository.ClearAllAsync();
            await LoadLogsAsync();
            MessageBox.Show(this, "Đã xóa lịch sử thành công.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private TabPage BuildSettingsTab()
    {
        var page = new TabPage("Setting");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var settingsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 9,
            ColumnCount = 1
        };
        for (var i = 0; i < 8; i++)
        {
            settingsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        }
        settingsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var fontSizeRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        InitializeFontSizeSelector(fontSizeRow);

        _browseClientWallpaperButton.Text = "Chọn";
        _browseClientWallpaperButton.Width = 80;
        _browseClientWallpaperButton.Click += BrowseClientWallpaperButton_Click;

        _clearClientWallpaperButton.Text = "Xóa";
        _clearClientWallpaperButton.Width = 70;
        _clearClientWallpaperButton.Click += ClearClientWallpaperButton_Click;

        _clientWallpaperPathTextBox.Dock = DockStyle.Fill;
        _clientWallpaperPathTextBox.Text = _clientWindowsWallpaperPath;
        _clientWallpaperPathTextBox.TextChanged += (_, _) => _clientWindowsWallpaperPath = _clientWallpaperPathTextBox.Text.Trim();

        _clientCafeNameTextBox.Dock = DockStyle.Fill;
        _clientCafeNameTextBox.Text = _clientCafeDisplayName;
        _clientCafeNameTextBox.TextChanged += (_, _) => _clientCafeDisplayName = _clientCafeNameTextBox.Text.Trim();

        _clientBannerMessageTextBox.Dock = DockStyle.Fill;
        _clientBannerMessageTextBox.Text = _clientBannerMessage;
        _clientBannerMessageTextBox.TextChanged += (_, _) => _clientBannerMessage = _clientBannerMessageTextBox.Text.Trim();

        _clientThemeAccentColorTextBox.Dock = DockStyle.Fill;
        _clientThemeAccentColorTextBox.Text = _clientThemeAccentColor;
        _clientThemeAccentColorTextBox.TextChanged += (_, _) => _clientThemeAccentColor = _clientThemeAccentColorTextBox.Text.Trim();

        _clientStatusFolderTextBox.Dock = DockStyle.Fill;
        _clientStatusFolderTextBox.Text = _clientStatusFolderPath;
        _clientStatusFolderTextBox.TextChanged += (_, _) => _clientStatusFolderPath = _clientStatusFolderTextBox.Text.Trim();

        var wallpaperRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4
        };
        wallpaperRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        wallpaperRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        wallpaperRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        wallpaperRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        wallpaperRow.Controls.Add(CreateFieldLabel("Hình nền Windows máy trạm"), 0, 0);
        wallpaperRow.Controls.Add(_clientWallpaperPathTextBox, 1, 0);
        wallpaperRow.Controls.Add(_browseClientWallpaperButton, 2, 0);
        wallpaperRow.Controls.Add(_clearClientWallpaperButton, 3, 0);

        var cafeNameRow = CreateTextSettingRow("Tên quán trên client", _clientCafeNameTextBox);
        var bannerRow = CreateTextSettingRow("Banner/thông báo", _clientBannerMessageTextBox);
        var themeRow = CreateTextSettingRow("Màu theme client", _clientThemeAccentColorTextBox);
        var statusFolderRow = CreateTextSettingRow("Thư mục trạng thái client", _clientStatusFolderTextBox);

        _enableClientCloseAppHotKeyCheckBox.Dock = DockStyle.Fill;
        _enableClientCloseAppHotKeyCheckBox.Text = "Cho phép máy trạm đóng ứng dụng bằng Ctrl + Alt + K";
        _enableClientCloseAppHotKeyCheckBox.Checked = _enableClientCloseApplicationHotKey;
        _enableClientCloseAppHotKeyCheckBox.CheckedChanged += (_, _) => _enableClientCloseApplicationHotKey = _enableClientCloseAppHotKeyCheckBox.Checked;

        _enableClientFullscreenKioskCheckBox.Dock = DockStyle.Fill;
        _enableClientFullscreenKioskCheckBox.Text = "Bật fullscreen/kiosk mode cho client";
        _enableClientFullscreenKioskCheckBox.Checked = _enableClientFullscreenKioskMode;
        _enableClientFullscreenKioskCheckBox.CheckedChanged += (_, _) => _enableClientFullscreenKioskMode = _enableClientFullscreenKioskCheckBox.Checked;

        var hintLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Lưu ý: client ghi trạng thái vào thư mục client-status cạnh games.catalog.json. Có thể nhập thư mục trạng thái riêng nếu dùng shared path.",
            TextAlign = ContentAlignment.TopLeft
        };

        settingsPanel.Controls.Add(fontSizeRow, 0, 0);
        settingsPanel.Controls.Add(cafeNameRow, 0, 1);
        settingsPanel.Controls.Add(bannerRow, 0, 2);
        settingsPanel.Controls.Add(themeRow, 0, 3);
        settingsPanel.Controls.Add(wallpaperRow, 0, 4);
        settingsPanel.Controls.Add(statusFolderRow, 0, 5);
        settingsPanel.Controls.Add(_enableClientCloseAppHotKeyCheckBox, 0, 6);
        settingsPanel.Controls.Add(_enableClientFullscreenKioskCheckBox, 0, 7);
        settingsPanel.Controls.Add(hintLabel, 0, 8);

        var actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        _saveSettingsButton.Text = "Lưu thiết lập";
        _saveSettingsButton.AutoSize = true;
        _saveSettingsButton.Click += SaveSettingsButton_Click;
        actionsPanel.Controls.Add(_saveSettingsButton);

        root.Controls.Add(settingsPanel, 0, 0);
        root.Controls.Add(actionsPanel, 0, 1);

        page.Controls.Add(root);
        return page;
    }

    private static TableLayoutPanel CreateTextSettingRow(string labelText, TextBox textBox)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(CreateFieldLabel(labelText), 0, 0);
        row.Controls.Add(textBox, 1, 0);
        return row;
    }

}




