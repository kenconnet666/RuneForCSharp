using System.ComponentModel;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 日历（vue-tsx-pro 的 Calendar，WAI-ARIA 的日期网格）：选一天或多天，可以并排几个月、显示周数、自定义日期格、切到月份与年份视图。
/// 参考 React Aria Calendar、Mantine DatePicker（inline）。
/// <code>
/// var departure = new CalendarControl { Min = "2026-01-01", AccessibleName = "出发日期" };
/// departure.ValueChanged += (_, _) => order.Departure = departure.Date;
/// var shifts = new CalendarControl { SelectionMode = CalendarSelectionMode.Multiple, VisibleMonths = 2 };
/// </code>
/// 值是 ISO 日期字符串（2026-10-20，与 DateFieldControl、URL 参数一致）；要 DateOnly 时用 <see cref="Date"/>。
/// 月份名、星期名、读屏念的完整日期按当前语言（ProviderControl 的 Lang）生成，一周从哪天开始按语言（可用 FirstDayOfWeek 覆盖）。
/// 选一个区间用 RangeCalendarControl；选月份、年份用 <see cref="Granularity"/> = Month / Year。
/// 键盘：←→ 一天、↑↓ 一周，Page Up / Down 一个月、加 Shift 一年，Home / End 到这周两端，Enter / 空格选中。外观见 <see cref="CalendarViewControl"/>。
/// </summary>
public class CalendarControl : CalendarViewControl
{
    bool writing;

    /// <summary>新建：今天所在的月。</summary>
    public CalendarControl() =>
        State = new CalendarState
        {
            Mode = () => SelectionMode,
            Selected = () => SelectionMode == CalendarSelectionMode.Multiple ? [.. Values.Select(Parse).OfType<DateOnly>()] : Parse(Value) is { } day ? [day] : [],
            Changed = Write,
            Limits = () => new DateLimits(Parse(Min), Parse(Max), IsDateUnavailable),
            FirstDayOfWeek = () => FirstDay,
            Unit = () => Granularity,
            VisibleMonths = () => VisibleMonths,
            ReadOnly = () => IsReadOnly || !Enabled,
            DefaultFocused = () => Parse(DefaultFocused),
        };

    /// <summary>单选时选中的日期（ISO）；没有时 null。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public string? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            ChangedValue();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>多选时选中的日期（ISO）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Values
    {
        get;
        set
        {
            field = value;
            ChangedValue();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    } = [];

    /// <summary>选择方式：Single（默认）或 Multiple；区间用 RangeCalendarControl。</summary>
    [Category("Behavior")]
    [DefaultValue(CalendarSelectionMode.Single)]
    public CalendarSelectionMode SelectionMode
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, CalendarSelectionMode.Range);
            field = value;
            Rebuild();
        }
    }

    /// <summary>最早的日期（ISO；写成日期时间时按它的日期比较）。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Min { get; set { field = value; Rebuild(); } }

    /// <summary>最晚的日期（ISO）。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Max { get; set { field = value; Rebuild(); } }

    /// <summary>不可选的日期（节假日、已约满）：划掉、不能选。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<DateOnly, bool>? IsDateUnavailable { get; set { field = value; Rebuild(); } }

    /// <summary>并排显示几个月，默认 1。</summary>
    [Category("Appearance")]
    [DefaultValue(1)]
    public int VisibleMonths
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
            Rebuild();
        }
    } = 1;

    /// <summary>选择的粒度：Day（默认）、Month（选月份）、Year（选年份）；值是那一段的第一天。</summary>
    [Category("Behavior")]
    [DefaultValue(DateUnit.Day)]
    public DateUnit Granularity
    {
        get;
        set
        {
            field = value;
            if (!IsHandleCreated) return;
            State!.UnitChanged();
            Rebuild();
        }
    } = DateUnit.Day;

    /// <summary>没有已选日期时一开始显示哪天所在的月（ISO），默认今天。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? DefaultFocused { get; set; }

    /// <summary>只读：能翻页、移动焦点，不能改选择。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsReadOnly { get; set { field = value; Rebuild(); } }

    /// <summary>单选时选中的日期（DateOnly）；没有时 null。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateOnly? Date
    {
        get => Parse(Value);
        set => Value = value is { } date ? Iso(date) : null;
    }

    /// <summary>值变了（点选、外面改值）。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>单选时点选了一天（再点已选的那天也会发）：日期选择器据此关闭弹层。</summary>
    public event EventHandler<CalendarPickedEventArgs>? Picked;

    /// <summary>ISO 值代表的那一天（月份、年份、带时间的值取它的第一天、日期部分）；格式不对时 null。</summary>
    static DateOnly? Parse(string? iso) => iso is not null && DateFieldRange.Period(iso) is { } period ? period.Start : null;

    static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    void Write(IReadOnlyList<DateOnly> dates)
    {
        writing = true;
        try
        {
            if (SelectionMode == CalendarSelectionMode.Multiple)
            {
                // 没变的那几天原样保留（带时间的值不丢时间）
                var current = Values;
                Values = [.. dates.Select(date => current.FirstOrDefault(item => Parse(item) is { } day && CalendarState.Same(day, date, Granularity)) ?? Iso(date))];
                return;
            }
            var picked = dates.Count > 0 ? (Parse(Value) is { } old && CalendarState.Same(old, dates[0], Granularity) ? Value : Iso(dates[0])) : null;
            Value = picked;
            if (picked is not null) Picked?.Invoke(this, new CalendarPickedEventArgs(dates[0], picked));
        }
        finally
        {
            writing = false;
        }
    }

    // 点格子写回的值由 CalendarViewControl 重画；外面改的值（快捷按钮、重置）翻到它那里
    void ChangedValue()
    {
        if (writing || !IsHandleCreated) return;
        State!.SelectionChanged();
        Rebuild();
    }
}

/// <summary>日历点选了一天。</summary>
/// <param name="date">点的日期。</param>
/// <param name="value">写回的值（ISO）。</param>
public sealed class CalendarPickedEventArgs(DateOnly date, string value) : EventArgs
{
    /// <summary>点的日期。</summary>
    public DateOnly Date { get; } = date;

    /// <summary>写回的值（ISO）。</summary>
    public string Value { get; } = value;
}
