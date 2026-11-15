using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

/// <summary>
/// 日历的界面（vue-tsx-pro 的 CalendarView，WAI-ARIA 的日期网格）：页头（上一页、标题、下一页）、并排的月份网格、月份视图与年份视图、页脚。
/// 状态由派生类给的 <see cref="CalendarState"/>（Calendar、RangeCalendar、日期选择器的弹层共用）。
/// <list type="bullet">
/// <item>每格是一个按钮（读屏念完整日期，今天加"今天"），只有焦点日期那一格在 Tab 顺序里，键盘移动后焦点跟过去；</item>
/// <item>标题按钮切到月份视图、再切到年份视图（不比选择的粒度更细）；翻页、换视图时播报新的标题；</item>
/// <item>并排几个月时只显示本月的日期（补齐的上下月留空）；只有一个月时补齐的日期变淡、可以点。</item>
/// </list>
/// 外观：页头三列（翻页按钮、居中的标题按钮：medium 字重、md 圆角、悬停 fillHover）；星期表头与周数 textMuted；
/// 日期格 max(2.25em, 24) 见方（<see cref="CalendarCellButton.SizeEm"/>）、md 圆角、悬停 fillHover；今天 semibold 加 accentBorder 细圈；
/// 已选 accent 底、accentContrast 字；区间中间的格透明、accentText 字，底色是格子那一层（<see cref="CalendarCell"/>）的 accentSoft 连成一条；
/// 补齐的日期 textMuted；不可选的划掉；不能选的变淡。部件样式键见 <see cref="CalendarViewParts"/>，默认外观在 CalendarView.xaml。
/// </summary>
public class CalendarView : CompositeControl
{
    /// <summary>字号档位，格子随之缩放；不写时继承外面的字号。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(CalendarView), new PropertyMetadata(default(TextSize), (d, _) => ((CalendarView)d).ApplySize()));

    /// <summary>在每行前显示一年里的第几周。</summary>
    public static readonly DependencyProperty ShowWeekNumbersProperty = Register(nameof(ShowWeekNumbers), typeof(bool), false);

    /// <summary>总是 6 行（翻页时高度不跳），默认 true。</summary>
    public static readonly DependencyProperty FixedWeeksProperty = Register(nameof(FixedWeeks), typeof(bool), true);

    /// <summary>一周的第一天；不写时按语言包、再按区域惯例。</summary>
    public static readonly DependencyProperty FirstDayOfWeekProperty = Register(nameof(FirstDayOfWeek), typeof(DayOfWeek?), null);

