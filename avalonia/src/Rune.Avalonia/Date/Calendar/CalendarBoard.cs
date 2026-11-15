using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaGrid = Avalonia.Controls.Grid;

namespace Rune.Avalonia;

/// <summary>一天的状态（<see cref="CalendarBoard.DayContent"/> 用）。</summary>
/// <param name="Today">今天。</param>
/// <param name="Outside">补齐的上个月、下个月的日子。</param>
/// <param name="Selected">选中的那一天。</param>
/// <param name="Weekend">周六、周日。</param>
/// <param name="Disabled">不可选（范围外、不可选的日期）。</param>
public readonly record struct CalendarBoardDayState(bool Today, bool Outside, bool Selected, bool Weekend, bool Disabled);

/// <summary>
/// 整页的月历（vue-tsx-pro 的 CalendarBoard，日历看板）：一个月铺满一页，每天一个大格子，格子里放日程、排班、节假日这类按天的内容。
/// 参考 Ant Design Calendar 的 fullscreen、Google 日历的月视图。要在输入框里选日期用 DatePicker，小日历用 Calendar。
/// <code>
/// &lt;rune:CalendarBoard ItemsSource="{Binding Meetings}" GetEventDate="{Binding DateOf}" GetEventLabel="{Binding TitleOf}"
///                     Value="{Binding Day}" Month="{Binding Month}" AutomationProperties.Name="会议日程" /&gt;
/// </code>
/// <list type="bullet">
/// <item>数据按天放进格子：GetEventDate 是哪一天，GetEventEnd 给了时跨天的每一天都有它；每天最多显示 MaxEvents 条，其余收成"还有 N 项"
/// （点了选中那一天，由页面在旁边列出全部）。EventTemplate 自定义一条的显示（默认是带色调的一行文字），DayContent 往格子里加别的内容（排班、价格），
/// GetDayNote 是日期旁边的小字（节假日、"休""班"、农历）。</item>
/// <item>键盘与 Calendar 一致（同一个 <see cref="CalendarState"/>）：每天的日期数字是一个按钮，只有当前日期在 Tab 顺序里，
/// ←→ 一天、↑↓ 一周、Page Up / Down 一个月、Home / End 到这周两端，Enter / 空格选中；读屏念完整日期与有几项日程。格子里的日程可以是按钮，Tab 照常进去。</item>
/// <item>页头：标题（翻页时播报）、Toolbar（筛选、视图切换）、"今天"、上个月 / 下个月；Value、Month 默认双向绑定（翻页写回 Month）。</item>
/// </list>
/// 部件：<see cref="CalendarBoardParts"/>，默认外观在 CalendarBoard.axaml。
/// </summary>
[PseudoClasses(":readonly")]
public class CalendarBoard : Control
{
    /// <summary>选中的那一天（ISO 日期，默认双向绑定）；没有时 null。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<CalendarBoard, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>显示哪个月（yyyy-MM，默认双向绑定，翻页时写回）；不写时从选中的那天（或今天）开始。</summary>
    public static readonly StyledProperty<string?> MonthProperty = AvaloniaProperty.Register<CalendarBoard, string?>(nameof(Month), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>一周的第一天；不写时按语言包、再按区域惯例。</summary>
    public static readonly StyledProperty<DayOfWeek?> FirstDayOfWeekProperty = AvaloniaProperty.Register<CalendarBoard, DayOfWeek?>(nameof(FirstDayOfWeek));

    /// <summary>最早能选的日期（ISO）。</summary>
    public static readonly StyledProperty<string?> MinProperty = AvaloniaProperty.Register<CalendarBoard, string?>(nameof(Min));

    /// <summary>最晚能选的日期（ISO）。</summary>
    public static readonly StyledProperty<string?> MaxProperty = AvaloniaProperty.Register<CalendarBoard, string?>(nameof(Max));

    /// <summary>不可选的日期。</summary>
    public static readonly StyledProperty<Func<DateOnly, bool>?> IsDateUnavailableProperty = AvaloniaProperty.Register<CalendarBoard, Func<DateOnly, bool>?>(nameof(IsDateUnavailable));

    /// <summary>按天放的数据（日程、排班、提醒）；ObservableCollection 增删时跟着。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<CalendarBoard, IEnumerable?>(nameof(ItemsSource));

    /// <summary>数据在哪一天（带时间的取日期）。</summary>
    public static readonly StyledProperty<Func<object?, DateOnly>?> GetEventDateProperty = AvaloniaProperty.Register<CalendarBoard, Func<object?, DateOnly>?>(nameof(GetEventDate));

    /// <summary>跨天的数据在哪一天结束（含）；不写时只在 GetEventDate 那一天。</summary>
    public static readonly StyledProperty<Func<object?, DateOnly>?> GetEventEndProperty = AvaloniaProperty.Register<CalendarBoard, Func<object?, DateOnly>?>(nameof(GetEventEnd));

    /// <summary>默认显示的文字；不写时是数据的 ToString()。</summary>
    public static readonly StyledProperty<Func<object?, string>?> GetEventLabelProperty = AvaloniaProperty.Register<CalendarBoard, Func<object?, string>?>(nameof(GetEventLabel));

    /// <summary>默认显示的色调，默认 accent。</summary>
    public static readonly StyledProperty<Func<object?, StatusTone>?> GetEventToneProperty = AvaloniaProperty.Register<CalendarBoard, Func<object?, StatusTone>?>(nameof(GetEventTone));

    /// <summary>自定义一条数据的显示（数据是 DataContext）；不写时是带色调的一行文字。</summary>
    public static readonly StyledProperty<IDataTemplate?> EventTemplateProperty = AvaloniaProperty.Register<CalendarBoard, IDataTemplate?>(nameof(EventTemplate));

    /// <summary>每天最多显示几条，其余收成"还有 N 项"，默认 3。</summary>
    public static readonly StyledProperty<int> MaxEventsProperty = AvaloniaProperty.Register<CalendarBoard, int>(nameof(MaxEvents), 3, validate: count => count >= 0);

    /// <summary>日期旁边的小字（节假日、"休""班"、农历）；返回 null 时没有。</summary>
    public static readonly StyledProperty<Func<DateOnly, string?>?> GetDayNoteProperty = AvaloniaProperty.Register<CalendarBoard, Func<DateOnly, string?>?>(nameof(GetDayNote));

    /// <summary>往格子里加别的内容（排班、价格），放在日程下面：拿到日期与这一天的状态，返回文字或控件（null 时没有）。</summary>
    public static readonly StyledProperty<Func<DateOnly, CalendarBoardDayState, object?>?> DayContentProperty =
        AvaloniaProperty.Register<CalendarBoard, Func<DateOnly, CalendarBoardDayState, object?>?>(nameof(DayContent));

    /// <summary>页头右边的内容（筛选、视图切换）。</summary>
    public static readonly StyledProperty<object?> ToolbarProperty = AvaloniaProperty.Register<CalendarBoard, object?>(nameof(Toolbar));

    /// <summary>只读：能翻页、移动焦点，不能选。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<CalendarBoard, bool>(nameof(IsReadOnly));

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Sm };
    readonly Flex header = new() { Name = CalendarBoardParts.Header, Align = FlexAlign.Center, Wrap = true, Gap = Space.Sm };
    readonly Text title = new() { Name = CalendarBoardParts.Title };
    readonly ContentControl toolbar = new() { IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
    readonly Button today = new() { Name = CalendarBoardParts.Today, Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral };
    readonly IconButton previous = new() { Name = CalendarBoardParts.Previous, Icon = Lucide.ChevronLeft, Size = TextSize.Sm };
    readonly IconButton next = new() { Name = CalendarBoardParts.Next, Icon = Lucide.ChevronRight, Size = TextSize.Sm };
    readonly AvaloniaGrid grid = new() { Name = CalendarBoardParts.Grid };
    readonly LiveRegion live = new() { Height = 0 };
    readonly List<DayEntry> days = [];
    INotifyCollectionChanged? watched;
    string? structure;
    string? announced;
    bool writingMonth;

    /// <summary>建好的一天：重画时按状态原地刷新（选中、焦点变了不重建，键盘焦点不丢）。</summary>
    sealed record DayEntry(DateOnly Date, Border Cell, CalendarBoardDayButton Button, string Label, int Count);

    /// <summary>新建：页头与月历表格；状态（选中、焦点、翻页、键盘）同 Calendar。</summary>
    public CalendarBoard()
    {
        State = new CalendarState
        {
            Mode = () => CalendarSelectionMode.Single,
            Selected = () => Day(Value) is { } day ? [day] : [],
            Changed = picked => SetCurrentValue(ValueProperty, picked.Count > 0 ? Iso(picked[0]) : null),
            Limits = () => new DateLimits(Day(Min), Day(Max), IsDateUnavailable),
            FirstDayOfWeek = () => FirstDay,
            ReadOnly = () => IsReadOnly,
            DefaultFocused = () => Month is { } month ? Day($"{month}-01") : null,
        };
        title.Classes.Add("title");
        Flex.SetGrow(title, true);
        header.Children.AddRange([title, toolbar, today, previous, next]);
        today.Click += (_, _) =>
        {
            State.Focus(State.Today());
            Rebuild(focus: true);
        };
        previous.Click += (_, _) => TurnPage(-1);
        next.Click += (_, _) => TurnPage(1);
        grid.AddHandler(KeyDownEvent, OnGridKeyDown);
        root.Children.AddRange([header, grid, live]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>选中的那一天。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>显示哪个月。</summary>
    public string? Month { get => GetValue(MonthProperty); set => SetValue(MonthProperty, value); }

    /// <summary>一周的第一天。</summary>
    public DayOfWeek? FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <summary>最早能选的日期。</summary>
    public string? Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最晚能选的日期。</summary>
    public string? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>不可选的日期。</summary>
    public Func<DateOnly, bool>? IsDateUnavailable { get => GetValue(IsDateUnavailableProperty); set => SetValue(IsDateUnavailableProperty, value); }

    /// <summary>按天放的数据。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据在哪一天。</summary>
    public Func<object?, DateOnly>? GetEventDate { get => GetValue(GetEventDateProperty); set => SetValue(GetEventDateProperty, value); }

    /// <summary>跨天的数据在哪一天结束。</summary>
    public Func<object?, DateOnly>? GetEventEnd { get => GetValue(GetEventEndProperty); set => SetValue(GetEventEndProperty, value); }

    /// <summary>默认显示的文字。</summary>
    public Func<object?, string>? GetEventLabel { get => GetValue(GetEventLabelProperty); set => SetValue(GetEventLabelProperty, value); }

    /// <summary>默认显示的色调。</summary>
    public Func<object?, StatusTone>? GetEventTone { get => GetValue(GetEventToneProperty); set => SetValue(GetEventToneProperty, value); }

    /// <summary>自定义一条数据的显示。</summary>
    public IDataTemplate? EventTemplate { get => GetValue(EventTemplateProperty); set => SetValue(EventTemplateProperty, value); }

    /// <summary>每天最多显示几条。</summary>
    public int MaxEvents { get => GetValue(MaxEventsProperty); set => SetValue(MaxEventsProperty, value); }

    /// <summary>日期旁边的小字。</summary>
    public Func<DateOnly, string?>? GetDayNote { get => GetValue(GetDayNoteProperty); set => SetValue(GetDayNoteProperty, value); }

    /// <summary>往格子里加的内容。</summary>
    public Func<DateOnly, CalendarBoardDayState, object?>? DayContent { get => GetValue(DayContentProperty); set => SetValue(DayContentProperty, value); }

    /// <summary>页头右边的内容。</summary>
    public object? Toolbar { get => GetValue(ToolbarProperty); set => SetValue(ToolbarProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>选中的那一天（DateOnly）；没有时 null。</summary>
    public DateOnly? Date => Day(Value);

    /// <summary>日历状态（选中、焦点、翻页、键盘）。</summary>
    internal CalendarState State { get; }

    /// <summary>显示着的每一天的格子（测试用）。</summary>
    internal IEnumerable<(DateOnly Date, Border Cell, CalendarBoardDayButton Button)> Days => days.Select(day => (day.Date, day.Cell, day.Button));

    Lang Lang => Provider.GetLang(this);

    CultureInfo Culture => CalendarText.CultureOf(Lang);

    DayOfWeek FirstDay => CalendarText.FirstDayOf(Lang, FirstDayOfWeek);

    double Em => global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);

    /// <summary>间距档位（em 系数）按看板的字号换成像素。</summary>
    double Gap(string key) => this.Resource<double>(key) * Em;

    static DateOnly? Day(string? iso) => iso is not null && DateOnly.TryParseExact(iso.Length > 10 ? iso[..10] : iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            State.SelectionChanged();
            Rebuild();
        }
        else if (change.Property == MonthProperty)
        {
            // 父级改了显示的月：翻过去（自己翻页写回时不动）。进界面前不碰状态：一开始的页由选中的那天、再由 Month 定（同 Calendar）
            if (!writingMonth && VisualRoot is not null && Day($"{Month}-01") is { } target && (target.Year, target.Month) != (State.Page.Year, State.Page.Month)) State.Focus(target);
            Rebuild();
        }
        else if (change.Property == ItemsSourceProperty)
        {
            if (watched is not null) watched.CollectionChanged -= OnItemsChanged;
            watched = ItemsSource as INotifyCollectionChanged;
            if (watched is not null) watched.CollectionChanged += OnItemsChanged;
            Restructure();
        }
        else if (change.Property == ToolbarProperty)
        {
            toolbar.Content = Toolbar;
            toolbar.IsVisible = Toolbar is not null;
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            PseudoClasses.Set(":readonly", IsReadOnly);
            Rebuild();
        }
        else if (change.Property == FirstDayOfWeekProperty || change.Property == MinProperty || change.Property == MaxProperty || change.Property == IsDateUnavailableProperty
                 || change.Property == GetEventDateProperty || change.Property == GetEventEndProperty || change.Property == GetEventLabelProperty
                 || change.Property == GetEventToneProperty || change.Property == EventTemplateProperty || change.Property == MaxEventsProperty
                 || change.Property == GetDayNoteProperty || change.Property == DayContentProperty || change.Property == Provider.LangProperty
                 || change.Property == global::Avalonia.Controls.Documents.TextElement.FontSizeProperty)
            Restructure();
    }

    void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Restructure();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    /// <summary>数据、内容、选项变了：格子重建。</summary>
    void Restructure()
    {
        structure = null;
        Rebuild();
    }

    void TurnPage(int direction)
    {
        State.TurnPage(direction);
        Rebuild();
    }

    /// <summary>页面变了重建表格，否则按状态刷新格子；翻页写回 Month；focus 时焦点交给当前日期。</summary>
    /// <param name="focus">把键盘焦点移到当前日期。</param>
    void Rebuild(bool focus = false)
    {
        // 没进界面前不画：今天、语言要等进了界面才定
        if (VisualRoot is null) return;
        var page = State.Page;
        var month = page.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        if (Month is not null && Month != month)
        {
            writingMonth = true;
            try
            {
                SetCurrentValue(MonthProperty, month);
            }
            finally
            {
                writingMonth = false;
            }
        }
        var lang = Lang;
        var key = string.Join("|", month, FirstDay, lang.Code, Em, IsReadOnly);
        if (key != structure)
        {
            structure = key;
            Build(page, lang);
        }
        Sync(lang);
        if (focus) days.FirstOrDefault(day => day.Button.IsTabStop)?.Button.Focus(NavigationMethod.Directional);
    }

    /// <summary>按天分好的数据：日期 → 那天的数据（跨天的每一天都有）。</summary>
    Dictionary<DateOnly, List<object?>> ByDay()
    {
        var map = new Dictionary<DateOnly, List<object?>>();
        if (ItemsSource is null || GetEventDate is not { } dateOf) return map;
        foreach (var item in ItemsSource)
        {
            var first = dateOf(item);
            var last = GetEventEnd?.Invoke(item) ?? first;
            // 跨天的最多铺 366 天：数据错了（结束早于开始、跨好几年）也不会卡住
            for (var (day, count) = (first, 0); count < 366; day = day.AddDays(1), count++)
            {
                if (!map.TryGetValue(day, out var list)) map[day] = list = [];
                list.Add(item);
                if (day >= last) break;
            }
        }
        return map;
    }

    void Build(DateOnly page, Lang lang)
    {
        var culture = Culture;
        var first = FirstDay;
        title.Text = CalendarText.YearMonth(page, culture);
        today.Content = CalendarText.PresetName(DateRangePreset.Today, lang);
        AutomationProperties.SetName(previous, lang.CalendarPreviousMonth);
        AutomationProperties.SetName(next, lang.CalendarNextMonth);
        AutomationProperties.SetName(grid, title.Text);
        // 翻页时播报新的月份
        if (announced is not null && announced != title.Text) live.Announce(title.Text);
        announced = title.Text;

        var weeks = CalendarState.MonthGrid(page, first, fixedWeeks: false);
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        days.Clear();
        // 七列等宽（同 table-layout: fixed）；表头行按内容高，日期行至少 7em
        for (var i = 0; i < 7; i++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 0 });
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        foreach (var _ in weeks) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var names = CalendarText.WeekdayNames(culture, first);
        for (var i = 0; i < 7; i++)
        {
            var weekday = new Text { Name = CalendarBoardParts.Weekday, Text = names[i] };
            AvaloniaGrid.SetColumn(weekday, i);
            grid.Children.Add(weekday);
        }
        var byDay = ByDay();
        for (var row = 0; row < weeks.Count; row++)
            for (var column = 0; column < 7; column++)
            {
                var date = weeks[row][column];
                var cell = DayCell(date, page, byDay.TryGetValue(date, out var list) ? list : [], culture, lang, lastRow: row == weeks.Count - 1, lastColumn: column == 6);
                AvaloniaGrid.SetRow(cell, row + 1);
                AvaloniaGrid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }
    }

    /// <summary>一天的格子：日期数字与小字一行，下面是日程（多的收成"还有 N 项"），再下面是 DayContent。状态由 Sync 给。</summary>
    Border DayCell(DateOnly date, DateOnly page, List<object?> list, CultureInfo culture, Lang lang, bool lastRow, bool lastColumn)
    {
        var state = State.Cell(date, page);
        var weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var button = new CalendarBoardDayButton(date) { Name = CalendarBoardParts.DayButton, Content = CalendarText.DayNumber(date) };
        button.Click += (_, e) =>
        {
            e.Handled = true;
            if (State.Cell(date, page).Disabled) return;
            State.Activate(date);
            Rebuild(focus: true);
        };
        // 点击、程序聚焦到别的日期时，键盘从这一天接着走
        button.GotFocus += (_, _) =>
        {
            if (!State.Cell(date, page).Focused) State.Focus(date);
        };
        var head = new Flex { Name = CalendarBoardParts.DayHead, Align = FlexAlign.Center, Gap = Space.Xs, Children = { button } };
        if (GetDayNote?.Invoke(date) is { Length: > 0 } note)
        {
            var noteText = new Text { Name = CalendarBoardParts.Note, Text = note, Truncate = true };
            Flex.SetShrink(noteText, true);
            head.Children.Add(noteText);
        }
        var body = new StackPanel { Children = { head } };
        var shown = list.Take(MaxEvents).ToList();
        if (shown.Count > 0)
        {
            var events = new StackPanel { Name = CalendarBoardParts.Events, Margin = new Thickness(0, Gap(ThemeKeys.Space.Xs)), Spacing = Gap(ThemeKeys.Space.Xxs) };
            foreach (var item in shown) events.Children.Add(EventView(item));
            if (list.Count > shown.Count)
            {
                var more = new ButtonBase { Name = CalendarBoardParts.More, Content = lang.CalendarBoardMore(list.Count - shown.Count), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(Gap(ThemeKeys.Space.Xs), 0) };
                more.Click += (_, e) =>
                {
                    e.Handled = true;
                    if (State.Cell(date, page).Disabled) return;
                    State.Activate(date);
                    Rebuild();
                };
                events.Children.Add(more);
            }
            body.Children.Add(events);
        }
        if (DayContent?.Invoke(date, new CalendarBoardDayState(state.Today, state.Outside, state.Selected, weekend, state.Disabled)) is { } extra)
            body.Children.Add(extra as Control ?? new Text { Text = extra.ToString() });
        // 表格线收拢：每格画右、下两边，最后一列、最后一行之外由表格的左、上边补齐
        // Border 没有字号：按看板的字号算 em（至少 7em 高、四周 xs；选中的内框伸到内边距外，贴着表格线）
        var xs = Gap(ThemeKeys.Space.Xs);
        var selection = new Border { Classes = { "selection" }, IsHitTestVisible = false, Margin = new Thickness(-xs) };
        var cell = new Border { Name = CalendarBoardParts.Cell, MinHeight = 7 * Em, Padding = new Thickness(xs), Child = new Panel { Children = { body, selection } } };
        cell.Classes.Set("weekend", weekend);
        cell.Classes.Set("outside", state.Outside);
        cell.Classes.Set("last-row", lastRow);
        cell.Classes.Set("last-column", lastColumn);
        // 点格子的空白处：选中那一天（点里面的按钮时照常）
        cell.PointerPressed += (_, e) =>
        {
            if (Interactive(e.Source as Visual)) return;
            if (State.Cell(date, page).Disabled) return;
            State.Activate(date);
            Rebuild();
        };
        days.Add(new DayEntry(date, cell, button, CalendarText.FullDate(date, culture), list.Count));
        return cell;
    }

    /// <summary>点在按钮、输入框这类自己处理点击的元素上（日程模板里的链接、按钮）。</summary>
    static bool Interactive(Visual? source)
    {
        for (var node = source; node is not null and not CalendarBoard; node = node.GetVisualParent())
            if (node is ButtonBase or global::Avalonia.Controls.Button or TextBox) return true;
        return false;
    }

    /// <summary>一条数据：EventTemplate 建的，或带色调的一行文字（色调的调色板绑在 Resolved.Palette 上，样式取 Soft 与 Text）。</summary>
    Control EventView(object? item)
    {
        if (EventTemplate is { } template && template.Build(item) is { } built)
        {
            built.DataContext = item;
            return built;
        }
        // 字号 xs、行高 1.6、左右 xs、sm 圆角（Border 没有字号，em 按 xs 字号与看板的字号在这里算）
        var size = this.Resource<double>(ThemeKeys.FontSize.Xs);
        var text = new Text { Text = GetEventLabel?.Invoke(item) ?? item?.ToString() ?? "", Truncate = true, LineHeight = 1.6 * size };
        var chip = new Border
        {
            Name = CalendarBoardParts.Event,
            DataContext = item,
            Child = text,
            Padding = new Thickness(Gap(ThemeKeys.Space.Xs), 0),
            CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * Em),
        };
        Resolved.Bind(chip, Resolved.PaletteProperty, (GetEventTone?.Invoke(item) is { Value: not null } tone ? tone : StatusTone.Accent).PaletteKey);
        return chip;
    }

    /// <summary>按状态刷新每一天：选中、今天、焦点、不能选，读屏名称（今天、几项日程）。</summary>
    void Sync(Lang lang)
    {
        var page = State.Page;
        previous.IsEnabled = State.CanTurnPage(-1);
        next.IsEnabled = State.CanTurnPage(1);
        foreach (var (date, cell, button, label, count) in days)
        {
            var state = State.Cell(date, page);
            cell.Classes.Set("selected", state.Selected);
            cell.Classes.Set("disabled", state.Disabled);
            button.Apply(state);
            button.IsTabStop = state.Focused && !state.Outside;
            var spoken = state.Today ? lang.CalendarToday(label) : label;
            AutomationProperties.SetName(button, count > 0 ? $"{spoken}，{lang.CalendarBoardCount(count)}" : spoken);
        }
    }

    void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        // 格子里的日程（按钮）自己处理按键
        if (e.Source is not CalendarBoardDayButton) return;
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return;
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
            _ => null,
        };
        if (key is not { } pressed || !State.HandleKey(pressed, (e.KeyModifiers & KeyModifiers.Shift) != 0, FlowDirection == FlowDirection.RightToLeft)) return;
        e.Handled = true;
        Rebuild(focus: true);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    sealed class GroupPeer(CalendarBoard owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

/// <summary>
/// 月历看板里的日期数字（<see cref="CalendarBoard"/> 建的）：可聚焦的圆形按钮。伪类 <c>:today</c>（accent 实底）、<c>:selected</c>、<c>:outside</c>、<c>:off</c>（不能选）。
/// </summary>
[PseudoClasses(":today", ":selected", ":outside", ":off")]
public sealed class CalendarBoardDayButton : ButtonBase
{
    internal CalendarBoardDayButton(DateOnly date) => Date = date;

    /// <summary>这一天。</summary>
    public DateOnly Date { get; }

    internal void Apply(CalendarCellState state)
    {
        PseudoClasses.Set(":today", state.Today);
        PseudoClasses.Set(":selected", state.Selected);
        PseudoClasses.Set(":outside", state.Outside);
        PseudoClasses.Set(":off", state.Disabled);
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ButtonBase);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new DayPeer(this);

    /// <summary>读屏：表格里的一格（选中状态在名称之外由 IsSelected 给）、不能选时不可用。</summary>
    sealed class DayPeer(CalendarBoardDayButton owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;

        protected override bool IsEnabledCore() => !owner.PseudoClasses.Contains(":off") && base.IsEnabledCore();
    }
}

/// <summary>CalendarBoard 的部件名（TS 的部件）。外层（TS 的 root）是看板自己。</summary>
public static class CalendarBoardParts
{
    /// <summary>页头（TS 的 propsHeader）：Flex，标题、工具栏、"今天"、翻页，隔 sm、放不下时换行。</summary>
    public const string Header = "PART_Header";

    /// <summary>标题（TS 的 propsTitle）：Text，lg、semibold，占满剩下的宽度。</summary>
    public const string Title = "PART_Title";

    /// <summary>"今天"（TS 的 propsToday）：Button，sm、soft、neutral。</summary>
    public const string Today = "PART_Today";

    /// <summary>上个月（TS 的 propsPrevious）：IconButton，sm。</summary>
    public const string Previous = "PART_Previous";

    /// <summary>下个月（TS 的 propsNext）：IconButton，sm。</summary>
    public const string Next = "PART_Next";

    /// <summary>月历表格（TS 的 propsGrid）：Grid，七列等宽，左、上两边的表格线。</summary>
    public const string Grid = "PART_Grid";

    /// <summary>星期表头（TS 的 propsWeekday）：Text，sm、textMuted，上下 xs、左右 sm。</summary>
    public const string Weekday = "PART_Weekday";

    /// <summary>一天（TS 的 propsCell）：Border，至少 7em 高、四周 xs、borderMuted 表格线；类 weekend（surfaceMuted 底）、outside（textMuted）、selected（accentSoft 底加 accentBorder 内框）、disabled（变淡）。</summary>
    public const string Cell = "PART_Cell";

    /// <summary>日期数字与小字那一行（TS 的 propsDayHead 部件）：Flex，隔 xs。</summary>
    public const string DayHead = "PART_DayHead";

    /// <summary>日期数字（TS 的 propsDayButton）：CalendarBoardDayButton，至少 1.75em 见方、圆形，今天 accent 底。</summary>
    public const string DayButton = "PART_DayButton";

    /// <summary>日期旁的小字（TS 的 propsNote）：Text，xs、textMuted，放不下时省略。</summary>
    public const string Note = "PART_Note";

    /// <summary>一天的日程（TS 的 propsEvents）：StackPanel，上下 xs、隔 xxs。</summary>
    public const string Events = "PART_Events";

    /// <summary>默认画法的一条日程（TS 的 propsEvent）：Border，色调的 soft 底与文字色、sm 圆角、左右 xs、xs 字号；DataContext 是那条数据。</summary>
    public const string Event = "PART_Event";

    /// <summary>"还有 N 项"（TS 的 propsMore）：ButtonBase，xs、textMuted，悬停 fillHover。</summary>
    public const string More = "PART_More";
}
