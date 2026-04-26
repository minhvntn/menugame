using System.Drawing.Drawing2D;
using System.Text;
using GameUpdater.Shared.Models;

namespace GameUpdater.WinForms.Controls;

public sealed class ServerGameCardControl : UserControl
{
    private const int IconSize = 40;
    private const int CardWidth = 96;
    private const int CardHeight = 94;

    private static readonly object IconCacheSyncRoot = new();
    private static readonly Dictionary<string, Image> IconCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly GameRecord _row;
    private readonly Action<GameRecord> _clickAction;
    private readonly Font _nameFont = new("Segoe UI Semibold", 8f, FontStyle.Bold);
    private bool _isSelected;

    public ServerGameCardControl(GameRecord row, Action<GameRecord> clickAction)
    {
        _row = row;
        _clickAction = clickAction;

        Width = CardWidth;
        Height = CardHeight;
        Margin = new Padding(8, 10, 8, 10);
        Padding = new Padding(2);
        BackColor = Color.FromArgb(24, 39, 54);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;

        BuildLayout();
        WireCardClick(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _nameFont.Dispose();
        }

        base.Dispose(disposing);
    }

    public GameRecord GameRecord => _row;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                BackColor = _isSelected ? Color.FromArgb(41, 64, 88) : Color.FromArgb(24, 39, 54);
                Invalidate();
            }
        }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var iconWrap = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        if (_row.IsHot)
        {
            var hotBadge = new Label
            {
                Text = "HOT",
                AutoSize = false,
                Width = 32,
                Height = 16,
                Left = 2,
                Top = 2,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(205, 67, 57)
            };
            hotBadge.Click += (_, _) => _clickAction(_row);
            hotBadge.DoubleClick += (_, _) => _clickAction(_row);
            iconWrap.Controls.Add(hotBadge);
            hotBadge.BringToFront();
        }

        var iconBox = new PictureBox
        {
            Width = IconSize,
            Height = IconSize,
            Anchor = AnchorStyles.None,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = LoadGameImage(_row),
            BackColor = Color.Transparent
        };
        iconWrap.Controls.Add(iconBox);
        CenterIconInPanel(iconWrap, iconBox);
        iconWrap.Resize += (_, _) => CenterIconInPanel(iconWrap, iconBox);

        var nameLabel = new Label
        {
            Text = BuildTwoLineText(_row.Name, _nameFont, CardWidth - 8),
            Dock = DockStyle.Fill,
            Font = _nameFont,
            TextAlign = ContentAlignment.TopCenter,
            AutoEllipsis = false,
            ForeColor = Color.FromArgb(236, 245, 252),
            Padding = new Padding(1, 1, 1, 0)
        };
        nameLabel.UseCompatibleTextRendering = true;

        root.Controls.Add(iconWrap, 0, 0);
        root.Controls.Add(nameLabel, 0, 1);

        Controls.Add(root);

        WireCardClick(root);
        WireCardClick(iconWrap);
        WireCardClick(iconBox);
        WireCardClick(nameLabel);
    }

    private void WireCardClick(Control control)
    {
        control.Click += (_, _) => _clickAction(_row);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_isSelected)
        {
            using var pen = new Pen(Color.FromArgb(178, 204, 224), 2f);
            e.Graphics.DrawRectangle(pen, 1, 1, Width - 2, Height - 2);
        }
    }

    private static Image LoadGameImage(GameRecord row)
    {
        var exePath = ResolveExecutablePath(row);
        var cacheKey = string.IsNullOrWhiteSpace(exePath) ? "__default__" : exePath;

        lock (IconCacheSyncRoot)
        {
            if (IconCache.TryGetValue(cacheKey, out var cachedImage))
            {
                return cachedImage;
            }
        }

        var image = CreateGameImage(exePath);
        lock (IconCacheSyncRoot)
        {
            if (IconCache.TryGetValue(cacheKey, out var existingImage))
            {
                image.Dispose();
                return existingImage;
            }

            IconCache[cacheKey] = image;
            return image;
        }
    }

    private static Image CreateGameImage(string exePath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                using var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon is not null)
                {
                    using var iconBitmap = icon.ToBitmap();
                    return CreateRoundedImage(iconBitmap);
                }
            }
        }
        catch
        {
            // Fallback
        }

        using var fallbackBitmap = SystemIcons.Application.ToBitmap();
        return CreateRoundedImage(fallbackBitmap);
    }

    private static string ResolveExecutablePath(GameRecord row)
    {
        if (string.IsNullOrWhiteSpace(row.InstallPath) || string.IsNullOrWhiteSpace(row.LaunchRelativePath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(Path.Combine(row.InstallPath, row.LaunchRelativePath));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Image CreateRoundedImage(Image source)
    {
        var size = IconSize;
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var path = new GraphicsPath();
        path.AddEllipse(1, 1, size - 3, size - 3);
        graphics.SetClip(path);
        graphics.DrawImage(source, 0, 0, size, size);

        using var borderPen = new Pen(Color.FromArgb(178, 204, 224), 1f);
        graphics.ResetClip();
        graphics.DrawEllipse(borderPen, 1, 1, size - 3, size - 3);

        return bitmap;
    }

    private static void CenterIconInPanel(Panel hostPanel, Control iconControl)
    {
        iconControl.Left = Math.Max(0, (hostPanel.ClientSize.Width - iconControl.Width) / 2);
        iconControl.Top = Math.Max(0, (hostPanel.ClientSize.Height - iconControl.Height) / 2);
    }

    private static string BuildTwoLineText(string text, Font font, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = string.Join(" ", text.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;

        var lines = new List<string>(2);
        var index = 0;

        while (index < normalized.Length && lines.Count < 2)
        {
            var currentLine = new StringBuilder();
            while (index < normalized.Length)
            {
                var candidate = currentLine.ToString() + normalized[index];
                if (currentLine.Length > 0 && TextRenderer.MeasureText(candidate, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width > maxWidth) break;
                currentLine.Append(normalized[index]);
                index++;
            }
            var lineText = currentLine.ToString().Trim();
            if (lineText.Length == 0 && index < normalized.Length) { lineText = normalized[index].ToString(); index++; }
            lines.Add(lineText);
        }

        if (lines.Count == 0) return string.Empty;
        if (index < normalized.Length)
        {
            if (lines.Count < 2) lines.Add(string.Empty);
            var workingLine = (lines[1] ?? string.Empty).TrimEnd();
            while (workingLine.Length > 0 && TextRenderer.MeasureText(workingLine + "...", font).Width > maxWidth) workingLine = workingLine[..^1].TrimEnd();
            lines[1] = workingLine.Length == 0 ? "..." : workingLine + "...";
        }
        return string.Join(Environment.NewLine, lines.Take(2));
    }
}
