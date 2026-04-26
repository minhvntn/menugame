using System.Diagnostics;
using GameLauncher.Client.Models;

namespace GameLauncher.Client.Forms;

public sealed partial class MainForm
{
    private static readonly Color HeaderBackColor = Color.FromArgb(17, 24, 39);
    private static readonly Color BodyBackColor = Color.FromArgb(11, 17, 32);

    private readonly Label _headerSectionLabel = new();
    private readonly Label _cafeNameLabel = new();
    private readonly Label _bannerMessageLabel = new();
    private readonly FlowLayoutPanel _hotCardsPanel = new();
    private readonly FlowLayoutPanel _normalCardsPanel = new();
    private readonly TextBox _searchTextBox = new();
    private readonly FlowLayoutPanel _categoriesPanel = new();
    private Image? _headerLogoImage;

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        root.Controls.Add(BuildHeaderPanel(), 0, 0);
        root.Controls.Add(BuildBodyPanel(), 0, 1);
        root.Controls.Add(BuildBottomNotificationPanel(), 0, 2);

        Controls.Add(root);
    }

    private Control BuildHeaderPanel()
    {
        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = HeaderBackColor,
            Padding = new Padding(10, 6, 10, 6)
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));

        _headerLogoImage = BuildHeaderLogoImage();
        var logoBox = new PictureBox
        {
            Width = 42,
            Height = 42,
            Margin = new Padding(0),
            Image = _headerLogoImage,
            SizeMode = PictureBoxSizeMode.Zoom
        };

        _cafeNameLabel.Text = CafeDisplayName;
        _cafeNameLabel.AutoSize = true;
        _cafeNameLabel.ForeColor = Color.White;
        _cafeNameLabel.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);

        var cafeTextPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(8, 8, 0, 0)
        };
        cafeTextPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        cafeTextPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        cafeTextPanel.Controls.Add(_cafeNameLabel, 0, 0);

        var leftPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        leftPanel.Controls.Add(logoBox);
        leftPanel.Controls.Add(cafeTextPanel);

        _headerSectionLabel.Dock = DockStyle.Fill;
        _headerSectionLabel.TextAlign = ContentAlignment.MiddleCenter;
        _headerSectionLabel.ForeColor = Color.White;
        _headerSectionLabel.Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold);
        _headerSectionLabel.Text = "Menu Trò Chơi";

        var systemTools = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0),
            AutoSize = true
        };
        systemTools.Controls.Add(CreateSystemToolButton("Dọn RAM", () => {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            MessageBox.Show(this, "Đã dọn dẹp bộ nhớ RAM thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }));
        systemTools.Controls.Add(CreateSystemToolButton("Chuột", () => {
            try { Process.Start(new ProcessStartInfo("main.cpl") { UseShellExecute = true }); } 
            catch (Exception ex) { MessageBox.Show(this, "Không thể mở bảng điều khiển chuột: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }));

        var headerCenterPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        headerCenterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerCenterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        headerCenterPanel.Controls.Add(systemTools, 0, 0);
        headerCenterPanel.Controls.Add(_headerSectionLabel, 1, 0);

        var quickActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        quickActions.Controls.Add(CreateHeaderLinkButton("YT", "YouTube", "https://www.youtube.com"));
        quickActions.Controls.Add(CreateHeaderLinkButton("FB", "Facebook", "https://www.facebook.com"));
        quickActions.Controls.Add(CreateHeaderLinkButton("Web", "Website", "https://www.google.com"));

        headerLayout.Controls.Add(leftPanel, 0, 0);
        headerLayout.Controls.Add(headerCenterPanel, 1, 0);
        headerLayout.Controls.Add(quickActions, 2, 0);

        headerPanel.Controls.Add(headerLayout);
        return headerPanel;
    }

    private Control BuildBodyPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10, 10, 10, 10),
            BackColor = BodyBackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var topToolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8)
        };
        topToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        topToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        
        _categoriesPanel.Dock = DockStyle.Fill;
        _categoriesPanel.FlowDirection = FlowDirection.LeftToRight;
        _categoriesPanel.WrapContents = false;
        
        _searchTextBox.Dock = DockStyle.Fill;
        _searchTextBox.Font = new Font("Segoe UI", 12f);
        _searchTextBox.PlaceholderText = "Tìm kiếm trò chơi...";
        _searchTextBox.TextChanged += (s, e) => {
            _currentSearchQuery = _searchTextBox.Text.Trim();
            ApplyFilter();
        };

        topToolbar.Controls.Add(_categoriesPanel, 0, 0);
        topToolbar.Controls.Add(_searchTextBox, 1, 0);

        _hotCardsPanel.Dock = DockStyle.Fill;
        _hotCardsPanel.AutoScroll = true;
        _hotCardsPanel.WrapContents = false;
        _hotCardsPanel.FlowDirection = FlowDirection.LeftToRight;
        _hotCardsPanel.Padding = new Padding(8, 8, 8, 8);
        _hotCardsPanel.Margin = new Padding(0, 0, 0, 8);
        _hotCardsPanel.BackColor = Color.FromArgb(17, 24, 39);

        _normalCardsPanel.Dock = DockStyle.Fill;
        _normalCardsPanel.AutoScroll = true;
        _normalCardsPanel.WrapContents = true;
        _normalCardsPanel.FlowDirection = FlowDirection.LeftToRight;
        _normalCardsPanel.Padding = new Padding(8, 8, 8, 8);
        _normalCardsPanel.BackColor = BodyBackColor;

        layout.Controls.Add(topToolbar, 0, 0);
        layout.Controls.Add(_hotCardsPanel, 0, 1);
        layout.Controls.Add(_normalCardsPanel, 0, 2);
        return layout;
    }

    private Control BuildBottomNotificationPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(12, 6, 12, 6)
        };

        _bannerMessageLabel.Dock = DockStyle.Fill;
        _bannerMessageLabel.TextAlign = ContentAlignment.MiddleCenter;
        _bannerMessageLabel.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        _bannerMessageLabel.ForeColor = Color.White;
        _bannerMessageLabel.BackColor = Color.Transparent;
        _bannerMessageLabel.Text = "Chào mừng quý khách";
        _bannerMessageLabel.Visible = true;

        panel.Controls.Add(_bannerMessageLabel);
        return panel;
    }

    private static Image BuildHeaderLogoImage()
    {
        using var source = SystemIcons.Shield.ToBitmap();
        var size = 36;
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var circleBrush = new SolidBrush(Color.FromArgb(34, 211, 238));
        graphics.FillEllipse(circleBrush, 0, 0, size - 1, size - 1);
        graphics.DrawImage(source, 6, 6, size - 12, size - 12);
        return bitmap;
    }

    private static Button CreateHeaderLinkButton(string text, string tooltip, string url)
    {
        var button = new Button
        {
            Text = text,
            Width = 58,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(6, 2, 0, 0),
            BackColor = Color.FromArgb(59, 130, 246),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(34, 211, 238);
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // Ignore url start errors.
            }
        };

        return button;
    }

    private Button CreateSystemToolButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(45, 55, 72),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 10f),
            Margin = new Padding(0, 0, 8, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (s, e) => onClick();
        return btn;
    }
}