    /// <summary>页脚：今天、清除这类按钮。</summary>
    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer), typeof(object), typeof(CalendarView), new PropertyMetadata(null, (d, _) => ((CalendarView)d).SyncFooter()));

    /// <summary>
    /// 自定义日期格的内容（价格、标记点）：拿到日期与这一格的状态，返回文字或元素；不写时是日期数字。读屏先念完整日期再念这里的文字。
    /// </summary>
    public static readonly DependencyProperty CellContentProperty = Register(nameof(CellContent), typeof(Func<DateOnly, CalendarCellState, object>), null);

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Sm };
    readonly Flex months = new() { Wrap = true, Gap = Space.Xl, Align = FlexAlign.Start };
    readonly LiveRegion live = new() { Height = 0 };
    readonly LiveRegion hint = new() { Height = 0 };
    readonly ContentControl footer = new() { Visibility = Visibility.Collapsed, Focusable = false };
    readonly List<CellEntry> entries = [];
    string? structure;
    string? announced;
    DateOnly? hovered;

    /// <summary>建好的一格：重画时按状态原地刷新（不重建，悬停预览时指针下的元素不换）。</summary>
    sealed record CellEntry(CalendarCell Holder, CalendarCellButton Button, DateOnly? Month, string Label, bool RowStart, bool RowEnd);

    static DependencyProperty Register(string name, Type type, object? fallback) =>
        DependencyProperty.Register(name, type, typeof(CalendarView), new PropertyMetadata(fallback, (d, _) => ((CalendarView)d).Rebuild()));

    static CalendarView()
    {
        // 语言、方向、字号、可用状态变了都要重画（文字、箭头、格子大小）
        Provider.LangProperty.OverrideMetadata(typeof(CalendarView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, Changed));
        FlowDirectionProperty.OverrideMetadata(typeof(CalendarView), new FrameworkPropertyMetadata(FlowDirection.LeftToRight, FrameworkPropertyMetadataOptions.Inherits, Changed));
        IsEnabledProperty.OverrideMetadata(typeof(CalendarView), new UIPropertyMetadata(true, Changed));
        FocusableProperty.OverrideMetadata(typeof(CalendarView), new FrameworkPropertyMetadata(false));
        static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((CalendarView)d).Rebuild();
    }

    /// <summary>新建：没有状态时什么都不画（派生类在构造时给）。</summary>
    public CalendarView()
    {
        months.SetResourceReference(StyleProperty, CalendarViewParts.Months);
        footer.SetResourceReference(StyleProperty, CalendarViewParts.Footer);
        root.Children.Add(months);
        root.Children.Add(live);
        root.Children.Add(hint);
        root.Children.Add(footer);
        Root = root;
        AddHandler(KeyDownEvent, new KeyEventHandler(OnGridKeyDown));
        Loaded += (_, _) => Rebuild();
    }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>显示周数。</summary>
    public bool ShowWeekNumbers { get => (bool)GetValue(ShowWeekNumbersProperty); set => SetValue(ShowWeekNumbersProperty, value); }

    /// <summary>总是 6 行。</summary>
    public bool FixedWeeks { get => (bool)GetValue(FixedWeeksProperty); set => SetValue(FixedWeeksProperty, value); }

    /// <summary>一周的第一天。</summary>
    public DayOfWeek? FirstDayOfWeek { get => (DayOfWeek?)GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <summary>页脚。</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>自定义日期格的内容。</summary>
    public Func<DateOnly, CalendarCellState, object>? CellContent
    {
        get => (Func<DateOnly, CalendarCellState, object>?)GetValue(CellContentProperty);
        set => SetValue(CellContentProperty, value);
    }

    /// <summary>选择无效（多选、区间里有不可选的日期）。</summary>
    public bool IsInvalid => State?.Invalid ?? false;

    /// <summary>日历状态（派生类给）。</summary>
    internal CalendarState? State { get; private protected set; }

    /// <summary>显示着的日期格（测试用）。</summary>
    internal IEnumerable<CalendarCellButton> Cells => Descendants(months).OfType<CalendarCellButton>();

    /// <summary>每月一张的网格（测试用）。</summary>
    internal IEnumerable<Panel> Grids => Descendants(months).OfType<Panel>().Where(panel => panel.Name == "PART_Grid");

    /// <summary>页头里的元素（测试用）：上一页、下一页、标题。</summary>
    internal IEnumerable<FrameworkElement> Named(string name) => Descendants(months).OfType<FrameworkElement>().Where(element => element.Name == name);

    internal Lang Lang => Provider.GetLang(this);

    internal CultureInfo Culture => CalendarText.CultureOf(Lang);

    /// <summary>一周的第一天：自己写的，否则语言包的，否则区域惯例。</summary>
    internal DayOfWeek FirstDay => CalendarText.FirstDayOf(Lang, FirstDayOfWeek);

    double Em => FontSize;

    double CellSide => Math.Max(2.25 * Em, ControlHeight.MinTarget);

    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

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
        if (e.Property == FontSizeProperty) Rebuild();
    }

    void SyncFooter()
    {
        footer.Content = Footer;
        footer.Visibility = Footer is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// 状态变了（选了、翻页、换视图、外面改值、悬停预览）：页面没换时只按状态刷新格子，换了页、视图或选项时重建页头与网格；
    /// focus 时把焦点交给新的焦点格（键盘移动后）。
    /// </summary>
    /// <param name="focus">把焦点移到焦点格。</param>
    internal void Rebuild(bool focus = false)
    {
        // 没进界面前不画：属性初始化的先后不定，状态要等各项（DefaultFocused、今天）都给了才开始
        if (State is not { } state || !IsLoaded) return;
        var lang = Lang;
        var shown = state.Months;
        var view = state.View;
        var key = string.Join("|", view, string.Join(",", shown), ShowWeekNumbers, FixedWeeks, FirstDay, lang.Code, IsEnabled, Em, FlowDirection, CellContent?.GetHashCode());
        if (key != structure)
        {
            structure = key;
            Build(state, lang, view, shown);
        }
        Sync(state, lang);
        if (focus) Cells.FirstOrDefault(cell => cell.IsTabStop)?.Focus();
    }

    void Build(CalendarState state, Lang lang, DateUnit view, IReadOnlyList<DateOnly> shown)
    {
        var culture = Culture;
        months.Children.Clear();
        entries.Clear();
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        var coarser = view == DateUnit.Day ? DateUnit.Month : view == DateUnit.Month ? DateUnit.Year : (DateUnit?)null;
        var (previousLabel, nextLabel) = view switch
        {
            DateUnit.Day => (lang.CalendarPreviousMonth, lang.CalendarNextMonth),
            DateUnit.Month => (lang.CalendarPreviousYear, lang.CalendarNextYear),
            _ => (lang.CalendarPreviousDecade, lang.CalendarNextDecade),
        };
        for (var index = 0; index < shown.Count; index++)
        {
            var month = shown[index];
            var title = CalendarText.Title(month, view, culture, lang);
            var header = new WpfGrid();
            header.SetResourceReference(StyleProperty, CalendarViewParts.Header);
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // 图标不随从右到左镜像：箭头按方向换，保持指向翻页的方向
            header.Children.Add(index == 0 ? PageButton(rtl ? Lucide.ChevronRight : Lucide.ChevronLeft, previousLabel, -1, "PART_Previous", CalendarViewParts.Previous) : Spacer());
            FrameworkElement heading;
            if (coarser is { } next && IsEnabled)
            {
                var button = new ButtonBase { Name = "PART_Heading", Content = title };
                button.SetResourceReference(StyleProperty, CalendarViewParts.Heading);
                AutomationProperties.SetName(button, next == DateUnit.Month ? lang.CalendarChooseMonth(title) : lang.CalendarChooseYear(title));
                button.Click += (_, _) =>
                {
                    state.SetView(next);
                    Rebuild();
                };
                heading = button;
            }
            // 已经是最粗的视图（或不可用）：标题只是文字
            else heading = new Text { Name = "PART_Heading", Text = title, Weight = TextWeight.Medium, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            WpfGrid.SetColumn(heading, 1);
            header.Children.Add(heading);
            var after = index == shown.Count - 1 ? PageButton(rtl ? Lucide.ChevronLeft : Lucide.ChevronRight, nextLabel, 1, "PART_Next", CalendarViewParts.Next) : Spacer();
            WpfGrid.SetColumn(after, 2);
            header.Children.Add(after);
            var column = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs };
            column.Children.Add(header);
            column.Children.Add(view == DateUnit.Day ? DayGrid(state, month, shown.Count == 1, title) : PeriodGrid(state, title));
            months.Children.Add(column);
        }
        // 翻页、换视图时播报新的标题；不放在标题按钮上，免得聚焦按钮时重复念
        var titles = string.Join("，", shown.Select(month => CalendarText.Title(month, view, culture, lang)));
        if (titles != announced)
        {
            if (announced is not null) live.Announce(titles);
            announced = titles;
        }
    }

    /// <summary>按状态刷新每一格：选中、焦点、区间与预览、不能选（读屏名称里的"今天"、自定义内容也跟着状态）。</summary>
    void Sync(CalendarState state, Lang lang)
    {
        hint.Text = state.Anchor is null ? "" : lang.CalendarPickEnd;
        var range = state.Mode() == CalendarSelectionMode.Range;
        var radius = this.Resource<double>(ThemeKeys.Radius.Md) * Em;
        foreach (var (holder, button, month, label, rowStart, rowEnd) in entries)
        {
            var cell = state.Cell(button.Date, month);
            var ends = range && (cell.RangeStart || cell.RangeEnd);
            button.Apply(cell, marked: range ? ends : cell.Selected, middle: cell.InRange && !ends, off: !IsEnabled || cell.Disabled);
            button.IsTabStop = cell.Focused && !cell.Outside;
            var content = month is null ? null : CellContent?.Invoke(button.Date, cell);
            // 不可选的划掉：TextBlock 的 TextDecorations 不继承，样式里设不到内容生成的文字上
            button.Content = content ?? (cell.Unavailable ? new TextBlock { Text = button.Text, TextDecorations = TextDecorations.Strikethrough } : button.Text);
            var spoken = cell.Today ? lang.CalendarToday(label) : label;
            AutomationProperties.SetName(button, content is null ? spoken : spoken + SpokenText(content));
            AutomationProperties.SetHelpText(button, state.Anchor is null ? "" : lang.CalendarPickEnd);
            holder.IsInRange = cell.InRange;
            // 区间的底色画在格子上，相邻的格连成一条；两端与每行的首尾是圆的
            var left = rowStart || cell.RangeStart;
            var right = rowEnd || cell.RangeEnd;
            holder.CornerRadius = cell.InRange ? new CornerRadius(left ? radius : 0, right ? radius : 0, right ? radius : 0, left ? radius : 0) : default;
        }
    }

    FrameworkElement PageButton(IconData icon, string label, int direction, string name, PartKey key)
    {
        var button = new IconButton { Name = name, Icon = icon, IsEnabled = IsEnabled && State!.CanTurnPage(direction) };
        button.SetResourceReference(StyleProperty, key);
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) =>
        {
            State!.TurnPage(direction);
            Rebuild();
        };
        return button;
    }

    // 占住按钮的位置：并排几个月时中间的月没有翻页按钮，标题仍居中
    FrameworkElement Spacer() => new Border { Width = Math.Max(this.Resource<double>(ThemeKeys.Size.Control) * Em, ControlHeight.MinTarget) };

    Panel DayGrid(CalendarState state, DateOnly month, bool single, string title)
    {
        var culture = Culture;
        var first = FirstDay;
        var weeks = CalendarState.MonthGrid(month, first, FixedWeeks);
        var columns = ShowWeekNumbers ? 8 : 7;
        // 列宽、行高按内容（表头行比日期行矮、周数列比日期列窄，同 TS 的 table）
        var grid = new CalendarDayGrid { Name = "PART_Grid" };
        grid.SetResourceReference(StyleProperty, CalendarViewParts.Grid);
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (var i = 0; i <= weeks.Count; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        void Add(UIElement child)
        {
            var index = grid.Children.Count;
            WpfGrid.SetRow(child, index / columns);
            WpfGrid.SetColumn(child, index % columns);
            grid.Children.Add(child);
        }
        AutomationProperties.SetName(grid, title);
        if (ShowWeekNumbers) Add(Part(new Text { Name = "PART_WeekNumber", Text = Lang.CalendarWeek, VerticalAlignment = VerticalAlignment.Center }, CalendarViewParts.WeekNumber));
        var names = CalendarText.WeekdayNames(culture, first);
        var full = CalendarText.WeekdayNames(culture, first, full: true);
        for (var i = 0; i < 7; i++)
        {
            var weekday = Part(new Text { Name = "PART_Weekday", Text = names[i], ToolTip = full[i], Margin = new Thickness(0, 0.25 * Em, 0, 0.25 * Em) }, CalendarViewParts.Weekday);
            Add(weekday);
        }
        foreach (var week in weeks)
        {
            if (ShowWeekNumbers)
            {
                var number = CalendarState.WeekOfYear(week[0], first);
                var label = Part(new WeekNumberText { Name = "PART_WeekNumber", Text = number.ToString(CultureInfo.InvariantCulture), VerticalAlignment = VerticalAlignment.Center }, CalendarViewParts.WeekNumber);
                AutomationProperties.SetName(label, Lang.CalendarWeekNumber(number));
                Add(label);
            }
            for (var i = 0; i < 7; i++)
            {
                var date = week[i];
                // 并排几个月时补齐的日期留空：同一天不在两张网格里重复出现
                if (date.Month != month.Month && !single)
                {
                    Add(new Border { Width = CellSide, Height = CellSide });
                    continue;
                }
                Add(Cell(state, date, month, CalendarText.FullDate(date, culture), CalendarText.DayNumber(date), i == 0, i == 6));
            }
        }
        grid.MouseLeave += (_, _) =>
        {
            hovered = null;
            state.Hover(null);
            if (state.Anchor is not null) Rebuild();
        };
        return grid;
    }

    static T Part<T>(T element, PartKey key) where T : FrameworkElement
    {
        element.SetResourceReference(StyleProperty, key);
        return element;
    }

    Panel PeriodGrid(CalendarState state, string title)
    {
        var culture = Culture;
        var month = state.View == DateUnit.Month;
        var items = month ? CalendarState.MonthsOfYear(state.Page) : CalendarState.YearsOfDecade(state.Page);
        var grid = new CalendarPeriodGrid { Name = "PART_Grid", Columns = 3, Width = 7 * CellSide };
        grid.SetResourceReference(StyleProperty, CalendarViewParts.PeriodGrid);
        AutomationProperties.SetName(grid, title);
        for (var i = 0; i < items.Count; i++)
        {
            var date = items[i];
            var label = month ? CalendarText.YearMonth(date, culture) : CalendarText.Year(date.Year, culture, Lang);
            var text = month ? CalendarText.MonthShort(date, culture) : CalendarText.Year(date.Year, culture, Lang);
            grid.Children.Add(Cell(state, date, null, label, text, i % 3 == 0, i % 3 == 2));
        }
        return grid;
    }

    /// <summary>一格：外层是画区间那一条的 <see cref="CalendarCell"/>，里面是按钮；状态由 Sync 给。month 为 null 时是月份、年份格。</summary>
    CalendarCell Cell(CalendarState state, DateOnly date, DateOnly? month, string label, string text, bool rowStart, bool rowEnd)
    {
        var period = month is null;
        var button = new CalendarCellButton(date, period, text)
        {
            Name = "PART_CellButton",
            HorizontalAlignment = period ? HorizontalAlignment.Stretch : HorizontalAlignment.Center,
        };
        button.SetResourceReference(StyleProperty, CalendarViewParts.CellButton);
        button.Click += (_, _) =>
        {
            if (!IsEnabled || state.Cell(date, month).Disabled) return;
            state.Activate(date);
            Rebuild(focus: true);
        };
        // 点击、程序聚焦到别的格子时，键盘从这一格接着走
        button.GotKeyboardFocus += (_, _) =>
        {
            if (state.Cell(date, month).Focused) return;
            state.Focus(date);
        };
        button.MouseEnter += (_, _) =>
        {
            if (state.Anchor is null || hovered == date) return;
            hovered = date;
            state.Hover(date);
            Rebuild();
        };
        var holder = new CalendarCell { Child = button };
        holder.SetResourceReference(StyleProperty, CalendarViewParts.Cell);
        entries.Add(new CellEntry(holder, button, month, label, rowStart, rowEnd));
        return holder;
    }

    /// <summary>自定义内容里读屏要念的文字：字符串本身、元素的读屏名称（数字不想重复念时给外层写名称），否则是里面的文字。</summary>
    static string SpokenText(object? content) => content switch
    {
        string text => text,
        FrameworkElement element when AutomationProperties.GetName(element) is { Length: > 0 } name => name,
        TextBlock block => block.Text ?? "",
        ContentControl holder => SpokenText(holder.Content),
        Decorator decorator => SpokenText(decorator.Child),
        Panel panel => string.Concat(panel.Children.OfType<object>().Select(SpokenText)),
        _ => "",
    };

    void OnGridKeyDown(object sender, KeyEventArgs e)
    {
        if (State is not { } state || !IsEnabled || e.OriginalSource is not CalendarCellButton) return;
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0) return;
        CalendarKey? key = e.Key switch
        {
            Key.Left => CalendarKey.Left,
            Key.Right => CalendarKey.Right,
            Key.Up => CalendarKey.Up,
            Key.Down => CalendarKey.Down,
            Key.PageUp => CalendarKey.PageUp,
            Key.PageDown => CalendarKey.PageDown,
            Key.Home => CalendarKey.Home,
            Key.End => CalendarKey.End,
            Key.Escape => CalendarKey.Escape,
            _ => null,
        };
        if (key is not { } pressed || !state.HandleKey(pressed, (Keyboard.Modifiers & ModifierKeys.Shift) != 0, FlowDirection == FlowDirection.RightToLeft)) return;
        e.Handled = true;
        Rebuild(focus: true);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    sealed class GroupPeer(CalendarView owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(CalendarView);
    }
}

