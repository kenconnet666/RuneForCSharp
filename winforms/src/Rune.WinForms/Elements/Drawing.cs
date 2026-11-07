using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>自绘元素共用的图形。</summary>
static class Drawing
{
    /// <summary>两个颜色按比例混合（逐通道，含不透明度）：<paramref name="amount"/> 为 0 是 <paramref name="from"/>，1 是 <paramref name="to"/>。</summary>
    public static Color Mix(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        int Channel(int a, int b) => (int)Math.Round(a + (b - a) * amount);
        return Color.FromArgb(Channel(from.A, to.A), Channel(from.R, to.R), Channel(from.G, to.G), Channel(from.B, to.B));
    }

    /// <summary>
    /// 圆角矩形的投影（主题的阴影 token，由下往上逐层画）：模糊用几圈由外向内收缩的半透明填充近似：
    /// 每圈只有这一层不透明度的几分之一，里面叠得多、边缘叠得少，因此是渐变的。
    /// </summary>
    /// <param name="graphics">画布（要开抗锯齿）。</param>
    /// <param name="bounds">投影对象的范围（设备像素）。</param>
    /// <param name="radius">圆角（设备像素）。</param>
    /// <param name="layers">阴影的各层（长度是设备无关像素）。</param>
    /// <param name="scale">设备无关像素 → 设备像素的系数。</param>
    public static void Shadow(Graphics graphics, RectangleF bounds, float radius, IReadOnlyList<ShadowLayer> layers, float scale)
    {
        const int Steps = 6;
        foreach (var layer in layers.Where(layer => !layer.Inset))
        {
            var spread = layer.Spread * scale;
            var reach = layer.Blur * scale / 2;
            for (var i = 0; i < Steps; i++)
            {
                // 第 i 圈：从最外（扩展 + 模糊半径）收到最里（扩展 - 模糊半径）
                var inflate = spread + reach - 2 * reach * i / (Steps - 1);
                var ring = RectangleF.Inflate(bounds, inflate, inflate);
                ring.Offset(layer.X * scale, layer.Y * scale);
                if (ring.Width <= 0 || ring.Height <= 0) continue;
                using var path = RoundedRectangle(ring, Math.Max(0, radius + inflate));
                using var brush = new SolidBrush(Color.FromArgb(Math.Max(1, layer.Color.A / Steps), layer.Color));
                graphics.FillPath(brush, path);
            }
        }
    }

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
