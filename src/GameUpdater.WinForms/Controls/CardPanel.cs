using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GameUpdater.WinForms.Controls;

public class CardPanel : Panel
{
    public int CornerRadius { get; set; } = 12;
    public Color CardBackColor { get; set; } = Color.White;
    public Color BorderColor { get; set; } = Color.FromArgb(226, 232, 240); // slate-200
    public float BorderWidth { get; set; } = 1.0f;

    public CardPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Clear background with parent's backcolor if available, otherwise slate-50
        var parentBg = Parent?.BackColor ?? Color.FromArgb(248, 250, 252);
        using (var bgBrush = new SolidBrush(parentBg))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        // Draw card background
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (rect.Width > 0 && rect.Height > 0)
        {
            using (var path = GetRoundedRectPath(rect, CornerRadius))
            {
                using (var fillBrush = new SolidBrush(CardBackColor))
                {
                    g.FillPath(fillBrush, path);
                }

                if (BorderWidth > 0)
                {
                    using (var borderPen = new Pen(BorderColor, BorderWidth))
                    {
                        g.DrawPath(borderPen, path);
                    }
                }
            }
        }
    }

    public static GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
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
