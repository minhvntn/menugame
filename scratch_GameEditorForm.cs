using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GameUpdater.Shared.Models;
using GameUpdater.Shared.Localization;
using GameUpdater.WinForms.Controls;

namespace GameUpdater.WinForms.Forms;

public sealed class GameEditorForm : Form
{
    private static readonly string[] CategoryOptions = { "Online", "Offline", "Tools" };

    private readonly TextBox _nameTextBox = new();
    private readonly ModernComboBox _categoryComboBox = new();
    private readonly TextBox _pathTextBox = new();
    private readonly TextBox _versionTextBox = new();
    private readonly DragDropExePanel _exePanel = new();
    private readonly TextBox _launchArgumentsTextBox = new();
    private readonly ModernCheckBox _isClientCheckBox = new();
    private readonly ModernCheckBox _isHotCheckBox = new();
    private readonly GameRecord? _existingGame;

    // Theme Colors
    private static readonly Color ColorPrimary = Color.FromArgb(88, 50, 228);
    private static readonly Color ColorTextMain = Color.FromArgb(30, 30, 40);
    private static readonly Color ColorTextSub = Color.FromArgb(130, 130, 140);
    private static readonly Color ColorBorder = Color.FromArgb(226, 232, 240);
    private static readonly Color ColorBgIconGray = Color.FromArgb(244, 245, 247);
    private static readonly Color ColorBgIconPurple = Color.FromArgb(240, 237, 252);
    private static readonly Font FontMainTitle = new Font("Segoe UI", 16f, FontStyle.Bold);
    private static readonly Font FontTitle = new Font("Segoe UI", 11f, FontStyle.Bold);
    private static readonly Font FontSubTitle = new Font("Segoe UI", 9f);
    private static readonly Font FontInput = new Font("Segoe UI", 10.5f);

    public GameRecord? EditedGame { get; private set; }

    public GameEditorForm(GameRecord? existingGame = null, Font? parentFont = null)
    {
        _existingGame = existingGame;
        if (parentFont is not null) this.Font = parentFont;
        else this.Font = new Font("Segoe UI", 10f);

        Text = existingGame is null ? I18n.Server.GameEditorAddTitle : I18n.Server.GameEditorEditTitle;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(850, 710); // Use ClientSize to guarantee no scrollbar
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        BackColor = Color.White;

        BuildUI();

        if (existingGame is not null)
        {
            _nameTextBox.Text = existingGame.Name;
            SelectCategory(existingGame.Category);
            _pathTextBox.Text = existingGame.InstallPath;
            _versionTextBox.Text = existingGame.Version;
            _exePanel.SetFile(existingGame.LaunchRelativePath);
            _launchArgumentsTextBox.Text = existingGame.LaunchArguments;
            _isHotCheckBox.Checked = existingGame.IsHot;
            _isClientCheckBox.Checked = true;
        }
        else
        {
            SelectCategory(I18n.Server.GameEditorDefaultCategory);
            _versionTextBox.Text = I18n.Server.GameEditorDefaultVersion;
            _isClientCheckBox.Checked = true;
        }
    }

