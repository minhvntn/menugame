using GameUpdater.Shared.Models;
using GameUpdater.Shared.Localization;

namespace GameUpdater.WinForms.Forms;

public sealed class GameEditorForm : Form
{
    private static readonly string[] CategoryOptions = { "Online", "Offline", "Tools" };

    private readonly TextBox _nameTextBox = new() { Dock = DockStyle.Fill };
    private readonly GameUpdater.WinForms.Controls.ModernComboBox _categoryComboBox = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList
    };
    private readonly TextBox _pathTextBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _versionTextBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _launchPathTextBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _launchArgumentsTextBox = new() { Dock = DockStyle.Fill };
    private readonly GameUpdater.WinForms.Controls.ModernCheckBox _isHotCheckBox = new()
    {
        Dock = DockStyle.Left,
        AutoSize = true,
        Text = I18n.Server.GameEditorHotCheckbox
    };
    private readonly TextBox _notesTextBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical
    };
    private readonly GameRecord? _existingGame;

    public GameEditorForm(GameRecord? existingGame = null, Font? parentFont = null)
    {
        _existingGame = existingGame;
        if (parentFont is not null)
        {
            this.Font = parentFont;
        }
        else
        {
            this.Font = new Font("Segoe UI", 12f);
        }

        Text = existingGame is null ? I18n.Server.GameEditorAddTitle : I18n.Server.GameEditorEditTitle;
        StartPosition = FormStartPosition.CenterParent;
        Width = (int)Math.Ceiling(this.Font.Size * 50);
        Height = (int)Math.Ceiling(this.Font.Size * 35);
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(248, 250, 252); // slate 50

        var rowHeight = Math.Max(38, (int)Math.Ceiling(this.Font.Size * 2.6f));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 9,
            Padding = new Padding(12),
            BackColor = Color.Transparent
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)Math.Ceiling(this.Font.Size * 10)));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)Math.Ceiling(this.Font.Size * 6.5f)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight + 10));

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorName), 0, 0);
        root.Controls.Add(_nameTextBox, 1, 0);
        root.SetColumnSpan(_nameTextBox, 2);

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorCategory), 0, 1);
        _categoryComboBox.Items.AddRange(CategoryOptions);
        root.Controls.Add(_categoryComboBox, 1, 1);
        root.SetColumnSpan(_categoryComboBox, 2);

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorInstallPath), 0, 2);
        root.Controls.Add(_pathTextBox, 1, 2);

        var browseInstallButton = new GameUpdater.WinForms.Controls.ModernButton
        {
            Text = I18n.Common.SelectButton,
            Dock = DockStyle.Fill,
            ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary,
            IconType = GameUpdater.WinForms.Controls.ButtonIconType.Folder
        };
        browseInstallButton.Click += BrowseInstallButton_Click;
        StyleFormButton(browseInstallButton);
        root.Controls.Add(browseInstallButton, 2, 2);

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorVersion), 0, 3);
        root.Controls.Add(_versionTextBox, 1, 3);
        root.SetColumnSpan(_versionTextBox, 2);

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorExe), 0, 4);
        root.Controls.Add(_launchPathTextBox, 1, 4);

        var browseLaunchButton = new GameUpdater.WinForms.Controls.ModernButton
        {
            Text = I18n.Common.SelectButton,
            Dock = DockStyle.Fill,
            ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary,
            IconType = GameUpdater.WinForms.Controls.ButtonIconType.Folder
        };
        browseLaunchButton.Click += BrowseLaunchButton_Click;
        StyleFormButton(browseLaunchButton);
        root.Controls.Add(browseLaunchButton, 2, 4);

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorLaunchArgs), 0, 5);
        root.Controls.Add(_launchArgumentsTextBox, 1, 5);
        root.SetColumnSpan(_launchArgumentsTextBox, 2);

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorClientVisible), 0, 6);
        root.Controls.Add(_isHotCheckBox, 1, 6);
        root.SetColumnSpan(_isHotCheckBox, 2);

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorNotes), 0, 7);
        root.Controls.Add(_notesTextBox, 1, 7);
        root.SetColumnSpan(_notesTextBox, 2);

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent
        };

        var saveButton = new GameUpdater.WinForms.Controls.ModernButton
        {
            Text = I18n.Common.SaveButton,
            ColorType = GameUpdater.WinForms.Controls.ButtonColorType.PrimaryBlue,
            IconType = GameUpdater.WinForms.Controls.ButtonIconType.Save
        };
        saveButton.Click += SaveButton_Click;
        StyleFormButton(saveButton, primary: true);

        var cancelButton = new GameUpdater.WinForms.Controls.ModernButton
        {
            Text = I18n.Common.CancelButton,
            ColorType = GameUpdater.WinForms.Controls.ButtonColorType.Secondary,
            IconType = GameUpdater.WinForms.Controls.ButtonIconType.Cancel
        };
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
        StyleFormButton(cancelButton);

        buttonsPanel.Controls.Add(saveButton);
        buttonsPanel.Controls.Add(cancelButton);
        root.Controls.Add(buttonsPanel, 0, 8);
        root.SetColumnSpan(buttonsPanel, 3);

        Controls.Add(root);

        if (existingGame is not null)
        {
            _nameTextBox.Text = existingGame.Name;
            SelectCategory(existingGame.Category);
            _pathTextBox.Text = existingGame.InstallPath;
            _versionTextBox.Text = existingGame.Version;
            _launchPathTextBox.Text = existingGame.LaunchRelativePath;
            _launchArgumentsTextBox.Text = existingGame.LaunchArguments;
            _isHotCheckBox.Checked = existingGame.IsHot;
            _notesTextBox.Text = existingGame.Notes;
        }
        else
        {
            SelectCategory(I18n.Server.GameEditorDefaultCategory);
            _versionTextBox.Text = I18n.Server.GameEditorDefaultVersion;
        }

        StyleControlsRecursively(Controls);
    }

    public GameRecord? EditedGame { get; private set; }

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

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var selectedFile = dialog.FileName;
        if (Directory.Exists(_pathTextBox.Text))
        {
            try
            {
                var relativePath = Path.GetRelativePath(_pathTextBox.Text, selectedFile);
                if (!relativePath.StartsWith("..", StringComparison.Ordinal))
                {
                    _launchPathTextBox.Text = relativePath;
                    return;
                }
            }
            catch
            {
                // Keep absolute path when relative conversion fails.
            }
        }

        _launchPathTextBox.Text = selectedFile;
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

        if (string.IsNullOrWhiteSpace(_launchPathTextBox.Text))
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
            LaunchRelativePath = _launchPathTextBox.Text.Trim(),
            LaunchArguments = _launchArgumentsTextBox.Text.Trim(),
            IsHot = _isHotCheckBox.Checked,
            Notes = _notesTextBox.Text.Trim(),
            LastScannedAt = _existingGame?.LastScannedAt,
            LastUpdatedAt = _existingGame?.LastUpdatedAt,
            SortOrder = _existingGame?.SortOrder ?? 999999
        };

        DialogResult = DialogResult.OK;
        Close();
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private void SelectCategory(string? category)
    {
        var normalized = category?.Trim() ?? string.Empty;
        var selectedCategory = CategoryOptions.FirstOrDefault(option =>
            string.Equals(option, normalized, StringComparison.OrdinalIgnoreCase))
            ?? I18n.Server.GameEditorDefaultCategory;

        _categoryComboBox.SelectedItem = selectedCategory;
        if (_categoryComboBox.SelectedIndex < 0)
        {
            _categoryComboBox.SelectedIndex = 0;
        }
    }

    private void StyleFormButton(Control button, bool primary = false)
    {
        if (button is GameUpdater.WinForms.Controls.ModernButton modernButton)
        {
            modernButton.IsPrimary = primary;
        }
        else if (button is Button stdButton)
        {
            stdButton.FlatStyle = FlatStyle.Flat;
            stdButton.FlatAppearance.BorderSize = 1;
            stdButton.FlatAppearance.BorderColor = primary ? Color.FromArgb(37, 99, 235) : Color.FromArgb(203, 213, 225);
            stdButton.BackColor = primary ? Color.FromArgb(37, 99, 235) : Color.FromArgb(241, 245, 249);
            stdButton.ForeColor = primary ? Color.White : Color.FromArgb(15, 23, 42);
            stdButton.UseVisualStyleBackColor = false;
        }
        button.Cursor = Cursors.Hand;
        button.Height = Math.Max(32, (int)Math.Ceiling(this.Font.Size * 2.5f));
        button.Width = Math.Max(90, (int)Math.Ceiling(this.Font.Size * 7.5f));
    }

    private void StyleControlsRecursively(Control.ControlCollection controls)
    {
        foreach (Control c in controls)
        {
            if (c is TextBox textBox)
            {
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = Color.White;
                textBox.ForeColor = Color.FromArgb(15, 23, 42);
            }
            else if (c is CheckBox checkBox)
            {
                checkBox.FlatStyle = FlatStyle.Flat;
                checkBox.ForeColor = Color.FromArgb(15, 23, 42);
                checkBox.FlatAppearance.BorderSize = 1;
                checkBox.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
                checkBox.FlatAppearance.CheckedBackColor = Color.FromArgb(99, 102, 241);
                checkBox.FlatAppearance.MouseDownBackColor = Color.FromArgb(79, 70, 229);
                checkBox.FlatAppearance.MouseOverBackColor = Color.FromArgb(238, 242, 255);
            }
            else if (c is ComboBox comboBox)
            {
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.BackColor = Color.White;
                comboBox.ForeColor = Color.FromArgb(15, 23, 42);
            }

            if (c.HasChildren)
            {
                StyleControlsRecursively(c.Controls);
            }
        }
    }
}
