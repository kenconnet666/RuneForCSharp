using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 区间日历（vue-tsx-pro 的 RangeCalendar）：先点开始、再点结束，中间悬停时预览；酒店的入住与离店、报表的起止日期。参考 React Aria RangeCalendar。
/// <code>
/// var stay = new RangeCalendarControl { VisibleMonths = 2, Min = "2026-10-20", AccessibleName = "入住与离店" };
/// stay.ValueChanged += (_, _) => booking.Stay = stay.Range;
/// </code>
/// 值是 <see cref="IsoDateRange"/>（两端都含，ISO 字符串）；要日期时用 <see cref="Range"/>。反着点也会排好序。
/// 默认区间里不能夹着不可选的日期（订满的那天）：选了开始之后，越过不可选日期的那一侧都不能再选；允许时设 <see cref="AllowsUnavailableInRange"/>。
/// 选到一半按 Esc 取消。Granularity 是 Month / Year 时按月、按年选，写回的区间是整月、整年（开始那月的 1 日到结束那月的最后一天）。
/// 键盘与外观同 <see cref="CalendarControl"/>（见 <see cref="CalendarViewControl"/>）。
/// </summary>
public class RangeCalendarControl : CalendarControlBase
{
    /// <summary>新建：今天所在的月。</summary>
    public RangeCalendarControl() => State = NewState(
        () => CalendarSelectionMode.Range,
        () => Value?.Dates is { } range ? [range.Start, range.End] : [],
        Write,
        () => AllowsUnavailableInRange);

    /// <summary>选中的区间（两端都含）；没有时 null。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IsoDateRange? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            SyncValue();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>区间里允许夹着不可选的日期（请假跨过周末），默认 false。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool AllowsUnavailableInRange { get; set { field = value; Rebuild(); } }

    /// <summary>选中的区间（日期）；没有时 null。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateRange? Range
    {
        get => Value?.Dates;
        set => Value = value is { } range ? IsoDateRange.From(range) : null;
    }

    /// <summary>值变了（选完一个区间、外面改值）。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>选完了一个区间（点了结束）：日期选择器据此关闭弹层。</summary>
    public event EventHandler<RangeCalendarPickedEventArgs>? Picked;

    /// <summary>按月、按年选时两端扩到整月、整年。</summary>
    DateRange Expand(DateOnly start, DateOnly end) => Granularity switch
    {
        DateUnit.Year => new(new DateOnly(start.Year, 1, 1), new DateOnly(end.Year, 12, 31)),
        DateUnit.Month => new(new DateOnly(start.Year, start.Month, 1), new DateOnly(end.Year, end.Month, 1).AddMonths(1).AddDays(-1)),
        _ => new(start, end),
    };

    void Write(IReadOnlyList<DateOnly> dates)
    {
        var range = IsoDateRange.From(Expand(dates[0], dates[1]));
        Value = range;
        Picked?.Invoke(this, new RangeCalendarPickedEventArgs(range));
    }
}

/// <summary>区间日历选完了一个区间。</summary>
/// <param name="value">写回的区间。</param>
public sealed class RangeCalendarPickedEventArgs(IsoDateRange value) : EventArgs
{
    /// <summary>写回的区间。</summary>
    public IsoDateRange Value { get; } = value;
}
