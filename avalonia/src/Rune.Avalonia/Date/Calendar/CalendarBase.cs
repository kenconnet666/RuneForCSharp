using System.Globalization;
using Avalonia;

namespace Rune.Avalonia;

/// <summary>
/// Calendar、RangeCalendar 共有的选项（上游两个组件各写一遍的 min、max、isDateUnavailable、visibleMonths、granularity、defaultFocused、readonly）
/// 与值的读写：ISO 字符串和日期的换算，点格子写回时不把自己的改动当成外面改值。
/// </summary>
public abstract class CalendarBase : CalendarView
{
    /// <summary>最早的日期（ISO；写成日期时间时按它的日期比较）。</summary>
    public static readonly StyledProperty<string?> MinProperty = AvaloniaProperty.Register<CalendarBase, string?>(nameof(Min));

    /// <summary>最晚的日期（ISO）。</summary>
    public static readonly StyledProperty<string?> MaxProperty = AvaloniaProperty.Register<CalendarBase, string?>(nameof(Max));

    /// <summary>不可选的日期（节假日、已约满）：划掉、不能选。</summary>
    public static readonly StyledProperty<Func<DateOnly, bool>?> IsDateUnavailableProperty = AvaloniaProperty.Register<CalendarBase, Func<DateOnly, bool>?>(nameof(IsDateUnavailable));

    /// <summary>并排显示几个月，默认 1。</summary>
    public static readonly StyledProperty<int> VisibleMonthsProperty = AvaloniaProperty.Register<CalendarBase, int>(nameof(VisibleMonths), 1, validate: count => count >= 1);

    /// <summary>选择的粒度：Day（默认）、Month（选月份）、Year（选年份）。</summary>
    public static readonly StyledProperty<DateUnit> GranularityProperty = AvaloniaProperty.Register<CalendarBase, DateUnit>(nameof(Granularity), DateUnit.Day);

    /// <summary>没有已选日期时一开始显示哪天所在的月（ISO），默认今天。</summary>
    public static readonly StyledProperty<string?> DefaultFocusedProperty = AvaloniaProperty.Register<CalendarBase, string?>(nameof(DefaultFocused));

    /// <summary>只读：能翻页、移动焦点，不能改选择。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<CalendarBase, bool>(nameof(IsReadOnly));

    bool writing;

    /// <summary>最早的日期。</summary>
    public string? Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最晚的日期。</summary>
    public string? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>不可选的日期。</summary>
    public Func<DateOnly, bool>? IsDateUnavailable { get => GetValue(IsDateUnavailableProperty); set => SetValue(IsDateUnavailableProperty, value); }

    /// <summary>并排显示几个月。</summary>
    public int VisibleMonths { get => GetValue(VisibleMonthsProperty); set => SetValue(VisibleMonthsProperty, value); }

    /// <summary>选择的粒度。</summary>
    public DateUnit Granularity { get => GetValue(GranularityProperty); set => SetValue(GranularityProperty, value); }

    /// <summary>一开始显示哪天所在的月。</summary>
    public string? DefaultFocused { get => GetValue(DefaultFocusedProperty); set => SetValue(DefaultFocusedProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>ISO 值代表的那一天（月份、年份、带时间的值取它的第一天、日期部分）；格式不对时 null。</summary>
    private protected static DateOnly? Parse(string? iso) => iso is not null && DateFieldRange.Period(iso) is { } period ? period.Start : null;

    private protected static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>派生类的状态：选择方式、已选的日期、写回由派生类给，其余接这里的选项。</summary>
    /// <param name="mode">选择方式。</param>
    /// <param name="selected">已选的日期。</param>
    /// <param name="write">写回选中的日期（点格子、按键时）。</param>
    /// <param name="allowsUnavailableInRange">区间里允许夹着不可选的日期。</param>
    private protected CalendarState NewState(Func<CalendarSelectionMode> mode, Func<IReadOnlyList<DateOnly>> selected, Action<IReadOnlyList<DateOnly>> write, Func<bool>? allowsUnavailableInRange = null) => new()
    {
        Mode = mode,
        Selected = selected,
        Changed = dates =>
        {
            writing = true;
            try
            {
                write(dates);
            }
            finally
            {
                writing = false;
            }
        },
        Limits = () => new DateLimits(Parse(Min), Parse(Max), IsDateUnavailable),
        FirstDayOfWeek = () => FirstDay,
        Unit = () => Granularity,
        VisibleMonths = () => VisibleMonths,
        AllowsUnavailableInRange = allowsUnavailableInRange ?? (() => false),
        ReadOnly = () => IsReadOnly || !IsEffectivelyEnabled,
        DefaultFocused = () => Parse(DefaultFocused),
    };

    /// <summary>
    /// 值属性变了：点格子写回的由 CalendarView 重画；外面改的值（快捷按钮、重置）翻到它那里。没进界面前不碰状态（见 Rebuild）。
    /// </summary>
    private protected void SyncValue()
    {
        if (writing || VisualRoot is null) return;
        State!.SelectionChanged();
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GranularityProperty)
        {
            if (VisualRoot is null) return;
            State!.UnitChanged();
            Rebuild();
        }
        else if (change.Property == MinProperty || change.Property == MaxProperty || change.Property == IsDateUnavailableProperty
                 || change.Property == VisibleMonthsProperty || change.Property == IsReadOnlyProperty)
            Rebuild();
    }
}
