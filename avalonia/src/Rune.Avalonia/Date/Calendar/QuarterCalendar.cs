using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using AvaloniaGrid = Avalonia.Controls.Grid;

namespace Rune.Avalonia;

/// <summary>
/// 季度面板（vue-tsx-pro 的 QuarterCalendar）：一年一页的四个季度（季度报表、考核周期），翻页换年；选一个季度，或选一段区间（先开始、再结束）。
/// DatePicker、DateRangePicker 的季度粒度弹出的就是它，也可以单独放在页面上。
/// <code>
/// &lt;rune:QuarterCalendar Value="{Binding Quarter}" AutomationProperties.Name="考核季度" /&gt;          &lt;!-- 2026-Q4 --&gt;
/// &lt;rune:QuarterCalendar SelectionMode="Range" RangeValue="{Binding Span}" /&gt;                  &lt;!-- 2026-Q1 到 2026-Q3 --&gt;
/// </code>
/// 外观与 Calendar 的月份视图一致（同一套部件名与样式，见 <see cref="CalendarViewParts"/>）：一行四格，区间连成一条，本季度带描边。
/// 键盘：←→ 移动（到头换年；从右到左时反过来），Home / End 到第一、第四季度，Page Up / Down 换年，Enter / 空格选中；区间选到一半时悬停预览，Esc 取消。
/// 整段在 Min–Max 之外的季度不可用。状态是孪生文件 <see cref="QuarterState"/>。
/// </summary>
public class QuarterCalendar : CalendarSurface
{
    /// <summary>单选时选中的季度（2026-Q4）；没有时 null。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<QuarterCalendar, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>选区间时选中的区间（两端是季度）；没有时 null。</summary>
    public static readonly StyledProperty<IsoDateRange?> RangeValueProperty = AvaloniaProperty.Register<QuarterCalendar, IsoDateRange?>(nameof(RangeValue), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>选择方式：Single（默认）或 Range。</summary>
    public static readonly StyledProperty<CalendarSelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<QuarterCalendar, CalendarSelectionMode>(nameof(SelectionMode), validate: mode => mode != CalendarSelectionMode.Multiple);

    /// <summary>最早可选的日期（ISO）：整个季度都早于它时不可用。</summary>
    public static readonly StyledProperty<string?> MinProperty = AvaloniaProperty.Register<QuarterCalendar, string?>(nameof(Min));

    /// <summary>最晚可选的日期（ISO）：整个季度都晚于它时不可用。</summary>
    public static readonly StyledProperty<string?> MaxProperty = AvaloniaProperty.Register<QuarterCalendar, string?>(nameof(Max));

    /// <summary>只读：能翻页、移动焦点，不能改选择。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<QuarterCalendar, bool>(nameof(IsReadOnly));

    /// <summary>选好了（单选选了一个，区间选了结束）：日期选择器据此关闭弹层。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> PickedEvent = RoutedEvent.Register<QuarterCalendar, RoutedEventArgs>(nameof(Picked), RoutingStrategies.Bubble);

    readonly AvaloniaGrid header = new() { Name = CalendarViewParts.Header, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
    readonly IconButton previous = new() { Name = CalendarViewParts.Previous, Icon = Lucide.ChevronLeft };
    readonly IconButton next = new() { Name = CalendarViewParts.Next, Icon = Lucide.ChevronRight };
    readonly Text heading = new() { Name = CalendarViewParts.Heading, Weight = TextWeight.Medium, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly UniformGrid grid = new() { Name = CalendarViewParts.Grid, Columns = 4 };
    readonly LiveRegion live = new() { Height = 0 };
    readonly LiveRegion hint = new() { Height = 0 };
    readonly List<(Border Holder, CalendarCellButton Button)> cells = [];
    bool writing;
    string? announced;

    /// <summary>新建：今年。</summary>
    public QuarterCalendar()
    {
        State = new QuarterState
        {
            IsRange = () => SelectionMode == CalendarSelectionMode.Range,
            Selected = () => Value,
            SelectedRange = () => RangeValue,
            IsDisabled = key => DateFieldRange.IsOutOfRange(key, Min, Max, null, null),
            ReadOnly = () => IsReadOnly || !IsEffectivelyEnabled,
        };
        foreach (var button in new[] { previous, next })
        {
            // 翻页箭头有方向：从右往左时跟着翻转（样式里设 FlipInRtl）
            var direction = button == previous ? -1 : 1;
            button.Click += (_, _) =>
            {
                State.TurnYear(direction);
                Sync();
            };
        }
        AvaloniaGrid.SetColumn(heading, 1);
        AvaloniaGrid.SetColumn(next, 2);
        header.Children.AddRange([previous, heading, next]);
        for (var quarter = 1; quarter <= 4; quarter++) grid.Children.Add(Cell(quarter));
        grid.PointerExited += (_, _) =>
        {
            State.Hover(null);
            Sync();
        };
        Root.Gap = Space.Xs;
        Root.Children.AddRange([header, grid, live, hint]);
        AddHandler(KeyDownEvent, OnGridKeyDown);
    }

    /// <summary>单选时选中的季度。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>选区间时选中的区间。</summary>
    public IsoDateRange? RangeValue { get => GetValue(RangeValueProperty); set => SetValue(RangeValueProperty, value); }

    /// <summary>选择方式。</summary>
    public CalendarSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>最早可选的日期。</summary>
    public string? Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最晚可选的日期。</summary>
    public string? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>选好了。</summary>
    public event EventHandler<RoutedEventArgs>? Picked
    {
        add => AddHandler(PickedEvent, value);
        remove => RemoveHandler(PickedEvent, value);
    }

    /// <summary>季度面板的状态。</summary>
    internal QuarterState State { get; }

    /// <summary>四个格子（测试用）。</summary>
    internal IEnumerable<CalendarCellButton> Cells => cells.Select(cell => cell.Button);

    Control Cell(int quarter)
    {
        var button = new CalendarCellButton(default, period: true) { Name = CalendarViewParts.CellButton, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) =>
        {
            if (!IsEffectivelyEnabled) return;
            // 区间的第一下只记下开始（返回 null），也要刷新出预览与提示
            if (State.Activate(State.Cell(quarter).Key) is not { } picked)
            {
                Sync();
                return;
            }
            writing = true;
            try
            {
                if (SelectionMode == CalendarSelectionMode.Range) SetCurrentValue(RangeValueProperty, picked);
                else SetCurrentValue(ValueProperty, picked.Start);
            }
            finally
            {
                writing = false;
            }
            Sync();
            RaiseEvent(new RoutedEventArgs(PickedEvent));
        };
        button.GotFocus += (_, _) => State.Focus(State.Cell(quarter).Key);
        button.PointerEntered += (_, _) =>
        {
            if (State.Anchor is null) return;
            State.Hover(State.Cell(quarter).Key);
            Sync();
        };
        var holder = new Border { Name = CalendarViewParts.Cell, Child = button };
        cells.Add((holder, button));
        return holder;
    }

    /// <summary>按状态刷新：年份标题、翻页按钮、四个格子（选中、区间、焦点、不可用），读屏播报换了的年份。</summary>
    void Sync(bool focus = false)
    {
        if (VisualRoot is null) return;
        var lang = Lang;
        var title = lang.CalendarYear(State.Year);
        heading.Text = title;
        AutomationProperties.SetName(grid, title);
        AutomationProperties.SetName(previous, lang.CalendarPreviousYear);
        AutomationProperties.SetName(next, lang.CalendarNextYear);
        previous.IsEnabled = next.IsEnabled = IsEffectivelyEnabled;
        hint.Text = State.Anchor is null ? "" : lang.CalendarPickEnd;
        if (title != announced)
        {
            if (announced is not null) live.Announce(title);
            announced = title;
        }
        var radius = this.Resource<double>(ThemeKeys.Radius.Md) * Em;
        var range = SelectionMode == CalendarSelectionMode.Range;
        for (var i = 0; i < cells.Count; i++)
        {
            var (holder, button) = cells[i];
            var cell = State.Cell(i + 1);
            var ends = cell.RangeStart || cell.RangeEnd;
            var off = !IsEffectivelyEnabled || cell.Disabled;
            button.Apply(new CalendarCellState(cell.Selected, cell.Disabled, false, cell.Today, cell.Focused, false, cell.RangeStart, cell.RangeEnd, cell.InRange),
                marked: range ? ends : cell.Selected, middle: cell.InRange && !ends, off: off);
            button.IsTabStop = cell.Focused;
            button.Content = lang.CalendarQuarter(i + 1);
            AutomationProperties.SetName(button, lang.DateFieldPeriodPatterns.Quarter.Replace("{year}", State.Year.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{quarter}", (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            AutomationProperties.SetHelpText(button, State.Anchor is null ? null : lang.CalendarPickEnd);
            holder.Classes.Set("in-range", cell.InRange);
            // 区间的底色画在格子上，相邻的格连成一条；两端与一行的首尾是圆的
            var left = i == 0 || cell.RangeStart;
            var right = i == 3 || cell.RangeEnd;
            holder.CornerRadius = cell.InRange ? new CornerRadius(left ? radius : 0, right ? radius : 0, right ? radius : 0, left ? radius : 0) : default;
        }
        grid.Width = 7 * Math.Max(2.25 * Em, this.Resource<double>(ThemeKeys.Size.MinTarget));
        if (focus) cells.FirstOrDefault(cell => cell.Button.IsTabStop).Button?.Focus(NavigationMethod.Directional);
    }

    void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsEffectivelyEnabled || e.Source is not CalendarCellButton) return;
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return;
        CalendarKey? key = e.Key switch
        {
            Key.Left => CalendarKey.Left,
            Key.Right => CalendarKey.Right,
            Key.PageUp => CalendarKey.PageUp,
            Key.PageDown => CalendarKey.PageDown,
            Key.Home => CalendarKey.Home,
            Key.End => CalendarKey.End,
            Key.Escape => CalendarKey.Escape,
            _ => null,
        };
        if (key is not { } pressed || !State.HandleKey(pressed, FlowDirection == global::Avalonia.Media.FlowDirection.RightToLeft)) return;
        e.Handled = true;
        Sync(focus: true);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == RangeValueProperty)
        {
            // 点格子写回的已经刷新过；外面改的值（快捷按钮、重置）翻到它那一年
            if (writing || VisualRoot is null) return;
            State.SelectionChanged();
            Sync();
        }
        else if (change.Property == SelectionModeProperty || change.Property == MinProperty || change.Property == MaxProperty || change.Property == IsReadOnlyProperty
                 || change.Property == IsEnabledProperty || change.Property == Provider.LangProperty || change.Property == global::Avalonia.Controls.Documents.TextElement.FontSizeProperty)
            Sync();
    }
}
