using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameUpdater.WinForms.Extensions;

public static class DpiExtensions
{
    public static int ScaleDpi(this Control control, int value)
    {
        return (int)Math.Round(value * (control.DeviceDpi / 96f));
    }

    public static Size ScaleSize(this Control control, int width, int height)
    {
        return new Size(control.ScaleDpi(width), control.ScaleDpi(height));
    }

    public static Point ScalePoint(this Control control, int x, int y)
    {
        return new Point(control.ScaleDpi(x), control.ScaleDpi(y));
    }

    public static Padding ScalePadding(this Control control, int left, int top, int right, int bottom)
    {
        return new Padding(control.ScaleDpi(left), control.ScaleDpi(top), control.ScaleDpi(right), control.ScaleDpi(bottom));
    }

    public static Padding ScalePadding(this Control control, int all)
    {
        return new Padding(control.ScaleDpi(all));
    }
}
