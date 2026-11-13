namespace Rune.WinForms;

/// <summary>
/// 图标的描线（TS 的 Icon draw）：把路径拆成一笔一笔（每个 M 开始的子路径），曲线展开成折线，
/// 按进度截出每一笔已经画出的部分。TS 给每个 SVG 元素写 pathLength = 1、再按比例设虚线；这里按每一笔自己的长度截，看起来一样。
/// 路径只认绝对坐标的 M、L、C、Z（Lucide 的数据都已归一成这几种）。
/// </summary>
sealed class IconStrokes
{
    /// <summary>一段三次贝塞尔展开成几段直线：图标最大 4em（约 64 像素），16 段看不出折角。</summary>
    const int CurveSegments = 16;

    readonly List<Stroke> strokes;

    IconStrokes(List<Stroke> strokes) => this.strokes = strokes;

    /// <summary>一笔：折线的点与每个点处已走过的长度。</summary>
    sealed record Stroke((double X, double Y)[] Points, double[] Distances)
    {
        public double Length => Distances[^1];
    }

    /// <summary>解析图标路径。</summary>
    /// <param name="path">绝对坐标的 M、L、C、Z。</param>
    public static IconStrokes Parse(string path)
    {
        var strokes = new List<Stroke>();
        var points = new List<(double X, double Y)>();
        var start = (X: 0d, Y: 0d);
        var i = 0;
        while (i < path.Length)
        {
            var command = path[i++];
            switch (command)
            {
                case 'M':
                    Flush();
                    start = Point();
                    points.Add(start);
                    break;
                case 'L':
                    points.Add(Point());
                    break;
                case 'C':
                    var (from, c1, c2, to) = (points[^1], Point(), Point(), Point());
                    for (var n = 1; n <= CurveSegments; n++) points.Add(Bezier(from, c1, c2, to, (double)n / CurveSegments));
                    break;
                case 'Z':
                    points.Add(start);
                    break;
                case ' ' or ',':
                    break;
                default:
                    throw new FormatException($"图标路径只认绝对坐标的 M、L、C、Z，位置 {i - 1} 是“{command}”：{path}");
            }
        }
        Flush();
        return new IconStrokes(strokes);

        void Flush()
        {
            if (points.Count == 0) return;
            var distances = new double[points.Count];
            for (var n = 1; n < points.Count; n++)
                distances[n] = distances[n - 1] + Math.Sqrt(Math.Pow(points[n].X - points[n - 1].X, 2) + Math.Pow(points[n].Y - points[n - 1].Y, 2));
            strokes.Add(new Stroke([.. points], distances));
            points.Clear();
        }

        (double X, double Y) Point() => (Number(), Number());

        double Number()
        {
            while (i < path.Length && path[i] is ' ' or ',') i++;
            var begin = i;
            if (i < path.Length && path[i] is '-' or '+') i++;
            while (i < path.Length && (char.IsDigit(path[i]) || path[i] == '.' || path[i] is 'e' or 'E' || (path[i] is '-' or '+' && path[i - 1] is 'e' or 'E'))) i++;
            return double.Parse(path.AsSpan(begin, i - begin), System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    static (double X, double Y) Bezier((double X, double Y) p0, (double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3, double t)
    {
        var u = 1 - t;
        return (u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X,
            u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y);
    }

    /// <summary>
    /// 每一笔画到 <paramref name="progress"/>（0–1，按这一笔自己的长度）时的折线；进度为 0 时什么也不画
    /// （圆头的线长度为 0 时还会画出一个点）。
    /// </summary>
    public IEnumerable<(double X, double Y)[]> Partial(double progress)
    {
        if (progress <= 0) yield break;
        foreach (var stroke in strokes)
        {
            if (progress >= 1 || stroke.Length == 0)
            {
                yield return stroke.Points;
                continue;
            }
            var target = stroke.Length * progress;
            var end = Array.FindIndex(stroke.Distances, distance => distance >= target);
            var shown = stroke.Points[..end];
            var (before, after) = (stroke.Points[end - 1], stroke.Points[end]);
            var span = stroke.Distances[end] - stroke.Distances[end - 1];
            var t = span == 0 ? 1 : (target - stroke.Distances[end - 1]) / span;
            yield return [.. shown, (before.X + (after.X - before.X) * t, before.Y + (after.Y - before.Y) * t)];
        }
    }
}
