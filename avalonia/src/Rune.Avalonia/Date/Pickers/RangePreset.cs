namespace Rune.Avalonia;

/// <summary>
/// 区间选择器的一个快捷区间（vue-tsx-pro 的 DateRangePreset | DateRangeCustomPreset）：内置的名字（按今天与一周的第一天算，名字取语言包），
/// 或自己的区间（财年、上个结算周期；函数时每次打开按当时算）。
/// <code>
/// Presets = [DateRangePreset.Last7Days, DateRangePreset.ThisMonth, new RangePreset("本结算周期", SettlementPeriod)];
/// </code>
/// </summary>
public sealed record RangePreset
{
    /// <summary>内置的快捷区间。</summary>
    /// <param name="builtin">名字。</param>
    public RangePreset(DateRangePreset builtin) => Builtin = builtin;

    /// <summary>自己的快捷区间：每次打开时算。</summary>
    /// <param name="label">按钮上的文字。</param>
    /// <param name="value">区间（两端是日期）。</param>
    public RangePreset(string label, Func<DateRange> value)
    {
        Label = label;
        Value = value;
    }

    /// <summary>自己的快捷区间：固定的区间。</summary>
    /// <param name="label">按钮上的文字。</param>
    /// <param name="value">区间（两端是日期）。</param>
    public RangePreset(string label, DateRange value) : this(label, () => value)
    {
    }

    /// <summary>内置的名字；自己的快捷区间时 null。</summary>
    public DateRangePreset? Builtin { get; }

    /// <summary>按钮上的文字（自己的快捷区间）。</summary>
    public string? Label { get; }

    /// <summary>区间（自己的快捷区间）。</summary>
    public Func<DateRange>? Value { get; }

    /// <summary>内置的名字直接当快捷区间用：<c>Presets = [DateRangePreset.Today, …]</c>。</summary>
    public static implicit operator RangePreset(DateRangePreset builtin) => new(builtin);

    /// <summary>按钮上的文字：内置的取语言包。</summary>
    public string LabelIn(Lang lang) => Builtin is { } builtin ? CalendarText.PresetName(builtin, lang) : Label!;

    /// <summary>此刻的区间：内置的按今天与一周的第一天算。</summary>
    public DateRange Resolve(DateOnly today, DayOfWeek firstDayOfWeek) =>
        Builtin is { } builtin ? CalendarState.PresetRange(builtin, today, firstDayOfWeek) : Value!();
}