/// <summary>日期网格：读屏的表格，名称是这个月（WPF 的 Panel 本来没有读屏节点）。</summary>
sealed class CalendarDayGrid : WpfGrid
{
    protected override AutomationPeer OnCreateAutomationPeer() => new CalendarGridPeer(this);
}

/// <summary>月份、年份网格：读屏的表格，名称是这一年、这十年。</summary>
sealed class CalendarPeriodGrid : UniformGrid
{
    protected override AutomationPeer OnCreateAutomationPeer() => new CalendarGridPeer(this);
}

sealed class CalendarGridPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

    protected override string GetClassNameCore() => "CalendarGrid";
}

/// <summary>周数：看到的是数字，读屏念"第 40 周"（TS 的行表头 aria-label；TextBlock 的读屏名称默认是它的文字）。</summary>
sealed class WeekNumberText : Text
{
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    sealed class Peer(WeekNumberText owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore() => AutomationProperties.GetName(Owner) is { Length: > 0 } name ? name : owner.Text;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.HeaderItem;
    }
}

/// <summary>日历的一格（<see cref="CalendarView"/> 建的）：外层的 Border，区间那一条画在它上面（<see cref="IsInRange"/>）。</summary>
public sealed class CalendarCell : Border
{
    static readonly DependencyPropertyKey IsInRangeKey = DependencyProperty.RegisterReadOnly(nameof(IsInRange), typeof(bool), typeof(CalendarCell), new PropertyMetadata(false));

