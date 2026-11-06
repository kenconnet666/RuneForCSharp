using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>自绘元素共用的图形。</summary>
static class Drawing
{
    /// <summary>圆角矩形的路径；圆角不超过短边的一半（同 CSS 把过大的 border-radius 缩回来），0 时是直角矩形。</summary>
    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }
        var diameter = 2 * radius;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
