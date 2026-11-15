using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 日期选择器（vue-tsx-pro 的 DatePicker）：DateField 加一个打开日历的按钮，日历弹在输入框下方；可以直接键入，也可以点选。
/// 参考 React Aria DatePicker、Mantine DatePickerInput。与 WPF 自带的 System.Windows.Controls.DatePicker 同名：同时引用两个命名空间时写 rune:DatePicker。
/// <code>
/// &lt;rune:Field Label="出发日期"&gt;&lt;rune:DatePicker Value="{Binding Departure}" Min="2026-10-20" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:DatePicker Granularity="Month" Value="{Binding Period}" /&gt;    &lt;!-- 2026-10 --&gt;
/// &lt;rune:DatePicker Granularity="Quarter" Value="{Binding Quarter}" /&gt; &lt;!-- 2026-Q4，弹出季度面板 --&gt;
/// </code>
/// Granularity 决定值的形式（同 DateField）与弹出的选择：Year、Month 选年份、月份，Quarter 弹出 <see cref="QuarterCalendar"/>，
/// Week 选周（日历按 ISO 周排、行首是周数，点哪天都是选它那一周），其余选日期。
/// 日历下方有"今天"（按粒度是今年、本季度、本月、本周；超出范围时不可用），旁边可以放快捷选项（<see cref="Presets"/>）。
/// 选好后关闭，焦点回到按钮；带时间时焦点落到"时"那一段接着输入（没有时间时先补 00:00）。Esc、点外部关闭；分段里按 Alt + ↓ 打开。
/// 部件样式键：同 DateField，另有 <see cref="PickerParts"/>（打开按钮、弹层、快捷选项、"今天"）。
/// </summary>
public class DatePicker : DateField
{
    /// <summary>弹层开着（双向）。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(DatePicker),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((DatePicker)d).OnOpenChanged()));

    /// <summary>弹出位置，默认 bottom-start；空间不够时翻到另一边。</summary>
    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(nameof(Placement), typeof(FloatingPlacement), typeof(DatePicker),
        new PropertyMetadata(FloatingPlacement.BottomStart, (d, _) => ((DatePicker)d).SyncPicker()));

    /// <summary>日历并排显示几个月，默认 1。</summary>
    public static readonly DependencyProperty VisibleMonthsProperty = DependencyProperty.Register(nameof(VisibleMonths), typeof(int), typeof(DatePicker),
        new PropertyMetadata(1), count => (int)count >= 1);

    /// <summary>日历旁边的快捷选项（一周前、月底）。</summary>
    public static readonly DependencyProperty PresetsProperty = DependencyProperty.Register(nameof(Presets), typeof(IReadOnlyList<DatePreset>), typeof(DatePicker),
        new PropertyMetadata(Array.Empty<DatePreset>()));

    /// <summary>日历下方的"今天"（选月份时"本月"、选年份时"今年"），默认 true。</summary>
    public static readonly DependencyProperty ShowTodayProperty = DependencyProperty.Register(nameof(ShowToday), typeof(bool), typeof(DatePicker), new PropertyMetadata(true));

    /// <summary>用户开、关了（按钮、Alt + ↓、选好了、Esc、点外部）；代码里改 IsOpen 时不发。</summary>
    public static readonly RoutedEvent OpenChangedEvent = EventManager.RegisterRoutedEvent(nameof(OpenChanged), RoutingStrategy.Direct, typeof(EventHandler<OpenChangedEventArgs>), typeof(DatePicker));

    readonly PickerPopover picker;
    OpenChangeReason? pending;

    /// <summary>新建：年月日。</summary>
    public DatePicker()
    {
        picker = new PickerPopover(this, Lucide.CalendarDays, SetOpen, FocusPanel);
        Suffix = picker.Button;
        AddLogicalChild(picker.Popup);
        Loaded += (_, _) =>
        {
            RefreshPanel();
            // 一开始就开着（IsOpen 写在初始化里）：等外壳排好版再弹出，平台的弹出层要锚点已经在窗口里
            Dispatcher.BeginInvoke(SyncPicker, DispatcherPriority.Loaded);
        };
    }