    /// <summary>在选中的区间里（含两端）。</summary>
    public static readonly DependencyProperty IsInRangeProperty = IsInRangeKey.DependencyProperty;

    /// <summary>在选中的区间里。</summary>
    public bool IsInRange { get => (bool)GetValue(IsInRangeProperty); internal set => SetValue(IsInRangeKey, value); }
}

/// <summary>
/// 日历格子里的按钮（<see cref="CalendarView"/> 建的）：日期、月份或年份。状态是只读的依赖属性，部件样式按它们写触发器：
/// <see cref="IsMarked"/>（已选或区间两端）、<see cref="IsMiddle"/>（区间中间）、<see cref="IsToday"/>、<see cref="IsOutside"/>（补齐的上下月）、
/// <see cref="IsUnavailable"/>（划掉）、<see cref="IsOff"/>（不能选）、<see cref="IsPeriod"/>（月份、年份格）。
/// </summary>
public sealed class CalendarCellButton : ButtonBase
{
    static readonly DependencyPropertyKey IsMarkedKey = ReadOnly(nameof(IsMarked));
    static readonly DependencyPropertyKey IsMiddleKey = ReadOnly(nameof(IsMiddle));
    static readonly DependencyPropertyKey IsTodayKey = ReadOnly(nameof(IsToday));
    static readonly DependencyPropertyKey IsOutsideKey = ReadOnly(nameof(IsOutside));
    static readonly DependencyPropertyKey IsUnavailableKey = ReadOnly(nameof(IsUnavailable));
    static readonly DependencyPropertyKey IsOffKey = ReadOnly(nameof(IsOff));
    static readonly DependencyPropertyKey IsPeriodKey = ReadOnly(nameof(IsPeriod));

