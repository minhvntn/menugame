using System.Drawing.Drawing2D;
using GameLauncher.Client.Models;

namespace GameLauncher.Client.Controls;

public sealed class GameCardControl : UserControl
{
    private readonly LauncherGameRow _row;
    private readonly Action<LauncherGameRow> _playAction;

    public GameCardControl(
        LauncherGameRow row,
        Action<LauncherGameRow> playAction)
    {
        _row = row;
        _playAction = playAction;

        Width = 190;
        Height = 180;
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
            RowCount = 3,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var iconBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = LoadGameImage(_row),
            Cursor = Cursors.Hand
        };
        iconBox.Click += (_, _) => _playAction(_row);
        iconBox.DoubleClick += (_, _) => _playAction(_row);

        var nameLabel = new Label
        {
            Text = _row.Name,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };

        root.Controls.Add(iconBox, 0, 0);
        root.Controls.Add(nameLabel, 0, 1);
        root.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 2);

        Controls.Add(root);
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
