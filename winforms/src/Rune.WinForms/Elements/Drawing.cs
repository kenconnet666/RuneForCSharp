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

    /// <summary>
    /// 圆角矩形的投影画进浮层的位图（预乘 ARGB，<see cref="LayeredWindow.Canvas"/>）：每层画出扩展、偏移后的圆角矩形遮罩，
    /// 三遍盒式模糊近似高斯模糊（CSS 的模糊长度是 2σ），按这一层的颜色叠上去。上面一圈圈的近似在浮层的大模糊上会出条纹。
    /// </summary>
    /// <param name="target">位图（要先于面板画）。</param>
    /// <param name="bounds">投影对象的范围（设备像素）。</param>
    /// <param name="radius">圆角（设备像素）。</param>
    /// <param name="layers">阴影的各层（长度是设备无关像素）。</param>
    /// <param name="scale">设备无关像素 → 设备像素的系数。</param>
    public static void Shadow(Bitmap target, RectangleF bounds, float radius, IReadOnlyList<ShadowLayer> layers, float scale)
    {
        var (width, height) = (target.Width, target.Height);
        foreach (var layer in layers.Where(layer => !layer.Inset))
        {
            var spread = layer.Spread * scale;
            var shape = RectangleF.Inflate(bounds, spread, spread);
            shape.Offset(layer.X * scale, layer.Y * scale);
            if (shape.Width <= 0 || shape.Height <= 0) continue;
            var mask = Mask(width, height, shape, Math.Max(0, radius + spread));
            foreach (var box in GaussianBoxes(layer.Blur * scale / 2))
            {
                BoxBlur(mask, width, height, box, horizontal: true);
                BoxBlur(mask, width, height, box, horizontal: false);
            }
            Composite(target, mask, layer.Color);
        }
    }

    /// <summary>圆角矩形的覆盖率（0–1，抗锯齿），按行存。</summary>
    static float[] Mask(int width, int height, RectangleF shape, float radius)
    {
        using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRectangle(shape, radius);
            graphics.FillPath(Brushes.Black, path);
        }
        var pixels = Pixels(bitmap, System.Drawing.Imaging.ImageLockMode.ReadOnly, null);
        var mask = new float[width * height];
        for (var i = 0; i < mask.Length; i++) mask[i] = pixels[4 * i + 3] / 255f;
        return mask;
    }

    /// <summary>三遍盒式模糊近似标准差 <paramref name="sigma"/> 的高斯模糊时，每遍的半径（Kovesi 的取法）。</summary>
    static IEnumerable<int> GaussianBoxes(float sigma)
    {
        if (sigma < 0.5f) yield break;
        const int Passes = 3;
        var ideal = Math.Sqrt(12 * sigma * sigma / Passes + 1);
        var lower = (int)Math.Floor(ideal);
        if (lower % 2 == 0) lower--;
        var upper = lower + 2;
        var lowerCount = Math.Round((12 * sigma * sigma - Passes * lower * lower - 4 * Passes * lower - 3 * Passes) / (-4.0 * lower - 4));
        for (var i = 0; i < Passes; i++) yield return ((i < lowerCount ? lower : upper) - 1) / 2;
    }

    /// <summary>一遍盒式模糊（滑动窗口求和，窗口外按 0 算）。</summary>
    static void BoxBlur(float[] values, int width, int height, int radius, bool horizontal)
    {
        if (radius <= 0) return;
        var (lines, length) = horizontal ? (height, width) : (width, height);
        var (step, stride) = horizontal ? (1, width) : (width, 1);
        var line = new float[length];
        var scale = 1f / (2 * radius + 1);
        for (var l = 0; l < lines; l++)
        {
            var start = l * stride;
            for (var i = 0; i < length; i++) line[i] = values[start + i * step];
            var sum = 0f;
            for (var i = 0; i < Math.Min(radius, length); i++) sum += line[i];
            for (var i = 0; i < length; i++)
            {
                if (i + radius < length) sum += line[i + radius];
                if (i - radius - 1 >= 0) sum -= line[i - radius - 1];
                values[start + i * step] = sum * scale;
            }
        }
    }

    /// <summary>按覆盖率把颜色叠到预乘 ARGB 的位图上（source-over）。</summary>
    static void Composite(Bitmap target, float[] mask, Color color)
    {
        Pixels(target, System.Drawing.Imaging.ImageLockMode.ReadWrite, pixels =>
        {
            for (var i = 0; i < mask.Length; i++)
            {
                var alpha = mask[i] * color.A / 255f;
                if (alpha <= 0) continue;
                var keep = 1 - alpha;
                var p = 4 * i;
                pixels[p] = (byte)Math.Round(color.B * alpha + pixels[p] * keep);
                pixels[p + 1] = (byte)Math.Round(color.G * alpha + pixels[p + 1] * keep);
                pixels[p + 2] = (byte)Math.Round(color.R * alpha + pixels[p + 2] * keep);
                pixels[p + 3] = (byte)Math.Round(255 * alpha + pixels[p + 3] * keep);
            }
        });
    }

    /// <summary>读出位图的像素（BGRA，按行紧排）；给了 <paramref name="edit"/> 时改完写回。</summary>
    static byte[] Pixels(Bitmap bitmap, System.Drawing.Imaging.ImageLockMode mode, Action<byte[]>? edit)
    {
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), mode, bitmap.PixelFormat);
        try
        {
            var pixels = new byte[bitmap.Width * bitmap.Height * 4];
            for (var y = 0; y < bitmap.Height; y++)
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4);
            if (edit is null) return pixels;
            edit(pixels);
            for (var y = 0; y < bitmap.Height; y++)
                System.Runtime.InteropServices.Marshal.Copy(pixels, y * bitmap.Width * 4, data.Scan0 + y * data.Stride, bitmap.Width * 4);
            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
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
