using GameUpdater.Shared.Models;
using GameUpdater.Shared.Localization;

namespace GameUpdater.WinForms.Forms;

public sealed class GameEditorForm : Form
{
    private readonly TextBox _nameTextBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _categoryTextBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _pathTextBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _versionTextBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _launchPathTextBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _launchArgumentsTextBox = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _isHotCheckBox = new()
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

    public GameEditorForm(GameRecord? existingGame = null)
    {
        _existingGame = existingGame;

        Text = existingGame is null ? I18n.Server.GameEditorAddTitle : I18n.Server.GameEditorEditTitle;
        StartPosition = FormStartPosition.CenterParent;
        Width = 740;
        Height = 520;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 9,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorName), 0, 0);
        root.Controls.Add(_nameTextBox, 1, 0);
        root.SetColumnSpan(_nameTextBox, 2);

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorCategory), 0, 1);
        root.Controls.Add(_categoryTextBox, 1, 1);
        root.SetColumnSpan(_categoryTextBox, 2);

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorInstallPath), 0, 2);
        root.Controls.Add(_pathTextBox, 1, 2);

        var browseInstallButton = new Button
        {
            Text = I18n.Common.SelectButton,
            Dock = DockStyle.Fill
        };
        browseInstallButton.Click += BrowseInstallButton_Click;
        root.Controls.Add(browseInstallButton, 2, 2);

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorVersion), 0, 3);
        root.Controls.Add(_versionTextBox, 1, 3);
        root.SetColumnSpan(_versionTextBox, 2);

        root.Controls.Add(CreateLabel(I18n.Server.GameEditorExe), 0, 4);
        root.Controls.Add(_launchPathTextBox, 1, 4);

        var browseLaunchButton = new Button
        {
            Text = I18n.Common.SelectButton,
            Dock = DockStyle.Fill
        };
        browseLaunchButton.Click += BrowseLaunchButton_Click;
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
            FlowDirection = FlowDirection.RightToLeft
        };

        var saveButton = new Button
        {
            Text = I18n.Common.SaveButton,
            Width = 90
        };
        saveButton.Click += SaveButton_Click;

        var cancelButton = new Button
        {
            Text = I18n.Common.CancelButton,
            Width = 90
        };
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        buttonsPanel.Controls.Add(saveButton);
        buttonsPanel.Controls.Add(cancelButton);
        root.Controls.Add(buttonsPanel, 0, 8);
        root.SetColumnSpan(buttonsPanel, 3);

        Controls.Add(root);

        if (existingGame is not null)
        {
            _nameTextBox.Text = existingGame.Name;
            _categoryTextBox.Text = existingGame.Category;
            _pathTextBox.Text = existingGame.InstallPath;
            _versionTextBox.Text = existingGame.Version;
            _launchPathTextBox.Text = existingGame.LaunchRelativePath;
            _launchArgumentsTextBox.Text = existingGame.LaunchArguments;
            _isHotCheckBox.Checked = existingGame.IsHot;
            _notesTextBox.Text = existingGame.Notes;
        }
        else
        {
            _categoryTextBox.Text = I18n.Server.GameEditorDefaultCategory;
            _versionTextBox.Text = I18n.Server.GameEditorDefaultVersion;
        }
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
            Category = string.IsNullOrWhiteSpace(_categoryTextBox.Text) ? I18n.Server.DefaultCategoryFallback : _categoryTextBox.Text.Trim(),
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
}
