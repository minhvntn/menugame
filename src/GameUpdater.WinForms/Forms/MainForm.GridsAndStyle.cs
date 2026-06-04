using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using GameUpdater.Core.Abstractions;
using GameUpdater.Core.Services;
using GameUpdater.Shared.Localization;
using GameUpdater.Shared.Models;

namespace GameUpdater.WinForms.Forms;

public sealed partial class MainForm
{
    private const string HotColumnName = "HotColumn";
    private const string GameSizeDisplayColumnName = "GameSizeDisplayColumn";

    private void ConfigureGamesGrid()
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
        _gamesGrid.CellFormatting -= GamesGrid_CellFormatting;
        _gamesGrid.CellFormatting += GamesGrid_CellFormatting;
        _gamesGrid.CellClick -= GamesGrid_CellClick;
        _gamesGrid.CellClick += GamesGrid_CellClick;
        _gamesGrid.CellPainting -= GamesGrid_CellPainting;
        _gamesGrid.CellPainting += GamesGrid_CellPainting;

        var hotColumn = CreateCheckBoxColumn("Hot", nameof(GameRecord.IsHot), 65);
        hotColumn.Name = HotColumnName;
        _gamesGrid.Columns.Add(hotColumn);

        _gamesGrid.Columns.Add(CreateTextColumn("Tên trò chơi  ⇅", nameof(GameRecord.Name), 230));
        _gamesGrid.Columns.Add(CreateTextColumn("Nhóm  ⇅", nameof(GameRecord.Category), 120));
        _gamesGrid.Columns.Add(CreateTextColumn("Tệp chạy  ⇅", nameof(GameRecord.LaunchRelativePath), 220));
        _gamesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = GameSizeDisplayColumnName,
            HeaderText = "Dung lượng (GB)  ⇅",
            Width = 110,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleRight
            }
        });
        _gamesGrid.Columns.Add(CreateTextColumn("Đường dẫn cài đặt", nameof(GameRecord.InstallPath), 320, null, true));
        _gamesGrid.Columns.Add(CreateTextColumn("Quét gần nhất", nameof(GameRecord.LastScannedAt), 140, "yyyy-MM-dd HH:mm:ss"));
        _gamesGrid.Columns.Add(CreateTextColumn("Cập nhật gần nhất", nameof(GameRecord.LastUpdatedAt), 140, "yyyy-MM-dd HH:mm:ss"));
    }

    private async void GamesGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _gamesGrid.Columns[e.ColumnIndex].Name != HotColumnName ||
            _gamesGrid.Rows[e.RowIndex].DataBoundItem is not GameRecord game)
        {
            return;
        }

        _gamesBinding.Position = e.RowIndex;
        _gamesGrid.CurrentCell = _gamesGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        await SetGameHotFromGridAsync(game, !game.IsHot);
    }

    private async Task SetGameHotFromGridAsync(GameRecord game, bool isHot)
    {
        if (game.IsHot == isHot)
        {
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            game.IsHot = isHot;
            var gameId = await _gameService.SaveGameAsync(game);
            await AutoExportCatalogAsync();
            await ReloadAllAsync(gameId);
        });
    }

    private Font? _gridBoldFont;
    private float _lastFontSize = -1;
    private string _lastFontFamily = string.Empty;

    private Font GetBoldFont()
    {
        if (_gridBoldFont == null || _gamesGrid.Font.Size != _lastFontSize || _gamesGrid.Font.FontFamily.Name != _lastFontFamily)
        {
            _gridBoldFont?.Dispose();
            _gridBoldFont = new Font(_gamesGrid.Font, FontStyle.Bold);
            _lastFontSize = _gamesGrid.Font.Size;
            _lastFontFamily = _gamesGrid.Font.FontFamily.Name;
        }
        return _gridBoldFont;
    }

    private void GamesGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.CellStyle is null)
        {
            return;
        }

        var col = _gamesGrid.Columns[e.ColumnIndex];
        
        // Define base row backgrounds
        var isAlternating = (e.RowIndex % 2 != 0);
        var baseBg = isAlternating ? Color.FromArgb(248, 250, 252) : Color.White;
        var selectedBg = Color.FromArgb(238, 242, 255); // Indigo-50 (#EEF2FF)

        e.CellStyle.BackColor = baseBg;
        e.CellStyle.SelectionBackColor = selectedBg;

        // Custom formatting for specific columns
        if (col.DataPropertyName == nameof(GameRecord.Name))
        {
            e.CellStyle.ForeColor = Color.FromArgb(15, 23, 42); // slate-900 (#0F172A)
            e.CellStyle.SelectionForeColor = Color.FromArgb(99, 102, 241); // indigo-500 (#6366F1)
            e.CellStyle.Font = GetBoldFont();
        }
        else if (col.DataPropertyName == nameof(GameRecord.Category))
        {
            var cellValue = e.Value?.ToString() ?? string.Empty;
            Color catColor = Color.FromArgb(71, 85, 105); // slate-600 (#475569)
            if (string.Equals(cellValue, "IDC", StringComparison.OrdinalIgnoreCase))
            {
                catColor = Color.FromArgb(59, 130, 246); // Blue-500 (#3B82F6)
            }
            else if (string.Equals(cellValue, "Offline", StringComparison.OrdinalIgnoreCase))
            {
                catColor = Color.FromArgb(239, 68, 68); // Red-500 (#EF4444)
            }
            else if (string.Equals(cellValue, "Online", StringComparison.OrdinalIgnoreCase) || 
                     string.Equals(cellValue, "Tools", StringComparison.OrdinalIgnoreCase))
            {
                catColor = Color.FromArgb(16, 185, 129); // Emerald-500 (#10B981)
            }

            e.CellStyle.ForeColor = catColor;
            e.CellStyle.SelectionForeColor = catColor;
            e.CellStyle.Font = GetBoldFont();
        }
        else if (col.DataPropertyName == nameof(GameRecord.LaunchRelativePath))
        {
            var blueColor = Color.FromArgb(37, 99, 235); // Blue-600 (#2563EB)
            e.CellStyle.ForeColor = blueColor;
            e.CellStyle.SelectionForeColor = blueColor;
        }
        else if (col.Name == GameSizeDisplayColumnName)
        {
            e.CellStyle.ForeColor = Color.FromArgb(71, 85, 105); // slate-600
            e.CellStyle.SelectionForeColor = Color.FromArgb(71, 85, 105);
            
            if (_gamesGrid.Rows[e.RowIndex].DataBoundItem is GameRecord game)
            {
                e.Value = GetGameSizeDisplay(game);
                e.FormattingApplied = true;
            }
            else
            {
                e.Value = "-";
                e.FormattingApplied = true;
            }
        }
        else if (col.DataPropertyName == nameof(GameRecord.InstallPath))
        {
            e.CellStyle.ForeColor = Color.FromArgb(71, 85, 105); // slate-600
            e.CellStyle.SelectionForeColor = Color.FromArgb(71, 85, 105);
        }
        else if (col.DataPropertyName == nameof(GameRecord.LastScannedAt) || col.DataPropertyName == nameof(GameRecord.LastUpdatedAt))
        {
            e.CellStyle.ForeColor = Color.FromArgb(100, 116, 139); // slate-500
            e.CellStyle.SelectionForeColor = Color.FromArgb(100, 116, 139);
        }
        else
        {
            e.CellStyle.ForeColor = Color.FromArgb(30, 41, 59); // slate-800
            e.CellStyle.SelectionForeColor = Color.FromArgb(30, 41, 59);
        }
    }

    private string GetGameSizeDisplay(GameRecord game)
    {
        var key = $"{game.Id}|{game.Name}|{game.InstallPath}";
        if (_gameSizeDisplayCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var manifest = TryLoadManifest(game);
        if (manifest?.Files is null || manifest.Files.Count == 0)
        {
            _gameSizeDisplayCache[key] = "-";
            return "-";
        }

        var totalBytes = manifest.Files
            .Where(file => file.Size > 0)
            .Sum(file => file.Size);
        var display = totalBytes <= 0
            ? "-"
            : (totalBytes / 1024d / 1024d / 1024d).ToString("N2");
        _gameSizeDisplayCache[key] = display;
        return display;
    }

    private Image? _onlineIcon;
    private Image? _offlineIcon;

    private void ConfigureClientStatusGrid()
    {
        var asm = typeof(MainForm).Assembly;
        try { _onlineIcon = Image.FromStream(asm.GetManifestResourceStream("GameUpdater.WinForms.Resources.online_icon.png")!); } catch {}
        try { _offlineIcon = Image.FromStream(asm.GetManifestResourceStream("GameUpdater.WinForms.Resources.offline_icon.png")!); } catch {}

        _clientStatusGrid.Dock = DockStyle.Fill;
        _clientStatusGrid.AutoGenerateColumns = false;
        _clientStatusGrid.AllowUserToAddRows = false;
        _clientStatusGrid.AllowUserToDeleteRows = false;
        _clientStatusGrid.MultiSelect = false;
        _clientStatusGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _clientStatusGrid.ReadOnly = true;
        _clientStatusGrid.RowHeadersVisible = false;
        _clientStatusGrid.RowTemplate.Height = 45; // Increased row height
        _clientStatusGrid.DataSource = _clientStatusBinding;

        var statusCol = new DataGridViewImageColumn
        {
            Name = "StatusIcon",
            HeaderText = "Trạng thái",
            DataPropertyName = nameof(ClientDashboardRow.IsOnline),
            Width = 115,
            ImageLayout = DataGridViewImageCellLayout.Zoom
        };
        _clientStatusGrid.Columns.Add(statusCol);
        _clientStatusGrid.Columns.Add(CreateTextColumn("Máy", nameof(ClientDashboardRow.MachineName), 150));
        _clientStatusGrid.Columns.Add(CreateTextColumn("User", nameof(ClientDashboardRow.UserName), 120));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Tên CPU", nameof(ClientDashboardRow.CpuName), 170));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Tên Card", nameof(ClientDashboardRow.GpuName), 170));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Nhiệt CPU", nameof(ClientDashboardRow.CpuTemperatureText), 95));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Nguồn CPU", nameof(ClientDashboardRow.CpuPowerText), 95));
        _clientStatusGrid.Columns.Add(CreateTextColumn("VGA Load", nameof(ClientDashboardRow.VgaLoadText), 150));
        _clientStatusGrid.Columns.Add(CreateTextColumn("CPU Load", nameof(ClientDashboardRow.CpuLoadText), 150));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Đang chơi", nameof(ClientDashboardRow.CurrentGameName), 200));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Uptime", nameof(ClientDashboardRow.UptimeText), 90));
        _clientStatusGrid.Columns.Add(CreateTextColumn("RAM", nameof(ClientDashboardRow.MemoryText), 130));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Mạng", nameof(ClientDashboardRow.NetworkText), 150));
        _clientStatusGrid.Columns.Add(CreateTextColumn("File chạy", nameof(ClientDashboardRow.CurrentGameExecutablePath), 280));
        _clientStatusGrid.Columns.Add(CreateTextColumn("Cập nhật cuối", nameof(ClientDashboardRow.LastSeenLocalText), 160));
        _clientStatusGrid.Columns.Add(CreateTextColumn("File", nameof(ClientDashboardRow.SourceFileName), 150));
        
        _clientStatusGrid.CellFormatting += (s, e) =>
        {
            if (_clientStatusGrid.Columns[e.ColumnIndex].Name == "StatusIcon")
            {
                if (e.Value is bool isOnline)
                {
                    e.Value = isOnline ? _onlineIcon : _offlineIcon;
                    e.FormattingApplied = true;
                }
            }
        };
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
                MessageBox.Show($"Loi doi vi tri: {ex.Message}", I18n.Common.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        _gamesContextMenu.Items.Add(_scanManifestGameMenuItem);
        _gamesContextMenu.Items.Add(_moveTopGameMenuItem);
        _gamesContextMenu.Items.Add(_moveUpGameMenuItem);
        _gamesContextMenu.Items.Add(_moveDownGameMenuItem);
        _gamesContextMenu.Items.Add(new ToolStripSeparator());
        _gamesContextMenu.Items.Add(_markHotGameMenuItem);
        _gamesContextMenu.Items.Add(_unmarkHotGameMenuItem);
        _gamesContextMenu.Items.Add(new ToolStripSeparator());
        _gamesContextMenu.Items.Add(_viewManifestMenuItem);
        _gamesContextMenu.Opening += GamesContextMenu_Opening;
        _addGameMenuItem.Click += AddGameButton_Click;
        _editGameMenuItem.Click += EditGameButton_Click;
        _deleteGameMenuItem.Click += DeleteGameButton_Click;
        _scanManifestGameMenuItem.Click += ScanManifestButton_Click;
        _moveTopGameMenuItem.Click += async (_, _) => await ReorderSelectedGameAsync(-99999);
        _moveUpGameMenuItem.Click += async (_, _) => await ReorderSelectedGameAsync(-15);
        _moveDownGameMenuItem.Click += async (_, _) => await ReorderSelectedGameAsync(15);
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
        var hasMultipleGames = _gamesBinding.Count > 1;
        var position = _gamesBinding.Position;
        _addGameMenuItem.Enabled = true;
        _editGameMenuItem.Enabled = hasSelectedGame;
        _deleteGameMenuItem.Enabled = hasSelectedGame;
        _scanManifestGameMenuItem.Enabled = hasSelectedGame;
        _moveTopGameMenuItem.Enabled = hasSelectedGame && hasMultipleGames && position > 0;
        _moveUpGameMenuItem.Enabled = hasSelectedGame && hasMultipleGames && position > 0;
        _moveDownGameMenuItem.Enabled = hasSelectedGame && hasMultipleGames && position >= 0 && position < _gamesBinding.Count - 1;
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
            ShowInfo(I18n.Server.NeedSelectGameFirst);
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
        var fontFamily = string.IsNullOrWhiteSpace(_clientThemeFontFamily) ? I18n.Server.DefaultThemeFontFamily : _clientThemeFontFamily;
        var uiFont = new Font(fontFamily, uiFontSize, FontStyle.Regular);

        SuspendLayout();
        try
        {
            Font = uiFont;
            UpdateAllControlsFont(Controls, fontFamily, uiFontSize);

            ApplyDataGridFont(_gamesGrid, uiFont);
            ApplyDataGridFont(_resourcesGrid, uiFont);
            ApplyDataGridFont(_downloadMonitorGrid, uiFont);
            ApplyDataGridFont(_logsGrid, uiFont);
            ApplyDataGridFont(_clientStatusGrid, uiFont);

            // Force taller row height for the client status grid to fit the icon
            _clientStatusGrid.RowTemplate.Height = Math.Max(45, (int)Math.Ceiling(uiFont.Size * 3.4f));
            foreach (DataGridViewRow row in _clientStatusGrid.Rows)
            {
                row.Height = _clientStatusGrid.RowTemplate.Height;
            }

            _updateOutputTextBox.Font = new Font("Consolas", Math.Max(13f, uiFontSize), FontStyle.Regular);
            ApplyListItemSpacing(uiFontSize);
            ApplyButtonSizing(uiFontSize);
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    private void UpdateAllControlsFont(Control.ControlCollection controls, string fontFamily, float uiFontSize)
    {
        foreach (Control c in controls)
        {
            if (c is Label || c is Button || c is TextBox || c is ComboBox || c is CheckBox || c is NumericUpDown || c is ModernTabButton)
            {
                if (c != _updateOutputTextBox)
                {
                    var currentStyle = c.Font.Style;
                    c.Font = new Font(fontFamily, uiFontSize, currentStyle);
                }
            }

            if (c is TextBox textBox)
            {
                if (textBox != _updateOutputTextBox)
                {
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    textBox.BackColor = Color.White;
                    textBox.ForeColor = Color.FromArgb(15, 23, 42);
                }
            }
            else if (c is NumericUpDown numeric)
            {
                numeric.BorderStyle = BorderStyle.FixedSingle;
                numeric.BackColor = Color.White;
                numeric.ForeColor = Color.FromArgb(15, 23, 42);
            }
            else if (c is CheckBox checkBox)
            {
                checkBox.FlatStyle = FlatStyle.Flat;
                checkBox.ForeColor = Color.FromArgb(15, 23, 42);
                checkBox.FlatAppearance.BorderSize = 1;
                checkBox.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225); // slate-300
                checkBox.FlatAppearance.CheckedBackColor = Color.FromArgb(99, 102, 241); // indigo-500
                checkBox.FlatAppearance.MouseDownBackColor = Color.FromArgb(79, 70, 229);
                checkBox.FlatAppearance.MouseOverBackColor = Color.FromArgb(238, 242, 255);
            }

            if (c.HasChildren)
            {
                UpdateAllControlsFont(c.Controls, fontFamily, uiFontSize);
            }
        }
    }

    private static void ApplyDataGridFont(DataGridView grid, Font uiFont)
    {
        grid.Font = uiFont;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Color.FromArgb(241, 245, 249); // slate-100 (subtle grid lines)
        grid.RowHeadersVisible = false;
        
        // Header styles
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font(uiFont.FontFamily, uiFont.Size, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252); // slate 50 (#F8FAFC)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(15, 23, 42); // slate 900 (#0F172A)
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(248, 250, 252);
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 10, 8, 10);
        
        // Default cell styles
        grid.DefaultCellStyle.Font = uiFont;
        grid.DefaultCellStyle.Padding = new Padding(10, 10, 10, 10); // slightly taller cell padding
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59); // slate 800 (#1E293B)
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 255); // Indigo-50 (#EEF2FF)
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(99, 102, 241); // Indigo-500 (#6366F1)
        
        // Alternating row style
        grid.AlternatingRowsDefaultCellStyle.Font = uiFont;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252); // slate 50 (#F8FAFC)
        grid.AlternatingRowsDefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59); // slate 800 (#1E293B)
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 255); // Indigo-50 (#EEF2FF)
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.FromArgb(99, 102, 241); // Indigo-500 (#6366F1)

        grid.RowTemplate.Height = Math.Max(42, (int)Math.Ceiling(uiFont.Size * 3.0f));
        grid.ColumnHeadersHeight = Math.Max(48, (int)Math.Ceiling(uiFont.Size * 3.4f));
        EnsureColumnHeadersFit(grid);
    }

    private static void EnsureColumnHeadersFit(DataGridView grid)
    {
        var headerFont = grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font;
        foreach (DataGridViewColumn column in grid.Columns)
        {
            var headerText = column.HeaderText?.Trim();
            if (string.IsNullOrWhiteSpace(headerText))
            {
                continue;
            }

            var measured = TextRenderer.MeasureText(
                headerText + " ",
                headerFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

            var requiredWidth = measured.Width + 18;
            if (column.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill)
            {
                if (column.MinimumWidth < requiredWidth)
                {
                    column.MinimumWidth = requiredWidth;
                }

                continue;
            }

            if (column.Width < requiredWidth)
            {
                column.Width = requiredWidth;
            }
        }
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
            UiFontSizeMode.VerySmall => 9.5f,
            UiFontSizeMode.Small => 11.5f,
            UiFontSizeMode.Big => 16f,
            UiFontSizeMode.VeryBig => 19f,
            _ => 13.5f
        };
    }

    private static GameUpdater.WinForms.Controls.ModernButton CreateButton(string text, EventHandler onClick, bool primary = false)
    {
        var button = new GameUpdater.WinForms.Controls.ModernButton
        {
            Text = text
        };
        StyleButton(button, primary);
        button.Click += onClick;
        return button;
    }

    private static void StyleButton(Control button, bool primary = false)
    {
        if (!StyledButtons.Contains(button))
        {
            StyledButtons.Add(button);
        }

        StyledButtonPrimaryStates[button] = primary;
        StyledButtonTargetColors[button] = primary ? AccentColor : SecondaryButtonColor;

        if (button is GameUpdater.WinForms.Controls.ModernButton modernButton)
        {
            modernButton.IsPrimary = primary;
            modernButton.Disposed -= StyledButton_Disposed;
            modernButton.Disposed += StyledButton_Disposed;
        }
        else if (button is Button stdButton)
        {
            stdButton.AutoSize = false;
            stdButton.FlatStyle = FlatStyle.Flat;
            stdButton.FlatAppearance.BorderSize = 1;
            stdButton.FlatAppearance.BorderColor = primary ? AccentColor : ButtonBorderColor;
            stdButton.FlatAppearance.MouseOverBackColor = primary ? AccentColor : SecondaryButtonColor;
            stdButton.FlatAppearance.MouseDownBackColor = primary ? AccentHoverColor : SecondaryButtonHoverColor;
            stdButton.UseVisualStyleBackColor = false;
            stdButton.Cursor = Cursors.Hand;
            stdButton.Margin = new Padding(5, 1, 7, 1);
            stdButton.Padding = new Padding(ButtonHorizontalPadding, ButtonVerticalPadding, ButtonHorizontalPadding, ButtonVerticalPadding);
            stdButton.TextAlign = ContentAlignment.MiddleCenter;
            stdButton.BackColor = primary ? AccentColor : SecondaryButtonColor;
            stdButton.ForeColor = primary ? Color.White : SecondaryButtonTextColor;

            stdButton.MouseEnter -= StyledButton_MouseEnter;
            stdButton.MouseLeave -= StyledButton_MouseLeave;
            stdButton.MouseDown -= StyledButton_MouseDown;
            stdButton.MouseUp -= StyledButton_MouseUp;
            stdButton.Disposed -= StyledButton_Disposed;

            stdButton.MouseEnter += StyledButton_MouseEnter;
            stdButton.MouseLeave += StyledButton_MouseLeave;
            stdButton.MouseDown += StyledButton_MouseDown;
            stdButton.MouseUp += StyledButton_MouseUp;
            stdButton.Disposed += StyledButton_Disposed;
            stdButton.Region?.Dispose();
            stdButton.Region = null;
        }
    }

    private static void ApplyButtonSizing(float uiFontSize)
    {
        var baseHeight = Math.Max(32, (int)Math.Ceiling(uiFontSize * 2.75f));
        foreach (var button in StyledButtons.Where(button => !button.IsDisposed))
        {
            var measuredTextSize = TextRenderer.MeasureText(
                button.Text + " ",
                button.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

            var requiredHeight = Math.Max(baseHeight, measuredTextSize.Height + button.Padding.Vertical + 10);
            var requiredWidth = measuredTextSize.Width + button.Padding.Horizontal + 20;

            var parentAvailableHeight = button.Parent is null
                ? 0
                : button.Parent.ClientSize.Height - button.Margin.Vertical;

            if (parentAvailableHeight > 0)
            {
                requiredHeight = Math.Min(requiredHeight, parentAvailableHeight);
            }

            requiredHeight = Math.Max(28, requiredHeight);

            button.Height = requiredHeight;
            button.MinimumSize = button.Dock == DockStyle.None
                ? new Size(requiredWidth, requiredHeight)
                : new Size(0, 0);

            if (button.Dock == DockStyle.None)
            {
                button.Width = Math.Max(button.Width, requiredWidth);
            }
        }
    }

    private static void StyledButton_MouseEnter(object? sender, EventArgs e)
    {
        if (sender is not Control button) return;
        var primary = StyledButtonPrimaryStates.TryGetValue(button, out var isPrimary) && isPrimary;
        AnimateButtonColor(button, primary ? AccentHoverColor : SecondaryButtonHoverColor);
    }

    private static void StyledButton_MouseLeave(object? sender, EventArgs e)
    {
        if (sender is not Control button) return;
        var primary = StyledButtonPrimaryStates.TryGetValue(button, out var isPrimary) && isPrimary;
        AnimateButtonColor(button, primary ? AccentColor : SecondaryButtonColor);
    }

    private static void StyledButton_MouseDown(object? sender, MouseEventArgs e)
    {
        if (sender is not Control button) return;
        var primary = StyledButtonPrimaryStates.TryGetValue(button, out var isPrimary) && isPrimary;
        AnimateButtonColor(button, primary ? Color.FromArgb(29, 78, 216) : Color.FromArgb(71, 85, 105));
    }

    private static void StyledButton_MouseUp(object? sender, MouseEventArgs e)
    {
        if (sender is not Control button) return;
        var primary = StyledButtonPrimaryStates.TryGetValue(button, out var isPrimary) && isPrimary;
        var isHovering = button.ClientRectangle.Contains(button.PointToClient(Cursor.Position));
        AnimateButtonColor(button, isHovering ? primary ? AccentHoverColor : SecondaryButtonHoverColor : primary ? AccentColor : SecondaryButtonColor);
    }

    private static void StyledButton_Disposed(object? sender, EventArgs e)
    {
        if (sender is not Control button) return;
        StyledButtons.Remove(button);
        StyledButtonPrimaryStates.Remove(button);
        StyledButtonTargetColors.Remove(button);
    }

    private static void AnimateButtonColor(Control button, Color targetColor)
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

    private void GamesGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics is null)
        {
            return;
        }

        if (_gamesGrid.Columns[e.ColumnIndex].Name == HotColumnName)
        {
            e.PaintBackground(e.CellBounds, true);

            var isChecked = false;
            if (e.Value is bool val)
            {
                isChecked = val;
            }

            int size = 20;
            int x = e.CellBounds.X + (e.CellBounds.Width - size) / 2;
            int y = e.CellBounds.Y + (e.CellBounds.Height - size) / 2;
            var boxRect = new Rectangle(x, y, size, size);

            using var brush = new SolidBrush(isChecked ? Color.FromArgb(99, 102, 241) : Color.White); // Indigo-500 when checked
            using var borderPen = new Pen(isChecked ? Color.FromArgb(99, 102, 241) : Color.FromArgb(203, 213, 225), 1.5f); // slate-300 when unchecked

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            FillRoundedRectangle(e.Graphics, brush, boxRect, 4);
            DrawRoundedRectangle(e.Graphics, borderPen, boxRect, 4);

            if (isChecked)
            {
                using var checkPen = new Pen(Color.White, 2.5f);
                checkPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                checkPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                e.Graphics.DrawLine(checkPen, x + 5, y + 10, x + 9, y + 14);
                e.Graphics.DrawLine(checkPen, x + 9, y + 14, x + 15, y + 6);
            }

            e.Handled = true;
        }
    }

    private static void FillRoundedRectangle(Graphics g, Brush brush, Rectangle rect, int radius)
    {
        using var path = GetRoundedRectPath(rect, radius);
        g.FillPath(brush, path);
    }

    private static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle rect, int radius)
    {
        using var path = GetRoundedRectPath(rect, radius);
        g.DrawPath(pen, path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        var arcRect = new Rectangle(rect.X, rect.Y, diameter, diameter);

        path.AddArc(arcRect, 180, 90);
        arcRect.X = rect.Right - diameter;
        path.AddArc(arcRect, 270, 90);
        arcRect.Y = rect.Bottom - diameter;
        path.AddArc(arcRect, 0, 90);
        arcRect.X = rect.X;
        path.AddArc(arcRect, 90, 90);
        path.CloseFigure();
        return path;
    }
}







