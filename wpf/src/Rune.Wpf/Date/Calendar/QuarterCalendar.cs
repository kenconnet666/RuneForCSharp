using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using WpfGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

/// <summary>
/// 季度面板（vue-tsx-pro 的 QuarterCalendar）：一年一页的四个季度（季度报表、考核周期），翻页换年；选一个季度，或选一段区间（先开始、再结束）。
/// DatePicker、DateRangePicker 的季度粒度弹出的就是它，也可以单独放在页面上。
/// <code>
/// &lt;rune:QuarterCalendar Value="{Binding Quarter}" AutomationProperties.Name="考核季度" /&gt;          &lt;!-- 2026-Q4 --&gt;
/// &lt;rune:QuarterCalendar SelectionMode="Range" RangeValue="{Binding Span}" /&gt;                  &lt;!-- 2026-Q1 到 2026-Q3 --&gt;
/// </code>
/// 外观与 Calendar 的月份视图一致（同一套部件样式键，见 <see cref="CalendarViewParts"/>）：一行四格，区间连成一条，本季度带描边。
/// 键盘：←→ 移动（到头换年；从右到左时反过来），Home / End 到第一、第四季度，Page Up / Down 换年，Enter / 空格选中；区间选到一半时悬停预览，Esc 取消。
/// 整段在 Min–Max 之外的季度不可用。状态是孪生文件 <see cref="QuarterState"/>。
/// </summary>
public class QuarterCalendar : CompositeControl
{
    /// <summary>字号档位，格子随之缩放；不写时继承外面的字号。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(QuarterCalendar), new PropertyMetadata(default(TextSize), (d, _) => ((QuarterCalendar)d).ApplySize()));

    /// <summary>单选时选中的季度（2026-Q4）；没有时 null。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(QuarterCalendar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((QuarterCalendar)d).SyncValue()));

    /// <summary>选区间时选中的区间（两端是季度）；没有时 null。</summary>
    public static readonly DependencyProperty RangeValueProperty = DependencyProperty.Register(nameof(RangeValue), typeof(IsoDateRange), typeof(QuarterCalendar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((QuarterCalendar)d).SyncValue()));

    /// <summary>选择方式：Single（默认）或 Range。</summary>
    public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(nameof(SelectionMode), typeof(CalendarSelectionMode), typeof(QuarterCalendar),
        new PropertyMetadata(CalendarSelectionMode.Single, Changed), mode => (CalendarSelectionMode)mode != CalendarSelectionMode.Multiple);

    /// <summary>最早可选的日期（ISO）：整个季度都早于它时不可用。</summary>
    public static readonly DependencyProperty MinProperty = DependencyProperty.Register(nameof(Min), typeof(string), typeof(QuarterCalendar), new PropertyMetadata(null, Changed));

    /// <summary>最晚可选的日期（ISO）：整个季度都晚于它时不可用。</summary>
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(nameof(Max), typeof(string), typeof(QuarterCalendar), new PropertyMetadata(null, Changed));

    /// <summary>只读：能翻页、移动焦点，不能改选择。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(QuarterCalendar), new PropertyMetadata(false, Changed));