    /// <summary>已选（区间时是两端）。</summary>
    public static readonly DependencyProperty IsMarkedProperty = IsMarkedKey.DependencyProperty;

    /// <summary>区间中间的格。</summary>
    public static readonly DependencyProperty IsMiddleProperty = IsMiddleKey.DependencyProperty;

    /// <summary>今天。</summary>
    public static readonly DependencyProperty IsTodayProperty = IsTodayKey.DependencyProperty;

    /// <summary>补齐的上下月。</summary>
    public static readonly DependencyProperty IsOutsideProperty = IsOutsideKey.DependencyProperty;

    /// <summary>不可选的日期（划掉）。</summary>
    public static readonly DependencyProperty IsUnavailableProperty = IsUnavailableKey.DependencyProperty;

    /// <summary>不能选（超出范围、日历不可用）。</summary>
    public static readonly DependencyProperty IsOffProperty = IsOffKey.DependencyProperty;

    /// <summary>月份、年份格。</summary>
    public static readonly DependencyProperty IsPeriodProperty = IsPeriodKey.DependencyProperty;

    /// <summary>格子的边长（em 系数，不小于 24px）：日期格默认 2.25，月份、年份格是高（宽度占满一列，默认 3）。放得下价格这类内容时在部件样式里调大。</summary>
    public static readonly DependencyProperty SizeEmProperty = DependencyProperty.Register(
        nameof(SizeEm), typeof(double), typeof(CalendarCellButton), new FrameworkPropertyMetadata(2.25, FrameworkPropertyMetadataOptions.AffectsMeasure));

