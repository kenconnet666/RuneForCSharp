using System.ComponentModel;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 日期选择器（vue-tsx-pro 的 DatePicker）：DateFieldControl 加一个打开日历的按钮，日历弹在输入框下方；可以直接键入，也可以点选。
/// 参考 React Aria DatePicker、Mantine DatePickerInput。
/// <code>
/// var departure = new DatePickerControl { Min = "2026-10-20" };
/// var period = new DatePickerControl { Granularity = DateFieldGranularity.Month };     // 2026-10
/// var quarter = new DatePickerControl { Granularity = DateFieldGranularity.Quarter };  // 2026-Q4，弹出季度面板
/// </code>
/// Granularity 决定值的形式（同 DateFieldControl）与弹出的选择：Year、Month 选年份、月份，Quarter 弹出 <see cref="QuarterCalendarControl"/>，
/// Week 选周（日历按 ISO 周排、行首是周数，点哪天都是选它那一周），其余选日期。
/// 日历下方有"今天"（按粒度是今年、本季度、本月、本周；超出范围时不可用），旁边可以放快捷选项（<see cref="Presets"/>）。
/// 选好后关闭，焦点回到按钮；带时间时焦点落到分段上接着输入时间（没有时间时先补 00:00）。Esc、点外部关闭；分段里按 Alt + ↓ 打开。
/// </summary>
public class DatePickerControl : DateFieldControl
{
    readonly PickerPopover picker;
    bool open;

    /// <summary>新建：年月日。</summary>
    public DatePickerControl()
    {
        picker = new PickerPopover(this, Lucide.CalendarDays, SetOpen);
        Suffix = picker.Button;
        SyncPicker();
    }

    /// <summary>弹层开着。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOpen { get => open; set => SetOpen(value, null); }

    /// <summary>弹出位置，默认 bottom-start；空间不够时翻到另一边。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FloatingPlacement Placement { get; set; } = FloatingPlacement.BottomStart;

