using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Rune.Avalonia;

/// <summary>
/// 表头筛选（vue-tsx-pro 的 ColumnFilterButton）：列标题上的漏斗按钮，弹出勾选的值（多于 8 个时可以搜索）或关键字输入框（停手 300 毫秒生效，回车立即生效）；改了立即发 <see cref="ValuesChanged"/>。
/// 有筛选时按钮是强调色，名称说明"已筛选"。DataTable 在列定义写了 Filter 时把它放进列标题；状态在表格（Filters）里，这里只管画与报告。
/// </summary>
public class ColumnFilterButton : Control
{
    /// <summary>选项多于这么多时，筛选里多一个搜索框。</summary>
    const int SearchThreshold = 8;

    /// <summary>列标题的文字：按钮的名称。</summary>
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<ColumnFilterButton, string>(nameof(Label), "");

    /// <summary>可以勾选的值；<see cref="IsKeyword"/> 时不用。</summary>
    public static readonly StyledProperty<IReadOnlyList<ColumnFilterOption>> OptionsProperty = AvaloniaProperty.Register<ColumnFilterButton, IReadOnlyList<ColumnFilterOption>>(nameof(Options), []);

    /// <summary>关键字筛选（上游 options: 'text'）。</summary>
    public static readonly StyledProperty<bool> IsKeywordProperty = AvaloniaProperty.Register<ColumnFilterButton, bool>(nameof(IsKeyword));

    /// <summary>选中的值（关键字筛选时是 [关键字]）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> ValuesProperty = AvaloniaProperty.Register<ColumnFilterButton, IReadOnlyList<string>>(nameof(Values), []);

    readonly IconButton trigger = new() { Name = ColumnHeaderActionsParts.Trigger, Icon = Lucide.Funnel, Size = TextSize.Xs, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };
    readonly Popover popover = new() { Placement = FloatingPlacement.BottomEnd };
    readonly Flex panel = new() { Name = ColumnHeaderActionsParts.Panel, Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, MinWidth = 12 * 16 };
    readonly SearchField search = new() { Size = TextSize.Sm };
    readonly SearchField keyword = new() { Size = TextSize.Sm };
    readonly Flex options = new() { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch };
    readonly ScrollArea scroll = new() { MaxHeight = 16 * 16 };
    readonly Button clear = new() { Name = ColumnHeaderActionsParts.Clear, Size = TextSize.Sm, Variant = ButtonVariant.Ghost };
    readonly List<Checkbox> boxes = [];

