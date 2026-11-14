namespace Rune.WinForms;

/// <summary>迷你图的画法（vue-tsx-pro 的 SparklineTypes）。</summary>
public enum SparklineType
{
    /// <summary>折线（默认）。</summary>
    Line,

    /// <summary>折线下面填一层浅色。</summary>
    Area,

    /// <summary>柱：每个值一根，从 0 起。</summary>
    Bar,
}

/// <summary>
/// 迷你图的几何（vue-tsx-pro 的 Sparkline 里的计算）：横坐标按位置（折线 0 到 n-1，柱每个占 1 格），纵坐标 0（上）到 100（下）。
/// 空值处断开成几段；柱从 0 起、宽 0.7 格居中；默认标出最后一个值。各平台按控件的宽高把这套坐标拉伸着画（TS 的 preserveAspectRatio="none"）。
/// 三个库各有一份，除命名空间外逐字相同（tools/twins）。
/// </summary>
sealed class SparklineGeometry
{
    /// <summary>一根柱：第几个值，顶边、底边（0–100）。</summary>
    public readonly record struct Bar(int Index, double Top, double Bottom);

    SparklineGeometry(double width, List<List<(double X, double Y)>> segments, List<Bar> bars, int lastIndex, (double X, double Y)? last)
    {
        Width = width;
        Segments = segments;
        Bars = bars;
        LastIndex = lastIndex;
        Last = last;
    }

    /// <summary>横向的格数：折线是 n-1（至少 1），柱是 n。</summary>
    public double Width { get; }

    /// <summary>折线的各段（空值处断开）。</summary>
    public IReadOnlyList<List<(double X, double Y)>> Segments { get; }

    /// <summary>柱（空值是空位）。</summary>
    public IReadOnlyList<Bar> Bars { get; }

    /// <summary>最后一个有值的位置；没有值时 -1。</summary>
    public int LastIndex { get; }

    /// <summary>最后一个值的点（折线、面积标出它）；没有值时为 null。</summary>
    public (double X, double Y)? Last { get; }

    /// <summary>按数据与纵向范围算出几何。</summary>
    /// <param name="data">各个值；null 是空值。</param>
    /// <param name="type">画法。</param>
    /// <param name="min">纵向的下限；不写时按数据（柱含 0）。</param>
    /// <param name="max">纵向的上限；不写时按数据（柱含 0）。</param>
    public static SparklineGeometry Of(IReadOnlyList<decimal?> data, SparklineType type, double? min = null, double? max = null)
    {
        var bars = type == SparklineType.Bar;
        var values = data.Select(value => value is { } number ? (double?)(double)number : null).ToList();
        var present = values.OfType<double>().Concat(bars ? [0d] : []).ToList();
        var low = min ?? (present.Count > 0 ? present.Min() : 0);
        var high = max ?? (present.Count > 0 ? present.Max() : 0);
        var span = high - low is var range && range != 0 ? range : 1;
        double YOf(double value) => 100 - (value - low) / span * 100;
        var segments = new List<List<(double X, double Y)>>();
        var current = new List<(double X, double Y)>();
        var columns = new List<Bar>();
        var baseline = YOf(Math.Max(low, 0));
        for (var position = 0; position < values.Count; position++)
        {
            if (values[position] is not { } value)
            {
                if (current.Count > 0) segments.Add(current);
                current = [];
                continue;
            }
            current.Add((position, YOf(value)));
            columns.Add(new Bar(position, Math.Min(YOf(value), baseline), Math.Max(YOf(value), baseline)));
        }
        if (current.Count > 0) segments.Add(current);
        var lastIndex = values.FindLastIndex(value => value is not null);
        return new SparklineGeometry(
            bars ? values.Count : Math.Max(values.Count - 1, 1),
            bars ? [] : segments,
            bars ? columns : [],
            lastIndex,
            lastIndex >= 0 ? (lastIndex, YOf(values[lastIndex]!.Value)) : null);
    }

    /// <summary>
    /// 给读屏的一句话（TS 的 aria-label）：几个值、从哪到哪、最低最高（按 <paramref name="format"/> 与语言格式化），label 写在前面；没有值时是"—"。
    /// </summary>
    public static string Summary(IReadOnlyList<decimal?> data, NumericFormat? format, Lang lang, string? label)
    {
        var present = data.OfType<decimal>().ToList();
        string Text(decimal value) => (format ?? NumericFormat.Default).Format(value, lang.Code, lang.Currency);
        var summary = present.Count == 0
            ? lang.EmptyValue
            : lang.SparklineSummary(present.Count, Text(present[0]), Text(present[^1]), Text(present.Min()), Text(present.Max()));
        return string.IsNullOrEmpty(label) ? summary : $"{label}：{summary}";
    }
}
