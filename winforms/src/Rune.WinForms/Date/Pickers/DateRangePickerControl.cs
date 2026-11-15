using System.ComponentModel;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 日期区间选择器（vue-tsx-pro 的 DateRangePicker）：一个输入框里两组分段（开始 – 结束），按钮弹出区间日历，旁边可以放快捷区间（最近 7 天、本月……）。
/// 参考 React Aria DateRangePicker（同一个外壳里两组 DateInput）、Ant Design RangePicker 的 presets。
/// <code>
/// var report = new DateRangePickerControl { Max = "2026-10-20", Presets = [DateRangePreset.Last7Days, DateRangePreset.ThisMonth] };
/// var budget = new DateRangePickerControl { Granularity = DateFieldGranularity.Month };   // 2026-01 到 2026-06
/// </code>
/// 值是 <see cref="IsoDateRange"/>（两端都含，ISO 字符串，形式随 Granularity 同 DateFieldControl），两端都填好才写回、发 <see cref="ValueChanged"/>，删掉一段写回 null。
/// Month、Year 按月、按年（日历选整月、整年），Quarter 弹出季度面板选一段季度，Week 按 ISO 周（日历周一开始、行首是周数），
/// Hour / Minute / Second 是日期时间区间（日历里选日期，开始补 00:00、结束补 23:59，已经填了的时间不变）。
/// 结束早于开始、超出范围、是不可选的日期时外壳显示出错，值照样写回，由表单校验决定能不能提交。
/// 开始那组刚填全时焦点自动进结束那组；日历里点开始、再点结束后关闭，焦点回到按钮。Esc、点外部关闭，分段里 Alt + ↓ 打开。
/// 快捷区间 <see cref="Presets"/> 是内置的名字（按今天与语言的一周第一天算）或自己的区间，夹到 Min–Max 之内。
/// </summary>
[DefaultEvent(nameof(ValueChanged))]
public class DateRangePickerControl : SegmentedFieldControlBase
{
    readonly PickerPopover picker;
    // 两端各自的 ISO 字符串（按粒度的形式）：只填了一端时值仍是 null，这里记着填了的那一端
    string? start;
    string? end;
    IsoDateRange? value;
    bool open;

    /// <summary>新建：两组年月日。</summary>
    public DateRangePickerControl() : base(2)
    {
        picker = new PickerPopover(this, Lucide.CalendarRange, SetOpen);
        Suffix = picker.Button;
        // 开始那组最后一段输入满了：焦点进结束那组；结束那组第一段删空后再删：回到开始那组的最后一段
        Lanes[0].Exit = direction =>
        {
            if (direction == 1) FocusLane(1, Lanes[1].Editable.FirstOrDefault()?.Type);
        };
        Lanes[1].Exit = direction =>
        {
            if (direction == -1) FocusLane(0, Lanes[0].Editable.LastOrDefault()?.Type);
        };
        SyncPicker();
    }

    /// <summary>区间（两端都含）；没填全时 null。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IsoDateRange? Value
    {
        get => value;
        set
        {
            start = value?.Start;
            end = value?.End;
            Write(value);
            SyncLanes();
        }
    }

    /// <summary>精确到哪一段（同 DateFieldControl），默认 Day。</summary>
    [Category("Behavior")]
    [DefaultValue(DateFieldGranularity.Day)]
    public DateFieldGranularity Granularity { get; set { field = value; Fields = DateSegments.FieldsOf(value); } } = DateFieldGranularity.Day;

    /// <summary>最早的日期（ISO）。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Min { get; set { field = value; Revalidate(); } }

    /// <summary>最晚的日期（ISO）。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Max { get; set { field = value; Revalidate(); } }

    /// <summary>不可选的日期。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<DateOnly, bool>? IsDateUnavailable { get; set { field = value; Revalidate(); } }

    /// <summary>区间里允许夹着不可选的日期，默认 false。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool AllowsUnavailableInRange { get; set; }

