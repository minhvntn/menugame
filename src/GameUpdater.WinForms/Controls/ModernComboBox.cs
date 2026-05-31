using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GameUpdater.WinForms.Controls;

public class ModernComboBox : ComboBox
{
    private bool _isMouseOver = false;

    public ModernComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        ItemHeight = 28;
        BackColor = Color.White;
        ForeColor = Color.FromArgb(15, 23, 42); // slate-900
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isMouseOver = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isMouseOver = false;
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        
        var bgColor = isSelected ? Color.FromArgb(238, 242, 255) : Color.White; // Indigo-50 hover
        var textColor = isSelected ? Color.FromArgb(99, 102, 241) : Color.FromArgb(15, 23, 42); // Indigo-500 / Slate-900

        using (var brush = new SolidBrush(bgColor))
        {
            g.FillRectangle(brush, e.Bounds);
        }

        var text = GetItemText(Items[e.Index]);
        using (var font = new Font(Font.FontFamily, Font.Size, isSelected ? FontStyle.Bold : FontStyle.Regular))
        {
            var textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height);
            TextRenderer.DrawText(
                g,
                text,
                font,
                textRect,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private const int WM_PAINT = 0x0F;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WM_PAINT)
        {
            using (var g = CreateGraphics())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var borderActive = Focused || _isMouseOver;
                var borderColor = borderActive ? Color.FromArgb(99, 102, 241) : Color.FromArgb(203, 213, 225);

                // Draw custom flat border
                using (var pen = new Pen(borderColor, 1.5f))
                {
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }

                // Draw dropdown arrow area cover
                int buttonWidth = 20;
                var buttonRect = new Rectangle(Width - buttonWidth - 2, 2, buttonWidth, Height - 4);
                using (var brush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(brush, buttonRect);
                }

                // Draw modern down arrow chevron
                using (var arrowPen = new Pen(Color.FromArgb(100, 116, 139), 2f)) // slate-500
                {
                    arrowPen.StartCap = LineCap.Round;
                    arrowPen.EndCap = LineCap.Round;
                    arrowPen.LineJoin = LineJoin.Round;

                    int cx = buttonRect.X + buttonRect.Width / 2;
                    int cy = buttonRect.Y + buttonRect.Height / 2;

                    g.DrawLine(arrowPen, cx - 4, cy - 2, cx, cy + 2);
                    g.DrawLine(arrowPen, cx, cy + 2, cx + 4, cy - 2);
                }
            }
        }
    }
}
