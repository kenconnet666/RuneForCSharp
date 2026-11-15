using System.Globalization;
using Avalonia;

namespace Rune.Avalonia;

/// <summary>
/// 日期输入框（vue-tsx-pro 的 DateField）：分段输入年、月、日（不带弹层），段的顺序按语言（zh-CN 年/月/日，en-US 月/日/年）。
/// 参考 React Aria DateField、原生 input type="date"。要弹出日历用 DatePicker。
/// <code>
/// &lt;rune:Field Label="生日"&gt;&lt;rune:DateField Value="{Binding Birthday}" Max="2026-10-20" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:DateField Granularity="Minute" Value="{Binding At}" /&gt;   &lt;!-- 2026-10-20T14:30 --&gt;
/// &lt;rune:DateField Granularity="Month" Value="{Binding Month}" /&gt;  &lt;!-- 2026-10 --&gt;
/// </code>
/// 值是 ISO 字符串，形式随 <see cref="Granularity"/>：Year 2026、Month 2026-10、Day 2026-10-20、Quarter 2026-Q4、Week 2026-W43，
/// Hour / Minute / Second 是日期加时间 2026-10-20T14:30[:15]（同原生 datetime-local）。要 DateOnly、DateTime 时用 <see cref="Date"/>、<see cref="DateTime"/>。
/// 值在 Min–Max 之外、是不可选的日期或时刻时显示出错，值照样写回，由表单校验决定能不能提交。键盘见 DateSegmentGroup；部件见 SegmentedField。
/// </summary>
public class DateField : SegmentedField
{
    /// <summary>精确到哪一段，默认 Day；到 Hour / Minute / Second 时是日期加时间。</summary>
    public static readonly StyledProperty<DateFieldGranularity> GranularityProperty = AvaloniaProperty.Register<DateField, DateFieldGranularity>(nameof(Granularity), DateFieldGranularity.Day);

    /// <summary>最早的日期（ISO：2026-01-01，带时间时可以 2026-01-01T09:00）。</summary>
    public static readonly StyledProperty<string?> MinProperty = AvaloniaProperty.Register<DateField, string?>(nameof(Min));

    /// <summary>最晚的日期（ISO）。</summary>
    public static readonly StyledProperty<string?> MaxProperty = AvaloniaProperty.Register<DateField, string?>(nameof(Max));

    /// <summary>不可选的日期（节假日、已约满）。</summary>
    public static readonly StyledProperty<Func<DateOnly, bool>?> IsDateUnavailableProperty = AvaloniaProperty.Register<DateField, Func<DateOnly, bool>?>(nameof(IsDateUnavailable));

    /// <summary>带时间时不可选的时刻（午休、非营业时间）；第二个参数是那一天，周末与平日可以不一样。</summary>
    public static readonly StyledProperty<Func<TimeOnly, DateOnly, bool>?> IsTimeUnavailableProperty =
        AvaloniaProperty.Register<DateField, Func<TimeOnly, DateOnly, bool>?>(nameof(IsTimeUnavailable));

    /// <summary>新建：年月日。</summary>
    public DateField() => Fields = DateSegments.FieldsOf(Granularity);

    /// <summary>精确到哪一段。</summary>
    public DateFieldGranularity Granularity { get => GetValue(GranularityProperty); set => SetValue(GranularityProperty, value); }

    /// <summary>最早的日期。</summary>
    public string? Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最晚的日期。</summary>
    public string? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>不可选的日期。</summary>
    public Func<DateOnly, bool>? IsDateUnavailable { get => GetValue(IsDateUnavailableProperty); set => SetValue(IsDateUnavailableProperty, value); }

    /// <summary>带时间时不可选的时刻。</summary>
    public Func<TimeOnly, DateOnly, bool>? IsTimeUnavailable { get => GetValue(IsTimeUnavailableProperty); set => SetValue(IsTimeUnavailableProperty, value); }

    /// <summary>值是日期（Year … Day、Quarter、Week 取这一段的第一天；带时间时取日期部分）；没填全时 null。</summary>
    public DateOnly? Date
    {
        get => Value is { } iso && DateFieldRange.Period(iso) is { } period ? period.Start : null;
        set => Value = value is { } date ? DateSegments.ToIso(DateSegments.FromDateTime(date.ToDateTime(TimeOnly.MinValue)), Fields) : null;
    }

    /// <summary>值是时刻（带时间时；日期粒度时是那一天的零点）；没填全时 null。</summary>
    public DateTime? DateTime
    {
        get => Value is { } iso && DateFieldRange.Instant(iso) is { } instant ? instant : null;
        set => Value = value is { } time ? DateSegments.ToIso(DateSegments.FromDateTime(time), Fields) : null;
    }

    /// <inheritdoc />
    protected override bool IsOutOfRange(string iso) => DateFieldRange.IsOutOfRange(iso, Min, Max, IsDateUnavailable, IsTimeUnavailable);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GranularityProperty) Fields = DateSegments.FieldsOf(Granularity);
        else if (change.Property == MinProperty || change.Property == MaxProperty || change.Property == IsDateUnavailableProperty || change.Property == IsTimeUnavailableProperty)
            Revalidate();
    }
}