    /// <summary>日历并排显示几个月，默认 2。</summary>
    [Category("Appearance")]
    [DefaultValue(2)]
    public int VisibleMonths
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    } = 2;

    /// <summary>快捷区间（日历旁边一列）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<RangePreset> Presets { get; set; } = [];

    /// <summary>弹层开着。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOpen { get => open; set => SetOpen(value, null); }

    /// <summary>弹出位置，默认 bottom-start；空间不够时翻到另一边。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FloatingPlacement Placement { get; set; } = FloatingPlacement.BottomStart;

    /// <summary>区间的日期（两端那一段的第一天、最后一天）；没填全时 null。</summary>
    [Browsable(false)]
    public DateRange? Range => Value is { } range && DateFieldRange.Period(range.Start) is { } first && DateFieldRange.Period(range.End) is { } last ? new DateRange(first.Start, last.End) : null;

    /// <summary>值变了。</summary>
    public event EventHandler? ValueChanged;

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

    DateLimits Limits => new(Day(Min), Day(Max), IsDateUnavailable);

    static DateOnly? Day(string? iso) => iso is not null && DateFieldRange.Period(iso) is { } period ? period.Start : null;

    static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private protected override DateFieldValues? LaneValue(int lane) => DateSegments.FromIso(lane == 0 ? start : end, Fields);

    private protected override void LaneChanged(int lane, string? iso)
    {
        if (lane == 0) start = iso;
        else end = iso;
        Commit();
    }

    private protected override bool HasValue => start is not null || end is not null;

    /// <summary>结束早于开始、超出范围、是不可选的日期（ISO 的同一种形式按字符串比较就是先后）。</summary>
    private protected override bool ValueInvalid =>
        start is not null && end is not null && string.CompareOrdinal(end, start) < 0 || OutOfRange(start) || OutOfRange(end);

    bool OutOfRange(string? iso) => iso is not null && DateFieldRange.IsOutOfRange(iso, Min, Max, IsDateUnavailable, null);

    private protected override string? LaneName(int lane, string? name)
    {
        var lang = ProviderControl.LangOf(this);
        var side = lane == 0 ? lang.DateRangeStart : lang.DateRangeEnd;
        return name is { Length: > 0 } ? $"{name} {side}" : side;
    }

    private protected override void Cleared()
    {
        start = end = null;
        Commit();
    }

    // 只填了一端时值一直是 null，重置前后没有变化：两端自己放回值
    private protected override void FormReset()
    {
        start = value?.Start;
        end = value?.End;
    }

    /// <summary>两端都填好才写回区间，否则写回 null（值本来就是空的不写）。</summary>
    void Commit()
    {
        Write(start is not null && end is not null ? new IsoDateRange(start, end) : null);
        Sync();
    }

    /// <summary>换值（两端已经记好，各组由调用方同步）：变了时发 ValueChanged。</summary>
    void Write(IsoDateRange? next)
    {
        if (next == value) return;
        value = next;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

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
        // 基类的构造里就会同步一次，那时打开按钮还没建
        if (picker is null) return;
        picker.Sync(ProviderControl.LangOf(this).DatePickerOpen, Editable);
        if (open && !Editable) SetOpen(false, OpenChangeReason.Close);
    }

    /// <summary>选好了：关上；带时间时接着输入时间，焦点去开始那组的"时"。</summary>
    void Picked()
    {
        open = false;
        if (HasTime)
        {
            Lanes[0].SetFocused(DateSegmentType.Hour);
            picker.Hide(Groups[0]);
        }
        else picker.Hide();
        OpenChanged?.Invoke(this, new OpenChangedEventArgs(false, OpenChangeReason.Select));
    }

    /// <summary>整天的区间 → 按粒度的形式：年、月截短，季度、周换成所在的那一段；带时间时开始补 00:00、结束补 23:59（已经填了的时间不变）。</summary>
    IsoDateRange FromDays(DateRange range)
    {
        string Period(DateOnly day) => Granularity switch
        {
            DateFieldGranularity.Year => day.ToString("yyyy", CultureInfo.InvariantCulture),
            DateFieldGranularity.Month => day.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            DateFieldGranularity.Quarter => QuarterState.Of(day),
            DateFieldGranularity.Week => $"{ISOWeek.GetYear(day.ToDateTime(TimeOnly.MinValue)).ToString("D4", CultureInfo.InvariantCulture)}-W{ISOWeek.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue)).ToString("D2", CultureInfo.InvariantCulture)}",
            _ => Iso(day),
        };
        if (!HasTime) return new(Period(range.Start), Period(range.End));
        static string TimeOf(string? iso, string fallback) => iso is { Length: > 10 } ? iso[10..] : fallback;
        var endTime = Granularity switch { DateFieldGranularity.Hour => "T23:00", DateFieldGranularity.Second => "T23:59:59", _ => "T23:59" };
        return new(Iso(range.Start) + TimeOf(start, Granularity == DateFieldGranularity.Second ? "T00:00:00" : "T00:00"), Iso(range.End) + TimeOf(end, endTime));
    }

    void Pick(IsoDateRange range)
    {
        start = range.Start;
        end = range.End;
        Write(range);
        SyncLanes();
    }

    /// <summary>快捷区间夹到 Min–Max 之内（今天之后不能选时，"本月"就是本月 1 日到今天）。</summary>
    DateRange PresetValue(RangePreset preset)
    {
        var limits = Limits;
        var range = preset.Resolve(Today(), CalendarText.FirstDayOf(ProviderControl.LangOf(this), null));
        return new DateRange(CalendarState.Clamp(range.Start, limits), CalendarState.Clamp(range.End, limits));
    }

    /// <summary>弹层的内容：快捷区间一列（有时，靠日历那边一条分隔线）、区间日历（选季度时是季度面板）。</summary>
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
        if (Granularity == DateFieldGranularity.Quarter)
        {
            var quarter = BuildQuarter(lang);
            body.Controls.Add(quarter);
            focus = () => quarter.Grid;
        }
        else
        {
            var calendar = BuildCalendar(lang);
            body.Controls.Add(calendar);
            focus = () => calendar.Grids.FirstOrDefault(grid => grid.TabStop);
        }
        return body;
    }

    Control BuildPresets(Lang lang)
    {
        var column = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch, AutoSize = true, AccessibleName = lang.DateRangePresets, AccessibleRole = AccessibleRole.Grouping };
        foreach (var preset in Presets)
        {
            var shown = FromDays(PresetValue(preset));
            var button = new PickerPresetButtonControl(preset.LabelIn(lang), shown.Start == start && shown.End == end);
            button.Click += (_, _) =>
            {
                Pick(FromDays(PresetValue(preset)));
                SetOpen(false, OpenChangeReason.Select);
            };
            column.Controls.Add(button);
        }
        return column;
    }

    QuarterCalendarControl BuildQuarter(Lang lang)
    {
        var quarter = new QuarterCalendarControl
        {
            SelectionMode = CalendarSelectionMode.Range,
            RangeValue = start is not null && end is not null ? new IsoDateRange(start, end) : null,
            Min = Min,
            Max = Max,
            AccessibleName = lang.DatePickerOpen,
        };
        quarter.State.Today = Today;
        quarter.Picked += (_, _) =>
        {
            if (quarter.RangeValue is { } range) Pick(range);
            Picked();
        };
        return quarter;
    }

    RangeCalendarControl BuildCalendar(Lang lang)
    {
        var calendar = new RangeCalendarControl
        {
            // 区间日历用整天的日期：按月、按年、按周时是整段（开始那段的第一天到结束那段的最后一天）
            Range = start is not null && end is not null && DateFieldRange.Period(start) is { } first && DateFieldRange.Period(end) is { } last ? new DateRange(first.Start, last.End) : null,
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
            AllowsUnavailableInRange = AllowsUnavailableInRange,
            AccessibleName = lang.DatePickerOpen,
        };
        calendar.State!.Today = Today;
        if (Granularity == DateFieldGranularity.Week)
        {
            // 按周：按 ISO 8601 的周排（周一开始），行首是第几周
            calendar.FirstDayOfWeek = DayOfWeek.Monday;
            calendar.ShowWeekNumbers = true;
        }
        calendar.Picked += (_, _) =>
        {
            if (calendar.Range is { } range) Pick(FromDays(range));
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
