using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace GameUpdater.WinForms.Forms;

public partial class MainForm
{
    private class IconButton : Panel
    {
        public Action<Graphics, Rectangle, Color> DrawIcon { get; set; } = null!;
        public Color NormalColor { get; set; } = Color.White;
        public Color HoverColor { get; set; } = Color.FromArgb(248, 250, 252);
        public Color PressedColor { get; set; } = Color.FromArgb(241, 245, 249);
        public Color IconNormalColor { get; set; } = Color.FromArgb(100, 116, 139);
        public Color IconHoverColor { get; set; } = Color.FromArgb(71, 85, 105);
        public Color BorderColor { get; set; } = Color.FromArgb(226, 232, 240);

        private bool _isHovered;
        private bool _isPressed;

        public IconButton()
        {
            DoubleBuffered = true;
            Size = new Size(36, 36);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; _isPressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _isPressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _isPressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color bg = _isPressed ? PressedColor : (_isHovered ? HoverColor : NormalColor);
            using var brush = new SolidBrush(bg);
            using var path = GameEditorForm.GetRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 6);
            e.Graphics.FillPath(brush, path);
            
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawPath(pen, path);

            if (DrawIcon != null)
            {
                Color iconColor = _isHovered ? IconHoverColor : IconNormalColor;
                DrawIcon(e.Graphics, new Rectangle(10, 10, Width - 20, Height - 20), iconColor);
            }
        }
    }

    private class IconTextBox : Panel
    {
        private TextBox _textBox;
        private Action<Graphics, Rectangle, Color> _drawIcon;

        public IconTextBox(Action<Graphics, Rectangle, Color> drawIcon)
        {
            _drawIcon = drawIcon;
            BackColor = Color.White;
            Padding = new Padding(36, 8, 10, 8);
            Height = 38;
            
            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5f),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30,30,40)
            };
            Controls.Add(_textBox);
        }
        
        public TextBox Input => _textBox;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            using var path = GameEditorForm.GetRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 6);
            e.Graphics.DrawPath(pen, path);
            
            if (_drawIcon != null)
            {
                _drawIcon(e.Graphics, new Rectangle(12, (Height - 16) / 2, 16, 16), Color.FromArgb(148, 163, 184));
            }
        }
    }

    private static void DrawDotsIcon(Graphics g, Rectangle rect, Color color)
    {
        using var brush = new SolidBrush(color);
        int d = 3;
        int y = rect.Y + (rect.Height - d) / 2;
        g.FillEllipse(brush, rect.X + rect.Width / 2 - 6, y, d, d);
        g.FillEllipse(brush, rect.X + rect.Width / 2 - d/2, y, d, d);
        g.FillEllipse(brush, rect.X + rect.Width / 2 + 6 - d, y, d, d);
    }

    private static void DrawTrashIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        g.DrawRectangle(pen, rect.X + 2, rect.Y + 4, rect.Width - 4, rect.Height - 4);
        g.DrawLine(pen, rect.X, rect.Y + 4, rect.Right, rect.Y + 4);
        g.DrawLine(pen, rect.X + rect.Width / 3, rect.Y + 4, rect.X + rect.Width / 3, rect.Y + 1);
        g.DrawLine(pen, rect.Right - rect.Width / 3, rect.Y + 4, rect.Right - rect.Width / 3, rect.Y + 1);
        g.DrawLine(pen, rect.X + rect.Width / 3, rect.Y + 1, rect.Right - rect.Width / 3, rect.Y + 1);
    }

    private static void DrawGlobeIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        g.DrawEllipse(pen, rect);
        g.DrawEllipse(pen, rect.X + rect.Width / 4, rect.Y, rect.Width / 2, rect.Height);
        g.DrawLine(pen, rect.X, rect.Y + rect.Height / 2, rect.Right, rect.Y + rect.Height / 2);
    }

    private static void DrawMonitorIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height - 4);
        g.DrawLine(pen, rect.X + rect.Width / 3, rect.Bottom, rect.Right - rect.Width / 3, rect.Bottom);
        g.DrawLine(pen, rect.X + rect.Width / 2, rect.Bottom - 4, rect.X + rect.Width / 2, rect.Bottom);
    }

    private static void DrawSpeedIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        g.DrawArc(pen, rect.X, rect.Y, rect.Width, rect.Height, 180, 180);
        g.DrawLine(pen, rect.X + rect.Width / 2, rect.Bottom - rect.Height / 2, rect.Right - 2, rect.Y + 2);
        g.FillEllipse(new SolidBrush(color), rect.X + rect.Width / 2 - 2, rect.Bottom - rect.Height / 2 - 2, 4, 4);
    }

    private static void DrawFolderIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        using var path = GameEditorForm.GetRoundedRectPath(new Rectangle(rect.X, rect.Y + 4, rect.Width, rect.Height - 4), 2);
        g.DrawPath(pen, path);
        g.DrawLine(pen, rect.X, rect.Y + 8, rect.Right, rect.Y + 8);
        g.DrawLine(pen, rect.X, rect.Y + 4, rect.X + 4, rect.Y);
        g.DrawLine(pen, rect.X + 4, rect.Y, rect.X + rect.Width/2 - 2, rect.Y);
        g.DrawLine(pen, rect.X + rect.Width/2 - 2, rect.Y, rect.X + rect.Width/2 + 2, rect.Y + 4);
    }
}
