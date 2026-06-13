import re

def update_layout():
    with open('i:/servermanagergame/src/GameUpdater.WinForms/Forms/MainForm.Layout.cs', 'r', encoding='utf-8') as f:
        content = f.read()

    # We need to replace UpdateResourceSourceRootPathFromUi up to the end of BuildTargetsUi
    # First, let's find the start of UpdateResourceSourceRootPathFromUi
    match1_start = re.search(r'    private void UpdateResourceSourceRootPathFromUi\(\)', content)
    
    # We need to find the end of BuildTargetsUi
    # It ends with _targetsContainer.Controls.Add(addBtn);\n    }
    match1_end = re.search(r'        _targetsContainer\.Controls\.Add\(addBtn\);\n    }', content)
    
    if not match1_start or not match1_end:
        print("Could not find Target 1")
        return
        
    part1_replacement = """    private void UpdateResourceSourceRootPathFromUi()
    {
        var paths = new System.Collections.Generic.List<string>();
        foreach (Control c in _sourcesContainer.Controls)
        {
            if (c is TableLayoutPanel tlp && tlp.Controls.Count > 1 && tlp.Controls[1] is IconTextBox tb)
            {
                paths.Add(tb.Input.Text.Trim());
            }
        }
        _resourceSourceRootPath = string.Join(";", paths.System.Linq.Enumerable.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private void AddSourceRowUi(string path)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            Padding = new Padding(0, 0, 0, 16),
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
        inputWrapper.Margin = new Padding(0, 0, 10, 0);
        inputWrapper.Input.Text = path;
        if (string.IsNullOrEmpty(path)) inputWrapper.Input.PlaceholderText = $"Nhập URL nguồn IDC {count}";
        inputWrapper.Input.TextChanged += (_, _) => UpdateResourceRootsFromInputs();

        var browseBtn = new IconButton 
        { 
            DrawIcon = DrawDotsIcon,
            Margin = new Padding(0, 0, 10, 0),
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
            Margin = new Padding(0), 
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
        var paths = GetConfiguredResourceSourceRoots().System.Linq.Enumerable.ToList();
            
        if (paths.Count == 0) paths.Add("");

        foreach (var path in paths)
        {
            AddSourceRowUi(path);
        }

        var addBtn = new GameUpdater.WinForms.Controls.ModernButton { Text = "+  Thêm nguồn IDC", Size = new Size(180, 36), Margin = new Padding(180, 0, 0, 0), CornerRadius = 6, ColorType = GameUpdater.WinForms.Controls.ButtonColorType.DashedPurple, Font = new Font("Segoe UI", 10.5f) };
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
        _resourceTargetRootPath = string.Join(";", paths.System.Linq.Enumerable.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private void AddTargetRowUi(string path)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            Padding = new Padding(0, 0, 0, 16),
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
        inputWrapper.Margin = new Padding(0, 0, 10, 0);
        inputWrapper.Input.Text = path;
        if (string.IsNullOrEmpty(path)) inputWrapper.Input.PlaceholderText = $"Chọn ổ cứng đích {count}";
        inputWrapper.Input.TextChanged += (_, _) => UpdateResourceTargetRootPathFromUi();

        var browseBtn = new IconButton 
        { 
            DrawIcon = DrawDotsIcon,
            Margin = new Padding(0, 0, 10, 0),
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
            Margin = new Padding(0), 
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
            .System.Linq.Enumerable.Select(p => p.Trim())
            .System.Linq.Enumerable.ToList();
            
        if (paths.Count == 0) paths.Add("");

        foreach (var path in paths)
        {
            AddTargetRowUi(path);
        }

        var addBtn = new GameUpdater.WinForms.Controls.ModernButton { Text = "+  Thêm đích máy chủ", Size = new Size(180, 36), Margin = new Padding(180, 0, 0, 0), CornerRadius = 6, ColorType = GameUpdater.WinForms.Controls.ButtonColorType.DashedPurple, Font = new Font("Segoe UI", 10.5f) };
        addBtn.Click += (_, _) => 
        {
            _targetsContainer.Controls.Remove(addBtn);
            AddTargetRowUi("");
            _targetsContainer.Controls.Add(addBtn);
            UpdateResourceTargetRootPathFromUi();
        };
        _targetsContainer.Controls.Add(addBtn);
    }"""
    
    content = content[:match1_start.start()] + part1_replacement + content[match1_end.end():]
    
    # Target 2: from BuildConfigWorkspaceLayout down to CreateInputWrapperWithIcon
    match2_start = re.search(r'    private Control BuildConfigWorkspaceLayout\(\)', content)
    match2_end = re.search(r'    private Panel CreateInputWrapperWithIcon\(string emoji, Control innerControl\)\n    {.*?return pnl;\n    }', content, re.DOTALL)
    
    if not match2_start or not match2_end:
        print("Could not find Target 2")
        return
        
    part2_replacement = """    private Control BuildConfigWorkspaceLayout()
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
            Padding = new Padding(32, 32, 32, 0),
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
        mainFlow.Controls.Add(CreateConfigSectionHeader(DrawGlobeIcon, "NGUỒN IDC", Color.FromArgb(88, 50, 228)));
        
        _sourcesContainer.Dock = DockStyle.Top;
        _sourcesContainer.AutoSize = true;
        _sourcesContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _sourcesContainer.FlowDirection = FlowDirection.TopDown;
        _sourcesContainer.WrapContents = false;
        _sourcesContainer.Padding = new Padding(0, 10, 0, 20);
        _sourcesContainer.Margin = new Padding(0);
        mainFlow.Controls.Add(_sourcesContainer);
        BuildSourcesUi(); // This will populate _sourcesContainer
        
        // Divider
        var divider1 = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(241, 245, 249), Margin = new Padding(0, 10, 0, 24) };
        mainFlow.Controls.Add(divider1);

        // Section 2: Đích Máy Chủ
        mainFlow.Controls.Add(CreateConfigSectionHeader(DrawMonitorIcon, "ĐÍCH MÁY CHỦ", Color.FromArgb(88, 50, 228)));
        
        _targetsContainer.Dock = DockStyle.Top;
        _targetsContainer.AutoSize = true;
        _targetsContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _targetsContainer.FlowDirection = FlowDirection.TopDown;
        _targetsContainer.WrapContents = false;
        _targetsContainer.Padding = new Padding(0, 10, 0, 20);
        _targetsContainer.Margin = new Padding(0);
        mainFlow.Controls.Add(_targetsContainer);
        BuildTargetsUi(); // This will populate _targetsContainer
        
        // Divider
        var divider2 = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(241, 245, 249), Margin = new Padding(0, 10, 0, 24) };
        mainFlow.Controls.Add(divider2);

        // Section 3: Giới Hạn
        mainFlow.Controls.Add(CreateConfigSectionHeader(DrawSpeedIcon, "GIỚI HẠN", Color.FromArgb(88, 50, 228)));
        var bandwidthRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            Padding = new Padding(0, 10, 0, 32),
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0)
        };
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        bandwidthRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var limitLabel = new Label { Text = "Giới hạn MB/s", Font = new Font("Segoe UI", 10.5f), ForeColor = Color.FromArgb(30,30,40), AutoSize = true, Anchor = AnchorStyles.Left };
        
        var numWrapper = new Panel { BackColor = Color.White, Padding = new Padding(8, 4, 0, 4), Width = 140, Height = 36 };
        numWrapper.Paint += (s, e) => {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, GameEditorForm.GetRoundedRectPath(new Rectangle(0,0,numWrapper.Width-1,numWrapper.Height-1), 6));
        };
        
        _resourceBandwidthLimitNumeric.BorderStyle = BorderStyle.None;
        _resourceBandwidthLimitNumeric.Dock = DockStyle.Fill;
        _resourceBandwidthLimitNumeric.Minimum = 0;
        _resourceBandwidthLimitNumeric.Maximum = 10000;
        _resourceBandwidthLimitNumeric.DecimalPlaces = 0;
        _resourceBandwidthLimitNumeric.Value = _resourceBandwidthLimitMbps;
        _resourceBandwidthLimitNumeric.Font = new Font("Segoe UI", 10.5f);
        _resourceBandwidthLimitNumeric.ValueChanged += (_, _) => _resourceBandwidthLimitMbps = Decimal.ToInt32(_resourceBandwidthLimitNumeric.Value);
        
        numWrapper.Controls.Add(_resourceBandwidthLimitNumeric);

        var hintLabel = new Label { Text = "0 = không giới hạn", Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(130,130,140), AutoSize = true, Anchor = AnchorStyles.Left };
        
        bandwidthRow.Controls.Add(limitLabel, 0, 0);
        bandwidthRow.Controls.Add(numWrapper, 1, 0);
        bandwidthRow.Controls.Add(hintLabel, 2, 0);
        mainFlow.Controls.Add(bandwidthRow);

        // Divider
        var divider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(226, 232, 240), Margin = new Padding(0, 0, 0, 24) };
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
        _saveResourceSettingsButton.Click -= SaveResourceSettingsButton_Click;
        _saveResourceSettingsButton.Click += SaveResourceSettingsButton_Click;
        _saveResourceSettingsButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary;
        _saveResourceSettingsButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Save;
        _saveResourceSettingsButton.Size = new Size(180, 42);
        
        _checkResourceHealthButton.Text = "Kiểm tra tài nguyên";
        _checkResourceHealthButton.Click -= CheckResourceHealthButton_Click;
        _checkResourceHealthButton.Click += CheckResourceHealthButton_Click;
        _checkResourceHealthButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary;
        _checkResourceHealthButton.IconType = GameUpdater.WinForms.Controls.ButtonIconType.Refresh;
        _checkResourceHealthButton.Size = new Size(180, 42);
        _checkResourceHealthButton.Margin = new Padding(16, 0, 0, 0);
        
        _syncSelectedResourceButton.Text = "Tải trò chơi đã chọn";
        _syncSelectedResourceButton.Click -= SyncSelectedResourceButton_Click;
        _syncSelectedResourceButton.Click += SyncSelectedResourceButton_Click;
        _syncSelectedResourceButton.ColorType = GameUpdater.WinForms.Controls.ButtonColorType.PrimaryBlue;
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
        
        var infoIcon = new Panel { Width = 20, Height = 20, Margin = new Padding(0, 0, 10, 0) };
        infoIcon.Paint += (s, e) => {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var p = new Pen(Color.FromArgb(88, 50, 228), 1.5f);
            e.Graphics.DrawEllipse(p, 2, 2, 16, 16);
            e.Graphics.DrawLine(p, 10, 6, 10, 7);
            e.Graphics.DrawLine(p, 10, 9, 10, 15);
        };

        var infoText = new Label { Text = "Cấu hình nguồn/đích và giới hạn băng thông tải tài nguyên.", Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(30,30,40), AutoSize = true, Margin = new Padding(0, 1, 0, 0) };
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
            Margin = new Padding(0, 0, 0, 16)
        };

        var iconContainer = new Panel
        {
            Width = 40,
            Height = 40,
            Margin = new Padding(0, 0, 16, 0),
            BackColor = Color.FromArgb(240, 237, 252) // Soft purple bg
        };
        iconContainer.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
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
            Margin = new Padding(0, 10, 0, 0)
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
            Size = new Size(36, 36);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; _isPressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _isPressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _isPressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
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
        private Action<Graphics, Rectangle, Color> _drawIcon;

        public IconTextBox(Action<Graphics, Rectangle, Color> drawIcon)
        {
            _drawIcon = drawIcon;
            BackColor = Color.White;
            Padding = new Padding(36, 8, 10, 8);
            Height = 36;
            
            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5f),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30,30,40)
            };
            Controls.Add(_textBox);
        }
        
        public TextBox Input => _textBox;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
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
        int d = 3;
        int y = rect.Y + (rect.Height - d) / 2;
        g.FillEllipse(brush, rect.X + rect.Width / 2 - 6, y, d, d);
        g.FillEllipse(brush, rect.X + rect.Width / 2 - d/2, y, d, d);
        g.FillEllipse(brush, rect.X + rect.Width / 2 + 6 - d, y, d, d);
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
        g.DrawLine(pen, rect.X + rect.Width / 2, rect.Bottom - rect.Height / 2, rect.Right - 2, rect.Y + 2);
        g.FillEllipse(new SolidBrush(color), rect.X + rect.Width / 2 - 2, rect.Bottom - rect.Height / 2 - 2, 4, 4);
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
"""

    content = content[:match2_start.start()] + part2_replacement + content[match2_end.end():]

    with open('i:/servermanagergame/src/GameUpdater.WinForms/Forms/MainForm.Layout.cs', 'w', encoding='utf-8') as f:
        f.write(content)

    print("Successfully replaced layout contents.")

if __name__ == '__main__':
    update_layout()