    static DependencyPropertyKey ReadOnly(string name) => DependencyProperty.RegisterReadOnly(name, typeof(bool), typeof(CalendarCellButton), new PropertyMetadata(false));

    internal CalendarCellButton(DateOnly date, bool period, string text)
    {
        Date = date;
        Text = text;
        SetValue(IsPeriodKey, period);
    }

    /// <summary>这一格的日期（月份、年份格是那个月、那年的第一天）。</summary>
    public DateOnly Date { get; }

    /// <summary>这一格的状态。</summary>
    public CalendarCellState State { get; private set; } = null!;

    /// <summary>格子里默认的文字（日期数字、月份名、年份）。</summary>
    internal string Text { get; }

    /// <summary>换成新的状态（CalendarView 刷新时）。</summary>
    internal void Apply(CalendarCellState state, bool marked, bool middle, bool off)
    {
        State = state;
        SetValue(IsTodayKey, state.Today);
        SetValue(IsOutsideKey, state.Outside);
        SetValue(IsUnavailableKey, state.Unavailable);
        SetValue(IsMarkedKey, marked);
        SetValue(IsMiddleKey, middle);
        SetValue(IsOffKey, off);
    }

    /// <summary>已选。</summary>
    public bool IsMarked => (bool)GetValue(IsMarkedProperty);

