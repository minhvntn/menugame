using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace GameUpdater.WinForms.Forms;

public partial class MainForm
{
    private Control BuildConfigWorkspaceLayout()
    {
        var wrapperLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(20),
            BackColor = Color.FromArgb(248, 250, 252) // slate-50
        };
        wrapperLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        wrapperLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var mainCard = new GameUpdater.WinForms.Controls.CardPanel
        {
            Dock = DockStyle.Fill,
            CardBackColor = Color.White,
            Padding = new Padding(24, 24, 24, 0),
            Margin = new Padding(0, 0, 0, 16),
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
        mainFlow.Controls.Add(CreateConfigSectionHeader("🌐", "NGUỒN IDC", Color.FromArgb(88, 50, 228)));
        
        _sourcesContainer.Dock = DockStyle.Top;
        _sourcesContainer.AutoSize = true;
        _sourcesContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _sourcesContainer.FlowDirection = FlowDirection.TopDown;
        _sourcesContainer.WrapContents = false;
        _sourcesContainer.Padding = new Padding(0, 10, 0, 20);
        _sourcesContainer.Margin = new Padding(0);
        mainFlow.Controls.Add(_sourcesContainer);
        BuildSourcesUi(); // This will populate _sourcesContainer

        // Section 2: Đích Máy Chủ
        mainFlow.Controls.Add(CreateConfigSectionHeader("🖥️", "ĐÍCH MÁY CHỦ", Color.FromArgb(88, 50, 228)));
        
        _targetsContainer.Dock = DockStyle.Top;
        _targetsContainer.AutoSize = true;
        _targetsContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _targetsContainer.FlowDirection = FlowDirection.TopDown;
        _targetsContainer.WrapContents = false;
        _targetsContainer.Padding = new Padding(0, 10, 0, 20);
        _targetsContainer.Margin = new Padding(0);
        mainFlow.Controls.Add(_targetsContainer);
        BuildTargetsUi(); // This will populate _targetsContainer

        // Section 3: Giới Hạn
        mainFlow.Controls.Add(CreateConfigSectionHeader("⏱️", "GIỚI HẠN", Color.FromArgb(88, 50, 228)));
        var bandwidthRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            Padding = new Padding(0, 10, 0, 20),
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0)
        };
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var limitLabel = new Label { Text = "Giới hạn MB/s", Font = new Font("Segoe UI", 10.5f), ForeColor = Color.FromArgb(30,30,40), AutoSize = true, Anchor = AnchorStyles.Left };
        
        _resourceBandwidthLimitNumeric.Dock = DockStyle.Fill;
        _resourceBandwidthLimitNumeric.Width = 140;
        _resourceBandwidthLimitNumeric.Minimum = 0;
        _resourceBandwidthLimitNumeric.Maximum = 10000;
        _resourceBandwidthLimitNumeric.DecimalPlaces = 0;
        _resourceBandwidthLimitNumeric.Value = _resourceBandwidthLimitMbps;
        _resourceBandwidthLimitNumeric.Font = new Font("Segoe UI", 10.5f);
        _resourceBandwidthLimitNumeric.ValueChanged += (_, _) => _resourceBandwidthLimitMbps = Decimal.ToInt32(_resourceBandwidthLimitNumeric.Value);

        var hintLabel = new Label { Text = "0 = không giới hạn", Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(130,130,140), AutoSize = true, Anchor = AnchorStyles.Left };
        
        bandwidthRow.Controls.Add(limitLabel, 0, 0);
        bandwidthRow.Controls.Add(_resourceBandwidthLimitNumeric, 1, 0);
        bandwidthRow.Controls.Add(hintLabel, 2, 0);
        mainFlow.Controls.Add(bandwidthRow);

        // Divider
        var divider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(226, 232, 240), Margin = new Padding(0, 10, 0, 20) };
        mainFlow.Controls.Add(divider);

        // Actions Row
        var actionsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 24),
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        
        _saveResourceSettingsButton.Text = "Lưu cấu hình";
        _saveResourceSettingsButton.Click += SaveResourceSettingsButton_Click;
        _saveResourceSettingsButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary;
        _saveResourceSettingsButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Save;
        _saveResourceSettingsButton.Size = new Size(180, 42);
        
        _checkResourceHealthButton.Text = "Kiểm tra tài nguyên";
        _checkResourceHealthButton.Click += CheckResourceHealthButton_Click;
        _checkResourceHealthButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary;
        _checkResourceHealthButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Refresh;
        _checkResourceHealthButton.Size = new Size(180, 42);
        _checkResourceHealthButton.Margin = new Padding(16, 0, 0, 0);
        
        _syncSelectedResourceButton.Text = "Tải trò chơi đã chọn";
        _syncSelectedResourceButton.Click += SyncSelectedResourceButton_Click;
        _syncSelectedResourceButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Purple;
        _syncSelectedResourceButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Refresh;
        _syncSelectedResourceButton.Size = new Size(300, 42);
        _syncSelectedResourceButton.Margin = new Padding(30, 0, 0, 0);

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
            Padding = new Padding(16, 12, 16, 12),
            AutoSize = true,
            Margin = new Padding(0)
        };
        var infoFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
        var infoIcon = new Label { Text = "ⓘ", Font = new Font("Segoe UI", 12f), ForeColor = Color.FromArgb(88, 50, 228), AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        var infoText = new Label { Text = "Cấu hình nguồn/đích và giới hạn băng thông tải tài nguyên.", Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(30,30,40), AutoSize = true, Margin = new Padding(0, 3, 0, 0) };
        infoFlow.Controls.Add(infoIcon);
        infoFlow.Controls.Add(infoText);
        infoBar.Controls.Add(infoFlow);

        wrapperLayout.Controls.Add(mainCard, 0, 0);
        wrapperLayout.Controls.Add(infoBar, 0, 1);

        return wrapperLayout;
    }

    private Control CreateConfigSectionHeader(string emoji, string title, Color iconBg)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 10)
        };

        var iconContainer = new Panel
        {
            Width = 36,
            Height = 36,
            Margin = new Padding(0, 0, 12, 0),
            BackColor = Color.FromArgb(20, iconBg.R, iconBg.G, iconBg.B) // Very light tint
        };
        iconContainer.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(20, iconBg.R, iconBg.G, iconBg.B));
            e.Graphics.FillEllipse(brush, new Rectangle(0, 0, 36, 36));
            TextRenderer.DrawText(e.Graphics, emoji, new Font("Segoe UI Emoji", 14), new Rectangle(0, 0, 36, 36), iconBg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30,30,40),
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };

        panel.Controls.Add(iconContainer);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private Panel CreateInputWrapperWithIcon(string emoji, Control innerControl)
    {
        var pnl = new Panel { BackColor = Color.White, Padding = new Padding(12, 8, 12, 8) };
        pnl.Paint += (s, e) => {
            using var path = GameEditorForm.GetRoundedRectPath(pnl.ClientRectangle, 6); // Assuming GameEditorForm is accessible, or just copy the method
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        };
        
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var iconLabel = new Label { Text = emoji, Font = new Font("Segoe UI Emoji", 11), ForeColor = Color.FromArgb(100, 116, 139), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0) };
        layout.Controls.Add(iconLabel, 0, 0);

        innerControl.Dock = DockStyle.Fill;
        innerControl.Font = new Font("Segoe UI", 10.5f);
        innerControl.Margin = new Padding(0, 2, 0, 0);
        
        if (innerControl is TextBox tb) {
            tb.BorderStyle = BorderStyle.None;
        }
        
        layout.Controls.Add(innerControl, 1, 0);
        pnl.Controls.Add(layout);
        return pnl;
    }
}
