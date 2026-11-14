namespace Rune.Wpf;

/// <summary>仪表盘外圈的一段区间（vue-tsx-pro 的 GaugeBand）：从上一段的终点（第一段从 Min）到 <paramref name="To"/>。</summary>
/// <param name="To">终点。</param>
/// <param name="Tone">色调：落在这一段时数值弧也用它。</param>
public readonly record struct GaugeBand(double To, StatusTone Tone);

/// <summary>
/// 仪表盘的几何与读法（vue-tsx-pro 的 Gauge 里的计算）：画布 200 × 108（viewBox 0 -4 200 108），半圆的圆心 (100, 100)，
/// 数值弧半径 80、线宽 14，外圈区间半径 94、线宽 4，目标刻度从 70 到 98、线宽 2.5。比例 0 在左、1 在右，经过上方。
/// 三个库各有一份，除命名空间外逐字相同（tools/twins）。
/// </summary>
static class GaugeGeometry
{
    /// <summary>画布的宽、高（高含上面伸出去的 4）与原点的纵向偏移。</summary>
    public const double Width = 200, Height = 108, Top = -4;

    /// <summary>圆心。</summary>
    public const double Center = 100;

    /// <summary>数值弧（与轨道）的半径、线宽。</summary>
    public const double Radius = 80, ArcWidth = 14;

    /// <summary>外圈区间的半径、线宽。</summary>
    public const double BandRadius = 94, BandWidth = 4;

    /// <summary>目标刻度的线宽。</summary>
    public const double TargetWidth = 2.5;

    /// <summary>值在范围里的比例（0–1，超出的夹住）。</summary>
    public static double Fraction(double value, double min, double max) => Math.Clamp((value - min) / (max - min is var span && span != 0 ? span : 1), 0, 1);

    /// <summary>比例 <paramref name="fraction"/> 在半径 <paramref name="radius"/> 上的点（画布坐标）。</summary>
    public static (double X, double Y) Point(double fraction, double radius)
    {
        var angle = Math.PI * (1 - fraction);
        return (Center + radius * Math.Cos(angle), Center - radius * Math.Sin(angle));
    }

    /// <summary>目标刻度：从数值弧的内沿穿过外圈。</summary>
    public static ((double X, double Y) From, (double X, double Y) To) Tick(double fraction) => (Point(fraction, Radius - 10), Point(fraction, BandRadius + 4));

    /// <summary>外圈各段的起止比例与色调（空段不画）。</summary>
    public static IEnumerable<(double From, double To, StatusTone Tone)> Bands(IReadOnlyList<GaugeBand> bands, double min, double max)
    {
        for (var position = 0; position < bands.Count; position++)
        {
            var from = Fraction(position == 0 ? min : bands[position - 1].To, min, max);
            var to = Fraction(bands[position].To, min, max);
            if (to > from) yield return (from, to, bands[position].Tone);
        }
    }

    /// <summary>数值弧的色调：写了的，否则取值所在区间的（超出最后一段时取最后一段），没有区间时 accent。</summary>
    public static StatusTone Tone(StatusTone? tone, IReadOnlyList<GaugeBand> bands, double value) =>
        tone is { Value: not null } own ? own : bands.Count == 0 ? StatusTone.Accent : bands.FirstOrDefault(band => value <= band.To, bands[^1]).Tone;

    /// <summary>格式化的数值（单位写成后缀）。</summary>
    public static string Text(decimal value, NumericFormat? format, string? unit, Lang lang) =>
        (unit is { Length: > 0 } ? (format ?? NumericFormat.Default) with { Suffix = unit } : format ?? NumericFormat.Default).Format(value, lang.Code, lang.Currency);

    /// <summary>读屏念的取值（TS 的 aria-valuetext）：数值，有目标时接"目标 …"。</summary>
    public static string Spoken(decimal value, decimal? target, NumericFormat? format, string? unit, Lang lang) =>
        target is { } goal ? $"{Text(value, format, unit, lang)}，{lang.ProgressTarget(Text(goal, format, unit, lang))}" : Text(value, format, unit, lang);
}