    /// <summary>新建：漏斗按钮与弹出的面板。</summary>
    public ColumnFilterButton()
    {
        popover.Trigger = trigger;
        popover.Content = panel;
        scroll.Content = options;
        panel.Children.AddRange([search, keyword, scroll, new Flex { Justify = FlexJustify.End, Children = { clear } }]);
        search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) Filter();
        };
        // 关键字：输入框里的是草稿，停手一会儿才生效（服务端筛选时不是每敲一个字发一次请求）
        keyword.SearchRequested += (_, text) => Change(text.Length > 0 ? [text] : []);
        clear.Click += (_, _) => Change([]);
        LogicalChildren.Add(popover);
        VisualChildren.Add(popover);
    }

    /// <summary>改了筛选：新的选中值。</summary>
    public event EventHandler<IReadOnlyList<string>>? ValuesChanged;

    /// <summary>列标题的文字。</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>可以勾选的值。</summary>
    public IReadOnlyList<ColumnFilterOption> Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }

    /// <summary>关键字筛选。</summary>
    public bool IsKeyword { get => GetValue(IsKeywordProperty); set => SetValue(IsKeywordProperty, value); }

    /// <summary>选中的值。</summary>
    public IReadOnlyList<string> Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>漏斗按钮（测试用）。</summary>
    internal IconButton Trigger => trigger;

    /// <summary>勾选框（测试用）：每个选项一个。</summary>
    internal IReadOnlyList<Checkbox> Boxes => boxes;

    /// <summary>关键字输入框（测试用）。</summary>
    internal SearchField Keyword => keyword;

    /// <summary>"清除"按钮（测试用）。</summary>
    internal Button ClearButton => clear;

    void Change(IReadOnlyList<string> values)
    {
        SetCurrentValue(ValuesProperty, values);
        ValuesChanged?.Invoke(this, values);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OptionsProperty || change.Property == IsKeywordProperty || change.Property == Provider.LangProperty) Build();
        else if (change.Property == ValuesProperty || change.Property == LabelProperty) Sync();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Build();
    }

    void Build()
    {
        var lang = Provider.GetLang(this);
        // 比正文小一档，不撑高表头（同上游 trigger 的 marginBlock: -0.25em）
        trigger.Margin = new Thickness(0, -0.25 * TextElement.GetFontSize(this));
        boxes.Clear();
        options.Children.Clear();
        foreach (var option in Options)
        {
            var box = new Checkbox { Name = ColumnHeaderActionsParts.Option, Content = option.Label ?? option.Value, Tag = option };
            box.Toggling += (_, e) =>
            {
                e.Cancel = true;
                Change(Values.Contains(option.Value) ? [.. Values.Where(value => value != option.Value)] : [.. Values, option.Value]);
            };
            boxes.Add(box);
            options.Children.Add(box);
        }
        search.IsVisible = !IsKeyword && Options.Count > SearchThreshold;
        keyword.IsVisible = IsKeyword;
        scroll.IsVisible = !IsKeyword;
        foreach (var field in new[] { search, keyword })
        {
            field.PlaceholderText = lang.TableFilterSearch;
            AutomationProperties.SetName(field, lang.TableFilterSearch);
        }
        clear.Content = lang.TableFilterClear;
        Sync();
    }

    /// <summary>按选中的值：勾选、按钮的强调色与名称、关键字草稿跟上外面的改动（清除、恢复保存的视图）。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        var active = Values.Count > 0;
        foreach (var box in boxes) box.IsChecked = Values.Contains(((ColumnFilterOption)box.Tag!).Value);
        var applied = IsKeyword && Values.Count > 0 ? Values[0] : "";
        if (IsKeyword && keyword.Text != applied) keyword.Text = applied;
        if (active) trigger[!TemplatedControl.ForegroundProperty] = this.GetResourceObservable(ThemeKeys.Brush.AccentText).ToBinding();
        else trigger.ClearValue(TemplatedControl.ForegroundProperty);
        AutomationProperties.SetName(trigger, lang.TableFilterColumn(Label, active));
        AutomationProperties.SetName(popover, lang.TableFilterColumn(Label, false));
        AutomationProperties.SetName(scroll, Label);
        clear.IsEnabled = active;
    }

    /// <summary>选项多时按搜索框里的字筛勾选框（按语言忽略大小写与重音）。</summary>
    void Filter()
    {
        var text = search.Text ?? "";
        var compare = System.Globalization.CultureInfo.GetCultureInfo(Provider.GetLang(this).Code).CompareInfo;
        foreach (var box in boxes)
        {
            var option = (ColumnFilterOption)box.Tag!;
            var label = option.Label as string ?? option.Value;
            box.IsVisible = text.Length == 0 || compare.IndexOf(label, text, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        popover.Measure(availableSize);
        return popover.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        popover.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>列菜单里的一项操作（vue-tsx-pro 的 ColumnMenuActions）。</summary>
public enum ColumnMenuAction
{
    /// <summary>按这一列升序。</summary>
    SortAscending,

    /// <summary>按这一列降序。</summary>
    SortDescending,

    /// <summary>取消这一列的排序。</summary>
    Unsort,

    /// <summary>固定在开头。</summary>
    PinStart,

    /// <summary>固定在结尾。</summary>
    PinEnd,

    /// <summary>取消固定。</summary>
    Unpin,

    /// <summary>往前移一列。</summary>
    MoveBackward,

    /// <summary>往后移一列。</summary>
    MoveForward,

    /// <summary>隐藏这一列。</summary>
    Hide,
}

/// <summary>
/// 列菜单（vue-tsx-pro 的 ColumnMenuButton）：列标题结尾的 ⋮ 按钮，弹出排序、固定、移动、隐藏；选了一项发 <see cref="ActionRequested"/>。
/// DataTable 开了 ColumnMenu 时放进每个列标题；状态（排序、固定、顺序、隐藏）在表格里，这里只管画与报告。
/// </summary>
public class ColumnMenuButton : Control
{
    /// <summary>列标题的文字：按钮的名称。</summary>
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<ColumnMenuButton, string>(nameof(Label), "");

    /// <summary>可以排序：菜单里有升序、降序。</summary>
    public static readonly StyledProperty<bool> SortableProperty = AvaloniaProperty.Register<ColumnMenuButton, bool>(nameof(Sortable));

    /// <summary>这一列当前的排序。</summary>
    public static readonly StyledProperty<SortDirection?> SortedProperty = AvaloniaProperty.Register<ColumnMenuButton, SortDirection?>(nameof(Sorted));

    /// <summary>这一列当前固定在哪一侧。</summary>
    public static readonly StyledProperty<ColumnSticky?> PinnedProperty = AvaloniaProperty.Register<ColumnMenuButton, ColumnSticky?>(nameof(Pinned));

    /// <summary>能往前移（已经在头上时不能；上游 canMove.backward）。</summary>
    public static readonly StyledProperty<bool> CanMoveBackwardProperty = AvaloniaProperty.Register<ColumnMenuButton, bool>(nameof(CanMoveBackward));

    /// <summary>能往后移（已经在尾上时不能；上游 canMove.forward）。</summary>
    public static readonly StyledProperty<bool> CanMoveForwardProperty = AvaloniaProperty.Register<ColumnMenuButton, bool>(nameof(CanMoveForward));

    /// <summary>能隐藏（只剩一列时不能）。</summary>
    public static readonly StyledProperty<bool> CanHideProperty = AvaloniaProperty.Register<ColumnMenuButton, bool>(nameof(CanHide), true);

    readonly IconButton trigger = new() { Name = ColumnHeaderActionsParts.Trigger, Icon = Lucide.EllipsisVertical, Size = TextSize.Xs, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };
    readonly Menu menu = new() { Placement = FloatingPlacement.BottomEnd };

    /// <summary>新建。</summary>
    public ColumnMenuButton()
    {
        menu.Trigger = trigger;
        LogicalChildren.Add(menu);
        VisualChildren.Add(menu);
    }

    /// <summary>选了一项。</summary>
    public event EventHandler<ColumnMenuAction>? ActionRequested;

    /// <summary>列标题的文字。</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>可以排序。</summary>
    public bool Sortable { get => GetValue(SortableProperty); set => SetValue(SortableProperty, value); }

    /// <summary>当前的排序。</summary>
    public SortDirection? Sorted { get => GetValue(SortedProperty); set => SetValue(SortedProperty, value); }

    /// <summary>当前固定在哪一侧。</summary>
    public ColumnSticky? Pinned { get => GetValue(PinnedProperty); set => SetValue(PinnedProperty, value); }

    /// <summary>能往前移。</summary>
    public bool CanMoveBackward { get => GetValue(CanMoveBackwardProperty); set => SetValue(CanMoveBackwardProperty, value); }

    /// <summary>能往后移。</summary>
    public bool CanMoveForward { get => GetValue(CanMoveForwardProperty); set => SetValue(CanMoveForwardProperty, value); }

    /// <summary>能隐藏。</summary>
    public bool CanHide { get => GetValue(CanHideProperty); set => SetValue(CanHideProperty, value); }

    /// <summary>⋮ 按钮（测试用）。</summary>
    internal IconButton Trigger => trigger;

    /// <summary>菜单项（测试用）：文字 → 项。</summary>
    internal IEnumerable<MenuItem> Items => menu.Items.OfType<MenuItem>();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == SortableProperty || change.Property == SortedProperty || change.Property == PinnedProperty
            || change.Property == CanMoveBackwardProperty || change.Property == CanMoveForwardProperty || change.Property == CanHideProperty || change.Property == Provider.LangProperty) Build();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Build();
    }

    void Build()
    {
        var lang = Provider.GetLang(this);
        trigger.Margin = new Thickness(0, -0.25 * TextElement.GetFontSize(this));
        AutomationProperties.SetName(trigger, lang.TableColumnMenu(Label));
        menu.Items.Clear();
        void Item(string text, ColumnMenuAction action, bool enabled = true)
        {
            var item = new MenuItem { Content = text, IsEnabled = enabled };
            item.Click += (_, _) => ActionRequested?.Invoke(this, action);
            menu.Items.Add(item);
        }
        if (Sortable)
        {
            Item(lang.TableSortAscending, ColumnMenuAction.SortAscending, Sorted != SortDirection.Ascending);
            Item(lang.TableSortDescending, ColumnMenuAction.SortDescending, Sorted != SortDirection.Descending);
            if (Sorted is not null) Item(lang.TableSortClear, ColumnMenuAction.Unsort);
            menu.Items.Add(new Separator());
        }
        if (Pinned != ColumnSticky.Start) Item(lang.TablePinStart, ColumnMenuAction.PinStart);
        if (Pinned != ColumnSticky.End) Item(lang.TablePinEnd, ColumnMenuAction.PinEnd);
        if (Pinned is not null) Item(lang.TableUnpin, ColumnMenuAction.Unpin);
        menu.Items.Add(new Separator());
        Item(lang.TableMoveBackward, ColumnMenuAction.MoveBackward, CanMoveBackward);
        Item(lang.TableMoveForward, ColumnMenuAction.MoveForward, CanMoveForward);
        Item(lang.TableHideColumn, ColumnMenuAction.Hide, CanHide);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        menu.Measure(availableSize);
        return menu.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        menu.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>ColumnFilterButton、ColumnMenuButton 的部件名。</summary>
public static class ColumnHeaderActionsParts
{
    /// <summary>列标题上的小按钮（漏斗、⋮）。</summary>
    public const string Trigger = "PART_Trigger";

    /// <summary>筛选弹出的面板。</summary>
    public const string Panel = "PART_Panel";

    /// <summary>筛选里的每个勾选项。</summary>
    public const string Option = "PART_Option";

    /// <summary>筛选的"清除"按钮。</summary>
    public const string Clear = "PART_Clear";
}
