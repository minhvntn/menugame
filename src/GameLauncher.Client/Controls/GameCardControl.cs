using System.Text;
using GameLauncher.Client.Models;

namespace GameLauncher.Client.Controls;

public sealed class GameCardControl : UserControl
{
    private static readonly object IconCacheSyncRoot = new();
    private static readonly Dictionary<string, Image> IconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> IconLoadsInFlight = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, Image> PlaceholderCache = new();

    private static readonly object TextCacheSyncRoot = new();
    private static readonly Dictionary<string, string> TwoLineTextCache = new(StringComparer.Ordinal);

    private readonly LauncherGameRow _row;
    private readonly Action<LauncherGameRow> _playAction;
    private readonly bool _isHotRow;
    private readonly int _iconSize;
    private readonly int _cardWidth;
    private readonly int _cardHeight;
    private readonly int _tileSize;
    private readonly Font _nameFont;
    private readonly PictureBox _iconBox = new();
    private readonly string _resolvedExecutablePath;
    private readonly string _iconCacheKey;

    public LauncherGameRow Row => _row;

    public GameCardControl(LauncherGameRow row, Action<LauncherGameRow> playAction, bool isHotRow, string fontFamily)
    {
        _row = row;
        _playAction = playAction;
        _isHotRow = isHotRow;

        _iconSize = _isHotRow ? 60 : 40;
        _tileSize = _isHotRow ? 78 : 56;
        _cardWidth = _isHotRow ? 118 : 96;
        _cardHeight = _isHotRow ? 114 : 98;
        _nameFont = new Font(string.IsNullOrWhiteSpace(fontFamily) ? "Segoe UI" : fontFamily, _isHotRow ? 8.5f : 8f, FontStyle.Bold);

        _resolvedExecutablePath = NormalizeExecutablePath(_row.ResolvedExecutablePath);
        _iconCacheKey = BuildIconCacheKey(_resolvedExecutablePath, _iconSize);

        Width = _cardWidth;
        Height = _cardHeight;
        Margin = _isHotRow ? new Padding(9, 0, 9, 0) : new Padding(8, 8, 8, 8);
        Padding = new Padding(2);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;

        BuildLayout();
        QueueIconLoadIfNeeded();
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, _tileSize + 8));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var hostPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        var iconTile = new Panel
        {
            Width = _tileSize,
            Height = _tileSize,
            BackColor = Color.FromArgb(30, 41, 59)
        };

        _iconBox.Width = _iconSize;
        _iconBox.Height = _iconSize;
        _iconBox.SizeMode = PictureBoxSizeMode.Zoom;
        _iconBox.Image = GetCachedImageOrPlaceholder(_iconCacheKey, _iconSize);
        _iconBox.BackColor = Color.Transparent;

        iconTile.Controls.Add(_iconBox);
        CenterControl(iconTile, _iconBox);
        iconTile.Resize += (_, _) => CenterControl(iconTile, _iconBox);

        hostPanel.Controls.Add(iconTile);
        CenterControl(hostPanel, iconTile);
        hostPanel.Resize += (_, _) => CenterControl(hostPanel, iconTile);

        var nameLabel = new Label
        {
            Text = BuildTwoLineTextCached(_row.Name, _nameFont, _cardWidth - 10),
            Dock = DockStyle.Fill,
            Font = _nameFont,
            TextAlign = ContentAlignment.TopCenter,
            AutoEllipsis = false,
            ForeColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(2, 1, 2, 0)
        };

        root.Controls.Add(hostPanel, 0, 0);
        root.Controls.Add(nameLabel, 0, 1);

        Controls.Add(root);

        WireCardClick(root);
        WireCardClick(hostPanel);
        WireCardClick(iconTile);
        WireCardClick(_iconBox);
        WireCardClick(nameLabel);
    }

    private void QueueIconLoadIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(_resolvedExecutablePath))
        {
            return;
        }

        lock (IconCacheSyncRoot)
        {
            if (IconCache.TryGetValue(_iconCacheKey, out var cachedImage))
            {
                ApplyLoadedIcon(cachedImage);
                return;
            }

            if (!IconLoadsInFlight.Add(_iconCacheKey))
            {
                return;
            }
        }

        _ = Task.Run(() =>
        {
            Image? createdImage = null;
            Image? targetImage = null;

            try
            {
                createdImage = CreateGameImage(_resolvedExecutablePath, _iconSize);

                lock (IconCacheSyncRoot)
                {
                    if (IconCache.TryGetValue(_iconCacheKey, out var existingImage))
                    {
                        targetImage = existingImage;
                    }
                    else
                    {
                        IconCache[_iconCacheKey] = createdImage;
                        targetImage = createdImage;
                        createdImage = null;
                    }

                    IconLoadsInFlight.Remove(_iconCacheKey);
                }

                if (targetImage is not null)
                {
                    ApplyLoadedIcon(targetImage);
                }
            }
            catch
            {
                lock (IconCacheSyncRoot)
                {
                    IconLoadsInFlight.Remove(_iconCacheKey);
                }
            }
            finally
            {
                createdImage?.Dispose();
            }
        });
    }

    private void ApplyLoadedIcon(Image image)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => ApplyLoadedIcon(image)));
            }
            catch
            {
                // Ignore cross-thread update failures during form disposal.
            }

            return;
        }

        if (IsDisposed || _iconBox.IsDisposed)
        {
            return;
        }

        _iconBox.Image = image;
        _iconBox.Invalidate();
    }

    private void WireCardClick(Control control)
    {
        control.Click += (_, _) => _playAction(_row);
        control.DoubleClick += (_, _) => _playAction(_row);
    }

    private static Image GetCachedImageOrPlaceholder(string cacheKey, int iconSize)
    {
        lock (IconCacheSyncRoot)
        {
            if (IconCache.TryGetValue(cacheKey, out var cachedImage))
            {
                return cachedImage;
            }

            if (!PlaceholderCache.TryGetValue(iconSize, out var placeholder))
            {
                placeholder = CreateFallbackImage(iconSize);
                PlaceholderCache[iconSize] = placeholder;
            }

            return placeholder;
        }
    }

    private static Image CreateGameImage(string executablePath, int iconSize)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
            {
                using var icon = Icon.ExtractAssociatedIcon(executablePath);
                if (icon is not null)
                {
                    using var iconBitmap = icon.ToBitmap();
                    return CreatePlainIcon(iconBitmap, iconSize);
                }
            }
        }
        catch
        {
            // Fall back to default icon.
        }

        return CreateFallbackImage(iconSize);
    }

    private static Image CreateFallbackImage(int iconSize)
    {
        using var fallbackBitmap = SystemIcons.Application.ToBitmap();
        return CreatePlainIcon(fallbackBitmap, iconSize);
    }

    private static string NormalizeExecutablePath(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(executablePath);
        }
        catch
        {
            return executablePath.Trim();
        }
    }

    private static string BuildIconCacheKey(string executablePath, int iconSize)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return $"__default__:{iconSize}";
        }

        return $"{executablePath}:{iconSize}";
    }

    private static Image CreatePlainIcon(Image source, int iconSize)
    {
        var bitmap = new Bitmap(iconSize, iconSize);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.Clear(Color.Transparent);
        graphics.DrawImage(source, 0, 0, iconSize, iconSize);
        return bitmap;
    }

    private static void CenterControl(Control hostPanel, Control childControl)
    {
        childControl.Left = Math.Max(0, (hostPanel.ClientSize.Width - childControl.Width) / 2);
        childControl.Top = Math.Max(0, (hostPanel.ClientSize.Height - childControl.Height) / 2);
    }

    private static string BuildTwoLineTextCached(string text, Font font, int maxWidth)
    {
        var cacheKey = $"{font.Name}|{font.SizeInPoints:F2}|{(int)font.Style}|{maxWidth}|{text}";

        lock (TextCacheSyncRoot)
        {
            if (TwoLineTextCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
        }

        var value = BuildTwoLineText(text, font, maxWidth);

        lock (TextCacheSyncRoot)
        {
            if (TwoLineTextCache.Count > 4096)
            {
                TwoLineTextCache.Clear();
            }

            TwoLineTextCache[cacheKey] = value;
        }

        return value;
    }

    private static string BuildTwoLineText(string text, Font font, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = NormalizeWhitespace(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lines = new List<string>(2);
        var index = 0;

        while (index < normalized.Length && lines.Count < 2)
        {
            var currentLine = new StringBuilder();
            while (index < normalized.Length)
            {
                currentLine.Append(normalized[index]);
                var candidate = currentLine.ToString();
                if (currentLine.Length > 1 && MeasureSingleLineWidth(candidate, font) > maxWidth)
                {
                    currentLine.Length -= 1;
                    break;
                }

                index++;
            }

            var lineText = currentLine.ToString().Trim();
            if (lineText.Length == 0 && index < normalized.Length)
            {
                lineText = normalized[index].ToString();
                index++;
            }

            lines.Add(lineText);
        }

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        if (index < normalized.Length)
        {
            if (lines.Count < 2)
            {
                lines.Add(string.Empty);
            }

            lines[1] = FitLineWithEllipsis(lines[1], font, maxWidth);
        }

        return string.Join(Environment.NewLine, lines.Take(2));
    }

    private static string NormalizeWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var previousIsWhitespace = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                if (previousIsWhitespace)
                {
                    continue;
                }

                builder.Append(' ');
                previousIsWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousIsWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    private static string FitLineWithEllipsis(string line, Font font, int maxWidth)
    {
        var workingLine = string.IsNullOrWhiteSpace(line)
            ? string.Empty
            : line.TrimEnd();

        const string ellipsis = "...";
        if (workingLine.Length == 0)
        {
            return ellipsis;
        }

        while (workingLine.Length > 0 && MeasureSingleLineWidth(workingLine + ellipsis, font) > maxWidth)
        {
            workingLine = workingLine[..^1].TrimEnd();
        }

        return workingLine.Length == 0 ? ellipsis : workingLine + ellipsis;
    }

    private static int MeasureSingleLineWidth(string text, Font font)
    {
        var measured = TextRenderer.MeasureText(
            text,
            font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

        return measured.Width;
    }
}
