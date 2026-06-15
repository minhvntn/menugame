using GameLauncher.Client.Extensions;
using System.Diagnostics;
using System.Reflection;
using GameUpdater.Shared.Localization;

namespace GameLauncher.Client.Forms;

public sealed partial class MainForm
{
    private static readonly Color HeaderBackColor = Color.FromArgb(13, 15, 20); // #0D0F14
    private static readonly Color BodyBackColor = Color.FromArgb(13, 15, 20); // #0D0F14
    private static readonly Color SidebarBackColor = Color.FromArgb(21, 24, 33); // #151821

    private readonly Label _headerSectionLabel = new();
    private readonly Label _cafeNameLabel = new();
    private readonly Label _bannerMessageLabel = new();
    private readonly Label _footerMachineLabel = new();
    private readonly Label _footerClockLabel = new();
    private readonly Label _hotSortLabel = new();
    private readonly Label _allSortLabel = new();
    private readonly TextBox _searchTextBox = new();
    private readonly FlowLayoutPanel _categoryListPanel = new();
    private readonly FlowLayoutPanel _hotCardsPanel = new();
    private readonly FlowLayoutPanel _normalCardsPanel = new();
    private readonly Button _hotLeftBtn = new();
    private readonly Button _hotRightBtn = new();
    private readonly Panel _hotCardsViewport = new();
    private readonly Dictionary<string, Button> _categoryButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer _clockTimer = new();
    private readonly System.Windows.Forms.Timer _slideTimer = new();
    private int _slideStartLeft;
    private int _slideTargetLeft;
    private float _slideProgress;
    private readonly Panel _customScrollbar = new();
    private bool _isDraggingScrollbar;
    private int _dragStartY;
    private int _dragStartScrollValue;
    private Image? _headerLogoImage;

    private void BuildLayout()
    {
        EnableDoubleBuffering(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BodyBackColor
        };
        root.Paint += (_, e) =>
        {
            var bounds = root.ClientRectangle;
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    bounds,
                    Color.FromArgb(22, 12, 42),
                    Color.FromArgb(8, 9, 13),
                    45f);

                var blend = new System.Drawing.Drawing2D.ColorBlend(3)
                {
                    Colors = new Color[] {
                        Color.FromArgb(22, 12, 42),  // Neon purple-violet
                        Color.FromArgb(13, 14, 25),  // Dark neon indigo/navy
                        Color.FromArgb(8, 9, 13)     // Rich dark charcoal
                    },
                    Positions = new float[] { 0.0f, 0.5f, 1.0f }
                };
                brush.InterpolationColors = blend;
                e.Graphics.FillRectangle(brush, bounds);
            }
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        root.Controls.Add(BuildHeaderPanel(), 0, 0);
        root.Controls.Add(BuildBodyPanel(), 0, 1);
        root.Controls.Add(BuildBottomNotificationPanel(), 0, 2);

