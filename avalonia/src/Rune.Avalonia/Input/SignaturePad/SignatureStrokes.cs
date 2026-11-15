using System.Globalization;
using System.Text;

namespace Rune.Avalonia;

/// <summary>笔画上的一个点（相对画布左上角，设备无关像素）与它的笔宽。</summary>
/// <param name="X">横坐标。</param>
/// <param name="Y">纵坐标。</param>
/// <param name="Width">笔宽。</param>
public readonly record struct StrokePoint(double X, double Y, double Width);

/// <summary>画布上的一段：起点、控制点（二次贝塞尔）、终点与笔宽。</summary>
/// <param name="From">起点。</param>
/// <param name="Control">控制点。</param>
/// <param name="To">终点。</param>
/// <param name="Width">笔宽。</param>
public readonly record struct StrokeSegment((double X, double Y) From, (double X, double Y) Control, (double X, double Y) To, double Width);

/// <summary>
/// 手写签名的笔画（vue-tsx-pro 的 SignaturePad 里的计算，三个库共用一份）：按速度定笔宽（写得快变细，参考 signature_pad），
/// 一笔拆成穿过相邻两点中点的二次贝塞尔段，导出 SVG。平台控件只把指针换成 <see cref="Begin"/>、<see cref="Extend"/>、<see cref="End"/>，自己画段。
/// </summary>
/// <param name="minWidth">最细的笔宽，默认 0.75。</param>
/// <param name="maxWidth">最粗的笔宽，默认 2.75。</param>
public sealed class SignatureStrokes(double minWidth = 0.75, double maxWidth = 2.75)
{
    /// <summary>速度的平滑权重：越大越稳（signature_pad 的 velocityFilterWeight）。</summary>
    const double VelocityWeight = 0.7;

    /// <summary>比这更近的点不记：手抖、高频事件。</summary>
    const double MinDistance = 1.5;

    readonly List<StrokePoint[]> strokes = [];
    List<StrokePoint>? drawing;
    double velocity;
    double time;

    /// <summary>最细的笔宽。</summary>
    public double MinWidth { get; set; } = minWidth;

    /// <summary>最粗的笔宽。</summary>
    public double MaxWidth { get; set; } = maxWidth;

    /// <summary>写完的笔画（不含正在写的一笔）。</summary>
    public IReadOnlyList<IReadOnlyList<StrokePoint>> Strokes => strokes;

    /// <summary>正在写。</summary>
    public bool IsDrawing => drawing is not null;

    /// <summary>正在写的一笔的点（没在写时为空）。</summary>
    public IReadOnlyList<StrokePoint> CurrentPoints => drawing ?? (IReadOnlyList<StrokePoint>)[];

    /// <summary>落笔：返回第一个点（画成一个圆点）。</summary>
    /// <param name="x">横坐标。</param>
    /// <param name="y">纵坐标。</param>
    /// <param name="milliseconds">时刻（毫秒，单调递增即可）。</param>
    public StrokePoint Begin(double x, double y, double milliseconds)
    {
        velocity = 0;
        time = milliseconds;
        var point = new StrokePoint(x, y, (MinWidth + MaxWidth) / 2);
        drawing = [point];
        return point;
    }

    /// <summary>接上一个点：太近的不记（返回 null）；记下时返回只要新画的一段（从上上个点与上个点的中点，经过上个点，到上个点与这个点的中点），前两个点时没有段。</summary>
    public StrokeSegment? Extend(double x, double y, double milliseconds)
    {
        if (drawing is not { } points) return null;
        var previous = points[^1];
        if (Math.Sqrt((x - previous.X) * (x - previous.X) + (y - previous.Y) * (y - previous.Y)) < MinDistance) return null;
        var elapsed = Math.Max(milliseconds - time, 1);
        var speed = Math.Sqrt((x - previous.X) * (x - previous.X) + (y - previous.Y) * (y - previous.Y)) / elapsed;
        velocity = VelocityWeight * speed + (1 - VelocityWeight) * velocity;
        time = milliseconds;
        points.Add(new StrokePoint(x, y, Math.Max(MaxWidth / (velocity + 1), MinWidth)));
        if (points.Count < 3) return null;
        var (a, b, c) = (points[^3], points[^2], points[^1]);
        return new StrokeSegment(points.Count == 3 ? (a.X, a.Y) : Mid(a, b), (b.X, b.Y), Mid(b, c), (a.Width + b.Width) / 2);
    }

