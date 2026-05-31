using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GameUpdater.WinForms.Controls;

public class ModernCheckBox : CheckBox
{
    private bool _isHovered = false;

    public ModernCheckBox()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 5, 0, 5);
        UseVisualStyleBackColor = false;
        ForeColor = Color.FromArgb(15, 23, 42); // default to slate-900
        BackColor = Color.White; // default to white card background
    }

    private string _customText = string.Empty;
    public override string Text
    {
        get => _customText;
        set
        {
            _customText = value ?? string.Empty;
            Invalidate();
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = base.GetPreferredSize(proposedSize);
        if (!string.IsNullOrEmpty(_customText))
        {
            var textSize = TextRenderer.MeasureText(_customText, Font);
            size.Width = Padding.Left + 18 + 8 + textSize.Width;
            size.Height = Math.Max(18, textSize.Height) + Padding.Top + Padding.Bottom;
        }
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
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Clear background with solid color to prevent double-buffer overlap
        Color bg = BackColor == Color.Transparent ? Color.White : BackColor;
        using (var bgBrush = new SolidBrush(bg))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        // Draw checkbox box
        int boxSize = 18;
        int boxX = Padding.Left;
        int boxY = (Height - boxSize) / 2;

        var boxRect = new Rectangle(boxX, boxY, boxSize, boxSize);

        Color boxBgColor;
        Color borderColor;

        if (Checked)
        {
            boxBgColor = Color.FromArgb(99, 102, 241); // Indigo-500
            borderColor = Color.FromArgb(99, 102, 241);
        }
        else
        {
            boxBgColor = Color.White;
            borderColor = _isHovered ? Color.FromArgb(99, 102, 241) : Color.FromArgb(203, 213, 225); // Slate-300
        }

        // Fill background
        using (var brush = new SolidBrush(boxBgColor))
        using (var path = GetRoundedRectPath(boxRect, 4))
        {
            g.FillPath(brush, path);
        }

        // Draw border
        using (var pen = new Pen(borderColor, 1.5f))
        using (var path = GetRoundedRectPath(boxRect, 4))
        {
            g.DrawPath(pen, path);
        }

        // Draw checkmark
        if (Checked)
        {
            using (var checkPen = new Pen(Color.White, 2.2f))
            {
                checkPen.StartCap = LineCap.Round;
                checkPen.EndCap = LineCap.Round;
                checkPen.LineJoin = LineJoin.Round;

                // Draw checkmark lines
                g.DrawLine(checkPen, boxX + 4.5f, boxY + 9f, boxX + 8f, boxY + 12.5f);
                g.DrawLine(checkPen, boxX + 8f, boxY + 12.5f, boxX + 13.5f, boxY + 5.5f);
            }
        }

        // Draw Text
        if (!string.IsNullOrEmpty(_customText))
        {
            var textX = boxX + boxSize + 8;
            var textWidth = Width - textX;
            var textRect = new Rectangle(textX, 0, textWidth, Height);

            TextRenderer.DrawText(
                g,
                _customText,
                Font,
                textRect,
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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