        Controls.Add(root);
        InitializeClock();
        InitializeSlideTimer();
    }

    private Control BuildHeaderPanel()
    {
        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = this.ScalePadding(18, 8, 20, 8)
        };
        headerPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(75, 42, 47, 61)); // #2A2F3D with ~30% opacity for a soft, blurry look
            e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _headerLogoImage = BuildHeaderLogoImage();
        var logoBox = new PictureBox
        {
            Width = 160,
            Height = 100,
            Margin = this.ScalePadding(0, 3, 0, 0),
            Image = _headerLogoImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        _cafeNameLabel.Text = CafeDisplayName.ToUpper();
        _cafeNameLabel.AutoSize = true;
        _cafeNameLabel.UseCompatibleTextRendering = true;
        _cafeNameLabel.ForeColor = Color.Transparent;
        _cafeNameLabel.Font = new Font("Segoe UI", 22f, FontStyle.Bold);
        _cafeNameLabel.Paint += (sender, e) =>
        {
            if (sender is not Label lbl) return;
            if (lbl.Width <= 0 || lbl.Height <= 0 || string.IsNullOrEmpty(lbl.Text)) return;
            
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                lbl.ClientRectangle,
                Color.White,
                Color.FromArgb(216, 180, 254),
                System.Drawing.Drawing2D.LinearGradientMode.Vertical);
                
            e.Graphics.DrawString(lbl.Text, lbl.Font, brush, 0, 0);
        };

        _headerSectionLabel.Text = I18n.Launcher.HeaderSectionTitle;
        _headerSectionLabel.AutoSize = true;
        _headerSectionLabel.ForeColor = Color.FromArgb(139, 147, 167); // #8B93A7
        _headerSectionLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        var cafeTextPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = this.ScalePadding(10, 16, 0, 0)
        };
        cafeTextPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        cafeTextPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cafeTextPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        cafeTextPanel.Controls.Add(_cafeNameLabel, 0, 0);
        cafeTextPanel.Controls.Add(_headerSectionLabel, 0, 1);

        var leftPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false
        };
        leftPanel.Controls.Add(logoBox);
        leftPanel.Controls.Add(cafeTextPanel);

        var searchCenterPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 1
        };
        searchCenterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        searchCenterPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var searchHost = new Panel
        {
            Width = 420,
            Height = 38,
            BackColor = Color.Transparent,
            Padding = this.ScalePadding(38, 9, 14, 9),
            Margin = this.ScalePadding(0),
            Anchor = AnchorStyles.None
        };
        searchHost.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var borderPath = CreateRoundRectPath(new Rectangle(0, 0, searchHost.Width - 1, searchHost.Height - 1), 18);
            using var pen = new Pen(Color.FromArgb(42, 47, 61)); // #2A2F3D
            e.Graphics.DrawPath(pen, borderPath);

            // Draw magnifying glass icon manually
            using var iconPen = new Pen(Color.FromArgb(139, 147, 167), 1.8f); // #8B93A7
            e.Graphics.DrawEllipse(iconPen, 15, 14, 9, 9);
            e.Graphics.DrawLine(iconPen, 22, 21, 26, 25);
        };

        _searchTextBox.Dock = DockStyle.Fill;
        _searchTextBox.BorderStyle = BorderStyle.None;
        _searchTextBox.BackColor = Color.FromArgb(16, 13, 31);
        _searchTextBox.ForeColor = Color.FromArgb(230, 232, 239);
        _searchTextBox.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _searchTextBox.PlaceholderText = "T\u00ecm ki\u1ebfm game...";
        _searchTextBox.TextChanged += (_, _) => ApplyFiltersAndRenderCards();
        searchHost.Controls.Add(_searchTextBox);
        searchCenterPanel.Controls.Add(searchHost, 0, 0);

        var quickActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = this.ScalePadding(0, 6, 0, 0)
        };
        quickActions.Controls.Add(CreateHeaderLinkButton(I18n.Launcher.QuickLinkYoutubeText, I18n.Launcher.QuickLinkYoutubeTooltip, I18n.Launcher.QuickLinkYoutubeUrl, Color.FromArgb(239, 68, 68), Color.FromArgb(185, 28, 28))); // red
        quickActions.Controls.Add(CreateHeaderLinkButton(I18n.Launcher.QuickLinkFacebookText, I18n.Launcher.QuickLinkFacebookTooltip, I18n.Launcher.QuickLinkFacebookUrl, Color.FromArgb(59, 130, 246), Color.FromArgb(29, 78, 216))); // blue
        quickActions.Controls.Add(CreateHeaderLinkButton(I18n.Launcher.QuickLinkWebText, I18n.Launcher.QuickLinkWebTooltip, I18n.Launcher.QuickLinkWebUrl, Color.FromArgb(16, 185, 129), Color.FromArgb(4, 120, 87))); // emerald
        
        quickActions.Controls.Add(CreateHeaderIconLabel("\uE962", "C\u00e0i \u0111\u1eb7t chu\u1ed9t", () =>
        {
            try { Process.Start(new ProcessStartInfo("control", "main.cpl") { UseShellExecute = true }); } catch { }
        })); // Mouse
        quickActions.Controls.Add(CreateHeaderIconLabel("\uE767", "C\u00e0i \u0111\u1eb7t \u00e2m thanh", () =>
        {
            try { Process.Start(new ProcessStartInfo("sndvol") { UseShellExecute = true }); } catch { }
        })); // Volume

        headerLayout.Controls.Add(leftPanel, 0, 0);
        headerLayout.Controls.Add(searchCenterPanel, 1, 0);
        headerLayout.Controls.Add(quickActions, 2, 0);
        headerPanel.Controls.Add(headerLayout);
        return headerPanel;
    }

    private Control BuildBodyPanel()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 162f));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        body.Controls.Add(BuildSidebarPanel(), 0, 0);
        body.Controls.Add(BuildGamesPanel(), 1, 0);
        return body;
    }

    private Control BuildSidebarPanel()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = this.ScalePadding(10, 16, 10, 12)
        };
        sidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(75, 42, 47, 61)); // #2A2F3D with ~30% opacity for a soft, blurry look
            e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };

        var vipPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 150,
            BackColor = Color.Transparent
        };
        vipPanel.Paint += (sender, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = CreateRoundRectPath(new Rectangle(0, 0, vipPanel.Width - 1, vipPanel.Height - 1), 8);
            using var fill = new System.Drawing.Drawing2D.LinearGradientBrush(
                vipPanel.ClientRectangle,
                Color.FromArgb(30, 245, 158, 11), // Amber/Gold tint
                Color.FromArgb(10, 245, 158, 11),
                90f);
            g.FillPath(fill, path);

            using var pen = new Pen(Color.FromArgb(80, 245, 158, 11), 1f);
            g.DrawPath(pen, path);
        };

        var vipLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Padding = this.ScalePadding(8, 12, 8, 8)
        };
        vipLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
        vipLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
        vipLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        vipLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var vipIcon = new Label
        {
            Text = "\uE735", // Star
            Font = new Font("Segoe MDL2 Assets", 15f, FontStyle.Regular),
            ForeColor = Color.FromArgb(252, 211, 77),
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = this.ScalePadding(0)
        };

        var vipTitle = new Label
        {
            Text = "H\u1ed8I VI\u00caN VIP",
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(252, 211, 77),
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = this.ScalePadding(0)
        };

        var vipDesc = new Label
        {
            Text = "Tr\u1edf th\u00e0nh h\u1ed9i vi\u00ean\nnh\u1eadn nhi\u1ec1u \u01b0u \u0111\u00e3i h\u01a1n v\u1ec1\ngi\u1edd ch\u01a1i v\u00e0 \u0111i\u1ec3m t\u00edch l\u0169y.",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(200, 205, 215),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopCenter,
            Margin = this.ScalePadding(0, 2, 0, 0)
        };

        vipLayout.Controls.Add(vipIcon, 0, 0);
        vipLayout.Controls.Add(vipTitle, 0, 1);
        vipLayout.Controls.Add(vipDesc, 0, 2);
        vipPanel.Controls.Add(vipLayout);

        var spacer = new Panel { Dock = DockStyle.Bottom, Height = 12, BackColor = Color.Transparent };

        _categoryListPanel.Dock = DockStyle.Fill;
        _categoryListPanel.FlowDirection = FlowDirection.TopDown;
        _categoryListPanel.WrapContents = false;
        _categoryListPanel.AutoScroll = true;
        _categoryListPanel.BackColor = Color.Transparent;
        _categoryListPanel.Padding = this.ScalePadding(0, 6, 4, 4);
        EnableDoubleBuffering(_categoryListPanel);

        sidebar.Controls.Add(_categoryListPanel);
        sidebar.Controls.Add(spacer);
        sidebar.Controls.Add(vipPanel);
        BuildCategoryButtons(Array.Empty<string>());
        return sidebar;
    }

    private Control BuildGamesPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Padding = this.ScalePadding(0, 18, 0, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 242f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var hotSectionPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        var hotTopBar = new TableLayoutPanel
        {
            Height = 42,
            Dock = DockStyle.Top,
            ColumnCount = 2,
            Padding = this.ScalePadding(32, 0, 22, 0),
            BackColor = Color.Transparent
        };
        hotTopBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        hotTopBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var hotTitlePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = this.ScalePadding(0),
            Padding = this.ScalePadding(0)
        };

        var fireIcon = new Label
        {
            Text = "🔥",
            ForeColor = Color.FromArgb(255, 87, 34), // Fiery Red/Orange
            AutoSize = true,
            Font = new Font("Segoe UI Emoji", 13f, FontStyle.Regular),
            Margin = this.ScalePadding(0, 6, 4, 0)
        };

        var hotTitleText = new Label
        {
            Text = "ĐỀ XUẤT CHO BẠN",
            ForeColor = Color.White,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
            Margin = this.ScalePadding(0, 8, 0, 0)
        };

        hotTitlePanel.Controls.Add(fireIcon);
        hotTitlePanel.Controls.Add(hotTitleText);

        _hotSortLabel.AutoSize = true;
        _hotSortLabel.Anchor = AnchorStyles.Right;
        _hotSortLabel.Cursor = Cursors.Hand;
        _hotSortLabel.ForeColor = Color.FromArgb(139, 147, 167);
        _hotSortLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        _hotSortLabel.Click += (_, _) => ToggleSortOrder();
        _hotSortLabel.Visible = false;

        hotTopBar.Controls.Add(hotTitlePanel, 0, 0);
        hotTopBar.Controls.Add(_hotSortLabel, 1, 0);

        _hotCardsViewport.Location = this.ScalePoint(32, 42);
        _hotCardsViewport.Size = this.ScaleSize(1312, 200);
        _hotCardsViewport.BackColor = Color.Transparent;
        _hotCardsViewport.AutoScroll = false;

        _hotCardsPanel.Location = this.ScalePoint(0, 0);
        _hotCardsPanel.AutoScroll = false;
        _hotCardsPanel.WrapContents = false;
        _hotCardsPanel.FlowDirection = FlowDirection.LeftToRight;
        _hotCardsPanel.Padding = this.ScalePadding(0, 8, 0, 8);
        _hotCardsPanel.Margin = this.ScalePadding(0);
        _hotCardsPanel.BackColor = Color.Transparent;
        _hotCardsPanel.AutoSize = true;
        _hotCardsPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        EnableDoubleBuffering(_hotCardsPanel);

        _hotCardsViewport.Controls.Add(_hotCardsPanel);

        // Left Carousel Button (Outside viewport, in the left 32px padding zone)
        _hotLeftBtn.Size = this.ScaleSize(32, 44);
        _hotLeftBtn.FlatStyle = FlatStyle.Flat;
        _hotLeftBtn.FlatAppearance.BorderSize = 0;
        _hotLeftBtn.FlatAppearance.MouseOverBackColor = Color.Transparent;
        _hotLeftBtn.FlatAppearance.MouseDownBackColor = Color.Transparent;
        _hotLeftBtn.Font = new Font("Segoe MDL2 Assets", 12f, FontStyle.Bold);
        _hotLeftBtn.Text = "\uE76B"; // ChevronLeft
        _hotLeftBtn.Cursor = Cursors.Hand;
        _hotLeftBtn.Paint += (sender, e) => DrawCarouselNavButton(sender, e);
        _hotLeftBtn.MouseEnter += (s, e) => _hotLeftBtn.Invalidate();
        _hotLeftBtn.MouseLeave += (s, e) => _hotLeftBtn.Invalidate();
        _hotLeftBtn.Click += (_, _) => SlideCarousel(164); // Slide left

        // Right Carousel Button (Outside viewport, in the right 22px padding zone)
        _hotRightBtn.Size = this.ScaleSize(32, 44);
        _hotRightBtn.FlatStyle = FlatStyle.Flat;
        _hotRightBtn.FlatAppearance.BorderSize = 0;
        _hotRightBtn.FlatAppearance.MouseOverBackColor = Color.Transparent;
        _hotRightBtn.FlatAppearance.MouseDownBackColor = Color.Transparent;
        _hotRightBtn.Font = new Font("Segoe MDL2 Assets", 12f, FontStyle.Bold);
        _hotRightBtn.Text = "\uE76C"; // ChevronRight
        _hotRightBtn.Cursor = Cursors.Hand;
        _hotRightBtn.Paint += (sender, e) => DrawCarouselNavButton(sender, e);
        _hotRightBtn.MouseEnter += (s, e) => _hotRightBtn.Invalidate();
        _hotRightBtn.MouseLeave += (s, e) => _hotRightBtn.Invalidate();
        _hotRightBtn.Click += (_, _) => SlideCarousel(-164); // Slide right

        hotSectionPanel.Controls.Add(hotTopBar);
        hotSectionPanel.Controls.Add(_hotLeftBtn);
        hotSectionPanel.Controls.Add(_hotRightBtn);
        hotSectionPanel.Controls.Add(_hotCardsViewport);

        // Make sure buttons draw on top of everything
        _hotLeftBtn.BringToFront();
        _hotRightBtn.BringToFront();

        hotSectionPanel.Resize += (_, _) =>
        {
            int topBarHeight = this.ScaleDpi(42);
            int maxWidth = this.ScaleDpi(1312);
            int paddingRight = this.ScaleDpi(54);

            _hotCardsViewport.Width = Math.Min(maxWidth, hotSectionPanel.Width - paddingRight);
            _hotCardsViewport.Height = hotSectionPanel.Height - topBarHeight;
            
            // Vertically center left/right buttons and place them in outer margins
            _hotLeftBtn.Location = new Point(0, topBarHeight + (_hotCardsViewport.Height - _hotLeftBtn.Height) / 2);
            _hotRightBtn.Location = new Point(hotSectionPanel.Width - _hotRightBtn.Width, topBarHeight + (_hotCardsViewport.Height - _hotRightBtn.Height) / 2);
            
            int minLeft = _hotCardsViewport.Width - _hotCardsPanel.Width;
            if (minLeft > 0) minLeft = 0;

            if (_hotCardsPanel.Left < minLeft)
            {
                _hotCardsPanel.Left = minLeft;
            }
            if (_slideTargetLeft < minLeft) _slideTargetLeft = minLeft;
            if (_slideTargetLeft > 0) _slideTargetLeft = 0;

            UpdateCarouselButtonsVisibility();
        };

        _normalCardsPanel.Dock = DockStyle.None;
        _normalCardsPanel.AutoScroll = true;
        _normalCardsPanel.WrapContents = true;
        _normalCardsPanel.FlowDirection = FlowDirection.LeftToRight;
        _normalCardsPanel.Padding = this.ScalePadding(0, 8, SystemInformation.VerticalScrollBarWidth, 8);
        _normalCardsPanel.Margin = this.ScalePadding(0);
        _normalCardsPanel.BackColor = Color.Transparent;
        EnableDoubleBuffering(_normalCardsPanel);

        layout.Controls.Add(hotSectionPanel, 0, 0);
        layout.Controls.Add(BuildSectionPanel("T\u1ea4T C\u1ea2 GAME", BuildNormalCardsScrollWrapper(), _allSortLabel), 0, 1);
        return layout;
    }

    private Control BuildSectionPanel(string title, Control bodyControl, Label sortLabel)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = this.ScalePadding(0),
            Margin = this.ScalePadding(0, 0, 0, 8)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = this.ScalePadding(32, 0, 22, 0)
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold)
        };

        sortLabel.AutoSize = true;
        sortLabel.Anchor = AnchorStyles.Right;
        sortLabel.Cursor = Cursors.Hand;
        sortLabel.ForeColor = Color.FromArgb(139, 147, 167); // #8B93A7
        sortLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        sortLabel.Click += (_, _) => ToggleSortOrder();
        sortLabel.Visible = false;

        topBar.Controls.Add(titleLabel, 0, 0);
        topBar.Controls.Add(sortLabel, 1, 0);
        layout.Controls.Add(topBar, 0, 0);

        bodyControl.Margin = this.ScalePadding(32, 0, 22, 0);
        layout.Controls.Add(bodyControl, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildBottomNotificationPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = this.ScalePadding(14, 6, 14, 6)
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(75, 42, 47, 61)); // #2A2F3D with ~30% opacity for a soft, blurry look
            e.Graphics.DrawLine(pen, 0, 0, panel.Width, 0);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _bannerMessageLabel.Dock = DockStyle.Fill;
        _bannerMessageLabel.TextAlign = ContentAlignment.MiddleLeft;
        _bannerMessageLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _bannerMessageLabel.ForeColor = Color.FromArgb(230, 232, 239); // #E6E8EF
        _bannerMessageLabel.BackColor = Color.Transparent;
        _bannerMessageLabel.Text = "👋  " + I18n.Launcher.DefaultBannerMessage;
        _bannerMessageLabel.Visible = true;
        _bannerMessageLabel.Padding = this.ScalePadding(0, 0, 0, 1);

        var rightPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = this.ScalePadding(0),
            Padding = this.ScalePadding(0)
        };

        var dotLabel = new Label
        {
            AutoSize = true,
            Text = "\u25CF",
            ForeColor = Color.FromArgb(139, 92, 246), // #8B5CF6
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Margin = this.ScalePadding(0, 5, 6, 0)
        };

        _footerMachineLabel.AutoSize = true;
        _footerMachineLabel.ForeColor = Color.FromArgb(139, 147, 167); // #8B93A7
        _footerMachineLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _footerMachineLabel.Margin = this.ScalePadding(0, 4, 12, 0);
        _footerMachineLabel.Text = Environment.MachineName;

        var barLabel = new Label
        {
            AutoSize = true,
            Text = "|",
            ForeColor = Color.FromArgb(42, 47, 61), // #2A2F3D
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            Margin = this.ScalePadding(0, 3, 12, 0)
        };

        _footerClockLabel.AutoSize = true;
        _footerClockLabel.ForeColor = Color.White;
        _footerClockLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        _footerClockLabel.Margin = this.ScalePadding(0, 4, 0, 0);

        rightPanel.Controls.Add(dotLabel);
        rightPanel.Controls.Add(_footerMachineLabel);
        rightPanel.Controls.Add(barLabel);
        rightPanel.Controls.Add(_footerClockLabel);

        layout.Controls.Add(_bannerMessageLabel, 0, 0);
        layout.Controls.Add(rightPanel, 1, 0);
        panel.Controls.Add(layout);
        return panel;
    }

    private void InitializeClock()
    {
        _clockTimer.Interval = 1000;
        _clockTimer.Tick += (_, _) => UpdateClockLabel();
        _clockTimer.Start();
        UpdateClockLabel();
    }

    private void UpdateClockLabel()
    {
        _footerClockLabel.Text = DateTime.Now.ToString("hh:mm tt");
    }

    private static Image BuildHeaderLogoImage()
    {
        var assembly = typeof(MainForm).Assembly;
        using var stream = assembly.GetManifestResourceStream("GameLauncher.Client.Resources.logo-client.png");
        if (stream is not null)
        {
            using var source = Image.FromStream(stream);
            return new Bitmap(source);
        }

        const int size = 40;
        using var fallback = SystemIcons.Shield.ToBitmap();
        var fallbackBitmap = new Bitmap(size, size);
        using var fallbackGraphics = Graphics.FromImage(fallbackBitmap);
        fallbackGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        fallbackGraphics.Clear(Color.Transparent);
        using var circleBrush = new SolidBrush(Color.FromArgb(34, 211, 238));
        fallbackGraphics.FillEllipse(circleBrush, 0, 0, size - 1, size - 1);
        fallbackGraphics.DrawImage(fallback, 6, 6, size - 12, size - 12);
        return fallbackBitmap;
    }

    private Button CreateCategoryButton(string category)
    {
        var button = new Button
        {
            Text = string.Empty,
            Width = 134,
            Height = 44,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = this.ScalePadding(0, 0, 0, 8),
            Padding = this.ScalePadding(0)
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.Transparent;
        button.FlatAppearance.MouseDownBackColor = Color.Transparent;
        
        button.Paint += (sender, e) =>
        {
            if (sender is not Button btn) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            
            var isSelected = string.Equals(category, _selectedCategory, StringComparison.OrdinalIgnoreCase);
            
            using (var path = CreateRoundRectPath(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), 8))
            {
                if (isSelected)
                {
                    using (var fill = new System.Drawing.Drawing2D.LinearGradientBrush(
                        new Rectangle(0, 0, btn.Width, btn.Height),
                        Color.FromArgb(46, 27, 78),   // #2E1B4E (deep violet)
                        Color.FromArgb(24, 17, 36),   // #181124 (very dark violet)
                        90f))
                    {
                        g.FillPath(fill, path);
                    }
                }
                else if (btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position)))
                {
                    using (var fill = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                    {
                        g.FillPath(fill, path);
                    }
                }
            }
            
            string iconGlyph = GetCategoryIconGlyph(category);
            var iconFont = new Font("Segoe MDL2 Assets", 11.5f, FontStyle.Regular);
            var textFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            
            var iconColor = isSelected ? Color.White : Color.FromArgb(139, 147, 167); // #8B93A7
            var textColor = isSelected ? Color.White : Color.FromArgb(230, 232, 239); // #E6E8EF
            
            var iconSize = g.MeasureString(iconGlyph, iconFont);
            var iconY = (btn.Height - iconSize.Height) / 2;
            using (var brush = new SolidBrush(iconColor))
            {
                g.DrawString(iconGlyph, iconFont, brush, 14, iconY + 1);
            }
            
            var textSize = g.MeasureString(category, textFont);
            var textY = (btn.Height - textSize.Height) / 2;
            using (var brush = new SolidBrush(textColor))
            {
                g.DrawString(category, textFont, brush, 38, textY);
            }
            
            iconFont.Dispose();
            textFont.Dispose();
        };
        
        button.MouseEnter += (s, e) => button.Invalidate();
        button.MouseLeave += (s, e) => button.Invalidate();
        
        return button;
    }

    private static string GetCategoryIconGlyph(string text)
    {
        if (string.Equals(text, I18n.Launcher.DefaultCategory, StringComparison.OrdinalIgnoreCase))
        {
            return "\uE8A9"; // All apps icon
        }
        if (string.Equals(text, "Hot", StringComparison.OrdinalIgnoreCase))
        {
            return "\uE735"; // Star
        }
        if (text.Contains("Online", StringComparison.OrdinalIgnoreCase))
        {
            return "\uE12B"; // Cloud icon
        }
        if (text.Contains("Offline", StringComparison.OrdinalIgnoreCase))
        {
            return "\uEA14"; // PC / Desktop icon
        }
        if (text.Contains("Tools", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Cong cu", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("C\u00f4ng c\u1ee5", StringComparison.OrdinalIgnoreCase))
        {
            return "\uE713"; // Settings gear icon
        }
        return "\uE712"; // More/Dot
    }

    private static Label CreateHeaderIconLabel(string iconGlyph, string tooltip, Action onClick)
    {
        var lbl = new Label
        {
            Text = iconGlyph,
            Width = 32,
            Height = 32,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(8, 0, 0, 0),
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(139, 147, 167),
            Font = new Font("Segoe MDL2 Assets", 14f, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        
        lbl.MouseEnter += (s, e) => lbl.ForeColor = Color.White;
        lbl.MouseLeave += (s, e) => lbl.ForeColor = Color.FromArgb(139, 147, 167);
        
        var tt = new ToolTip();
        tt.SetToolTip(lbl, tooltip);
        
        // Example placeholders for click events - these can be wired up later
        lbl.Click += (s, e) => onClick?.Invoke();
        
        return lbl;
    }

    private static Button CreateHeaderLinkButton(string text, string tooltip, string url, Color gradientStart, Color gradientEnd)
    {
        var button = new Button
        {
            Text = text,
            Width = 58,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(8, 0, 0, 0),
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.Transparent;
        button.FlatAppearance.MouseDownBackColor = Color.Transparent;
        button.Paint += (sender, e) =>
        {
            if (sender is not Button btn) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            
            var rect = btn.ClientRectangle;
            using (var bgBrush = new SolidBrush(Color.Transparent))
            {
                g.FillRectangle(bgBrush, rect);
            }
            
            var isHover = btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position));
            
            using (var path = CreateRoundRectPath(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), 6))
            {
                // Draw base gradient
                using (var fill = new System.Drawing.Drawing2D.LinearGradientBrush(btn.ClientRectangle, gradientStart, gradientEnd, 45f))
                {
                    g.FillPath(fill, path);
                }
                
                // Add hover overlay
                if (isHover)
                {
                    using (var hoverOverlay = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    {
                        g.FillPath(hoverOverlay, path);
                    }
                }
                

            }
            
            var textSize = g.MeasureString(btn.Text, btn.Font);
            g.DrawString(
                btn.Text,
                btn.Font,
                Brushes.White,
                (btn.Width - textSize.Width) / 2,
                (btn.Height - textSize.Height) / 2);
        };
        button.MouseEnter += (s, e) => button.Invalidate();
        button.MouseLeave += (s, e) => button.Invalidate();

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

        var toolTipControl = new ToolTip();
        toolTipControl.SetToolTip(button, tooltip);
        return button;
    }

    private static void EnableDoubleBuffering(Control control)
    {
        if (SystemInformation.TerminalServerSession)
        {
            return;
        }

        try
        {
            var property = typeof(Control).GetProperty(
                "DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);
            property?.SetValue(control, true, null);
        }
        catch
        {
            // Keep default buffering if reflection is blocked.
        }
    }

    private static void CenterControl(Control hostControl, Control childControl)
    {
        childControl.Left = Math.Max(0, (hostControl.ClientSize.Width - childControl.Width) / 2);
        childControl.Top = Math.Max(0, (hostControl.ClientSize.Height - childControl.Height) / 2);
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundRectPath(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void DrawCarouselNavButton(object? sender, PaintEventArgs e)
    {
        if (sender is not Button btn) return;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var isHover = btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position));
        
        if (isHover)
        {
            using var fill = new SolidBrush(Color.FromArgb(30, 139, 92, 246)); // Subtly transparent neon purple
            using var pen = new Pen(Color.FromArgb(100, 139, 92, 246), 1.5f);
            g.FillEllipse(fill, 2, (btn.Height - 28) / 2, 28, 28);
            g.DrawEllipse(pen, 2, (btn.Height - 28) / 2, 28, 28);
        }
        else
        {
            using var fill = new SolidBrush(Color.FromArgb(10, 255, 255, 255));
            g.FillEllipse(fill, 2, (btn.Height - 28) / 2, 28, 28);
        }

        var arrowColor = isHover ? Color.White : Color.FromArgb(139, 147, 167);
        using var brush = new SolidBrush(arrowColor);
        var size = g.MeasureString(btn.Text, btn.Font);
        g.DrawString(btn.Text, btn.Font, brush, (btn.Width - size.Width) / 2, (btn.Height - size.Height) / 2 + 0.5f);
    }

    private void InitializeSlideTimer()
    {
        _slideTimer.Interval = 15;
        _slideTimer.Tick += SlideTimer_Tick;
    }

    private void SlideTimer_Tick(object? sender, EventArgs e)
    {
        _slideProgress += 0.05f; // ~300ms transition time
        if (_slideProgress >= 1.0f)
        {
            _slideProgress = 1.0f;
            _hotCardsPanel.Left = _slideTargetLeft;
            _slideTimer.Stop();
        }
        else
        {
            float t = EaseOutCubic(_slideProgress);
            _hotCardsPanel.Left = (int)(_slideStartLeft + (_slideTargetLeft - _slideStartLeft) * t);
        }
    }

    private static float EaseOutCubic(float t)
    {
        return 1f - (float)Math.Pow(1f - t, 3f);
    }

    private void SlideCarousel(int amount)
    {
        int currentTarget = _slideTimer.Enabled ? _slideTargetLeft : _hotCardsPanel.Left;
        int newTargetLeft = currentTarget + amount;
        
        int minLeft = _hotCardsViewport.Width - _hotCardsPanel.Width;
        if (minLeft > 0) minLeft = 0;
        
        if (newTargetLeft < minLeft) newTargetLeft = minLeft;
        if (newTargetLeft > 0) newTargetLeft = 0;
        
        if (newTargetLeft == _hotCardsPanel.Left && !_slideTimer.Enabled)
        {
            return;
        }
        
        _slideStartLeft = _hotCardsPanel.Left;
        _slideTargetLeft = newTargetLeft;
        _slideProgress = 0f;
        
        if (!_slideTimer.Enabled)
        {
            _slideTimer.Start();
        }
    }

    private void UpdateCarouselButtonsVisibility()
    {
        int cardCount = _hotCards.Count;
        bool needCarousel = cardCount > 8;
        
        _hotLeftBtn.Visible = needCarousel;
        _hotRightBtn.Visible = needCarousel;
        
        if (!needCarousel)
        {
            _slideTimer.Stop();
            _hotCardsPanel.Left = 0;
            _slideTargetLeft = 0;
            _slideStartLeft = 0;
            _slideProgress = 0f;
        }
    }

    private Control BuildNormalCardsScrollWrapper()
    {
        var wrapper = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16f)); // Space for scrollbar track & hover zone

        var viewport = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = this.ScalePadding(0)
        };

        _normalCardsPanel.Dock = DockStyle.None;
        _normalCardsPanel.Padding = this.ScalePadding(0, 8, SystemInformation.VerticalScrollBarWidth, 8);

        viewport.Controls.Add(_normalCardsPanel);

        viewport.Resize += (s, e) =>
        {
            _normalCardsPanel.Size = new Size(viewport.Width + SystemInformation.VerticalScrollBarWidth, viewport.Height);
        };

        _customScrollbar.Width = 16;
        _customScrollbar.Dock = DockStyle.Fill;
        _customScrollbar.BackColor = Color.Transparent;
        _customScrollbar.Margin = this.ScalePadding(0, 8, 0, 8);
        _customScrollbar.Cursor = Cursors.Hand;

        _customScrollbar.Paint += CustomScrollbar_Paint;
        _customScrollbar.MouseDown += CustomScrollbar_MouseDown;
        _customScrollbar.MouseMove += CustomScrollbar_MouseMove;
        _customScrollbar.MouseUp += CustomScrollbar_MouseUp;
        _customScrollbar.MouseEnter += (s, e) => _customScrollbar.Invalidate();
        _customScrollbar.MouseLeave += (s, e) => _customScrollbar.Invalidate();

        _normalCardsPanel.Scroll += (s, e) => _customScrollbar.Invalidate();
        _normalCardsPanel.MouseWheel += (s, e) => _customScrollbar.Invalidate();
        _normalCardsPanel.Layout += (s, e) => _customScrollbar.Invalidate();
        _normalCardsPanel.Paint += (s, e) => _customScrollbar.Invalidate();

        wrapper.Controls.Add(viewport, 0, 0);
        wrapper.Controls.Add(_customScrollbar, 1, 0);

        return wrapper;
    }

    private int GetScrollThumbHeight()
    {
        int viewHeight = _normalCardsPanel.Height;
        int totalHeight = _normalCardsPanel.DisplayRectangle.Height;
        if (totalHeight <= viewHeight || viewHeight <= 0) return 0;

        int trackHeight = _customScrollbar.Height;
        int thumbHeight = (int)((double)viewHeight / totalHeight * trackHeight);
        return Math.Max(30, thumbHeight); // Minimum 30px thumb height for usability
    }

    private int GetScrollThumbY()
    {
        int viewHeight = _normalCardsPanel.Height;
        int totalHeight = _normalCardsPanel.DisplayRectangle.Height;
        int maxScroll = totalHeight - viewHeight;
        if (maxScroll <= 0) return 0;

        int trackHeight = _customScrollbar.Height;
        int thumbHeight = GetScrollThumbHeight();
        int scrollVal = -_normalCardsPanel.AutoScrollPosition.Y; // AutoScrollPosition.Y is negative in WinForms

        return (int)((double)scrollVal / maxScroll * (trackHeight - thumbHeight));
    }

    private void CustomScrollbar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        int thumbY = GetScrollThumbY();
        int thumbHeight = GetScrollThumbHeight();

        if (e.Y >= thumbY && e.Y <= thumbY + thumbHeight)
        {
            _isDraggingScrollbar = true;
            _dragStartY = e.Y;
            _dragStartScrollValue = -_normalCardsPanel.AutoScrollPosition.Y;
            _customScrollbar.Capture = true;
            _customScrollbar.Invalidate();
        }
        else
        {
            // Jump scrolling (Page Up / Page Down)
            int viewHeight = _normalCardsPanel.Height;
            int scrollVal = -_normalCardsPanel.AutoScrollPosition.Y;
            if (e.Y < thumbY)
            {
                ScrollToValue(scrollVal - viewHeight);
            }
            else
            {
                ScrollToValue(scrollVal + viewHeight);
            }
        }
    }

    private void CustomScrollbar_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDraggingScrollbar)
        {
            _customScrollbar.Invalidate();
            return;
        }

        int deltaY = e.Y - _dragStartY;
        int viewHeight = _normalCardsPanel.Height;
        int totalHeight = _normalCardsPanel.DisplayRectangle.Height;
        int maxScroll = totalHeight - viewHeight;
        if (maxScroll <= 0) return;

        int trackHeight = _customScrollbar.Height;
        int thumbHeight = GetScrollThumbHeight();
        int maxThumbY = trackHeight - thumbHeight;
        if (maxThumbY <= 0) return;

        double scrollDelta = (double)deltaY / maxThumbY * maxScroll;
        int newScrollVal = _dragStartScrollValue + (int)scrollDelta;

        ScrollToValue(newScrollVal);
    }

    private void CustomScrollbar_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_isDraggingScrollbar)
        {
            _isDraggingScrollbar = false;
            _customScrollbar.Capture = false;
            _customScrollbar.Invalidate();
        }
    }

    private void ScrollToValue(int value)
    {
        int min = 0;
        int max = _normalCardsPanel.DisplayRectangle.Height - _normalCardsPanel.Height;
        if (max < 0) max = 0;

        int clampedVal = Math.Clamp(value, min, max);

        _normalCardsPanel.AutoScrollPosition = this.ScalePoint(0, clampedVal);
        _customScrollbar.Invalidate();
    }

    private void CustomScrollbar_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int thumbHeight = GetScrollThumbHeight();
        if (thumbHeight <= 0) return;

        int thumbY = GetScrollThumbY();
        int cellWidth = _customScrollbar.Width;
        const int scrollbarWidth = 6;
        int scrollbarX = (cellWidth - scrollbarWidth) / 2;

        // Draw track: subtly dark transparent track
        using (var trackBrush = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
        {
            using (var trackPath = CreateRoundRectPath(new Rectangle(scrollbarX, 0, scrollbarWidth, _customScrollbar.Height), scrollbarWidth / 2))
            {
                g.FillPath(trackBrush, trackPath);
            }
        }

        // Draw thumb: changes color on hover and drag
        var clientPos = _customScrollbar.PointToClient(Cursor.Position);
        bool isHover = _customScrollbar.ClientRectangle.Contains(clientPos);

        Color thumbColor;
        if (_isDraggingScrollbar)
        {
            thumbColor = Color.FromArgb(190, 139, 92, 246); // Bright neon purple
        }
        else if (isHover)
        {
            thumbColor = Color.FromArgb(140, 139, 92, 246); // Semi-bright neon purple
        }
        else
        {
            thumbColor = Color.FromArgb(40, 255, 255, 255);  // Translucent glassmorphic white
        }

        using (var thumbBrush = new SolidBrush(thumbColor))
        {
            using (var thumbPath = CreateRoundRectPath(new Rectangle(scrollbarX, thumbY, scrollbarWidth, thumbHeight), scrollbarWidth / 2))
            {
                g.FillPath(thumbBrush, thumbPath);
            }
        }
    }
}