    /// <summary>弹层开着。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>弹出位置。</summary>
    public FloatingPlacement Placement { get => (FloatingPlacement)GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>日历并排显示几个月。</summary>
    public int VisibleMonths { get => (int)GetValue(VisibleMonthsProperty); set => SetValue(VisibleMonthsProperty, value); }

    /// <summary>快捷选项。</summary>
    public IReadOnlyList<DatePreset> Presets { get => (IReadOnlyList<DatePreset>)GetValue(PresetsProperty); set => SetValue(PresetsProperty, value); }

    /// <summary>显示"今天"。</summary>
    public bool ShowToday { get => (bool)GetValue(ShowTodayProperty); set => SetValue(ShowTodayProperty, value); }

    /// <summary>用户开、关了。</summary>
    public event EventHandler<OpenChangedEventArgs> OpenChanged
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

    bool Editable => IsEnabled && !ReadOnly;

    /// <summary>改开关并记下原因（属性变化时报告）；已经是这样时不动。</summary>
    void SetOpen(bool open, OpenChangeReason reason)
    {
        if (IsOpen == open || open && !Editable) return;
        pending = reason;
        SetCurrentValue(IsOpenProperty, open);
        pending = null;
    }

    void OnOpenChanged()
    {
        var reason = pending;
        RefreshPanel();
        SyncPicker();
        if (reason is { } known) RaiseEvent(new OpenChangedEventArgs(OpenChangedEvent, IsOpen, known));
    }

    /// <inheritdoc />
    private protected override void Synced() => SyncPicker();

    void SyncPicker()
    {
        // 基类的构造里就会同步一次（设段的时候），那时打开按钮还没建
        if (picker is null) return;
        picker.Sync(IsOpen, Placement, Provider.GetLang(this).DatePickerOpen, Editable);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) SyncPicker();
    }

    /// <summary>内容只在开着、进了界面时建（"今天"、快捷选项按打开的那一刻算）。</summary>
    void RefreshPanel() => picker.Panel.Child = IsOpen && IsLoaded ? BuildPanel() : null;

    /// <summary>打开后：焦点进日历当前的那一格（已选或今天），不是"上个月"按钮。</summary>
    void FocusPanel()
    {
        var cell = Descendants(picker.Panel).OfType<CalendarCellButton>().FirstOrDefault(button => button.IsTabStop);
        if (cell is not null) cell.Focus();
        else picker.Panel.Focus();
    }

    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    /// <summary>选好了：关上；带时间时接着输入时间，焦点去"时"那一段。</summary>
    void Picked()
    {
        SetOpen(false, OpenChangeReason.Select);
        picker.Picked(HasTime ? Group?.Boxes.FirstOrDefault(box => box.Type == DateSegmentType.Hour) : null);
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
            SetCurrentValue(ValueProperty, PeriodOf(day));
            return;
        }
        var current = Value ?? "";
        var midnight = Granularity == DateFieldGranularity.Second ? "T00:00:00" : "T00:00";
        SetCurrentValue(ValueProperty, PeriodOf(day) + (current.Length > 10 ? current[10..] : midnight));
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

    /// <summary>弹层的内容：快捷选项一列（有时）、日历（选季度时是季度面板）与"今天"。</summary>
    FrameworkElement BuildPanel()
    {
        var lang = Provider.GetLang(this);
        var em = FontSize;
        var body = new Flex { Gap = Space.Sm, Align = FlexAlign.Start };
        if (Presets.Count > 0) body.Children.Add(BuildPresets(lang, em));
        var main = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs };
        main.Children.Add(Granularity == DateFieldGranularity.Quarter ? BuildQuarter(lang) : BuildCalendar(lang));
        if (ShowToday)
        {
            var today = Today();
            var button = new Button { Name = "PART_Today", Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Content = TodayLabel(lang), IsEnabled = Selectable(PeriodOf(today)), HorizontalAlignment = HorizontalAlignment.Center };
            button.SetResourceReference(StyleProperty, PickerParts.Today);
            button.Click += (_, _) =>
            {
                PickDay(today);
                Picked();
            };
            var footer = new Border { Child = button, Padding = new Thickness(0, this.Resource<double>(ThemeKeys.Space.Xs) * em, 0, 0) };
            footer.SetResourceReference(StyleProperty, PickerParts.Footer);
            main.Children.Add(footer);
        }
        body.Children.Add(main);
        return body;
    }

    FrameworkElement BuildPresets(Lang lang, double em)
    {
        var column = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch };
        foreach (var preset in Presets)
        {
            var value = preset.Value();
            var button = new PickerPresetButton(preset.Label, value == Value) { Name = "PART_Preset", IsEnabled = Selectable(value) };
            button.Click += (_, _) =>
            {
                SetCurrentValue(ValueProperty, value);
                Picked();
            };
            column.Children.Add(button);
        }
        var group = new Border { Child = column, Padding = new Thickness(0, 0, this.Resource<double>(ThemeKeys.Space.Sm) * em, 0) };
        group.SetResourceReference(StyleProperty, PickerParts.Presets);
        AutomationProperties.SetName(group, lang.DatePickerPresets);
        return group;
    }

    FrameworkElement BuildQuarter(Lang lang)
    {
        var quarter = new QuarterCalendar { Value = Value, Min = Min, Max = Max };
        quarter.State.Today = Today;
        AutomationProperties.SetName(quarter, lang.DatePickerOpen);
        quarter.Picked += (_, _) =>
        {
            SetCurrentValue(ValueProperty, quarter.Value);
            Picked();
        };
        return quarter;
    }

    FrameworkElement BuildCalendar(Lang lang)
    {
        var calendar = new Calendar
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
        };
        calendar.State!.Today = Today;
        AutomationProperties.SetName(calendar, lang.DatePickerOpen);
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
}

/// <summary>DatePicker 的快捷选项（一周前、上个月末）。</summary>
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
