using System.Diagnostics;
using System.Reflection;
using GameUpdater.Shared.Localization;

namespace GameLauncher.Client.Forms;

public sealed partial class MainForm
{
    private static readonly Color HeaderBackColor = Color.FromArgb(4, 10, 24);
    private static readonly Color BodyBackColor = Color.FromArgb(2, 7, 20);

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
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                root.ClientRectangle,
                Color.FromArgb(2, 8, 24),
                Color.FromArgb(1, 5, 16),
                0f);
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
            using var topGlow = new Pen(Color.FromArgb(35, 84, 160));
            e.Graphics.DrawLine(topGlow, 0, 0, headerPanel.Width, 0);
            using var pen = new Pen(Color.FromArgb(24, 60, 116));
            e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));

        _headerLogoImage = BuildHeaderLogoImage();
        var logoBox = new PictureBox
        {
            Width = 44,
            Height = 44,
            Margin = new Padding(0, 3, 0, 0),
            Image = _headerLogoImage,
            SizeMode = PictureBoxSizeMode.Zoom
        };

        _cafeNameLabel.Text = CafeDisplayName;
        _cafeNameLabel.AutoSize = true;
        _cafeNameLabel.ForeColor = Color.White;
        _cafeNameLabel.Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold);

        _headerSectionLabel.Text = I18n.Launcher.HeaderSectionTitle;
        _headerSectionLabel.AutoSize = true;
        _headerSectionLabel.ForeColor = Color.FromArgb(174, 210, 255);
        _headerSectionLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);

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

        var searchCenterPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        var searchHost = new Panel
        {
            Width = 420,
            Height = 38,
            BackColor = Color.FromArgb(6, 12, 30),
            Padding = new Padding(14, 9, 14, 9),
            Margin = new Padding(0)
        };
        searchHost.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var borderPath = CreateRoundRectPath(new Rectangle(0, 0, searchHost.Width - 1, searchHost.Height - 1), 18);
            using var fill = new SolidBrush(Color.FromArgb(6, 12, 30));
            using var pen = new Pen(Color.FromArgb(79, 101, 139));
            e.Graphics.FillPath(fill, borderPath);
            e.Graphics.DrawPath(pen, borderPath);
        };

        _searchTextBox.Dock = DockStyle.Fill;
        _searchTextBox.BorderStyle = BorderStyle.None;
        _searchTextBox.BackColor = searchHost.BackColor;
        _searchTextBox.ForeColor = Color.FromArgb(233, 246, 255);
        _searchTextBox.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        _searchTextBox.PlaceholderText = "T\u00ecm ki\u1ebfm game...";
        _searchTextBox.TextChanged += (_, _) => ApplyFiltersAndRenderCards();
        searchHost.Controls.Add(_searchTextBox);
        searchCenterPanel.Controls.Add(searchHost);
        CenterControl(searchCenterPanel, searchHost);
        searchCenterPanel.Resize += (_, _) => CenterControl(searchCenterPanel, searchHost);

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
            BackColor = Color.FromArgb(5, 12, 28),
            Padding = new Padding(10, 16, 10, 12)
        };
        sidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(22, 36, 59));
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250f));
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
        panel.Paint += (_, e) =>
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                panel.ClientRectangle,
                BodyBackColor,
                Color.FromArgb(3, 9, 24),
                0f);
            e.Graphics.FillRectangle(brush, panel.ClientRectangle);
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
        sortLabel.ForeColor = Color.FromArgb(232, 238, 248);
        sortLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        sortLabel.Click += (_, _) => ToggleSortOrder();

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
            BackColor = Color.FromArgb(3, 10, 23),
            Padding = new Padding(14, 6, 14, 6)
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(35, 84, 160));
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
        _bannerMessageLabel.ForeColor = Color.White;
        _bannerMessageLabel.BackColor = Color.Transparent;
        _bannerMessageLabel.Text = I18n.Launcher.DefaultBannerMessage;
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

        _footerMachineLabel.AutoSize = true;
        _footerMachineLabel.ForeColor = Color.FromArgb(183, 209, 255);
        _footerMachineLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _footerMachineLabel.Margin = new Padding(0, 4, 18, 0);
        _footerMachineLabel.Text = Environment.MachineName;

        _footerClockLabel.AutoSize = true;
        _footerClockLabel.ForeColor = Color.White;
        _footerClockLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        _footerClockLabel.Margin = new Padding(0, 4, 0, 0);

        rightPanel.Controls.Add(_footerMachineLabel);
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

    private Button CreateCategoryButton(string text)
    {
        var button = new Button
        {
            Text = GetCategoryDisplayText(text),
            Width = 134,
            Height = 44,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(5, 12, 28),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10, 0, 0, 0)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(5, 12, 28);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(22, 32, 55);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(47, 30, 91);
        return button;
    }

    private static string GetCategoryDisplayText(string text)
    {
        if (string.Equals(text, I18n.Launcher.DefaultCategory, StringComparison.OrdinalIgnoreCase))
        {
            return "\u25B6  " + text;
        }

        if (string.Equals(text, "Hot", StringComparison.OrdinalIgnoreCase))
        {
            return "\u2606  " + text;
        }

        if (text.Contains("Online", StringComparison.OrdinalIgnoreCase))
        {
            return "\u25CE  " + text;
        }

        if (text.Contains("Phieu", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Phi", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Phi\u00eau", StringComparison.OrdinalIgnoreCase))
        {
            return "\u25C8  " + text;
        }

        if (text.Contains("Chien", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Chi\u1ebfn", StringComparison.OrdinalIgnoreCase))
        {
            return "\u2694  " + text;
        }

        if (text.Contains("Casual", StringComparison.OrdinalIgnoreCase))
        {
            return "\u2667  " + text;
        }

        if (text.Contains("Tri", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Tr\u00ed", StringComparison.OrdinalIgnoreCase))
        {
            return "\u25C9  " + text;
        }

        return "\u2723  " + text;
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
            BackColor = Color.FromArgb(10, 22, 49),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(77, 135, 223);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(34, 72, 132);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 82, 150);
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
