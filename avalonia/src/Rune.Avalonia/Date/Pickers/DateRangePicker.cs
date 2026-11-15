using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 日期区间选择器（vue-tsx-pro 的 DateRangePicker）：一个输入框里两组分段（开始 – 结束），按钮弹出区间日历，旁边可以放快捷区间（最近 7 天、本月……）。
/// 参考 React Aria DateRangePicker（同一个外壳里两组 DateInput）、Ant Design RangePicker 的 presets。
/// <code>
/// &lt;rune:Field Label="统计区间"&gt;&lt;rune:DateRangePicker Value="{Binding Range}" Max="2026-10-20" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:DateRangePicker Granularity="Month" Value="{Binding Budget}" /&gt;   &lt;!-- 2026-01 到 2026-06 --&gt;
/// </code>
/// 值是 <see cref="IsoDateRange"/>（两端都含，ISO 字符串，形式随 Granularity 同 DateField），两端都填好才写回，删掉一段写回 null。
/// Month、Year 按月、按年（日历选整月、整年），Quarter 弹出季度面板选一段季度，Week 按 ISO 周（日历周一开始、行首是周数），
/// Hour / Minute / Second 是日期时间区间（日历里选日期，开始补 00:00、结束补 23:59，已经填了的时间不变）。
/// 结束早于开始、超出范围、是不可选的日期时外壳显示出错，值照样写回，由表单校验决定能不能提交。
/// 开始那组刚填全时焦点自动进结束那组；日历里点开始、再点结束后关闭，焦点回到按钮。Esc、点外部关闭，分段里 Alt + ↓ 打开。
/// 快捷区间 <see cref="Presets"/> 是内置的名字（按今天与语言的一周第一天算）或自己的区间，夹到 Min–Max 之内。
/// 部件：外壳同 DateField（开始、结束两组是 PART_Input、PART_End，中间 PART_Separator），另有 <see cref="PickerParts"/>。
/// </summary>
public class DateRangePicker : SegmentedFieldBase
{
    /// <summary>区间（两端都含，默认双向绑定）；没填全时 null。</summary>
    public static readonly StyledProperty<IsoDateRange?> ValueProperty = AvaloniaProperty.Register<DateRangePicker, IsoDateRange?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>精确到哪一段（同 DateField），默认 Day。</summary>
    public static readonly StyledProperty<DateFieldGranularity> GranularityProperty = AvaloniaProperty.Register<DateRangePicker, DateFieldGranularity>(nameof(Granularity), DateFieldGranularity.Day);

    /// <summary>最早的日期（ISO）。</summary>
    public static readonly StyledProperty<string?> MinProperty = AvaloniaProperty.Register<DateRangePicker, string?>(nameof(Min));

    /// <summary>最晚的日期（ISO）。</summary>
    public static readonly StyledProperty<string?> MaxProperty = AvaloniaProperty.Register<DateRangePicker, string?>(nameof(Max));

    /// <summary>不可选的日期。</summary>
    public static readonly StyledProperty<Func<DateOnly, bool>?> IsDateUnavailableProperty = AvaloniaProperty.Register<DateRangePicker, Func<DateOnly, bool>?>(nameof(IsDateUnavailable));

    /// <summary>区间里允许夹着不可选的日期，默认 false。</summary>
    public static readonly StyledProperty<bool> AllowsUnavailableInRangeProperty = AvaloniaProperty.Register<DateRangePicker, bool>(nameof(AllowsUnavailableInRange));

    /// <summary>日历并排显示几个月，默认 2。</summary>
    public static readonly StyledProperty<int> VisibleMonthsProperty = AvaloniaProperty.Register<DateRangePicker, int>(nameof(VisibleMonths), 2, validate: count => count >= 1);

    /// <summary>快捷区间（日历旁边一列）。</summary>
    public static readonly StyledProperty<IReadOnlyList<RangePreset>> PresetsProperty = AvaloniaProperty.Register<DateRangePicker, IReadOnlyList<RangePreset>>(nameof(Presets), []);

    /// <summary>弹层开着（双向）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<DateRangePicker, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>弹出位置，默认 bottom-start。</summary>
    public static readonly StyledProperty<FloatingPlacement> PlacementProperty = AvaloniaProperty.Register<DateRangePicker, FloatingPlacement>(nameof(Placement), FloatingPlacement.BottomStart);

    /// <summary>用户开、关了（按钮、Alt + ↓、选好了、Esc、点外部）；代码里改 IsOpen 时不发。</summary>
    public static readonly RoutedEvent<OpenChangedEventArgs> OpenChangedEvent = RoutedEvent.Register<DateRangePicker, OpenChangedEventArgs>(nameof(OpenChanged), RoutingStrategies.Direct);

