using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GameUpdater.WinForms.Controls;

public enum ButtonColorType
{
    Secondary,
    PrimaryBlue,
    Purple,
    Red,
    Green,
    Orange
}

public enum ButtonIconType
{
    None,
    Add,
    Edit,
    Delete,
    Export,
    Refresh,
    Folder,
    Save,
    Cancel
}

public class ModernButton : Control
{
    private ButtonColorType _colorType = ButtonColorType.Secondary;
    public ButtonColorType ColorType
    {
        get => _colorType;
        set
        {
            if (_colorType != value)
            {
                _colorType = value;
                Invalidate();
            }
        }
    }

    public bool IsPrimary
    {
        get => ColorType == ButtonColorType.PrimaryBlue;
        set
        {
            if (value)
            {
                ColorType = ButtonColorType.PrimaryBlue;
            }
            else if (ColorType == ButtonColorType.PrimaryBlue)
            {
                ColorType = ButtonColorType.Secondary;
            }
        }
    }

    private ButtonIconType _iconType = ButtonIconType.None;
    public ButtonIconType IconType
    {
        get => _iconType;
        set
        {
            if (_iconType != value)
            {
                _iconType = value;
                Invalidate();
            }
        }
    }

    private int _cornerRadius = 8;
    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (_cornerRadius != value)
            {
                _cornerRadius = value;
                Invalidate();
            }
        }
    }

    // Modern color themes based on Tailwind palettes
    private static readonly Color SecondaryColor = Color.FromArgb(241, 245, 249); // slate-100
    private static readonly Color SecondaryHoverColor = Color.FromArgb(226, 232, 240); // slate-200
    private static readonly Color SecondaryPressedColor = Color.FromArgb(203, 213, 225); // slate-300
    private static readonly Color SecondaryDisabledColor = Color.FromArgb(248, 250, 252); // slate-50
    private static readonly Color SecondaryTextColor = Color.FromArgb(15, 23, 42); // slate-900
    private static readonly Color SecondaryBorderColor = Color.FromArgb(203, 213, 225); // slate-300

    private static readonly Color BlueColor = Color.FromArgb(37, 99, 235); // blue-600
    private static readonly Color BlueHoverColor = Color.FromArgb(29, 78, 216); // blue-700
    private static readonly Color BluePressedColor = Color.FromArgb(30, 64, 175); // blue-800
    private static readonly Color BlueDisabledColor = Color.FromArgb(191, 219, 254); // blue-200

    private static readonly Color PurpleColor = Color.FromArgb(99, 102, 241); // indigo-500
    private static readonly Color PurpleHoverColor = Color.FromArgb(79, 70, 229); // indigo-600
    private static readonly Color PurplePressedColor = Color.FromArgb(67, 56, 202); // indigo-700
    private static readonly Color PurpleDisabledColor = Color.FromArgb(199, 210, 254); // indigo-200

    private static readonly Color RedColor = Color.FromArgb(239, 68, 68); // red-500
    private static readonly Color RedHoverColor = Color.FromArgb(220, 38, 38); // red-600
    private static readonly Color RedPressedColor = Color.FromArgb(185, 28, 28); // red-700
    private static readonly Color RedDisabledColor = Color.FromArgb(252, 165, 165); // red-300

    private static readonly Color GreenColor = Color.FromArgb(16, 185, 129); // emerald-500
    private static readonly Color GreenHoverColor = Color.FromArgb(5, 150, 105); // emerald-600
    private static readonly Color GreenPressedColor = Color.FromArgb(4, 120, 87); // emerald-700
    private static readonly Color GreenDisabledColor = Color.FromArgb(110, 231, 183); // emerald-300

    private static readonly Color OrangeColor = Color.FromArgb(245, 158, 11); // amber-500
    private static readonly Color OrangeHoverColor = Color.FromArgb(217, 119, 6); // amber-600
    private static readonly Color OrangePressedColor = Color.FromArgb(180, 83, 9); // amber-700
    private static readonly Color OrangeDisabledColor = Color.FromArgb(252, 211, 77); // amber-300

    private static readonly Color DisabledTextColor = Color.FromArgb(148, 163, 184); // slate-400
    private static readonly Color ActiveTextColor = Color.White;

    private bool _isHovered;
    private bool _isPressed;

    public ModernButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        Cursor = Cursors.Hand;
        TabStop = false;
        Margin = new Padding(5, 2, 5, 2);
        Padding = new Padding(14, 8, 14, 8); // Slightly taller modern padding
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = base.GetPreferredSize(proposedSize);

        int iconSize = 16;
        int spacing = 6;
        
        var measuredText = TextRenderer.MeasureText(
            Text ?? string.Empty,
            Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        int contentWidth = (IconType != ButtonIconType.None ? iconSize : 0) +
                           (IconType != ButtonIconType.None && !string.IsNullOrEmpty(Text) ? spacing : 0) +
                           measuredText.Width;

        size.Width = contentWidth + Padding.Left + Padding.Right;
        size.Height = Math.Max(iconSize, measuredText.Height) + Padding.Top + Padding.Bottom;

        return size;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        if (mevent.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _isPressed = false;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
        {
            _isPressed = true;
            Invalidate();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (_isPressed && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
        {
            _isPressed = false;
            Invalidate();
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyUp(e);
    }

    private Color GetOpaqueParentBackColor()
    {
        Control? parent = Parent;
        while (parent != null)
        {
            if (parent.BackColor != Color.Transparent && parent.BackColor != Color.Empty)
            {
                return parent.BackColor;
            }
            parent = parent.Parent;
        }
        return Color.FromArgb(248, 250, 252);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Draw opaque parent background first to completely eliminate black artifacts outside the rounded corners
        Color parentBgColor = GetOpaqueParentBackColor();
        using (var parentBrush = new SolidBrush(parentBgColor))
        {
            g.FillRectangle(parentBrush, ClientRectangle);
        }

        // Determine color theme variables based on states
        Color backColor;
        Color textColor;
        Color currBorderColor = Color.Transparent;

        if (!Enabled)
        {
            textColor = DisabledTextColor;
            backColor = ColorType switch
            {
                ButtonColorType.PrimaryBlue => BlueDisabledColor,
                ButtonColorType.Purple => PurpleDisabledColor,
                ButtonColorType.Red => RedDisabledColor,
                ButtonColorType.Green => GreenDisabledColor,
                ButtonColorType.Orange => OrangeDisabledColor,
                _ => SecondaryDisabledColor
            };
            if (ColorType == ButtonColorType.Secondary)
            {
                currBorderColor = Color.FromArgb(226, 232, 240);
            }
        }
        else
        {
            textColor = ColorType == ButtonColorType.Secondary ? SecondaryTextColor : ActiveTextColor;
            if (ColorType == ButtonColorType.Secondary)
            {
                currBorderColor = SecondaryBorderColor;
                if (_isPressed)
                    backColor = SecondaryPressedColor;
                else if (_isHovered)
                    backColor = SecondaryHoverColor;
                else
                    backColor = SecondaryColor;
            }
            else
            {
                var (normal, hover, pressed) = ColorType switch
                {
                    ButtonColorType.Purple => (PurpleColor, PurpleHoverColor, PurplePressedColor),
                    ButtonColorType.Red => (RedColor, RedHoverColor, RedPressedColor),
                    ButtonColorType.Green => (GreenColor, GreenHoverColor, GreenPressedColor),
                    ButtonColorType.Orange => (OrangeColor, OrangeHoverColor, OrangePressedColor),
                    _ => (BlueColor, BlueHoverColor, BluePressedColor)
                };

                if (_isPressed)
                    backColor = pressed;
                else if (_isHovered)
                    backColor = hover;
                else
                    backColor = normal;
            }
        }

        var rect = new Rectangle(1, 1, ClientRectangle.Width - 2, ClientRectangle.Height - 2);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using (var path = GetRoundedRectPath(rect, CornerRadius))
        {
            // Draw background path
            using (var brush = new SolidBrush(backColor))
            {
                g.FillPath(brush, path);
            }

            // Draw border if Secondary
            if (ColorType == ButtonColorType.Secondary && currBorderColor != Color.Transparent)
            {
                using (var pen = new Pen(currBorderColor, 1.5f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Draw focus outline inside
            if (Focused && Enabled)
            {
                using (var focusPen = new Pen(Color.FromArgb(96, 165, 250), 1.5f))
                {
                    focusPen.DashStyle = DashStyle.Dot;
                    var focusRect = rect;
                    focusRect.Inflate(-3, -3);
                    using (var focusPath = GetRoundedRectPath(focusRect, Math.Max(2, CornerRadius - 2)))
                    {
                        g.DrawPath(focusPen, focusPath);
                    }
                }
            }
        }

        // Layout Icon and Text
        int iconSize = 16;
        int spacing = 6;

        var measuredText = TextRenderer.MeasureText(
            Text,
            Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        int totalWidth = (IconType != ButtonIconType.None ? iconSize : 0) +
                         (IconType != ButtonIconType.None && !string.IsNullOrEmpty(Text) ? spacing : 0) +
                         measuredText.Width;

        int startX = (Width - totalWidth) / 2;

        if (IconType != ButtonIconType.None)
        {
            int iconX = startX;
            int iconY = (Height - iconSize) / 2;
            DrawVectorIcon(g, IconType, iconX, iconY, iconSize, textColor);
            startX += iconSize + spacing;
        }

        int textY = (Height - measuredText.Height) / 2;
        var textRect = new Rectangle(startX, textY, measuredText.Width + 4, measuredText.Height);

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            textRect,
            textColor,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private static void DrawVectorIcon(Graphics g, ButtonIconType type, int x, int y, int size, Color color)
    {
        using var pen = new Pen(color, 2f);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;
        pen.LineJoin = LineJoin.Round;

        g.SmoothingMode = SmoothingMode.AntiAlias;

        switch (type)
        {
            case ButtonIconType.Add:
                // Draw Plus Symbol
                g.DrawLine(pen, x + 3, y + 8, x + 13, y + 8);
                g.DrawLine(pen, x + 8, y + 3, x + 8, y + 13);
                break;

            case ButtonIconType.Edit:
                // Draw Pencil Icon
                pen.Width = 1.5f;
                // Pencil body outline
                g.DrawLine(pen, x + 11, y + 3, x + 13, y + 5);
                g.DrawLine(pen, x + 13, y + 5, x + 6, y + 12);
                g.DrawLine(pen, x + 6, y + 12, x + 4, y + 12);
                g.DrawLine(pen, x + 4, y + 12, x + 4, y + 10);
                g.DrawLine(pen, x + 4, y + 10, x + 11, y + 3);
                // Tip detail
                g.DrawLine(pen, x + 5, y + 11, x + 6, y + 10);
                break;

            case ButtonIconType.Delete:
                // Draw Trash Icon
                pen.Width = 1.5f;
                // Lid
                g.DrawLine(pen, x + 2, y + 4, x + 14, y + 4);
                // Lid handle
                g.DrawLine(pen, x + 6, y + 2, x + 10, y + 2);
                // Basket
                g.DrawLine(pen, x + 4, y + 4, x + 4, y + 13);
                g.DrawLine(pen, x + 4, y + 13, x + 12, y + 13);
                g.DrawLine(pen, x + 12, y + 13, x + 12, y + 4);
                // Vertical lines inside basket
                g.DrawLine(pen, x + 7, y + 7, x + 7, y + 10);
                g.DrawLine(pen, x + 9, y + 7, x + 9, y + 10);
                break;

            case ButtonIconType.Export:
                // Draw Export tray + arrow up
                pen.Width = 1.5f;
                // Tray
                g.DrawLine(pen, x + 3, y + 10, x + 3, y + 13);
                g.DrawLine(pen, x + 3, y + 13, x + 13, y + 13);
                g.DrawLine(pen, x + 13, y + 13, x + 13, y + 10);
                // Arrow stem
                g.DrawLine(pen, x + 8, y + 11, x + 8, y + 3);
                // Arrow head
                g.DrawLine(pen, x + 5, y + 6, x + 8, y + 3);
                g.DrawLine(pen, x + 8, y + 3, x + 11, y + 6);
                break;

            case ButtonIconType.Refresh:
                // Draw Circular Refresh Arrow
                pen.Width = 1.5f;
                // Open circular arc
                g.DrawArc(pen, x + 2, y + 2, 12, 12, 45, 270);
                // Arrow head
                g.DrawLine(pen, x + 11, y + 5, x + 11, y + 2);
                g.DrawLine(pen, x + 11, y + 5, x + 8, y + 5);
                break;

            case ButtonIconType.Folder:
                // Draw Folder
                pen.Width = 1.5f;
                var folderPath = new GraphicsPath();
                folderPath.AddLine(x + 2, y + 3, x + 6, y + 3);
                folderPath.AddLine(x + 6, y + 3, x + 8, y + 5);
                folderPath.AddLine(x + 8, y + 5, x + 14, y + 5);
                folderPath.AddLine(x + 14, y + 5, x + 14, y + 13);
                folderPath.AddLine(x + 14, y + 13, x + 2, y + 13);
                folderPath.CloseFigure();
                g.DrawPath(pen, folderPath);
                break;

            case ButtonIconType.Save:
                // Draw Floppy Disk
                pen.Width = 1.5f;
                g.DrawLine(pen, x + 2, y + 2, x + 11, y + 2);
                g.DrawLine(pen, x + 11, y + 2, x + 14, y + 5);
                g.DrawLine(pen, x + 14, y + 5, x + 14, y + 14);
                g.DrawLine(pen, x + 14, y + 14, x + 2, y + 14);
                g.DrawLine(pen, x + 2, y + 14, x + 2, y + 2);
                // Slider metal bit
                g.DrawRectangle(pen, x + 5, y + 9, 6, 5);
                break;

            case ButtonIconType.Cancel:
                // Draw "X"
                g.DrawLine(pen, x + 4, y + 4, x + 12, y + 12);
                g.DrawLine(pen, x + 12, y + 4, x + 4, y + 12);
                break;
        }
    }

    private static GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }
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
