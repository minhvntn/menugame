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
{    private void ConfigureGamesGrid()
    {
        _gamesGrid.Dock = DockStyle.Fill;
        _gamesGrid.AutoGenerateColumns = false;
        _gamesGrid.AllowUserToAddRows = false;
        _gamesGrid.AllowUserToDeleteRows = false;
        _gamesGrid.MultiSelect = false;
        _gamesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gamesGrid.ReadOnly = true;
        _gamesGrid.RowHeadersVisible = false;
        _gamesGrid.DataSource = _gamesBinding;
        _gamesGrid.Columns.Add(CreateCheckBoxColumn("Hot", nameof(GameRecord.IsHot), 55));

        _gamesGrid.Columns.Add(CreateTextColumn("Ưu tiên", nameof(GameRecord.SortOrder), 70));

        _gamesGrid.Columns.Add(CreateTextColumn("Tên trò chơi", nameof(GameRecord.Name), 180));
        _gamesGrid.Columns.Add(CreateTextColumn("Nhóm", nameof(GameRecord.Category), 120));
        _gamesGrid.Columns.Add(CreateTextColumn("Phiên bản", nameof(GameRecord.Version), 90));
        _gamesGrid.Columns.Add(CreateTextColumn("Tệp chạy", nameof(GameRecord.LaunchRelativePath), 220));
        _gamesGrid.Columns.Add(CreateTextColumn("Đường dẫn cài đặt", nameof(GameRecord.InstallPath), 320));
        _gamesGrid.Columns.Add(CreateTextColumn("Quét gần nhất", nameof(GameRecord.LastScannedAt), 140, "yyyy-MM-dd HH:mm:ss"));
        _gamesGrid.Columns.Add(CreateTextColumn("Cập nhật gần nhất", nameof(GameRecord.LastUpdatedAt), 140, "yyyy-MM-dd HH:mm:ss"));
    }

    private void ConfigureClientStatusGrid()
    {
        _clientStatusGrid.Dock = DockStyle.Fill;
        _clientStatusGrid.AutoGenerateColumns = false;
        _clientStatusGrid.AllowUserToAddRows = false;
        _clientStatusGrid.AllowUserToDeleteRows = false;
        _clientStatusGrid.MultiSelect = false;
        _clientStatusGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _clientStatusGrid.ReadOnly = true;
        _clientStatusGrid.RowHeadersVisible = false;
        _clientStatusGrid.DataSource = _clientStatusBinding;
        _clientStatusGrid.Columns.Add(CreateTextColumn("Trạng thái", nameof(ClientDashboardRow.StatusText), 110));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Máy", nameof(ClientDashboardRow.MachineName), 150));
        _clientStatusGrid.Columns.Add(CreateTextColumn("User", nameof(ClientDashboardRow.UserName), 120));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Đang chơi", nameof(ClientDashboardRow.CurrentGameName), 200));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Uptime", nameof(ClientDashboardRow.UptimeText), 90));
        _clientStatusGrid.Columns.Add(CreateTextColumn("RAM", nameof(ClientDashboardRow.MemoryText), 130));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Mạng", nameof(ClientDashboardRow.NetworkText), 150));
        _clientStatusGrid.Columns.Add(CreateTextColumn("File chạy", nameof(ClientDashboardRow.CurrentGameExecutablePath), 280));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Cập nhật cuối", nameof(ClientDashboardRow.LastSeenLocalText), 160));
        _clientStatusGrid.Columns.Add(CreateTextColumn("File", nameof(ClientDashboardRow.SourceFileName), 150));
    }

    private void ConfigureGamesGridPanel()
    {
        _gamesGridPanel.Dock = DockStyle.Fill;
        _gamesGridPanel.AutoScroll = true;
        _gamesGridPanel.Visible = false;
        _gamesGridPanel.BackColor = Color.FromArgb(11, 17, 32);
        _gamesGridPanel.Padding = new Padding(12);
    }

    private void GamesViewModeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var isGrid = _gamesViewModeComboBox.SelectedIndex == 1;
        _gamesGrid.Visible = !isGrid;
        _gamesGridPanel.Visible = isGrid;
        if (isGrid) RefreshGamesGridPanel();
    }

    private void RefreshGamesGridPanel()
    {
        if (_gamesViewModeComboBox.SelectedIndex != 1) return;

        _gamesGridPanel.SuspendLayout();
        var controlsToDispose = _gamesGridPanel.Controls.Cast<Control>().ToList();
        _gamesGridPanel.Controls.Clear();
        foreach (var control in controlsToDispose)
        {
            control.Dispose();
        }

        var games = _gamesBinding.DataSource as List<GameRecord> ?? new List<GameRecord>();
        var ordered = games.OrderBy(g => g.SortOrder).ThenBy(g => g.Name).ToList();

        foreach (var game in ordered)
        {
            var card = new GameUpdater.WinForms.Controls.ServerGameCardControl(game, clickedGame =>
            {
                foreach (GameUpdater.WinForms.Controls.ServerGameCardControl c in _gamesGridPanel.Controls) c.IsSelected = false;
                
                var control = _gamesGridPanel.Controls.OfType<GameUpdater.WinForms.Controls.ServerGameCardControl>().FirstOrDefault(c => c.GameRecord.Id == clickedGame.Id);
                if (control != null) control.IsSelected = true;

                for (var i = 0; i < _gamesBinding.Count; i++)
                {
                    if (_gamesBinding[i] is GameRecord gr && gr.Id == clickedGame.Id)
                    {
                        _gamesBinding.Position = i;
                        break;
                    }
                }
            });
            
            if (SelectedGame != null && SelectedGame.Id == game.Id) card.IsSelected = true;
            _gamesGridPanel.Controls.Add(card);
        }
        _gamesGridPanel.ResumeLayout();
    }

    private async Task ReorderSelectedGameAsync(int deltaOffset)
    {
        if (SelectedGame == null) return;
        var games = _gamesBinding.DataSource as List<GameRecord>;
        if (games == null || games.Count == 0) return;

        var currentIndex = games.FindIndex(g => g.Id == SelectedGame.Id);
        if (currentIndex < 0) return;

        var targetIndex = Math.Max(0, Math.Min(games.Count - 1, currentIndex + Math.Sign(deltaOffset)));
        if (deltaOffset == -99999) targetIndex = 0;

        if (targetIndex != currentIndex)
        {
            var targetGame = games[targetIndex];
            var currentSort = SelectedGame.SortOrder;
            SelectedGame.SortOrder = targetGame.SortOrder;
            targetGame.SortOrder = currentSort;
            
            if (SelectedGame.SortOrder == targetGame.SortOrder)
            {
                SelectedGame.SortOrder -= Math.Sign(deltaOffset);
            }

            try
            {
                await _gameService.SaveGameAsync(SelectedGame);
                await _gameService.SaveGameAsync(targetGame);
                await LoadGamesAsync(SelectedGame.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đổi vị trí: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void EnsureGamesContextMenu()
    {
        if (_gamesContextMenuInitialized)
        {
            return;
        }

        _gamesContextMenuInitialized = true;
        _gamesContextMenu.Items.Add(_addGameMenuItem);
        _gamesContextMenu.Items.Add(_deleteGameMenuItem);
        _gamesContextMenu.Items.Add(_editGameMenuItem);
        _gamesContextMenu.Items.Add(new ToolStripSeparator());
        _gamesContextMenu.Items.Add(_markHotGameMenuItem);
        _gamesContextMenu.Items.Add(_unmarkHotGameMenuItem);
        _gamesContextMenu.Items.Add(new ToolStripSeparator());
        _gamesContextMenu.Items.Add(_viewManifestMenuItem);
        _gamesContextMenu.Opening += GamesContextMenu_Opening;
        _addGameMenuItem.Click += AddGameButton_Click;
        _editGameMenuItem.Click += EditGameButton_Click;
        _deleteGameMenuItem.Click += DeleteGameButton_Click;
        _markHotGameMenuItem.Click += MarkHotGameMenuItem_Click;
        _unmarkHotGameMenuItem.Click += UnmarkHotGameMenuItem_Click;
        _viewManifestMenuItem.Click += ViewManifestMenuItem_Click;

        _gamesGrid.ContextMenuStrip = _gamesContextMenu;
        _gamesGrid.MouseDown += GamesGrid_MouseDown;
    }

    private void GamesGrid_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var hit = _gamesGrid.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0 || hit.RowIndex >= _gamesGrid.Rows.Count)
        {
            return;
        }

        _gamesGrid.ClearSelection();
        var row = _gamesGrid.Rows[hit.RowIndex];
        row.Selected = true;
        _gamesGrid.CurrentCell = row.Cells[0];
        _gamesBinding.Position = row.Index;
    }

    private void GamesContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var hasSelectedGame = SelectedGame is not null;
        _addGameMenuItem.Enabled = true;
        _editGameMenuItem.Enabled = hasSelectedGame;
        _deleteGameMenuItem.Enabled = hasSelectedGame;
        _markHotGameMenuItem.Enabled = hasSelectedGame && SelectedGame is { IsHot: false };
        _unmarkHotGameMenuItem.Enabled = hasSelectedGame && SelectedGame is { IsHot: true };
        _viewManifestMenuItem.Enabled = hasSelectedGame;
    }

    private async void ViewManifestMenuItem_Click(object? sender, EventArgs e)
    {
        if (SelectedGame is null)
        {
            return;
        }

        var game = SelectedGame;
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            var manifestPreview = await _gameService.GetManifestPreviewAsync(game);
            ShowManifestDialog(game.Name, manifestPreview);
        });
    }

    private async void MarkHotGameMenuItem_Click(object? sender, EventArgs e)
    {
        await SetSelectedGameHotAsync(true);
    }

    private async void UnmarkHotGameMenuItem_Click(object? sender, EventArgs e)
    {
        await SetSelectedGameHotAsync(false);
    }

    private async Task SetSelectedGameHotAsync(bool isHot)
    {
        if (SelectedGame is null)
        {
            ShowInfo("Vui lòng chọn trò chơi trước.");
            return;
        }

        if (SelectedGame.IsHot == isHot)
        {
            return;
        }

        var game = SelectedGame;
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            game.IsHot = isHot;
            var gameId = await _gameService.SaveGameAsync(game);
            await AutoExportCatalogAsync();
            await ReloadAllAsync(gameId);
        });
    }

    private void ShowManifestDialog(string gameName, string manifestText)
    {
        using var dialog = new Form
        {
            Text = $"Manifest - {gameName}",
            Width = 900,
            Height = 700,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = true
        };

        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", Math.Max(10f, GetUiFontSize(_uiFontSizeMode))),
            ReadOnly = true,
            WordWrap = false,
            Text = manifestText
        };

        dialog.Controls.Add(textBox);
        dialog.ShowDialog(this);
    }

    private void ConfigureLogsGrid()
    {
        _logsGrid.Dock = DockStyle.Fill;
        _logsGrid.AutoGenerateColumns = false;
        _logsGrid.AllowUserToAddRows = false;
        _logsGrid.AllowUserToDeleteRows = false;
        _logsGrid.MultiSelect = false;
        _logsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _logsGrid.ReadOnly = true;
        _logsGrid.RowHeadersVisible = false;
        _logsGrid.DataSource = _logsBinding;

        _logsGrid.Columns.Add(CreateTextColumn("Thời gian", nameof(UpdateLogEntry.CreatedAt), 150, "yyyy-MM-dd HH:mm:ss"));
        _logsGrid.Columns.Add(CreateTextColumn("Trò chơi", nameof(UpdateLogEntry.GameName), 160));
        _logsGrid.Columns.Add(CreateTextColumn("Hành động", nameof(UpdateLogEntry.Action), 120));
        _logsGrid.Columns.Add(CreateTextColumn("Trạng thái", nameof(UpdateLogEntry.Status), 90));
        _logsGrid.Columns.Add(CreateTextColumn("Nội dung", nameof(UpdateLogEntry.Message), 600, fill: true));
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string header, string propertyName, int width, string? format = null, bool fill = false)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = propertyName,
            Width = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None
        };

        if (!string.IsNullOrWhiteSpace(format))
        {
            column.DefaultCellStyle.Format = format;
            column.DefaultCellStyle.NullValue = string.Empty;
        }

        return column;
    }

    private static DataGridViewCheckBoxColumn CreateCheckBoxColumn(string header, string propertyName, int width)
    {
        return new DataGridViewCheckBoxColumn
        {
            HeaderText = header,
            DataPropertyName = propertyName,
            Width = width,
            ReadOnly = true,
            ThreeState = false,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        };
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private void ApplyResourcesSplitDistance()
    {
        if (_resourcesSplitContainer is null || _resourcesSplitContainer.Width <= 0)
        {
            return;
        }

        var split = _resourcesSplitContainer;
        var hardMax = Math.Max(0, split.Width - 1);
        var preferredLeft = 220;
        var minLeft = 120;
        var reserveRight = 360;

        var target = Math.Min(preferredLeft, Math.Max(minLeft, split.Width - reserveRight));
        target = Math.Clamp(target, 0, hardMax);

        if (split.SplitterDistance != target)
        {
            split.SplitterDistance = target;
        }
    }

    private void InitializeFontSizeSelector(FlowLayoutPanel toolbar)
    {
        var label = new Label
        {
            Text = "Cỡ chữ giao diện",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 5, 8, 0)
        };

        _fontSizeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _fontSizeComboBox.Width = 140;
        _fontSizeComboBox.Margin = new Padding(0, 2, 0, 0);
        _fontSizeComboBox.DisplayMember = nameof(FontSizeOption.Name);
        _fontSizeComboBox.ValueMember = nameof(FontSizeOption.Mode);
        _fontSizeComboBox.DataSource = new List<FontSizeOption>
        {
            new() { Mode = UiFontSizeMode.Normal, Name = "Bình thường" },
            new() { Mode = UiFontSizeMode.Big, Name = "Lớn" },
            new() { Mode = UiFontSizeMode.VeryBig, Name = "Rất lớn" }
        };

        SetFontSizeSelection(_uiFontSizeMode);
        _fontSizeComboBox.SelectedIndexChanged += FontSizeComboBox_SelectedIndexChanged;

        toolbar.Controls.Add(label);
        toolbar.Controls.Add(_fontSizeComboBox);
    }

    private async void FontSizeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingFontSizeSelection || _fontSizeComboBox.SelectedValue is not UiFontSizeMode mode)
        {
            return;
        }

        if (mode == _uiFontSizeMode)
        {
            return;
        }

        ApplyUiFontSize(mode);
        await ExecuteWithErrorHandlingAsync(SaveUiSettingsAsync);
    }

    private void SetFontSizeSelection(UiFontSizeMode mode)
    {
        if (_fontSizeComboBox.DataSource is null)
        {
            return;
        }

        _isUpdatingFontSizeSelection = true;
        try
        {
            _fontSizeComboBox.SelectedValue = mode;
        }
        finally
        {
            _isUpdatingFontSizeSelection = false;
        }
    }

    private void ApplyUiFontSize(UiFontSizeMode mode)
    {
        _uiFontSizeMode = mode;
        var uiFontSize = GetUiFontSize(mode);
        var uiFont = new Font("Segoe UI", uiFontSize, FontStyle.Regular);

        SuspendLayout();
        try
        {
            Font = uiFont;
            ApplyDataGridFont(_gamesGrid, uiFont);
            ApplyDataGridFont(_resourcesGrid, uiFont);
            ApplyDataGridFont(_downloadMonitorGrid, uiFont);
            ApplyDataGridFont(_logsGrid, uiFont);

            _updateOutputTextBox.Font = new Font("Consolas", Math.Max(13f, uiFontSize), FontStyle.Regular);
            ApplyListItemSpacing(uiFontSize);
            ApplyButtonSizing(uiFontSize);
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    private static void ApplyDataGridFont(DataGridView grid, Font uiFont)
    {
        grid.Font = uiFont;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font(uiFont, FontStyle.Bold); // Make header font slightly bolder for better appearance
        grid.DefaultCellStyle.Font = uiFont;
        grid.DefaultCellStyle.Padding = new Padding(0, 4, 0, 4);
        grid.RowTemplate.Height = Math.Max(34, (int)Math.Ceiling(uiFont.Size * 2.6f));
        grid.ColumnHeadersHeight = Math.Max(46, (int)Math.Ceiling(uiFont.Size * 3.4f)); // Increased header height
    }

    private void ApplyListItemSpacing(float uiFontSize)
    {
        var itemHeight = Math.Max(28, (int)Math.Ceiling(uiFontSize * 2.4f));
        _fontSizeComboBox.ItemHeight = itemHeight;
        _updateGameComboBox.ItemHeight = itemHeight;
        _updateSourceKindComboBox.ItemHeight = itemHeight;
        _gamesViewModeComboBox.ItemHeight = itemHeight;
        _resourceTree.ItemHeight = Math.Max(26, (int)Math.Ceiling(uiFontSize * 2.2f));
    }

    private static float GetUiFontSize(UiFontSizeMode mode)
    {
        return mode switch
        {
            UiFontSizeMode.Big => 14f,
            UiFontSizeMode.VeryBig => 16f,
            _ => 12f
        };
    }

    private static Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text
        };
        StyleButton(button);
        button.Click += onClick;
        return button;
    }

    private static void StyleButton(Button button, bool primary = false)
    {
        if (!StyledButtons.Contains(button))
        {
            StyledButtons.Add(button);
        }

        StyledButtonPrimaryStates[button] = primary;
        StyledButtonTargetColors[button] = primary ? AccentColor : SecondaryButtonColor;

        button.AutoSize = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? AccentColor : ButtonBorderColor;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentColor : SecondaryButtonColor;
        button.FlatAppearance.MouseDownBackColor = primary ? AccentHoverColor : SecondaryButtonHoverColor;
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(5, 4, 7, 4);
        button.Padding = new Padding(ButtonHorizontalPadding, ButtonVerticalPadding, ButtonHorizontalPadding, ButtonVerticalPadding);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.BackColor = primary ? AccentColor : SecondaryButtonColor;
        button.ForeColor = primary ? Color.White : SecondaryButtonTextColor;

        button.MouseEnter -= StyledButton_MouseEnter;
        button.MouseLeave -= StyledButton_MouseLeave;
        button.MouseDown -= StyledButton_MouseDown;
        button.MouseUp -= StyledButton_MouseUp;
        button.Disposed -= StyledButton_Disposed;

        button.MouseEnter += StyledButton_MouseEnter;
        button.MouseLeave += StyledButton_MouseLeave;
        button.MouseDown += StyledButton_MouseDown;
        button.MouseUp += StyledButton_MouseUp;
        button.Disposed += StyledButton_Disposed;
        button.Region?.Dispose();
        button.Region = null;
    }

    private static void ApplyButtonSizing(float uiFontSize)
    {
        var height = Math.Max(42, (int)Math.Ceiling(uiFontSize * 3.2f));
        foreach (var button in StyledButtons.Where(button => !button.IsDisposed))
        {
            button.Height = height;
            button.MinimumSize = new Size(0, height);
            button.Width = Math.Max(button.Width, TextRenderer.MeasureText(button.Text, button.Font).Width + (ButtonHorizontalPadding * 2) + 8);
        }
    }

    private static void StyledButton_MouseEnter(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;
        var primary = StyledButtonPrimaryStates.TryGetValue(button, out var isPrimary) && isPrimary;
        AnimateButtonColor(button, primary ? AccentHoverColor : SecondaryButtonHoverColor);
    }

    private static void StyledButton_MouseLeave(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;
        var primary = StyledButtonPrimaryStates.TryGetValue(button, out var isPrimary) && isPrimary;
        AnimateButtonColor(button, primary ? AccentColor : SecondaryButtonColor);
    }

    private static void StyledButton_MouseDown(object? sender, MouseEventArgs e)
    {
        if (sender is not Button button) return;
        var primary = StyledButtonPrimaryStates.TryGetValue(button, out var isPrimary) && isPrimary;
        AnimateButtonColor(button, primary ? Color.FromArgb(29, 78, 216) : Color.FromArgb(71, 85, 105));
    }

    private static void StyledButton_MouseUp(object? sender, MouseEventArgs e)
    {
        if (sender is not Button button) return;
        var primary = StyledButtonPrimaryStates.TryGetValue(button, out var isPrimary) && isPrimary;
        var isHovering = button.ClientRectangle.Contains(button.PointToClient(Cursor.Position));
        AnimateButtonColor(button, isHovering ? primary ? AccentHoverColor : SecondaryButtonHoverColor : primary ? AccentColor : SecondaryButtonColor);
    }

    private static void StyledButton_Disposed(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;
        StyledButtons.Remove(button);
        StyledButtonPrimaryStates.Remove(button);
        StyledButtonTargetColors.Remove(button);
    }

    private static void AnimateButtonColor(Button button, Color targetColor)
    {
        StyledButtonTargetColors[button] = targetColor;
        var startColor = button.BackColor;
        const int steps = 5;
        var currentStep = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 12 };
        timer.Tick += (_, _) =>
        {
            if (button.IsDisposed || !StyledButtonTargetColors.TryGetValue(button, out var latestTarget) || latestTarget != targetColor)
            {
                timer.Stop();
                timer.Dispose();
                return;
            }

            currentStep++;
            button.BackColor = InterpolateColor(startColor, targetColor, currentStep / (float)steps);
            if (currentStep >= steps)
            {
                button.BackColor = targetColor;
                timer.Stop();
                timer.Dispose();
            }
        };
        timer.Start();
    }

    private static Color InterpolateColor(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)(from.R + ((to.R - from.R) * amount)),
            (int)(from.G + ((to.G - from.G) * amount)),
            (int)(from.B + ((to.B - from.B) * amount)));
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        };
    }
}