    /// <summary>区间中间。</summary>
    public bool IsMiddle => (bool)GetValue(IsMiddleProperty);

    /// <summary>今天。</summary>
    public bool IsToday => (bool)GetValue(IsTodayProperty);

    /// <summary>补齐的上下月。</summary>
    public bool IsOutside => (bool)GetValue(IsOutsideProperty);

    /// <summary>不可选的日期。</summary>
    public bool IsUnavailable => (bool)GetValue(IsUnavailableProperty);

    /// <summary>不能选。</summary>
    public bool IsOff => (bool)GetValue(IsOffProperty);

    /// <summary>月份、年份格。</summary>
    public bool IsPeriod => (bool)GetValue(IsPeriodProperty);

    /// <summary>格子的边长（em 系数）。</summary>
    public double SizeEm { get => (double)GetValue(SizeEmProperty); set => SetValue(SizeEmProperty, value); }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        var side = Math.Max(SizeEm * FontSize, ControlHeight.MinTarget);
        var desired = base.MeasureOverride(constraint);
        return IsPeriod ? new Size(desired.Width, Math.Max(desired.Height, side)) : new Size(Math.Max(desired.Width, side), Math.Max(desired.Height, side));
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CellPeer(this);

    /// <summary>读屏：格子（不能选时念"不可用"，仍能用方向键经过）。</summary>
    sealed class CellPeer(CalendarCellButton owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;

        protected override bool IsEnabledCore() => !owner.IsOff && base.IsEnabledCore();
    }
}

