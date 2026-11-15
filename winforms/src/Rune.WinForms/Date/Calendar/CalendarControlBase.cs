using System.ComponentModel;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// CalendarControl、RangeCalendarControl 共有的选项（上游两个组件各写一遍的 min、max、isDateUnavailable、visibleMonths、granularity、defaultFocused、readonly）
/// 与值的读写：ISO 字符串和日期的换算，点格子写回时不把自己的改动当成外面改值。
/// </summary>
[ToolboxItem(false)]
public abstract class CalendarControlBase : CalendarViewControl
{
    bool writing;

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

    /// <summary>选择的粒度：Day（默认）、Month（选月份）、Year（选年份）。</summary>
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
        ReadOnly = () => IsReadOnly || !Enabled,
        DefaultFocused = () => Parse(DefaultFocused),
    };

    /// <summary>
    /// 值属性变了：点格子写回的由 CalendarViewControl 重画；外面改的值（快捷按钮、重置）翻到它那里。没有窗口句柄前不碰状态（见 Rebuild）。
    /// </summary>
    private protected void SyncValue()
    {
        if (writing || !IsHandleCreated) return;
        State!.SelectionChanged();
        Rebuild();
    }
}