    /// <summary>抬笔（取消也一样收下已写的部分）：返回这一笔有没有（写过的才算）。</summary>
    public bool End()
    {
        if (drawing is not { } points) return false;
        drawing = null;
        strokes.Add([.. points]);
        return true;
    }

    /// <summary>撤销上一笔；没有笔画时返回 false。</summary>
    public bool Undo()
    {
        if (strokes.Count == 0) return false;
        strokes.RemoveAt(strokes.Count - 1);
        return true;
    }

    /// <summary>清空笔画。</summary>
    public void Clear()
    {
        strokes.Clear();
        drawing = null;
    }

    static (double X, double Y) Mid(StrokePoint a, StrokePoint b) => ((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    /// <summary>一笔拆成二次贝塞尔的段：穿过相邻两点的中点，控制点是原来的点；笔宽取两端的平均。只有一个点时没有段（画成圆点）。</summary>
    public static IReadOnlyList<StrokeSegment> SegmentsOf(IReadOnlyList<StrokePoint> stroke)
    {
        if (stroke.Count < 2) return [];
        var result = new List<StrokeSegment>(stroke.Count - 1);
        var from = (stroke[0].X, stroke[0].Y);
        for (var i = 1; i < stroke.Count; i++)
        {
            var point = stroke[i];
            var last = i == stroke.Count - 1;
            var to = last ? (point.X, point.Y) : Mid(point, stroke[i + 1]);
            var control = last ? to : (point.X, point.Y);
            result.Add(new StrokeSegment(from, control, to, (stroke[i - 1].Width + point.Width) / 2));
            from = to;
        }
        return result;
    }

    /// <summary>
    /// SVG 文字：笔画是路径（一个点的是圆），读回的旧签名（PNG 字节）是嵌入的图片；没签时 null。
    /// </summary>
    /// <param name="width">画布的宽。</param>
    /// <param name="height">画布的高。</param>
    /// <param name="color">墨色（#rrggbb）。</param>
    /// <param name="basePng">读回的旧签名（PNG），画在最底下。</param>
    public string? ToSvg(double width, double height, string color, byte[]? basePng = null)
    {
        if (strokes.Count == 0 && basePng is null) return null;
        static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        var svg = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(width)}\" height=\"{N(height)}\" viewBox=\"0 0 {N(width)} {N(height)}\">");
        if (basePng is not null)
            svg.Append($"<image href=\"data:image/png;base64,{Convert.ToBase64String(basePng)}\" width=\"{N(width)}\" height=\"{N(height)}\"/>");
        svg.Append($"<g fill=\"none\" stroke=\"{color}\" stroke-linecap=\"round\">");
        foreach (var stroke in strokes)
        {
            if (stroke.Length == 1)
            {
                svg.Append($"<circle cx=\"{N(stroke[0].X)}\" cy=\"{N(stroke[0].Y)}\" r=\"{N(stroke[0].Width / 2)}\" fill=\"{color}\"/>");
                continue;
            }
            foreach (var (from, control, to, w) in SegmentsOf(stroke))
                svg.Append($"<path d=\"M{N(from.X)} {N(from.Y)}Q{N(control.X)} {N(control.Y)} {N(to.X)} {N(to.Y)}\" stroke-width=\"{w.ToString("0.00", CultureInfo.InvariantCulture)}\"/>");
        }
        return svg.Append("</g></svg>").ToString();
    }
}