/// <summary>CalendarView 的部件样式键（TS 的部件）。外层（TS 的 root）就是日历自己。</summary>
public static class CalendarViewParts
{
    /// <summary>并排的月份（TS 的 propsMonths）。</summary>
    public static readonly PartKey Months = new(typeof(CalendarView), nameof(Months), typeof(Flex));

    /// <summary>页头（TS 的 propsHeader）：三列的 Grid。</summary>
    public static readonly PartKey Header = new(typeof(CalendarView), nameof(Header), typeof(WpfGrid));

    /// <summary>上一页（TS 的 propsPrevious）。</summary>
    public static readonly PartKey Previous = new(typeof(CalendarView), nameof(Previous), typeof(IconButton));

    /// <summary>下一页（TS 的 propsNext）。</summary>
    public static readonly PartKey Next = new(typeof(CalendarView), nameof(Next), typeof(IconButton));

    /// <summary>标题按钮（TS 的 propsHeading）；最粗的视图时标题是文字。</summary>
    public static readonly PartKey Heading = new(typeof(CalendarView), nameof(Heading), typeof(ButtonBase));

    /// <summary>日期网格（TS 的 propsGrid）：Grid，列宽、行高按内容。</summary>
    public static readonly PartKey Grid = new(typeof(CalendarView), nameof(Grid), typeof(WpfGrid));

    /// <summary>月份、年份网格：三列的 UniformGrid，与日期网格同宽。</summary>
    public static readonly PartKey PeriodGrid = new(typeof(CalendarView), nameof(PeriodGrid), typeof(UniformGrid));

    /// <summary>星期表头（TS 的 propsWeekday）。</summary>
    public static readonly PartKey Weekday = new(typeof(CalendarView), nameof(Weekday), typeof(Text));

    /// <summary>周数（TS 的 propsWeekNumber）。</summary>
    public static readonly PartKey WeekNumber = new(typeof(CalendarView), nameof(WeekNumber), typeof(Text));

    /// <summary>一格（TS 的 propsCell）：区间那一条画在它上面。</summary>
    public static readonly PartKey Cell = new(typeof(CalendarView), nameof(Cell), typeof(CalendarCell));

    /// <summary>格子里的按钮（TS 的 propsCellButton）。</summary>
    public static readonly PartKey CellButton = new(typeof(CalendarView), nameof(CellButton), typeof(CalendarCellButton));

    /// <summary>页脚（TS 的 propsFooter）。</summary>
    public static readonly PartKey Footer = new(typeof(CalendarView), nameof(Footer), typeof(ContentControl));
}