    private void BuildUI()
    {
        Controls.Clear();

        var mainPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0) };
        Controls.Add(mainPanel);

        // --- Header ---
        var headerPanel = new Panel { Dock = DockStyle.Top, Height = 90 };
        headerPanel.Paint += (s, e) => {
            using var pen = new Pen(ColorBorder);
            e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };
        
        var headerIcon = new IconBox(IconShape.Gamepad, ColorBgIconPurple, ColorPrimary) { Location = new Point(24, 20), Size = new Size(48, 48) };
        var headerTitle = new Label { Text = "Thêm trò chơi", Font = FontMainTitle, ForeColor = ColorTextMain, AutoSize = true, Location = new Point(88, 22) };
        var headerSub = new Label { Text = "Thêm thông tin game để hệ thống có thể khởi chạy và quản lý", Font = FontSubTitle, ForeColor = ColorTextSub, AutoSize = true, Location = new Point(90, 52) };
        
        headerPanel.Controls.Add(headerIcon);
        headerPanel.Controls.Add(headerTitle);
        headerPanel.Controls.Add(headerSub);
        mainPanel.Controls.Add(headerPanel);

        // --- Footer ---
        var footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 80 };
        footerPanel.Paint += (s, e) => {
            using var pen = new Pen(ColorBorder);
            e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
        };

        _isClientCheckBox.Text = "Hiển thị client";
        _isClientCheckBox.Location = new Point(30, 30);
        _isClientCheckBox.AutoSize = true;

        _isHotCheckBox.Text = "Hiển thị trong Hot game (client)";
        _isHotCheckBox.Location = new Point(200, 30);
        _isHotCheckBox.AutoSize = true;

        var saveBtn = new ModernButton { Text = I18n.Common.SaveButton, Width = 140, Height = 42, ColorType = ButtonColorType.Purple, IconType = ButtonIconType.Save, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
        saveBtn.Location = new Point(Width - saveBtn.Width - 40, 20);
        saveBtn.Click += SaveButton_Click;

        footerPanel.Controls.Add(_isClientCheckBox);
        footerPanel.Controls.Add(_isHotCheckBox);
        footerPanel.Controls.Add(saveBtn);
        mainPanel.Controls.Add(footerPanel);

        // --- Content ---
        var contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };
        mainPanel.Controls.Add(contentPanel);
        contentPanel.BringToFront();

        int y = 20;
        int rowSpacing = 20;

        // Name
        _nameTextBox.PlaceholderText = "Nhập tên trò chơi";
        var nameRow = CreateRow(IconShape.Gamepad, "Tên trò chơi", "Tên hiển thị trong hệ thống", CreateInputWrapper(_nameTextBox), 44, y);
        contentPanel.Controls.Add(nameRow);
        y += nameRow.Height + rowSpacing;

        // Category
        _categoryComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _categoryComboBox.Items.AddRange(CategoryOptions);
        var catRow = CreateRow(IconShape.Users, "Nhóm", "Chọn nhóm phù hợp", CreateInputWrapper(_categoryComboBox), 44, y);
        contentPanel.Controls.Add(catRow);
        y += catRow.Height + rowSpacing;

        // Install Path
        _pathTextBox.PlaceholderText = "Chọn đường dẫn cài đặt";
        var pathPnl = new Panel { Size = new Size(500, 44) };
        var pathInnerWrap = CreateInputWrapper(_pathTextBox);
        pathInnerWrap.Size = new Size(385, 44);
        pathInnerWrap.Location = new Point(0, 0);
        
        var browseBtn = new ModernButton { Text = "Chọn", Size = new Size(105, 44), Location = new Point(395, 0), ColorType = ButtonColorType.Secondary, IconType = ButtonIconType.Folder, Font = new Font("Segoe UI", 10f, FontStyle.Regular) };
        browseBtn.Click += BrowseInstallButton_Click;

        pathPnl.Controls.Add(pathInnerWrap);
        pathPnl.Controls.Add(browseBtn);
        
        var pathRow = CreateRow(IconShape.Link, "Đường dẫn cài đặt", "Thư mục chứa game", pathPnl, 44, y);
        contentPanel.Controls.Add(pathRow);
        y += pathRow.Height + rowSpacing;

        // Version
        _versionTextBox.PlaceholderText = "1.0.0";
        var verRow = CreateRow(IconShape.Code, "Phiên bản", "Phiên bản game", CreateInputWrapper(_versionTextBox), 44, y);
        contentPanel.Controls.Add(verRow);
        y += verRow.Height + rowSpacing;

        // EXE
        _exePanel.BrowseClicked += BrowseLaunchButton_Click;
        var exeRow = CreateRow(IconShape.Running, "Tệp chạy (EXE)", "File thực thi để chạy game", _exePanel, 120, y);
        contentPanel.Controls.Add(exeRow);
        y += exeRow.Height + rowSpacing;

        // Arguments
        _launchArgumentsTextBox.PlaceholderText = "Nhập tham số (nếu có)";
        var argRow = CreateRow(IconShape.Hash, "Tham số (tuỳ chọn)", "Tham số khi chạy game", CreateInputWrapper(_launchArgumentsTextBox), 44, y);
        contentPanel.Controls.Add(argRow);
    }

    private Panel CreateRow(IconShape iconShape, string title, string subtitle, Control inputControl, int inputHeight, int yPos)
    {
        int rowHeight = Math.Max(56, inputHeight + 4);
        var row = new Panel { Width = 800, Height = rowHeight, Location = new Point(30, yPos) };
        
        var iconBox = new IconBox(iconShape, ColorBgIconGray, ColorPrimary) { Location = new Point(0, (rowHeight - 56) / 2), Size = new Size(56, 56) };
        row.Controls.Add(iconBox);

        var titleLbl = new Label { Text = title, Font = FontTitle, ForeColor = ColorTextMain, AutoSize = true, Location = new Point(70, (rowHeight - 56) / 2 + 8) };
        var subLbl = new Label { Text = subtitle, Font = FontSubTitle, ForeColor = ColorTextSub, AutoSize = true, Location = new Point(70, (rowHeight - 56) / 2 + 32) };
        row.Controls.Add(titleLbl);
        row.Controls.Add(subLbl);

        inputControl.Location = new Point(250, (rowHeight - inputHeight) / 2);
        inputControl.Size = new Size(500, inputHeight);
        row.Controls.Add(inputControl);

        return row;
    }

    private Panel CreateInputWrapper(Control innerControl)
    {
        var pnl = new Panel { BackColor = Color.White, Padding = new Padding(12, 10, 12, 10) };
        pnl.Paint += (s, e) => {
            using var path = GetRoundedRectPath(pnl.ClientRectangle, 6);
            using var pen = new Pen(ColorBorder);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        };
        
        innerControl.Dock = DockStyle.Fill;
        innerControl.Font = FontInput;
        
        if (innerControl is TextBox tb) {
            tb.BorderStyle = BorderStyle.None;
        } else if (innerControl is ComboBox cb) {
            cb.FlatStyle = FlatStyle.Flat;
        }
        
        pnl.Controls.Add(innerControl);
        return pnl;
    }

    public static GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        rect = new Rectangle(rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        int diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void SelectCategory(string? category)
    {
        var normalized = category?.Trim() ?? string.Empty;
        var selectedCategory = CategoryOptions.FirstOrDefault(option =>
            string.Equals(option, normalized, StringComparison.OrdinalIgnoreCase))
            ?? I18n.Server.GameEditorDefaultCategory;

        _categoryComboBox.SelectedItem = selectedCategory;
        if (_categoryComboBox.SelectedIndex < 0) _categoryComboBox.SelectedIndex = 0;
    }

    private void BrowseInstallButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = I18n.Server.GameEditorFolderPickerDescription,
            UseDescriptionForTitle = true,
            SelectedPath = _pathTextBox.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void BrowseLaunchButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = I18n.Server.GameEditorExeDialogTitle,
            Filter = I18n.Server.GameEditorExeDialogFilter,
            CheckFileExists = true,
            InitialDirectory = Directory.Exists(_pathTextBox.Text) ? _pathTextBox.Text : string.Empty
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var selectedFile = dialog.FileName;
            if (Directory.Exists(_pathTextBox.Text))
            {
                try
                {
                    var relativePath = Path.GetRelativePath(_pathTextBox.Text, selectedFile);
                    if (!relativePath.StartsWith("..", StringComparison.Ordinal))
                    {
                        _exePanel.SetFile(relativePath);
                        return;
                    }
                }
                catch { }
            }
            _exePanel.SetFile(selectedFile);
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            MessageBox.Show(this, I18n.Server.ValidationNameRequired, I18n.Common.ValidationTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_pathTextBox.Text))
        {
            MessageBox.Show(this, I18n.Server.ValidationInstallPathRequired, I18n.Common.ValidationTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_exePanel.FilePath))
        {
            MessageBox.Show(this, I18n.Server.ValidationExeRequired, I18n.Common.ValidationTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        EditedGame = new GameRecord
        {
            Id = _existingGame?.Id ?? 0,
            Name = _nameTextBox.Text.Trim(),
            Category = _categoryComboBox.SelectedItem?.ToString() ?? I18n.Server.GameEditorDefaultCategory,
            InstallPath = _pathTextBox.Text.Trim(),
            Version = string.IsNullOrWhiteSpace(_versionTextBox.Text) ? I18n.Server.GameEditorDefaultVersion : _versionTextBox.Text.Trim(),
            LaunchRelativePath = _exePanel.FilePath.Trim(),
            LaunchArguments = _launchArgumentsTextBox.Text.Trim(),
            IsHot = _isHotCheckBox.Checked,
            Notes = _existingGame?.Notes ?? string.Empty, // Keep existing notes since we removed it from UI
            LastScannedAt = _existingGame?.LastScannedAt,
            LastUpdatedAt = _existingGame?.LastUpdatedAt,
            SortOrder = _existingGame?.SortOrder ?? 999999
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}

// --- Custom Controls ---

public enum IconShape { Gamepad, Users, Link, Code, Running, Hash, Cloud, File }

public class IconBox : Control
{
    public IconShape Shape { get; set; }
    public Color BgColor { get; set; }
    public Color IconColor { get; set; }

    public IconBox(IconShape shape, Color bgColor, Color iconColor)
    {
        Shape = shape;
        BgColor = bgColor;
        IconColor = iconColor;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = GameEditorForm.GetRoundedRectPath(ClientRectangle, 12);
        using var brush = new SolidBrush(BgColor);
        e.Graphics.FillPath(brush, path);

        using var pen = new Pen(IconColor, 2f);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;
        pen.LineJoin = LineJoin.Round;

        int cx = Width / 2;
        int cy = Height / 2;
        int size = 16;
        int hs = size / 2;

        switch (Shape)
        {
            case IconShape.Gamepad:
                e.Graphics.DrawRoundedRectangle(pen, cx - 10, cy - 7, 20, 14, 4);
                e.Graphics.DrawLine(pen, cx - 6, cy, cx - 2, cy); // D-pad H
                e.Graphics.DrawLine(pen, cx - 4, cy - 2, cx - 4, cy + 2); // D-pad V
                e.Graphics.DrawEllipse(pen, cx + 3, cy + 1, 2, 2); // btn
                e.Graphics.DrawEllipse(pen, cx + 6, cy - 2, 2, 2); // btn
                break;
            case IconShape.Users:
                e.Graphics.DrawEllipse(pen, cx - 4, cy - 8, 8, 8); // Head 1
                e.Graphics.DrawArc(pen, cx - 8, cy + 2, 16, 12, 180, 180); // Body 1
                using (var pen2 = new Pen(IconColor, 1.5f)) {
                    e.Graphics.DrawEllipse(pen2, cx + 4, cy - 6, 6, 6); // Head 2
                    e.Graphics.DrawArc(pen2, cx + 2, cy + 2, 12, 10, 180, 180); // Body 2
                }
                break;
            case IconShape.Link:
                e.Graphics.DrawArc(pen, cx - 8, cy - 4, 8, 8, 90, 180);
                e.Graphics.DrawArc(pen, cx, cy - 4, 8, 8, -90, 180);
                e.Graphics.DrawLine(pen, cx - 4, cy, cx + 4, cy);
                break;
            case IconShape.Code:
                e.Graphics.DrawLine(pen, cx - 3, cy - 5, cx - 8, cy);
                e.Graphics.DrawLine(pen, cx - 8, cy, cx - 3, cy + 5);
                e.Graphics.DrawLine(pen, cx + 3, cy - 5, cx + 8, cy);
                e.Graphics.DrawLine(pen, cx + 8, cy, cx + 3, cy + 5);
                e.Graphics.DrawLine(pen, cx + 2, cy - 6, cx - 2, cy + 6);
                break;
            case IconShape.Running:
                e.Graphics.DrawEllipse(pen, cx, cy - 8, 4, 4); // Head
                e.Graphics.DrawLine(pen, cx + 2, cy - 4, cx, cy + 2); // Torso
                e.Graphics.DrawLine(pen, cx, cy + 2, cx - 4, cy + 6); // Leg 1
                e.Graphics.DrawLine(pen, cx, cy + 2, cx + 4, cy + 8); // Leg 2
                e.Graphics.DrawLine(pen, cx - 4, cy - 2, cx + 2, cy - 4); // Arm 1
                e.Graphics.DrawLine(pen, cx + 2, cy - 4, cx + 6, cy); // Arm 2
                break;
            case IconShape.Hash:
                e.Graphics.DrawLine(pen, cx - 4, cy - 6, cx - 2, cy + 6);
                e.Graphics.DrawLine(pen, cx + 2, cy - 6, cx + 4, cy + 6);
                e.Graphics.DrawLine(pen, cx - 6, cy - 2, cx + 6, cy - 2);
                e.Graphics.DrawLine(pen, cx - 6, cy + 2, cx + 6, cy + 2);
                break;
            case IconShape.Cloud:
                e.Graphics.DrawArc(pen, cx - 12, cy - 2, 8, 8, 90, 180);
                e.Graphics.DrawArc(pen, cx - 8, cy - 8, 12, 12, 180, 180);
                e.Graphics.DrawArc(pen, cx, cy - 6, 10, 10, 270, 180);
                e.Graphics.DrawLine(pen, cx - 8, cy + 6, cx + 5, cy + 6);
                // Arrow up
                e.Graphics.DrawLine(pen, cx - 2, cy + 2, cx - 2, cy + 10);
                e.Graphics.DrawLine(pen, cx - 5, cy + 5, cx - 2, cy + 2);
                e.Graphics.DrawLine(pen, cx + 1, cy + 5, cx - 2, cy + 2);
                break;
            case IconShape.File:
                e.Graphics.DrawLine(pen, cx - 5, cy - 8, cx + 1, cy - 8);
                e.Graphics.DrawLine(pen, cx + 1, cy - 8, cx + 5, cy - 4);
                e.Graphics.DrawLine(pen, cx + 5, cy - 4, cx + 5, cy + 8);
                e.Graphics.DrawLine(pen, cx + 5, cy + 8, cx - 5, cy + 8);
                e.Graphics.DrawLine(pen, cx - 5, cy + 8, cx - 5, cy - 8);
                e.Graphics.DrawLine(pen, cx + 1, cy - 8, cx + 1, cy - 4);
                e.Graphics.DrawLine(pen, cx + 1, cy - 4, cx + 5, cy - 4);
                // Lines inside
                e.Graphics.DrawLine(pen, cx - 2, cy + 1, cx + 2, cy + 1);
                e.Graphics.DrawLine(pen, cx - 2, cy + 4, cx + 2, cy + 4);
                break;
        }
    }
}

public static class GraphicsExtensions
{
    public static void DrawRoundedRectangle(this Graphics g, Pen pen, int x, int y, int width, int height, int radius)
    {
        using var path = GameEditorForm.GetRoundedRectPath(new Rectangle(x, y, width, height), radius);
        g.DrawPath(pen, path);
    }
}

public class DragDropExePanel : Panel
{
    public string FilePath { get; private set; } = string.Empty;
    public event EventHandler? BrowseClicked;

    private readonly Label _cloudLbl = new Label { Text = "Kéo thả file .exe vào đây", Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(30,30,40) };
    private readonly Label _orLbl = new Label { Text = "hoặc", Font = new Font("Segoe UI", 9f), AutoSize = true, ForeColor = Color.FromArgb(130,130,140) };
    private readonly ModernButton _browseBtn = new ModernButton { Text = "Chọn file .exe", Width = 150, Height = 36, ColorType = ButtonColorType.Secondary, IconType = ButtonIconType.Folder, Font = new Font("Segoe UI", 10f) };
    private readonly IconBox _cloudIcon = new IconBox(IconShape.Cloud, Color.Transparent, Color.FromArgb(88, 50, 228)) { Size = new Size(32, 32) };

    private readonly Panel _filePanel = new Panel { Visible = false, Height = 60, BackColor = Color.White };
    private readonly IconBox _fileIcon = new IconBox(IconShape.File, Color.FromArgb(240, 245, 255), Color.FromArgb(37, 99, 235)) { Size = new Size(40, 40) };
    private readonly Label _fileNameLbl = new Label { Font = new Font("Segoe UI", 10f, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(30,30,40) };
    private readonly Label _filePathLbl = new Label { Font = new Font("Segoe UI", 8.5f), AutoSize = true, ForeColor = Color.FromArgb(130,130,140) };
    private readonly Label _fileSizeLbl = new Label { Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(16, 185, 129) };
    private readonly Label _removeBtn = new Label { Text = "✕", Font = new Font("Segoe UI", 12f), AutoSize = true, ForeColor = Color.FromArgb(130,130,140), Cursor = Cursors.Hand };

    public DragDropExePanel()
    {
        AllowDrop = true;
        BackColor = Color.FromArgb(250, 251, 255);
        Padding = new Padding(2);
        
        // Empty State
        _cloudIcon.Location = new Point(140, 15);
        _cloudLbl.Location = new Point(180, 20);
        _orLbl.Location = new Point(230, 45);
        _browseBtn.Location = new Point(180, 70);
        _browseBtn.Click += (s, e) => BrowseClicked?.Invoke(this, e);

        Controls.Add(_cloudIcon);
        Controls.Add(_cloudLbl);
        Controls.Add(_orLbl);
        Controls.Add(_browseBtn);

        // Filled State
        _filePanel.Paint += (s, e) => {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            using var path = GameEditorForm.GetRoundedRectPath(_filePanel.ClientRectangle, 6);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        };
        _filePanel.Padding = new Padding(10);
        
        _fileIcon.Location = new Point(10, 10);
        _fileNameLbl.Location = new Point(60, 10);
        _filePathLbl.Location = new Point(60, 32);
        
        _removeBtn.Location = new Point(0, 20); // Set later
        _removeBtn.Click += (s, e) => SetFile(string.Empty);
        _removeBtn.MouseEnter += (s, e) => _removeBtn.ForeColor = Color.Red;
        _removeBtn.MouseLeave += (s, e) => _removeBtn.ForeColor = Color.FromArgb(130,130,140);

        _fileSizeLbl.Location = new Point(0, 20); // Set later

        _filePanel.Controls.Add(_fileIcon);
        _filePanel.Controls.Add(_fileNameLbl);
        _filePanel.Controls.Add(_filePathLbl);
        _filePanel.Controls.Add(_fileSizeLbl);
        _filePanel.Controls.Add(_removeBtn);

        Controls.Add(_filePanel);

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        Resize += OnResize;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(199, 210, 254), 1.5f) { DashStyle = DashStyle.Dash };
        using var path = GameEditorForm.GetRoundedRectPath(ClientRectangle, 8);
        e.Graphics.DrawPath(pen, path);
    }

    private void OnResize(object? sender, EventArgs e)
    {
        int cx = Width / 2;
        _cloudIcon.Location = new Point(cx - _cloudLbl.Width/2 - 20, 15);
        _cloudLbl.Location = new Point(_cloudIcon.Right + 10, 20);
        _orLbl.Location = new Point(cx - _orLbl.Width/2, 45);
        _browseBtn.Location = new Point(cx - _browseBtn.Width/2, 70);

        _filePanel.Location = new Point(10, 10);
        _filePanel.Width = Width - 20;
        _filePanel.Height = Height - 20;

        _removeBtn.Location = new Point(_filePanel.Width - 30, 20);
        _fileSizeLbl.Location = new Point(_filePanel.Width - 90, 20);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && files[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }
        }
        e.Effect = DragDropEffects.None;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                SetFile(files[0]);
            }
        }
    }

    public void SetFile(string path)
    {
        FilePath = path;
        if (string.IsNullOrWhiteSpace(path))
        {
            _filePanel.Visible = false;
            _cloudIcon.Visible = true;
            _cloudLbl.Visible = true;
            _orLbl.Visible = true;
            _browseBtn.Visible = true;
        }
        else
        {
            _cloudIcon.Visible = false;
            _cloudLbl.Visible = false;
            _orLbl.Visible = false;
            _browseBtn.Visible = false;

            _fileNameLbl.Text = Path.GetFileName(path);
            _filePathLbl.Text = path;
            
            // Try to get size if absolute
            if (File.Exists(path)) {
                long bytes = new FileInfo(path).Length;
                _fileSizeLbl.Text = (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
            } else {
                _fileSizeLbl.Text = "";
            }

            _filePanel.Visible = true;
        }
    }
}
