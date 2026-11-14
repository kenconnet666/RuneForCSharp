namespace Rune.WinForms;

/// <summary>量表的一段区间（vue-tsx-pro 的 ProgressBand）：从上一段的终点（第一段从 0）到 <paramref name="To"/>。</summary>
/// <param name="To">终点（与 Value 同一单位）；最后一段没有上限时写 decimal.MaxValue。</param>
/// <param name="Tone">值落在这一段时的色调。</param>
public readonly record struct ProgressBand(decimal To, StatusTone Tone);

/// <summary>量表区间的规则（Progress 的 Bands）。</summary>
public static class ProgressBands
{
    /// <summary>值所在那一段的色调：第一段终点不小于值的；超过最后一段的算最后一段。没有值或没有区间时为 null（用 Tone）。</summary>
    /// <param name="value">取值。</param>
    /// <param name="bands">区间（按 To 从小到大）。</param>
    public static StatusTone? ToneAt(decimal? value, IReadOnlyList<ProgressBand>? bands)
    {
        if (value is not { } current || bands is not { Count: > 0 }) return null;
        foreach (var band in bands)
            if (current <= band.To) return band.Tone;
        return bands[^1].Tone;
    }
}
