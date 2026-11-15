using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace Rune.Avalonia;

/// <summary>
/// 列设置（vue-tsx-pro 的 ColumnSettings）：表格工具栏上的按钮，弹出全部的列——勾选显示哪些、拖动（或聚焦把手后用键盘）调整顺序，"重置"回到列定义。
/// <code>
/// &lt;rune:ColumnSettings Columns="{Binding Columns}" Hidden="{Binding Hidden}" Order="{Binding Order}" /&gt;
/// </code>
/// <see cref="Hidden"/>、<see cref="Order"/> 默认双向绑定，写回的是新的列表（不改原来的），与表格的隐藏列、列顺序绑同一份状态；
/// 用 <see cref="TableColumns.Arrange"/> 按 Order 排列、去掉 Hidden 里的列就是表格要显示的列。只剩一列可见时不能再隐藏。
/// 点了"重置"：隐藏与顺序清空，再发 <see cref="ResetRequested"/>（要连列宽、固定一起还原时在这里清掉）；<see cref="Changed"/> 表示还有别的列设置改过，这时"重置"也可用。
/// 部件：<see cref="ColumnSettingsParts"/>。
/// </summary>
public class ColumnSettings : Control
{
    /// <summary>全部的列（列定义的顺序）。</summary>
    public static readonly StyledProperty<IReadOnlyList<DataTableColumn>> ColumnsProperty =
        AvaloniaProperty.Register<ColumnSettings, IReadOnlyList<DataTableColumn>>(nameof(Columns), []);