    /// <summary>日历并排显示几个月，默认 1。</summary>
    [Category("Appearance")]
    [DefaultValue(1)]
    public int VisibleMonths
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    } = 1;

    /// <summary>日历旁边的快捷选项（一周前、月底）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<DatePreset> Presets { get; set; } = [];

    /// <summary>日历下方的"今天"（选月份时"本月"、选年份时"今年"），默认 true。</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool ShowToday { get; set; } = true;

    /// <summary>用户开、关了（按钮、Alt + ↓、选好了、Esc、点外部）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>打开按钮（测试用）。</summary>
    internal IconButtonControl Trigger => picker.Button;

    /// <summary>弹层的内容窗口（测试用）。</summary>
    internal Form Surface => picker.Surface;

    /// <summary>今天（测试里换掉）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(System.DateTime.Today);

    bool HasTime => Granularity is DateFieldGranularity.Hour or DateFieldGranularity.Minute or DateFieldGranularity.Second;

    bool Editable => Enabled && !ReadOnly;

    /// <summary>改开关并带上原因（代码里改 IsOpen 时为 null，不报告）；只读、不可用时不开。</summary>
    void SetOpen(bool next, OpenChangeReason? reason)
    {
        if (next == open || next && !Editable) return;
        open = next;
        if (next) picker.Show(BuildPanel(out var focus), Placement, ProviderControl.LangOf(this).DatePickerOpen, focus);
        else picker.Hide();
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(next, known));
    }

    void SetOpen(bool next, OpenChangeReason reason) => SetOpen(next, (OpenChangeReason?)reason);

    /// <inheritdoc />
    private protected override void Synced() => SyncPicker();

    void SyncPicker()
    {
        // 基类的构造里就会同步一次（设段的时候），那时打开按钮还没建
        if (picker is null) return;
        picker.Sync(ProviderControl.LangOf(this).DatePickerOpen, Editable);
        if (open && !Editable) SetOpen(false, OpenChangeReason.Close);
    }

    /// <summary>选好了：关上；带时间时接着输入时间，焦点回到分段（"时"那一段）。</summary>
    void Picked()
    {
        open = false;
        if (HasTime)
        {
            Segments.SetFocused(DateSegmentType.Hour);
            picker.Hide(Group);
        }
        else picker.Hide();
        OpenChanged?.Invoke(this, new OpenChangedEventArgs(false, OpenChangeReason.Select));
    }

    /// <summary>一天 → 它所在的那一段（按粒度：年、月、季度、周；日与带时间的就是这一天）。</summary>
    string PeriodOf(DateOnly day) => Granularity switch
    {
        DateFieldGranularity.Year => day.ToString("yyyy", CultureInfo.InvariantCulture),
        DateFieldGranularity.Month => day.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        DateFieldGranularity.Quarter => QuarterState.Of(day),
        DateFieldGranularity.Week => WeekOf(day),
        _ => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    static string WeekOf(DateOnly day)
    {
        var time = day.ToDateTime(TimeOnly.MinValue);
        return $"{ISOWeek.GetYear(time).ToString("D4", CultureInfo.InvariantCulture)}-W{ISOWeek.GetWeekOfYear(time).ToString("D2", CultureInfo.InvariantCulture)}";
    }

    /// <summary>日历选了一天：换回粒度的形式；带时间的保留原来的时间，没有时补 00:00。</summary>
    void PickDay(DateOnly day)
    {
        if (!HasTime)
        {
            Value = PeriodOf(day);
            return;
        }
        var current = Value ?? "";
        var midnight = Granularity == DateFieldGranularity.Second ? "T00:00:00" : "T00:00";
        Value = PeriodOf(day) + (current.Length > 10 ? current[10..] : midnight);
    }

    /// <summary>能不能选：它那一段（选月份、年份时是那个月、那一年）在范围内、不是不可选的日期。</summary>
    bool Selectable(string iso) => !DateFieldRange.IsOutOfRange(iso, Min, Max, IsDateUnavailable, null);

    /// <summary>"今天"按钮的文字：按粒度是今年、本季度、本月、本周或今天。</summary>
    string TodayLabel(Lang lang) => Granularity switch
    {
        DateFieldGranularity.Year => CalendarText.PresetName(DateRangePreset.ThisYear, lang),
        DateFieldGranularity.Quarter => lang.DatePickerThisQuarter,
        DateFieldGranularity.Month => CalendarText.PresetName(DateRangePreset.ThisMonth, lang),
        DateFieldGranularity.Week => CalendarText.PresetName(DateRangePreset.ThisWeek, lang),
        _ => CalendarText.PresetName(DateRangePreset.Today, lang),
    };

    /// <summary>弹层的内容：快捷选项一列（有时，靠日历那边一条分隔线）、日历（选季度时是季度面板）与"今天"（上边一条分隔线）。</summary>
    /// <param name="focus">打开后聚焦的控件：日历当前的那一格所在的网格。</param>
    Control BuildPanel(out Func<Control?> focus)
    {
        var lang = ProviderControl.LangOf(this);
        var body = new FlexControl { Gap = Space.Sm, Align = FlexAlign.Stretch, AutoSize = true };
        if (Presets.Count > 0)
        {
            body.Controls.Add(BuildPresets(lang));
            body.Controls.Add(new SeparatorControl { Orientation = SeparatorOrientation.Vertical });
        }
        var main = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch, AutoSize = true };
        if (Granularity == DateFieldGranularity.Quarter)
        {
            var quarter = BuildQuarter(lang);
            main.Controls.Add(quarter);
            focus = () => quarter.Grid;
        }
        else
        {
            var calendar = BuildCalendar(lang);
            main.Controls.Add(calendar);
            focus = () => calendar.Grids.FirstOrDefault(grid => grid.TabStop);
        }
        if (ShowToday)
        {
            var today = Today();
            var button = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Text = TodayLabel(lang), Enabled = Selectable(PeriodOf(today)) };
            button.Click += (_, _) =>
            {
                PickDay(today);
                Picked();
            };
            main.Controls.Add(new SeparatorControl());
            var footer = new FlexControl { Justify = FlexJustify.Center, AutoSize = true };
            footer.Controls.Add(button);
            main.Controls.Add(footer);
        }
        body.Controls.Add(main);
        return body;
    }

    Control BuildPresets(Lang lang)
    {
        var column = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch, AutoSize = true, AccessibleName = lang.DatePickerPresets, AccessibleRole = AccessibleRole.Grouping };
        foreach (var preset in Presets)
        {
            var value = preset.Value();
            var button = new PickerPresetButtonControl(preset.Label, value == Value) { Enabled = Selectable(value) };
            button.Click += (_, _) =>
            {
                Value = value;
                Picked();
            };
            column.Controls.Add(button);
        }
        return column;
    }

    QuarterCalendarControl BuildQuarter(Lang lang)
    {
        var quarter = new QuarterCalendarControl { Value = Value, Min = Min, Max = Max, AccessibleName = lang.DatePickerOpen };
        quarter.State.Today = Today;
        quarter.Picked += (_, _) =>
        {
            Value = quarter.Value;
            Picked();
        };
        return quarter;
    }

    CalendarControl BuildCalendar(Lang lang)
    {
        var calendar = new CalendarControl
        {
            // 日历只认完整的日期：2026-10、2026-W43 取那一段的第一天
            Value = Value is { } iso && DateFieldRange.Period(iso) is { } period ? period.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            Granularity = Granularity switch
            {
                DateFieldGranularity.Year => DateUnit.Year,
                DateFieldGranularity.Month => DateUnit.Month,
                _ => DateUnit.Day,
            },
            Min = Min,
            Max = Max,
            IsDateUnavailable = IsDateUnavailable,
            VisibleMonths = VisibleMonths,
            AccessibleName = lang.DatePickerOpen,
        };
        calendar.State!.Today = Today;
        if (Granularity == DateFieldGranularity.Week)
        {
            // 选周：按 ISO 8601 的周（周一开始）排，行首是第几周，与值 2026-W43 一致；已选那一周的另外六天浅色底
            calendar.FirstDayOfWeek = DayOfWeek.Monday;
            calendar.ShowWeekNumbers = true;
            calendar.Highlighted = date => Value is { } value && WeekOf(date) == value;
        }
        calendar.Picked += (_, e) =>
        {
            PickDay(e.Date);
            Picked();
        };
        return calendar;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) picker.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>DatePickerControl 的快捷选项（一周前、上个月末）。</summary>
