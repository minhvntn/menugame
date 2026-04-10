using System.Drawing.Drawing2D;
using GameLauncher.Client.Models;

namespace GameLauncher.Client.Controls;

public sealed class GameCardControl : UserControl
{
    private readonly LauncherGameRow _row;
    private readonly Action<LauncherGameRow> _playAction;
    private readonly Action<LauncherGameRow> _openFolderAction;

    public GameCardControl(
        LauncherGameRow row,
        Action<LauncherGameRow> playAction,
        Action<LauncherGameRow> openFolderAction)
    {
        _row = row;
        _playAction = playAction;
        _openFolderAction = openFolderAction;

        Width = 220;
        Height = 280;
        Margin = new Padding(10);
        BackColor = Color.White;
        BorderStyle = BorderStyle.FixedSingle;

        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 7,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var iconBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = LoadGameImage(_row)
        };

        var nameLabel = new Label
        {
            Text = _row.Name,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };

        var categoryLabel = new Label
        {
            Text = $"Nhóm: {_row.Category}",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var versionLabel = new Label
        {
            Text = $"Phiên bản: {_row.Version}",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var statusLabel = new Label
        {
            Text = $"Trạng thái: {_row.Status}",
            Dock = DockStyle.Fill,
            ForeColor = string.Equals(_row.Status, "Sẵn sàng", StringComparison.OrdinalIgnoreCase)
                ? Color.ForestGreen
                : Color.Firebrick,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var actionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

        var playButton = new Button
        {
            Text = "Chơi",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(27, 132, 255),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        playButton.FlatAppearance.BorderSize = 0;
        playButton.Click += (_, _) => _playAction(_row);

        var folderButton = new Button
        {
            Text = "Mở",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat
        };
        folderButton.Click += (_, _) => _openFolderAction(_row);

        actionsPanel.Controls.Add(playButton, 0, 0);
        actionsPanel.Controls.Add(folderButton, 1, 0);

        root.Controls.Add(iconBox, 0, 0);
        root.Controls.Add(nameLabel, 0, 1);
        root.Controls.Add(categoryLabel, 0, 2);
        root.Controls.Add(versionLabel, 0, 3);
        root.Controls.Add(statusLabel, 0, 4);
        root.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 5);
        root.Controls.Add(actionsPanel, 0, 6);

        Controls.Add(root);

        foreach (Control control in root.Controls)
        {
            control.DoubleClick += (_, _) => _playAction(_row);
        }
    }

    private static Image LoadGameImage(LauncherGameRow row)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(row.ResolvedExecutablePath) && File.Exists(row.ResolvedExecutablePath))
            {
                using var icon = Icon.ExtractAssociatedIcon(row.ResolvedExecutablePath);
                if (icon is not null)
                {
                    return CreateRoundedImage(icon.ToBitmap());
                }
            }
        }
        catch
        {
            // Fallback to app icon.
        }

        return CreateRoundedImage(SystemIcons.Application.ToBitmap());
    }

    private static Image CreateRoundedImage(Image source)
    {
        var size = 80;
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var path = new GraphicsPath();
        path.AddEllipse(0, 0, size - 1, size - 1);
        graphics.SetClip(path);
        graphics.DrawImage(source, 0, 0, size, size);

        return bitmap;
    }
}
