using System.Windows;

namespace Rune.Wpf;

/// <summary>
/// 日历（vue-tsx-pro 的 Calendar，WAI-ARIA 的日期网格）：选一天或多天，可以并排几个月、显示周数、自定义日期格、切到月份与年份视图。
/// 参考 React Aria Calendar、Mantine DatePicker（inline）。
/// <code>
/// &lt;rune:Calendar Value="{Binding Departure}" Min="2026-01-01" AutomationProperties.Name="出发日期" /&gt;
/// &lt;rune:Calendar SelectionMode="Multiple" Values="{Binding Days}" VisibleMonths="2" /&gt;
/// </code>
/// 值是 ISO 日期字符串（2026-10-20，与 DateField、URL 参数一致）；要 DateOnly 时用 <see cref="Date"/>。
/// 月份名、星期名、读屏念的完整日期按当前语言（<see cref="Provider"/> 的 Lang）生成，一周从哪天开始按语言（可用 FirstDayOfWeek 覆盖）。
/// 选一个区间用 <see cref="RangeCalendar"/>；选月份、年份用 Granularity = Month / Year，值是那一段的第一天。
/// 键盘：←→ 一天、↑↓ 一周，Page Up / Down 一个月、加 Shift 一年，Home / End 到这周两端，Enter / 空格选中。部件见 <see cref="CalendarView"/>。
/// 与 WPF 自带的 System.Windows.Controls.Calendar 同名：同时引用两个命名空间时写 rune:Calendar。
/// </summary>
public class Calendar : CalendarBase
{
    /// <summary>单选时选中的日期（ISO）；没有时 null。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(Calendar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Calendar)d).SyncValue()));

    /// <summary>多选时选中的日期（ISO）。</summary>
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(nameof(Values), typeof(IReadOnlyList<string>), typeof(Calendar),
        new FrameworkPropertyMetadata(Array.Empty<string>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Calendar)d).SyncValue()));

    /// <summary>选择方式：Single（默认）或 Multiple；区间用 RangeCalendar。</summary>
    public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(nameof(SelectionMode), typeof(CalendarSelectionMode), typeof(Calendar),
        new PropertyMetadata(CalendarSelectionMode.Single, Changed), mode => (CalendarSelectionMode)mode != CalendarSelectionMode.Range);

    /// <summary>单选时点选了一天（再点已选的那天也会发）：日期选择器据此关闭弹层。</summary>
    public static readonly RoutedEvent PickedEvent = EventManager.RegisterRoutedEvent(nameof(Picked), RoutingStrategy.Bubble, typeof(EventHandler<CalendarPickedEventArgs>), typeof(Calendar));

    /// <summary>新建：今天所在的月。</summary>
    public Calendar() => State = NewState(
        () => SelectionMode,
        () => SelectionMode == CalendarSelectionMode.Multiple ? [.. Values.Select(Parse).OfType<DateOnly>()] : Parse(Value) is { } day ? [day] : [],
        Write);

    /// <summary>单选时选中的日期。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>多选时选中的日期。</summary>
    public IReadOnlyList<string> Values { get => (IReadOnlyList<string>)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>选择方式。</summary>
    public CalendarSelectionMode SelectionMode { get => (CalendarSelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>单选时选中的日期（DateOnly）；没有时 null。</summary>
    public DateOnly? Date
    {
        get => Parse(Value);
        set => Value = value is { } date ? Iso(date) : null;
    }

    /// <summary>单选时点选了一天。</summary>
    public event EventHandler<CalendarPickedEventArgs> Picked
    {
        add => AddHandler(PickedEvent, value);
        remove => RemoveHandler(PickedEvent, value);
    }

    void Write(IReadOnlyList<DateOnly> dates)
    {
        if (SelectionMode == CalendarSelectionMode.Multiple)
        {
            // 没变的那几天原样保留（带时间的值不丢时间）
            var current = Values;
            SetCurrentValue(ValuesProperty, dates.Select(date => current.FirstOrDefault(item => Parse(item) is { } day && CalendarState.Same(day, date, Granularity)) ?? Iso(date)).ToArray());
            return;
        }
        var picked = dates.Count > 0 ? (Parse(Value) is { } old && CalendarState.Same(old, dates[0], Granularity) ? Value : Iso(dates[0])) : null;
        SetCurrentValue(ValueProperty, picked);
        if (picked is not null) RaiseEvent(new CalendarPickedEventArgs(PickedEvent, dates[0], picked));
    }
}

/// <summary>日历点选了一天。</summary>
/// <param name="routedEvent">路由事件。</param>
/// <param name="date">点的日期。</param>
/// <param name="value">写回的值（ISO）。</param>
public sealed class CalendarPickedEventArgs(RoutedEvent routedEvent, DateOnly date, string value) : RoutedEventArgs(routedEvent)
{
    /// <summary>点的日期。</summary>
    public DateOnly Date { get; } = date;

    /// <summary>写回的值（ISO）。</summary>
    public string Value { get; } = value;
}