/// <param name="Label">按钮上的文字。</param>
/// <param name="Value">值（按 Granularity 的形式）；每次打开时取（跟着今天变）。</param>
public sealed record DatePreset(string Label, Func<string> Value)
{
    /// <summary>固定的值。</summary>
    /// <param name="label">按钮上的文字。</param>
    /// <param name="value">值。</param>
    public DatePreset(string label, string value) : this(label, () => value)
    {
    }
}

/// <summary>
/// 弹层里的一个快捷选项（vue-tsx-pro 的 renderPickerPresets）：sm、中性色的按钮，文字靠左；当前值就是它时 soft 样式，读屏念"已按下"（TS 的 aria-pressed）。
/// </summary>
sealed class PickerPresetButtonControl : ButtonControl
{
    public PickerPresetButtonControl(string label, bool active)
    {
        Text = label;
        IsActive = active;
        TextSize = TextSize.Sm;
        Variant = active ? ButtonVariant.Soft : ButtonVariant.Ghost;
        Tone = StatusTone.Neutral;
    }

    /// <summary>当前值就是它。</summary>
    public bool IsActive { get; }

    protected override AccessibleObject CreateAccessibilityInstance() => new PresetAccessibleObject(this);

    sealed class PresetAccessibleObject(PickerPresetButtonControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.CheckButton;

        public override AccessibleStates State => base.State | (owner.IsActive ? AccessibleStates.Pressed : 0);
    }
}
