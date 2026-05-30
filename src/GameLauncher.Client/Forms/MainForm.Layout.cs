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
    private readonly Dictionary<string, Button> _categoryButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer _clockTimer = new();
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
            using var brush = new SolidBrush(BodyBackColor);
            e.Graphics.FillRectangle(brush, root.ClientRectangle);
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        root.Controls.Add(BuildHeaderPanel(), 0, 0);
        root.Controls.Add(BuildBodyPanel(), 0, 1);
        root.Controls.Add(BuildBottomNotificationPanel(), 0, 2);

        Controls.Add(root);
        InitializeClock();
    }

    private Control BuildHeaderPanel()
    {
        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = HeaderBackColor,
            Padding = new Padding(18, 8, 20, 8)
        };
        headerPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(42, 47, 61)); // #2A2F3D
            e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _headerLogoImage = BuildHeaderLogoImage();
        var logoBox = new Label
        {
            Width = 44,
            Height = 44,
            Margin = new Padding(0, 3, 0, 0),
            Font = new Font("Segoe MDL2 Assets", 22f, FontStyle.Regular),
            ForeColor = Color.White,
            Text = "\uE7FC",
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };

        _cafeNameLabel.Text = CafeDisplayName.ToUpper();
        _cafeNameLabel.AutoSize = true;
        _cafeNameLabel.ForeColor = Color.FromArgb(230, 232, 239); // #E6E8EF
        _cafeNameLabel.Font = new Font("Segoe UI", 14f, FontStyle.Bold);

        _headerSectionLabel.Text = I18n.Launcher.HeaderSectionTitle;
        _headerSectionLabel.AutoSize = true;
        _headerSectionLabel.ForeColor = Color.FromArgb(139, 147, 167); // #8B93A7
        _headerSectionLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        var cafeTextPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(10, 3, 0, 0)
        };
        cafeTextPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        cafeTextPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
        cafeTextPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));
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
            BackColor = Color.FromArgb(21, 24, 33), // #151821
            Padding = new Padding(38, 9, 14, 9),
            Margin = new Padding(0),
            Anchor = AnchorStyles.None
        };
        searchHost.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var borderPath = CreateRoundRectPath(new Rectangle(0, 0, searchHost.Width - 1, searchHost.Height - 1), 18);
            using var fill = new SolidBrush(Color.FromArgb(21, 24, 33)); // #151821
            using var pen = new Pen(Color.FromArgb(42, 47, 61)); // #2A2F3D
            e.Graphics.FillPath(fill, borderPath);
            e.Graphics.DrawPath(pen, borderPath);

            // Draw magnifying glass icon manually
            using var iconPen = new Pen(Color.FromArgb(139, 147, 167), 1.8f); // #8B93A7
            e.Graphics.DrawEllipse(iconPen, 15, 14, 9, 9);
            e.Graphics.DrawLine(iconPen, 22, 21, 26, 25);
        };

        _searchTextBox.Dock = DockStyle.Fill;
        _searchTextBox.BorderStyle = BorderStyle.None;
        _searchTextBox.BackColor = Color.FromArgb(21, 24, 33);
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
            Padding = new Padding(0, 6, 0, 0)
        };
        quickActions.Controls.Add(CreateHeaderLinkButton(I18n.Launcher.QuickLinkYoutubeText, I18n.Launcher.QuickLinkYoutubeTooltip, I18n.Launcher.QuickLinkYoutubeUrl));
        quickActions.Controls.Add(CreateHeaderLinkButton(I18n.Launcher.QuickLinkFacebookText, I18n.Launcher.QuickLinkFacebookTooltip, I18n.Launcher.QuickLinkFacebookUrl));
        quickActions.Controls.Add(CreateHeaderLinkButton(I18n.Launcher.QuickLinkWebText, I18n.Launcher.QuickLinkWebTooltip, I18n.Launcher.QuickLinkWebUrl));

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
            BackColor = BodyBackColor
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
            BackColor = SidebarBackColor,
            Padding = new Padding(10, 16, 10, 12)
        };
        sidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(42, 47, 61)); // #2A2F3D
            e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };

        _categoryListPanel.Dock = DockStyle.Fill;
        _categoryListPanel.FlowDirection = FlowDirection.TopDown;
        _categoryListPanel.WrapContents = false;
        _categoryListPanel.AutoScroll = true;
        _categoryListPanel.BackColor = Color.Transparent;
        _categoryListPanel.Padding = new Padding(0, 6, 4, 4);
        EnableDoubleBuffering(_categoryListPanel);

        sidebar.Controls.Add(_categoryListPanel);
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
            BackColor = BodyBackColor,
            Padding = new Padding(32, 18, 22, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 286f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _hotCardsPanel.Dock = DockStyle.Fill;
        _hotCardsPanel.AutoScroll = true;
        _hotCardsPanel.WrapContents = false;
        _hotCardsPanel.FlowDirection = FlowDirection.LeftToRight;
        _hotCardsPanel.Padding = new Padding(0, 8, 0, 8);
        _hotCardsPanel.Margin = new Padding(0);
        _hotCardsPanel.BackColor = BodyBackColor;
        EnableDoubleBuffering(_hotCardsPanel);

        _normalCardsPanel.Dock = DockStyle.Fill;
        _normalCardsPanel.AutoScroll = true;
        _normalCardsPanel.WrapContents = true;
        _normalCardsPanel.FlowDirection = FlowDirection.LeftToRight;
        _normalCardsPanel.Padding = new Padding(0, 8, 0, 8);
        _normalCardsPanel.Margin = new Padding(0);
        _normalCardsPanel.BackColor = BodyBackColor;
        EnableDoubleBuffering(_normalCardsPanel);

        layout.Controls.Add(BuildSectionPanel("GAME N\u1ed4I B\u1eacT", _hotCardsPanel, _hotSortLabel), 0, 0);
        layout.Controls.Add(BuildSectionPanel("T\u1ea4T C\u1ea2 GAME", _normalCardsPanel, _allSortLabel), 0, 1);
        return layout;
    }

    private Control BuildSectionPanel(string title, Control bodyControl, Label sortLabel)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BodyBackColor,
            Padding = new Padding(0),
            Margin = new Padding(0, 0, 0, 8)
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
            Padding = new Padding(0, 0, 0, 0)
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
        layout.Controls.Add(bodyControl, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildBottomNotificationPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BodyBackColor,
            Padding = new Padding(14, 6, 14, 6)
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(42, 47, 61)); // #2A2F3D
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
        _bannerMessageLabel.Padding = new Padding(0, 0, 0, 1);

        var rightPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        var dotLabel = new Label
        {
            AutoSize = true,
            Text = "\u25CF",
            ForeColor = Color.FromArgb(139, 92, 246), // #8B5CF6
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Margin = new Padding(0, 5, 6, 0)
        };

        _footerMachineLabel.AutoSize = true;
        _footerMachineLabel.ForeColor = Color.FromArgb(139, 147, 167); // #8B93A7
        _footerMachineLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _footerMachineLabel.Margin = new Padding(0, 4, 12, 0);
        _footerMachineLabel.Text = Environment.MachineName;

        var barLabel = new Label
        {
            AutoSize = true,
            Text = "|",
            ForeColor = Color.FromArgb(42, 47, 61), // #2A2F3D
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            Margin = new Padding(0, 3, 12, 0)
        };

        _footerClockLabel.AutoSize = true;
        _footerClockLabel.ForeColor = Color.White;
        _footerClockLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        _footerClockLabel.Margin = new Padding(0, 4, 0, 0);

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
        const int size = 40;
        var assembly = typeof(MainForm).Assembly;
        using var stream = assembly.GetManifestResourceStream("GameLauncher.Client.Resources.game-logo.png");
        if (stream is not null)
        {
            using var source = Image.FromStream(stream);
            var bitmap = new Bitmap(size, size);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            const int leftRightInset = 2;
            const int topInset = 1;
            const int bottomInset = 6;
            graphics.DrawImage(source, leftRightInset, topInset, size - (leftRightInset * 2), size - topInset - bottomInset);
            return bitmap;
        }

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
            BackColor = SidebarBackColor,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(0)
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.Transparent;
        button.FlatAppearance.MouseDownBackColor = Color.Transparent;
        
        button.Paint += (sender, e) =>
        {
            var btn = (Button)sender;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            
            var rect = btn.ClientRectangle;
            
            using (var bgBrush = new SolidBrush(SidebarBackColor))
            {
                g.FillRectangle(bgBrush, rect);
            }
            
            var isSelected = string.Equals(category, _selectedCategory, StringComparison.OrdinalIgnoreCase);
            
            using (var path = CreateRoundRectPath(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), 8))
            {
                if (isSelected)
                {
                    using (var fill = new System.Drawing.Drawing2D.LinearGradientBrush(
                        new Rectangle(0, 0, btn.Width, btn.Height),
                        Color.FromArgb(46, 27, 78),   // #2E1B4E (deep violet)
                        Color.FromArgb(24, 17, 36),   // #181124 (very dark violet)
                        0f))
                    {
                        g.FillPath(fill, path);
                    }
                }
                else if (btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position)))
                {
                    using (var fill = new SolidBrush(Color.FromArgb(28, 31, 41))) // #1C1F29
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
            return "\uE990"; // Filled gamepad icon
        }
        if (string.Equals(text, "Hot", StringComparison.OrdinalIgnoreCase))
        {
            return "\uE734";
        }
        if (text.Contains("Online", StringComparison.OrdinalIgnoreCase))
        {
            return "\uE774"; // Globe icon
        }
        if (text.Contains("Offline", StringComparison.OrdinalIgnoreCase))
        {
            return "\uE779"; // PC / Local Network icon
        }
        if (text.Contains("Tools", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Cong cu", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("C\u00f4ng c\u1ee5", StringComparison.OrdinalIgnoreCase))
        {
            return "\uE812"; // Wrench/Tool icon
        }
        return "\uE712";
    }

    private static Button CreateHeaderLinkButton(string text, string tooltip, string url)
    {
        var button = new Button
        {
            Text = text,
            Width = 58,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(8, 0, 0, 0),
            BackColor = Color.FromArgb(28, 31, 41), // #1C1F29
            ForeColor = Color.FromArgb(230, 232, 239), // #E6E8EF
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.Paint += (sender, e) =>
        {
            var btn = (Button)sender;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            
            var rect = btn.ClientRectangle;
            using (var bgBrush = new SolidBrush(HeaderBackColor))
            {
                g.FillRectangle(bgBrush, rect);
            }
            
            var isHover = btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position));
            var backColor = isHover ? Color.FromArgb(139, 92, 246) : Color.FromArgb(28, 31, 41); // #8B5CF6 / #1C1F29
            var borderColor = isHover ? Color.FromArgb(139, 92, 246) : Color.FromArgb(42, 47, 61); // #8B5CF6 / #2A2F3D
            
            using (var path = CreateRoundRectPath(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), 6))
            {
                using (var fill = new SolidBrush(backColor))
                {
                    g.FillPath(fill, path);
                }
                using (var pen = new Pen(borderColor, 1))
                {
                    g.DrawPath(pen, path);
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
}