    readonly PickerPopover picker;
    // 两端各自的 ISO 字符串（按粒度的形式）：只填了一端时值仍是 null，这里记着填了的那一端
    string? start;
    string? end;
    bool writing;
    OpenChangeReason? pending;

    /// <summary>新建：两组年月日。</summary>
    public DateRangePicker() : base(2)
    {
        Fields = DateSegments.FieldsOf(Granularity);
        picker = new PickerPopover(this, Lucide.CalendarRange, SetOpen, FocusPanel);
        Suffix = picker.Button;
        LogicalChildren.Add(picker.Popup);
        // 开始那组最后一段输入满了：焦点进结束那组；结束那组第一段删空后再删：回到开始那组的最后一段
        Lanes[0].Exit = direction =>
        {
            if (direction == 1) FocusLane(1, Lanes[1].Editable.FirstOrDefault()?.Type);
        };
        Lanes[1].Exit = direction =>
        {
            if (direction == -1) FocusLane(0, Lanes[0].Editable.LastOrDefault()?.Type);
        };
    }

    /// <summary>区间。</summary>
    public IsoDateRange? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>精确到哪一段。</summary>
    public DateFieldGranularity Granularity { get => GetValue(GranularityProperty); set => SetValue(GranularityProperty, value); }

    /// <summary>最早的日期。</summary>
    public string? Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最晚的日期。</summary>
    public string? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>不可选的日期。</summary>
    public Func<DateOnly, bool>? IsDateUnavailable { get => GetValue(IsDateUnavailableProperty); set => SetValue(IsDateUnavailableProperty, value); }

    /// <summary>区间里允许夹着不可选的日期。</summary>
    public bool AllowsUnavailableInRange { get => GetValue(AllowsUnavailableInRangeProperty); set => SetValue(AllowsUnavailableInRangeProperty, value); }

    /// <summary>日历并排显示几个月。</summary>
    public int VisibleMonths { get => GetValue(VisibleMonthsProperty); set => SetValue(VisibleMonthsProperty, value); }

    /// <summary>快捷区间。</summary>
    public IReadOnlyList<RangePreset> Presets { get => GetValue(PresetsProperty); set => SetValue(PresetsProperty, value); }

    /// <summary>弹层开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>弹出位置。</summary>
    public FloatingPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>区间的日期（两端那一段的第一天、最后一天）；没填全时 null。</summary>
    public DateRange? Range => Value is { } value && DateFieldRange.Period(value.Start) is { } first && DateFieldRange.Period(value.End) is { } last ? new DateRange(first.Start, last.End) : null;

    /// <summary>用户开、关了。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    /// <summary>弹层（测试用）。</summary>
    internal Border Panel => picker.Panel;

    /// <summary>打开按钮（测试用）。</summary>
    internal IconButton Trigger => picker.Button;

    /// <summary>今天（测试里换掉）。</summary>
    internal Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(System.DateTime.Today);

    bool HasTime => Granularity is DateFieldGranularity.Hour or DateFieldGranularity.Minute or DateFieldGranularity.Second;