    /// <summary>隐藏的列的 Key（TS 的 _hidden）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> HiddenProperty =
        AvaloniaProperty.Register<ColumnSettings, IReadOnlyList<string>>(nameof(Hidden), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>列的顺序（TS 的 _order）：Key 的列表，没列到的按定义的顺序排在后面。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> OrderProperty =
        AvaloniaProperty.Register<ColumnSettings, IReadOnlyList<string>>(nameof(Order), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>显示与顺序之外还有别的列设置改过（固定、列宽，由 ResetRequested 还原）：这时"重置"也可用。</summary>
    public static readonly StyledProperty<bool> ChangedProperty = AvaloniaProperty.Register<ColumnSettings, bool>(nameof(Changed));

    /// <summary>点了"重置"（TS 的 onReset）。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ResetRequestedEvent = RoutedEvent.Register<ColumnSettings, RoutedEventArgs>(nameof(ResetRequested), RoutingStrategies.Bubble);

    readonly Popover popover = new() { Name = ColumnSettingsParts.Panel, Placement = FloatingPlacement.BottomEnd };
    readonly IconButton trigger = new() { Name = ColumnSettingsParts.Trigger, Icon = Lucide.Columns3, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };
    readonly Text title = new() { Name = ColumnSettingsParts.Title, Size = TextSize.Sm, Weight = TextWeight.Medium };
    readonly Button reset = new() { Name = ColumnSettingsParts.Reset, Size = TextSize.Sm, Variant = ButtonVariant.Ghost };
    readonly Sortable list = new() { Name = ColumnSettingsParts.List };
    readonly ObservableCollection<DataTableColumn> arranged = [];
    // 各列的勾选框（列表按项建的）：隐藏的列变了就地改勾选与可用，不重建列表（键盘勾选时焦点不丢）
    readonly Dictionary<string, Checkbox> boxes = [];
    // 拖完排序时 Sortable 已经就地改好了 arranged，写回 Order 时不再按 Order 重排
    bool reordering;

    /// <summary>新建：触发按钮与弹出的面板（标题、重置、可排序的勾选列表）。</summary>
    public ColumnSettings()
    {
        reset.Click += (_, _) => Reset();
        list.ItemsSource = arranged;
        list.GetKey = column => ((DataTableColumn)column).Key;
        list.GetLabel = column => TableColumns.Label((DataTableColumn)column);
        list.ItemTemplate = new FuncDataTemplate<DataTableColumn>((column, _) => Box(column));
        list.Reordered += (_, _) =>
        {
            reordering = true;
            SetCurrentValue(OrderProperty, arranged.Select(column => column.Key).ToList());
            reordering = false;
        };
        var header = new Flex
        {
            Name = ColumnSettingsParts.Header, Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md,
            Children = { title, reset },
        };
        var scroll = new ScrollArea { Content = list, MaxHeight = 20 * 16 };
        popover.Trigger = trigger;
        popover.Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, MinWidth = 14 * 16, Children = { header, scroll } };
        LogicalChildren.Add(popover);
        VisualChildren.Add(popover);
    }

    /// <summary>全部的列。</summary>
    public IReadOnlyList<DataTableColumn> Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>隐藏的列。</summary>
    public IReadOnlyList<string> Hidden { get => GetValue(HiddenProperty); set => SetValue(HiddenProperty, value); }

    /// <summary>列的顺序。</summary>
    public IReadOnlyList<string> Order { get => GetValue(OrderProperty); set => SetValue(OrderProperty, value); }

    /// <summary>还有别的列设置改过。</summary>
    public bool Changed { get => GetValue(ChangedProperty); set => SetValue(ChangedProperty, value); }

    /// <summary>点了"重置"。</summary>
    public event EventHandler<RoutedEventArgs>? ResetRequested
    {
        add => AddHandler(ResetRequestedEvent, value);
        remove => RemoveHandler(ResetRequestedEvent, value);
    }

    /// <summary>弹出层（测试用）。</summary>
    internal Popover Popover => popover;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColumnsProperty || change.Property == OrderProperty && !reordering) Arrange();
        if (change.Property == ColumnsProperty || change.Property == HiddenProperty || change.Property == OrderProperty || change.Property == ChangedProperty
            || change.Property == Provider.LangProperty) Sync();
    }

    /// <summary>按当前顺序列出（就地改 arranged：Sortable 看的就是它）。</summary>
    void Arrange()
    {
        boxes.Clear();
        arranged.Clear();
        foreach (var column in TableColumns.Arrange(Columns, Order)) arranged.Add(column);
    }

    /// <summary>名称、"重置"的可用（显示与顺序都没改、也没有别的改动时不可用）与各勾选框的状态。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        title.Text = lang.TableColumnSettings;
        reset.Content = lang.TableColumnSettingsReset;
        reset.IsEnabled = Hidden.Count > 0 || Order.Count > 0 || Changed;
        foreach (var target in new Control[] { popover, trigger, list }) target.SetValue(AutomationProperties.NameProperty, lang.TableColumnSettings, BindingPriority.Template);
        foreach (var (key, box) in boxes) Apply(key, box);
    }

    /// <summary>勾上是显示；只剩这一列可见时不能再隐藏（只数现有的列：记下来的设置里可能还留着已经删掉的列）。</summary>
    void Apply(string key, Checkbox box)
    {
        var visible = !Hidden.Contains(key);
        box.IsChecked = visible;
        box.IsEnabled = !(visible && Columns.Count(item => !Hidden.Contains(item.Key)) <= 1);
    }

    /// <summary>一列的勾选框：勾选变了写回新的 Hidden。</summary>
    Checkbox Box(DataTableColumn column)
    {
        var box = new Checkbox { Name = ColumnSettingsParts.Checkbox, Content = TableColumns.Label(column) };
        boxes[column.Key] = box;
        Apply(column.Key, box);
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property != Checkbox.IsCheckedProperty || box.IsChecked == !Hidden.Contains(column.Key)) return;
            SetCurrentValue(HiddenProperty, box.IsChecked ? Hidden.Where(key => key != column.Key).ToList() : [.. Hidden, column.Key]);
        };
        return box;
    }

    void Reset()
    {
        SetCurrentValue(HiddenProperty, Array.Empty<string>());
        SetCurrentValue(OrderProperty, Array.Empty<string>());
        RaiseEvent(new RoutedEventArgs(ResetRequestedEvent));
    }
}

/// <summary>ColumnSettings 的部件名（TS 的 propsTrigger、propsPanel、propsList、propsCheckbox、propsReset）。</summary>
public static class ColumnSettingsParts
{
    /// <summary>触发按钮（IconButton，ghost、中性、Columns3）。</summary>
    public const string Trigger = "PART_Trigger";

    /// <summary>弹出层（Popover，bottom-end）。</summary>
    public const string Panel = "PART_Panel";

    /// <summary>面板的标题行：标题与"重置"。</summary>
    public const string Header = "PART_Header";

    /// <summary>标题（中等字重、sm）。</summary>
    public const string Title = "PART_Title";

    /// <summary>"重置"按钮（Button，sm、ghost）。</summary>
    public const string Reset = "PART_Reset";

    /// <summary>列的列表（Sortable），最高 20rem、再多时滚动。</summary>
    public const string List = "PART_List";

    /// <summary>每一列的勾选框（显示 / 隐藏）；勾选与不可用由组件管理。</summary>
    public const string Checkbox = "PART_Checkbox";
}
