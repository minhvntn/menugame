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

    private Panel? _cardShell;
    private float _hoverProgress;
    private float _breathAngle;
    private readonly System.Windows.Forms.Timer _hoverTimer;

    private readonly Color _startNormal;
    private readonly Color _endNormal;
    private readonly Color _startHover;
    private readonly Color _endHover;
    private readonly Color _borderNormal;
    private readonly Color _borderHover;

    public LauncherGameRow Row => _row;

    public GameCardControl(LauncherGameRow row, Action<LauncherGameRow> playAction, bool isHotRow, string fontFamily)
    {
        _row = row;
        _playAction = playAction;
        _isHotRow = isHotRow;

        _iconSize = _isHotRow ? 64 : 48;
        _tileSize = _isHotRow ? 78 : 60;
        _cardWidth = _isHotRow ? 144 : 110;
        _cardHeight = _isHotRow ? 176 : 140;
        _nameFont = new Font(string.IsNullOrWhiteSpace(fontFamily) ? "Segoe UI" : fontFamily, _isHotRow ? 9f : 8.5f, FontStyle.Bold);

        _resolvedExecutablePath = NormalizeExecutablePath(_row.ResolvedExecutablePath);
        _iconCacheKey = BuildIconCacheKey(_resolvedExecutablePath, _iconSize);

        _hoverTimer = new System.Windows.Forms.Timer
        {
            Interval = 15
        };
        _hoverTimer.Tick += HoverTimer_Tick;

        Width = _cardWidth;
        Height = _cardHeight;
        Margin = _isHotRow ? new Padding(10, 2, 10, 4) : new Padding(8, 4, 8, 6);
        Padding = new Padding(0);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        EnableDoubleBuffer(this);

        // Generate card-specific colors using a stable hash of the game's name (only for hot/featured games)
        if (_isHotRow)
        {
            int hash = GetStableHash(_row.Name);
            double h1 = (Math.Abs(hash) % 360) / 360.0;
            double h2 = ((Math.Abs(hash) % 360 + 25) % 360) / 360.0;

            _startNormal = ColorFromHsl(h1, 0.28, 0.16);
            _endNormal = ColorFromHsl(h2, 0.20, 0.08);
            _startHover = ColorFromHsl(h1, 0.38, 0.22);
            _endHover = ColorFromHsl(h2, 0.25, 0.11);
            _borderNormal = ColorFromHsl(h1, 0.18, 0.20);
            _borderHover = ColorFromHsl(h1, 0.95, 0.65);
        }
        else
        {
            _startNormal = Color.FromArgb(36, 40, 56);
            _endNormal = Color.FromArgb(20, 22, 31);
            _startHover = Color.FromArgb(52, 58, 82);
            _endHover = Color.FromArgb(28, 31, 44);
            _borderNormal = Color.FromArgb(42, 47, 61);
            _borderHover = Color.FromArgb(139, 92, 246);
        }

        BuildLayout();
        QueueIconLoadIfNeeded();
        WireCardClick(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _nameFont.Dispose();
            _hoverTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        if (_cardShell == null || _cardShell.IsDisposed)
        {
            _hoverTimer.Stop();
            return;
        }

        var clientPos = _cardShell.PointToClient(Cursor.Position);
        bool targetHover = _cardShell.ClientRectangle.Contains(clientPos);

        bool changed = false;
        if (targetHover)
        {
            if (_hoverProgress < 1f)
            {
                _hoverProgress = Math.Min(1f, _hoverProgress + 0.08f);
            }
            
            _breathAngle += 0.05f;
            if (_breathAngle > (float)Math.PI * 2)
            {
                _breathAngle -= (float)Math.PI * 2;
            }
            changed = true; // Always repaint to support continuous breathing when hovered
        }
        else
        {
            if (_hoverProgress > 0f)
            {
                _hoverProgress = Math.Max(0f, _hoverProgress - 0.08f);
                _breathAngle += 0.05f;
                changed = true;
            }
        }

        if (changed)
        {
            _cardShell.Invalidate(true);
        }
        else
        {
            _hoverTimer.Stop();
            _breathAngle = 0f;
        }
    }

    private static float EaseInOut(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2f) / 2f;
    }

    private static Color InterpolateColor(Color c1, Color c2, float t)
    {
        int r = (int)(c1.R + (c2.R - c1.R) * t);
        int g = (int)(c1.G + (c2.G - c1.G) * t);
        int b = (int)(c1.B + (c2.B - c1.B) * t);
        return Color.FromArgb(r, g, b);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = _isHotRow ? 3 : 2,
            BackColor = Color.Transparent
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, _tileSize + (_isHotRow ? 16 : 12)));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        if (_isHotRow)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        }

        _cardShell = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = _isHotRow ? new Padding(8, 8, 8, 8) : new Padding(6, 6, 6, 5)
        };
        EnableDoubleBuffer(_cardShell);
        EnableDoubleBuffer(root);

        _cardShell.Resize += (_, _) => ApplyRoundedRegion(_cardShell, _isHotRow ? 12 : 10);
        _cardShell.Paint += (_, e) =>
        {
            if (_cardShell == null) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            // Use static inset to prevent thick border (up to 4px) from being clipped
            var bounds = new Rectangle(2, 2, _cardShell.Width - 4, _cardShell.Height - 4);
            using var path = CreateRoundRectPath(bounds, _isHotRow ? 12 : 10);

            float breath = 0.5f + 0.5f * (float)Math.Sin(_breathAngle);
            float glowFactor = 0.4f + 0.6f * breath; // breathes between 0.4 and 1.0

            float t = EaseInOut(_hoverProgress) * glowFactor;

            Color currentStart = InterpolateColor(_startNormal, _startHover, EaseInOut(_hoverProgress));
            Color currentEnd = InterpolateColor(_endNormal, _endHover, EaseInOut(_hoverProgress));

            using var fill = new System.Drawing.Drawing2D.LinearGradientBrush(
                bounds,
                currentStart,
                currentEnd,
                90f);
            e.Graphics.FillPath(fill, path);

            Color currentBorder = InterpolateColor(_borderNormal, _borderHover, t);

            float currentWidth = 2f + 2f * t; // normal: 2px, active: 4px

            using var pen = new Pen(currentBorder, currentWidth);
            e.Graphics.DrawPath(pen, path);
        };

        var hostPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        EnableDoubleBuffer(hostPanel);

        var iconTile = new Panel
        {
            Width = _tileSize,
            Height = _tileSize,
            BackColor = Color.Transparent
        };
        EnableDoubleBuffer(iconTile);

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
            Text = BuildTwoLineTextCached(_row.Name, _nameFont, _cardWidth - 12),
            Dock = DockStyle.Fill,
            Font = _nameFont,
            TextAlign = ContentAlignment.TopCenter,
            AutoEllipsis = false,
            ForeColor = Color.FromArgb(230, 232, 239),
            Padding = new Padding(1, 1, 1, 0)
        };

        root.Controls.Add(hostPanel, 0, 0);
        root.Controls.Add(nameLabel, 0, 1);

        if (_isHotRow)
        {
            var playButton = new Button
            {
                Text = "Ch\u01a1i ngay  \u2192",
                Dock = DockStyle.Fill,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(42, 47, 61),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 8, 8)
            };
            playButton.FlatAppearance.BorderSize = 0;
            playButton.Paint += (sender, e) =>
            {
                if (sender is not Button btn) return;
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                
                var rect = btn.ClientRectangle;
                
                var normalColor = Color.FromArgb(42, 47, 61);
                var backColor = InterpolateColor(normalColor, _borderHover, EaseInOut(_hoverProgress));
                
                using (var path = CreateRoundRectPath(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), 6))
                {
                    using (var fill = new SolidBrush(backColor))
                    {
                        g.FillPath(fill, path);
                    }
                }
                
                var textSize = g.MeasureString(btn.Text, btn.Font);
                g.DrawString(
                    btn.Text,
                    btn.Font,
                    Brushes.White,
                    (btn.Width - textSize.Width) / 2,
                    (btn.Height - textSize.Height) / 2);
            };
            playButton.MouseEnter += (s, e) => playButton.Invalidate();
            playButton.MouseLeave += (s, e) => playButton.Invalidate();
            playButton.Click += (_, _) => _playAction(_row);
            root.Controls.Add(playButton, 0, 2);
            WireCardHover(playButton, _cardShell);
        }

        _cardShell.Controls.Add(root);
        Controls.Add(_cardShell);

        WireCardClick(root);
        WireCardClick(_cardShell);
        WireCardClick(hostPanel);
        WireCardClick(iconTile);
        WireCardClick(_iconBox);
        WireCardClick(nameLabel);

        WireCardHover(root, _cardShell);
        WireCardHover(_cardShell, _cardShell);
        WireCardHover(hostPanel, _cardShell);
        WireCardHover(iconTile, _cardShell);
        WireCardHover(_iconBox, _cardShell);
        WireCardHover(nameLabel, _cardShell);
    }

    private void WireCardHover(Control control, Panel cardShell)
    {
        control.MouseEnter += (s, e) =>
        {
            if (_hoverTimer != null && !_hoverTimer.Enabled)
            {
                _hoverTimer.Start();
            }
            cardShell.Invalidate();
        };
        control.MouseLeave += (s, e) =>
        {
            if (_hoverTimer != null && !_hoverTimer.Enabled)
            {
                _hoverTimer.Start();
            }
            cardShell.Invalidate();
        };
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

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        using var path = CreateRoundRectPath(new Rectangle(0, 0, control.Width, control.Height), radius);
        control.Region?.Dispose();
        control.Region = new Region(path);
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

    private static void EnableDoubleBuffer(Control control)
    {
        try
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, control, new object[] { true });
        }
        catch
        {
            // Ignore if reflection fails
        }
    }

    private static int GetStableHash(string str)
    {
        int hash = 5381;
        foreach (char c in str)
        {
            hash = ((hash << 5) + hash) + c;
        }
        return hash;
    }

    private static Color ColorFromHsl(double h, double s, double l)
    {
        double r = 0, g = 0, b = 0;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            double p = 2.0 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }
        return Color.FromArgb(
            (int)Math.Clamp(r * 255, 0, 255),
            (int)Math.Clamp(g * 255, 0, 255),
            (int)Math.Clamp(b * 255, 0, 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1.0;
        if (t > 1) t -= 1.0;
        if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
        return p;
    }
}