    /// <summary>选好了（单选选了一个，区间选了结束）：日期选择器据此关闭弹层。</summary>
    public static readonly RoutedEvent PickedEvent = EventManager.RegisterRoutedEvent(nameof(Picked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(QuarterCalendar));

    readonly WpfGrid header = new() { HorizontalAlignment = HorizontalAlignment.Left };
    readonly IconButton previous = new() { Name = "PART_Previous" };
    readonly IconButton next = new() { Name = "PART_Next" };
    readonly Text heading = new() { Name = "PART_Heading", Weight = TextWeight.Medium, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly CalendarPeriodGrid grid = new() { Name = "PART_Grid", Columns = 4, HorizontalAlignment = HorizontalAlignment.Left };
    readonly LiveRegion live = new() { Height = 0 };
    readonly LiveRegion hint = new() { Height = 0 };
    readonly List<(CalendarCell Holder, CalendarCellButton Button)> cells = [];
    bool writing;
    string? announced;

    static QuarterCalendar()
    {
        // 语言、方向、字号、可用状态变了都要刷新（文字、箭头、格子大小）
        Provider.LangProperty.OverrideMetadata(typeof(QuarterCalendar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, Changed));
        FlowDirectionProperty.OverrideMetadata(typeof(QuarterCalendar), new FrameworkPropertyMetadata(FlowDirection.LeftToRight, FrameworkPropertyMetadataOptions.Inherits, Changed));
        IsEnabledProperty.OverrideMetadata(typeof(QuarterCalendar), new UIPropertyMetadata(true, Changed));
        FocusableProperty.OverrideMetadata(typeof(QuarterCalendar), new FrameworkPropertyMetadata(false));
    }

    static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((QuarterCalendar)d).Sync();

    /// <summary>新建：今年。</summary>
    public QuarterCalendar()
    {
        State = new QuarterState
        {
            IsRange = () => SelectionMode == CalendarSelectionMode.Range,
            Selected = () => Value,
            SelectedRange = () => RangeValue,
            IsDisabled = key => DateFieldRange.IsOutOfRange(key, Min, Max, null, null),
            ReadOnly = () => IsReadOnly || !IsEnabled,
        };
        header.SetResourceReference(StyleProperty, CalendarViewParts.Header);
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        previous.SetResourceReference(StyleProperty, CalendarViewParts.Previous);
        next.SetResourceReference(StyleProperty, CalendarViewParts.Next);
        previous.Click += (_, _) => TurnYear(-1);
        next.Click += (_, _) => TurnYear(1);
        WpfGrid.SetColumn(heading, 1);
        WpfGrid.SetColumn(next, 2);
        header.Children.Add(previous);
        header.Children.Add(heading);
        header.Children.Add(next);
        grid.SetResourceReference(StyleProperty, CalendarViewParts.PeriodGrid);
        for (var quarter = 1; quarter <= 4; quarter++) grid.Children.Add(Cell(quarter));
        grid.MouseLeave += (_, _) =>
        {
            State.Hover(null);
            Sync();
        };
        var root = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs };
        foreach (var child in new UIElement[] { header, grid, live, hint }) root.Children.Add(child);
        Root = root;
        AddHandler(KeyDownEvent, new KeyEventHandler(OnGridKeyDown));
        Loaded += (_, _) => Sync();
    }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>单选时选中的季度。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>选区间时选中的区间。</summary>
    public IsoDateRange? RangeValue { get => (IsoDateRange?)GetValue(RangeValueProperty); set => SetValue(RangeValueProperty, value); }

    /// <summary>选择方式。</summary>
    public CalendarSelectionMode SelectionMode { get => (CalendarSelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>最早可选的日期。</summary>
    public string? Min { get => (string?)GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最晚可选的日期。</summary>
    public string? Max { get => (string?)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>选好了。</summary>
    public event RoutedEventHandler Picked
    {
        add => AddHandler(PickedEvent, value);
        remove => RemoveHandler(PickedEvent, value);
    }

    /// <summary>季度面板的状态。</summary>
    internal QuarterState State { get; }

    /// <summary>四个格子（测试用）。</summary>
    internal IEnumerable<CalendarCellButton> Cells => cells.Select(cell => cell.Button);

    /// <summary>年份标题（测试用）。</summary>
    internal Text Heading => heading;

    /// <summary>上一年、下一年（测试用）。</summary>
    internal (IconButton Previous, IconButton Next) PageButtons => (previous, next);

    double Em => FontSize;

    void ApplySize()
    {
        if (Size.Pixels is { } pixels) SetValue(FontSizeProperty, pixels);
        else if (Size.ResourceKey is { } key) SetResourceReference(FontSizeProperty, key);
        else ClearValue(FontSizeProperty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) Sync();
    }

    void TurnYear(int direction)
    {
        State.TurnYear(direction);
        Sync();
    }

    CalendarCell Cell(int quarter)
    {
        var button = new CalendarCellButton(default, period: true, "") { Name = "PART_CellButton", HorizontalAlignment = HorizontalAlignment.Stretch };
        button.SetResourceReference(StyleProperty, CalendarViewParts.CellButton);
        button.Click += (_, _) =>
        {
            if (!IsEnabled) return;
            // 区间的第一下只记下开始（返回 null），也要刷新出预览与提示
            if (State.Activate(State.Cell(quarter).Key) is { } picked)
            {
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
                RaiseEvent(new RoutedEventArgs(PickedEvent));
            }
            Sync();
        };
        button.GotKeyboardFocus += (_, _) => State.Focus(State.Cell(quarter).Key);
        button.MouseEnter += (_, _) =>
        {
            if (State.Anchor is null) return;
            State.Hover(State.Cell(quarter).Key);
            Sync();
        };
        var holder = new CalendarCell { Child = button };
        holder.SetResourceReference(StyleProperty, CalendarViewParts.Cell);
        cells.Add((holder, button));
        return holder;
    }

    // 点格子写回的已经刷新过；外面改的值（快捷按钮、重置）翻到它那一年
    void SyncValue()
    {
        if (writing || !IsLoaded) return;
        State.SelectionChanged();
        Sync();
    }

    /// <summary>按状态刷新：年份标题、翻页按钮、四个格子（选中、区间、焦点、不可用），读屏播报换了的年份。</summary>
    void Sync(bool focus = false)
    {
        if (!IsLoaded) return;
        var lang = Provider.GetLang(this);
        var title = lang.CalendarYear(State.Year);
        heading.Text = title;
        AutomationProperties.SetName(grid, title);
        // 图标不随从右到左镜像：箭头按方向换，保持指向翻页的方向
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        previous.Icon = rtl ? Lucide.ChevronRight : Lucide.ChevronLeft;
        next.Icon = rtl ? Lucide.ChevronLeft : Lucide.ChevronRight;
        AutomationProperties.SetName(previous, lang.CalendarPreviousYear);
        AutomationProperties.SetName(next, lang.CalendarNextYear);
        previous.IsEnabled = next.IsEnabled = IsEnabled;
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
            button.Apply(new CalendarCellState(cell.Selected, cell.Disabled, false, cell.Today, cell.Focused, false, cell.RangeStart, cell.RangeEnd, cell.InRange),
                marked: range ? ends : cell.Selected, middle: cell.InRange && !ends, off: !IsEnabled || cell.Disabled);
            button.IsTabStop = cell.Focused;
            button.Content = lang.CalendarQuarter(i + 1);
            AutomationProperties.SetName(button, lang.DateFieldPeriodPatterns.Quarter.Replace("{year}", State.Year.ToString(CultureInfo.InvariantCulture)).Replace("{quarter}", (i + 1).ToString(CultureInfo.InvariantCulture)));
            AutomationProperties.SetHelpText(button, State.Anchor is null ? "" : lang.CalendarPickEnd);
            holder.IsInRange = cell.InRange;
            // 区间的底色画在格子上，相邻的格连成一条；两端与一行的首尾是圆的
            var left = i == 0 || cell.RangeStart;
            var right = i == 3 || cell.RangeEnd;
            holder.CornerRadius = cell.InRange ? new CornerRadius(left ? radius : 0, right ? radius : 0, right ? radius : 0, left ? radius : 0) : default;
        }
        // 与日期网格同宽；页头跟着网格（放在拉伸的容器里时不铺满）
        grid.Width = header.Width = 7 * Math.Max(2.25 * Em, ControlHeight.MinTarget);
        if (focus) cells.FirstOrDefault(cell => cell.Button.IsTabStop).Button?.Focus();
    }

    void OnGridKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsEnabled || e.OriginalSource is not CalendarCellButton) return;
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0) return;
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
        if (key is not { } pressed || !State.HandleKey(pressed, FlowDirection == FlowDirection.RightToLeft)) return;
        e.Handled = true;
        Sync(focus: true);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    sealed class GroupPeer(QuarterCalendar owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(QuarterCalendar);
    }
}