    bool Editable => IsEffectivelyEnabled && !ReadOnly;

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
        var side = lane == 0 ? Provider.GetLang(this).DateRangeStart : Provider.GetLang(this).DateRangeEnd;
        return name is { Length: > 0 } ? $"{name} {side}" : side;
    }

    private protected override void Cleared()
    {
        start = end = null;
        Commit();
    }

    // 只填了一端时值一直是 null，重置前后没有变化：两端自己放回值
    private protected override void FormReset() => SyncEnds();

    /// <summary>两端都填好才写回区间，否则写回 null（值本来就是空的不写）。</summary>
    void Commit()
    {
        var next = start is not null && end is not null ? new IsoDateRange(start, end) : null;
        if (next == Value)
        {
            Sync();
            return;
        }
        writing = true;
        try
        {
            SetCurrentValue(ValueProperty, next);
        }
        finally
        {
            writing = false;
        }
        Sync();
    }

    void SyncEnds()
    {
        start = Value?.Start;
        end = Value?.End;
    }

    void SetOpen(bool open, OpenChangeReason reason)
    {
        if (IsOpen == open || open && !Editable) return;
        pending = reason;
        SetCurrentValue(IsOpenProperty, open);
        pending = null;
    }

    /// <inheritdoc />
    private protected override void Synced() => SyncPicker();

    void SyncPicker()
    {
        // 基类的构造里就会同步一次，那时打开按钮还没建
        if (picker is null) return;
        picker.Sync(IsOpen, Placement, Provider.GetLang(this).DatePickerOpen, Editable);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            // 自己写回的值不再同步回两端（否则删掉一端时，写回的 null 会把另一端也清掉）
            if (writing) return;
            SyncEnds();
            SyncLanes();
        }
        else if (change.Property == GranularityProperty) Fields = DateSegments.FieldsOf(Granularity);
        else if (change.Property == MinProperty || change.Property == MaxProperty || change.Property == IsDateUnavailableProperty) Revalidate();
        else if (change.Property == IsOpenProperty)
        {
            var reason = pending;
            RefreshPanel();
            SyncPicker();
            if (reason is { } known) RaiseEvent(new OpenChangedEventArgs(OpenChangedEvent, IsOpen, known));
        }
        else if (change.Property == PlacementProperty || change.Property == FontSizeProperty) SyncPicker();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshPanel();
        // 一开始就开着：等外壳排好版再弹出，平台的弹出层要锚点已经在窗口里
        Dispatcher.UIThread.Post(SyncPicker, DispatcherPriority.Loaded);
    }

    /// <summary>内容只在开着、进了界面时建（快捷区间按打开的那一刻算）。</summary>
    void RefreshPanel() => picker.Panel.Child = IsOpen && VisualRoot is not null ? BuildPanel() : null;

    /// <summary>打开后：焦点进日历当前的那一格。</summary>
    void FocusPanel()
    {
        var cell = picker.Panel.GetLogicalDescendants().OfType<CalendarCellButton>().FirstOrDefault(button => button.IsTabStop);
        (cell ?? (InputElement)picker.Panel).Focus(NavigationMethod.Tab);
    }

    /// <summary>选好了：关上；带时间时接着输入时间，焦点去开始那组的"时"。</summary>
    void Picked()
    {
        SetOpen(false, OpenChangeReason.Select);
        picker.Picked(HasTime ? Groups[0]?.Boxes.FirstOrDefault(box => box.Type == DateSegmentType.Hour) : null);
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
        Commit();
        SyncLanes();
    }

    /// <summary>快捷区间夹到 Min–Max 之内（今天之后不能选时，"本月"就是本月 1 日到今天）。</summary>
    DateRange PresetValue(RangePreset preset)
    {
        var limits = Limits;
        var range = preset.Resolve(Today(), CalendarText.FirstDayOf(Provider.GetLang(this), null));
        return new DateRange(CalendarState.Clamp(range.Start, limits), CalendarState.Clamp(range.End, limits));
    }

    /// <summary>弹层的内容：快捷区间一列（有时）、区间日历（选季度时是季度面板）。</summary>
    Control BuildPanel()
    {
        var lang = Provider.GetLang(this);
        var body = new Flex { Gap = Space.Sm, Align = FlexAlign.Start };
        if (Presets.Count > 0)
        {
            var column = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch };
            foreach (var preset in Presets)
            {
                var shown = FromDays(PresetValue(preset));
                var button = new PickerPresetButton(preset.LabelIn(lang), shown.Start == start && shown.End == end);
                button.Click += (_, _) =>
                {
                    Pick(FromDays(PresetValue(preset)));
                    SetOpen(false, OpenChangeReason.Select);
                };
                column.Children.Add(button);
            }
            var group = new Border { Name = PickerParts.Presets, Child = column, Padding = new Thickness(0, 0, this.Resource<double>(ThemeKeys.Space.Sm) * FontSize, 0) };
            AutomationProperties.SetName(group, lang.DateRangePresets);
            body.Children.Add(group);
        }
        body.Children.Add(Granularity == DateFieldGranularity.Quarter ? BuildQuarter(lang) : BuildCalendar(lang));
        return body;
    }

    Control BuildQuarter(Lang lang)
    {
        var quarter = new QuarterCalendar
        {
            Name = PickerParts.Calendar,
            SelectionMode = CalendarSelectionMode.Range,
            RangeValue = start is not null && end is not null ? new IsoDateRange(start, end) : null,
            Min = Min,
            Max = Max,
        };
        quarter.State.Today = Today;
        AutomationProperties.SetName(quarter, lang.DatePickerOpen);
        quarter.Picked += (_, _) =>
        {
            if (quarter.RangeValue is { } range) Pick(range);
            Picked();
        };
        return quarter;
    }

    Control BuildCalendar(Lang lang)
    {
        var calendar = new RangeCalendar
        {
            Name = PickerParts.Calendar,
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
        };
        calendar.State!.Today = Today;
        AutomationProperties.SetName(calendar, lang.DatePickerOpen);
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
}
