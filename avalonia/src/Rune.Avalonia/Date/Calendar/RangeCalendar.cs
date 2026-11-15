using Avalonia;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace Rune.Avalonia;

/// <summary>
/// 区间日历（vue-tsx-pro 的 RangeCalendar）：先点开始、再点结束，中间悬停时预览；酒店的入住与离店、报表的起止日期。参考 React Aria RangeCalendar。
/// <code>
/// &lt;rune:RangeCalendar Value="{Binding Stay}" VisibleMonths="2" Min="2026-10-20" /&gt;
/// </code>
/// 值是 <see cref="IsoDateRange"/>（两端都含，ISO 字符串）；要日期时用 <see cref="Range"/>。反着点也会排好序。
/// 默认区间里不能夹着不可选的日期（订满的那天）：选了开始之后，越过不可选日期的那一侧都不能再选；允许时设 <see cref="AllowsUnavailableInRange"/>。
/// 选到一半按 Esc 取消。Granularity 是 Month / Year 时按月、按年选，写回的区间是整月、整年（开始那月的 1 日到结束那月的最后一天）。
/// 键盘与部件同 <see cref="Calendar"/>（部件见 <see cref="CalendarView"/>）。
/// </summary>
public class RangeCalendar : CalendarBase
{
    /// <summary>选中的区间（两端都含）；没有时 null。</summary>
    public static readonly StyledProperty<IsoDateRange?> ValueProperty = AvaloniaProperty.Register<RangeCalendar, IsoDateRange?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>区间里允许夹着不可选的日期（请假跨过周末），默认 false。</summary>
    public static readonly StyledProperty<bool> AllowsUnavailableInRangeProperty = AvaloniaProperty.Register<RangeCalendar, bool>(nameof(AllowsUnavailableInRange));

    /// <summary>选完了一个区间（点了结束）：日期选择器据此关闭弹层。</summary>
    public static readonly RoutedEvent<RangeCalendarPickedEventArgs> PickedEvent = RoutedEvent.Register<RangeCalendar, RangeCalendarPickedEventArgs>(nameof(Picked), RoutingStrategies.Bubble);

    /// <summary>新建：今天所在的月。</summary>
    public RangeCalendar() => State = NewState(
        () => CalendarSelectionMode.Range,
        () => Value?.Dates is { } range ? [range.Start, range.End] : [],
        Write,
        () => AllowsUnavailableInRange);

    /// <summary>选中的区间。</summary>
    public IsoDateRange? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>区间里允许夹着不可选的日期。</summary>
    public bool AllowsUnavailableInRange { get => GetValue(AllowsUnavailableInRangeProperty); set => SetValue(AllowsUnavailableInRangeProperty, value); }

    /// <summary>选中的区间（日期）；没有时 null。</summary>
    public DateRange? Range
    {
        get => Value?.Dates;
        set => Value = value is { } range ? IsoDateRange.From(range) : null;
    }

    /// <summary>选完了一个区间。</summary>
    public event EventHandler<RangeCalendarPickedEventArgs>? Picked
    {
        add => AddHandler(PickedEvent, value);
        remove => RemoveHandler(PickedEvent, value);
    }

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
        SetCurrentValue(ValueProperty, range);
        RaiseEvent(new RangeCalendarPickedEventArgs(PickedEvent, range));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty) SyncValue();
        else if (change.Property == AllowsUnavailableInRangeProperty) Rebuild();
    }
}

/// <summary>区间日历选完了一个区间。</summary>
/// <param name="routedEvent">路由事件。</param>
/// <param name="value">写回的区间。</param>
public sealed class RangeCalendarPickedEventArgs(RoutedEvent routedEvent, IsoDateRange value) : RoutedEventArgs(routedEvent)
{
    /// <summary>写回的区间。</summary>
    public IsoDateRange Value { get; } = value;
}
