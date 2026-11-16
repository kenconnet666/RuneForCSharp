using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 数据表格（vue-tsx-pro 的 DataTable）：按列定义（<see cref="DataTableColumn"/>）渲染一组行，始终虚拟滚动（十万行也只建看得见的几十行）。
/// <code>
/// &lt;rune:DataTable ItemsSource="{Binding Orders}" Columns="{Binding Columns}" GetKey="{Binding OrderKey}" Sort="{Binding Sort}"
///                 SelectionMode="multiple" Selected="{Binding Picked}" IsLoading="{Binding Loading}" AutomationProperties.Name="订单" /&gt;
/// </code>
/// <list type="bullet">
/// <item>高度：最高 32em（写 MaxHeight 改，写 Height 定高）；表头吸顶、合计行吸底、固定列（列的 Sticky，要写宽度）不随横向滚动。</item>
/// <item>列宽：写了 Width 的列按宽度（<see cref="ColumnWidths"/> 里调整过的优先），其余平分剩下的宽度；放不下各列的最小宽度时横向滚动。</item>
/// <item>排序：点可排序（Sortable）的列标题在 升序 → 降序 → 不排 之间切换，写回 <see cref="Sort"/>；默认在本地排（数字、日期、按语言的文字，空值总在最后），
/// <see cref="ManualSort"/> 时只写回，由调用方向服务端要排好序的数据。</item>
/// <item>选择：<see cref="SelectionMode"/> 为 single / multiple 时多一列单选框 / 勾选框（多选的表头有全选，部分选中时是半选），<see cref="Selected"/> 是已选行的 key（<see cref="GetKey"/>，默认行本身）；
/// <see cref="IsRowSelectable"/> 为 false 的行不能选、全选跳过它。已选的行整行 accentSoft 底。</item>
/// <item>加载：<see cref="IsLoading"/> 时表体换成骨架行（首次加载与刷新一样，不另加进度条）；没有行时显示 <see cref="Empty"/>（默认"暂无数据"）。</item>
/// <item>合计：列写了 Summary 时底部多一行吸底的合计；<see cref="RowTone"/> 给整行着色（导入校验的错误行）；<see cref="HiddenColumns"/> 隐藏列。</item>
/// <item>定位：<see cref="ScrollToRow"/> 滚到那一行（停在吸顶表头之下）。</item>
/// </list>
/// 列宽：<see cref="Resizable"/>（或列的 Resizable）时表头的列结束边有 <see cref="ColumnResizer"/>，拖动或用 ← / → 调整，写回 <see cref="ColumnWidths"/>。
/// 行高按第一行量出的高度（单元格默认一行，放不下省略号）。表头筛选、列菜单、就地编辑、行拖动、详情行、分组表头还没做（见 docs/DESIGN.md）。
/// 部件：<see cref="DataTableParts"/>。外观同 Table：<see cref="Density"/>、<see cref="Variant"/>、<see cref="Striped"/>。
/// </summary>
public partial class DataTable : Control
{
    /// <summary>数据（TS 的 rows）：实现了 INotifyCollectionChanged 的集合增删时跟着变。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<DataTable, IEnumerable?>(nameof(ItemsSource));

    /// <summary>列定义。</summary>
    public static readonly StyledProperty<IReadOnlyList<DataTableColumn>> ColumnsProperty = AvaloniaProperty.Register<DataTable, IReadOnlyList<DataTableColumn>>(nameof(Columns), []);

    /// <summary>行的 key（选择、定位用）；不写时是行本身。</summary>
    public static readonly StyledProperty<Func<object, object>?> GetKeyProperty = AvaloniaProperty.Register<DataTable, Func<object, object>?>(nameof(GetKey));

    /// <summary>当前的排序（双向绑定）；没有排序时 null。</summary>
    public static readonly StyledProperty<SortState?> SortProperty = AvaloniaProperty.Register<DataTable, SortState?>(nameof(Sort), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>不在本地排序：只写回 Sort，数据由调用方排好。</summary>
    public static readonly StyledProperty<bool> ManualSortProperty = AvaloniaProperty.Register<DataTable, bool>(nameof(ManualSort));

    /// <summary>选择方式：single 每行一个单选框，multiple 每行一个勾选框（表头有全选）；null 时没有选择列。</summary>
    public static readonly StyledProperty<SelectionMode?> SelectionModeProperty = AvaloniaProperty.Register<DataTable, SelectionMode?>(nameof(SelectionMode));

    /// <summary>已选行的 key（双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<object>> SelectedProperty = AvaloniaProperty.Register<DataTable, IReadOnlyList<object>>(nameof(Selected), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>这一行能不能选；不写时都能。</summary>
    public static readonly StyledProperty<Func<object, bool>?> IsRowSelectableProperty = AvaloniaProperty.Register<DataTable, Func<object, bool>?>(nameof(IsRowSelectable));

    /// <summary>加载中：表体换成骨架行。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<DataTable, bool>(nameof(IsLoading));

    /// <summary>没有行时显示的内容；不写时是 Empty（"暂无数据"）。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<DataTable, object?>(nameof(Empty));

    /// <summary>整行的色调（导入校验的错误行）：返回 info、success、warning、danger，其余不着色。</summary>
    public static readonly StyledProperty<Func<object, StatusTone?>?> RowToneProperty = AvaloniaProperty.Register<DataTable, Func<object, StatusTone?>?>(nameof(RowTone));

    /// <summary>隐藏的列（列的 Key，双向绑定：列菜单的"隐藏"写回）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> HiddenColumnsProperty = AvaloniaProperty.Register<DataTable, IReadOnlyList<string>?>(nameof(HiddenColumns), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>多列排序：排序写在 <see cref="Sorts"/>（几列），点表头只按这一列，按住 Shift 点是追加一列。</summary>
    public static readonly StyledProperty<bool> MultiSortProperty = AvaloniaProperty.Register<DataTable, bool>(nameof(MultiSort));

    /// <summary>多列排序时的排序（双向绑定），前面的优先。</summary>
    public static readonly StyledProperty<IReadOnlyList<SortState>> SortsProperty = AvaloniaProperty.Register<DataTable, IReadOnlyList<SortState>>(nameof(Sorts), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>表头筛选：各列选中的值（列的 Key → 值，关键字筛选时是 [关键字]；双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, IReadOnlyList<string>>?> FiltersProperty =
        AvaloniaProperty.Register<DataTable, IReadOnlyDictionary<string, IReadOnlyList<string>>?>(nameof(Filters), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>不在本地筛选：只写回 Filters，数据由调用方筛好。</summary>
    public static readonly StyledProperty<bool> ManualFilterProperty = AvaloniaProperty.Register<DataTable, bool>(nameof(ManualFilter));

    /// <summary>列菜单：每个列标题结尾有 ⋮（排序、固定在开头 / 结尾、前后移动、隐藏），写回 ColumnPins、ColumnOrder、HiddenColumns。</summary>
    public static readonly StyledProperty<bool> ColumnMenuProperty = AvaloniaProperty.Register<DataTable, bool>(nameof(ColumnMenu));

    /// <summary>列的顺序（列的 Key，没列到的按定义的顺序排在后面；双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ColumnOrderProperty = AvaloniaProperty.Register<DataTable, IReadOnlyList<string>?>(nameof(ColumnOrder), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>列的固定（列的 Key → 固定在哪一侧，null 取消列定义里的固定；双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, ColumnSticky?>?> ColumnPinsProperty =
        AvaloniaProperty.Register<DataTable, IReadOnlyDictionary<string, ColumnSticky?>?>(nameof(ColumnPins), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>调整过的列宽（列的 Key → 像素，双向绑定，可以存到本地设置里）。</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, double>?> ColumnWidthsProperty =
        AvaloniaProperty.Register<DataTable, IReadOnlyDictionary<string, double>?>(nameof(ColumnWidths), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>可以拖动或用键盘调整列宽（列的 Resizable 优先）：调整后写回 <see cref="ColumnWidths"/>。</summary>
    public static readonly StyledProperty<bool> ResizableProperty = AvaloniaProperty.Register<DataTable, bool>(nameof(Resizable));

    /// <summary>合计行第一列的内容（第一列自己没写 Summary 时），默认取语言包（合计）。</summary>
    public static readonly StyledProperty<object?> SummaryLabelProperty = AvaloniaProperty.Register<DataTable, object?>(nameof(SummaryLabel));

    /// <summary>密度，默认 standard。</summary>
    public static readonly StyledProperty<TableDensity> DensityProperty = AvaloniaProperty.Register<DataTable, TableDensity>(nameof(Density), TableDensity.Standard);

    /// <summary>样式，默认 surface。</summary>
    public static readonly StyledProperty<TableVariant> VariantProperty = AvaloniaProperty.Register<DataTable, TableVariant>(nameof(Variant), TableVariant.Surface);

    /// <summary>斑马纹：隔行浅色底。</summary>
    public static readonly StyledProperty<bool> StripedProperty = AvaloniaProperty.Register<DataTable, bool>(nameof(Striped));

    /// <summary>分组表头：组标题与组里各列的 Key，相邻的列上方共用一个标题（表头变成两行）。</summary>
    public static readonly StyledProperty<IReadOnlyList<DataTableColumnGroup>?> ColumnGroupsProperty = AvaloniaProperty.Register<DataTable, IReadOnlyList<DataTableColumnGroup>?>(nameof(ColumnGroups));

    /// <summary>详情：给出一行展开后下面那一行的内容（控件或文字）；给了时多一列展开按钮。</summary>
    public static readonly StyledProperty<Func<object, object?>?> RenderDetailProperty = AvaloniaProperty.Register<DataTable, Func<object, object?>?>(nameof(RenderDetail));

    /// <summary>这一行有没有详情（没有时没有展开按钮）；不写时都有。</summary>
    public static readonly StyledProperty<Func<object, bool>?> HasDetailProperty = AvaloniaProperty.Register<DataTable, Func<object, bool>?>(nameof(HasDetail));

    /// <summary>展开了的行的 key（双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<object>> ExpandedRowsProperty =
        AvaloniaProperty.Register<DataTable, IReadOnlyList<object>>(nameof(ExpandedRows), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>勾选列的宽度（像素，同上游 SELECT_WIDTH）。</summary>
    internal const double SelectWidth = 44;

    /// <summary>详情列（展开按钮）的宽度（像素，同上游 DETAIL_WIDTH）。</summary>
    internal const double DetailWidth = 40;

    /// <summary>详情行的估计高度（像素）：建出来量过之后以实际高度为准。</summary>
    internal const double DetailHeight = 160;

    readonly Border frame = new() { ClipToBounds = true };
    readonly ScrollArea scroller = new() { Axis = ScrollAreaAxis.Both };
    readonly DataTableSurface surface;
    INotifyCollectionChanged? observed;
    IReadOnlyList<object> source = [];

    /// <summary>新建。</summary>
    public DataTable()
    {
        surface = new DataTableSurface(this);
        scroller.Content = surface;
        frame.Child = scroller;
        scroller.ScrollChanged += (_, _) => surface.Scrolled();
        LogicalChildren.Add(frame);
        VisualChildren.Add(frame);
    }

    static DataTable() => AffectsMeasure<DataTable>(DensityProperty);

    /// <summary>数据。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>列定义。</summary>
    public IReadOnlyList<DataTableColumn> Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>行的 key。</summary>
    public Func<object, object>? GetKey { get => GetValue(GetKeyProperty); set => SetValue(GetKeyProperty, value); }

    /// <summary>当前的排序。</summary>
    public SortState? Sort { get => GetValue(SortProperty); set => SetValue(SortProperty, value); }

    /// <summary>不在本地排序。</summary>
    public bool ManualSort { get => GetValue(ManualSortProperty); set => SetValue(ManualSortProperty, value); }

    /// <summary>选择方式。</summary>
    public SelectionMode? SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>已选行的 key。</summary>
    public IReadOnlyList<object> Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }

    /// <summary>这一行能不能选。</summary>
    public Func<object, bool>? IsRowSelectable { get => GetValue(IsRowSelectableProperty); set => SetValue(IsRowSelectableProperty, value); }

    /// <summary>加载中。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>没有行时显示的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>整行的色调。</summary>
    public Func<object, StatusTone?>? RowTone { get => GetValue(RowToneProperty); set => SetValue(RowToneProperty, value); }

    /// <summary>隐藏的列。</summary>
    public IReadOnlyList<string>? HiddenColumns { get => GetValue(HiddenColumnsProperty); set => SetValue(HiddenColumnsProperty, value); }

    /// <summary>多列排序。</summary>
    public bool MultiSort { get => GetValue(MultiSortProperty); set => SetValue(MultiSortProperty, value); }

    /// <summary>多列排序时的排序。</summary>
    public IReadOnlyList<SortState> Sorts { get => GetValue(SortsProperty); set => SetValue(SortsProperty, value); }

    /// <summary>表头筛选。</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Filters { get => GetValue(FiltersProperty); set => SetValue(FiltersProperty, value); }

    /// <summary>不在本地筛选。</summary>
    public bool ManualFilter { get => GetValue(ManualFilterProperty); set => SetValue(ManualFilterProperty, value); }

    /// <summary>列菜单。</summary>
    public bool ColumnMenu { get => GetValue(ColumnMenuProperty); set => SetValue(ColumnMenuProperty, value); }

    /// <summary>列的顺序。</summary>
    public IReadOnlyList<string>? ColumnOrder { get => GetValue(ColumnOrderProperty); set => SetValue(ColumnOrderProperty, value); }

    /// <summary>列的固定。</summary>
    public IReadOnlyDictionary<string, ColumnSticky?>? ColumnPins { get => GetValue(ColumnPinsProperty); set => SetValue(ColumnPinsProperty, value); }

    /// <summary>调整过的列宽。</summary>
    public IReadOnlyDictionary<string, double>? ColumnWidths { get => GetValue(ColumnWidthsProperty); set => SetValue(ColumnWidthsProperty, value); }

    /// <summary>分组表头。</summary>
    public IReadOnlyList<DataTableColumnGroup>? ColumnGroups { get => GetValue(ColumnGroupsProperty); set => SetValue(ColumnGroupsProperty, value); }

    /// <summary>表头第一行的各格（有分组时）；没有分组显示时为空（一行表头）。</summary>
    internal IReadOnlyList<HeaderLayoutCell> HeaderCells { get; private set; } = [];

    /// <summary>详情。</summary>
    public Func<object, object?>? RenderDetail { get => GetValue(RenderDetailProperty); set => SetValue(RenderDetailProperty, value); }

    /// <summary>这一行有没有详情。</summary>
    public Func<object, bool>? HasDetail { get => GetValue(HasDetailProperty); set => SetValue(HasDetailProperty, value); }

    /// <summary>展开了的行。</summary>
    public IReadOnlyList<object> ExpandedRows { get => GetValue(ExpandedRowsProperty); set => SetValue(ExpandedRowsProperty, value); }

    /// <summary>这一行有详情（给了 RenderDetail，HasDetail 不是 false）。</summary>
    internal bool RowHasDetail(object row) => RenderDetail is not null && (HasDetail?.Invoke(row) ?? true);

    /// <summary>展开、收起一行的详情：写回 ExpandedRows。</summary>
    internal void ToggleDetail(object row)
    {
        var key = KeyOf(row);
        SetCurrentValue(ExpandedRowsProperty, ExpandedRows.Contains(key) ? [.. ExpandedRows.Where(item => !Equals(item, key))] : [.. ExpandedRows, key]);
    }

    /// <summary>可以调整列宽。</summary>
    public bool Resizable { get => GetValue(ResizableProperty); set => SetValue(ResizableProperty, value); }

    /// <summary>合计行第一列的内容。</summary>
    public object? SummaryLabel { get => GetValue(SummaryLabelProperty); set => SetValue(SummaryLabelProperty, value); }

    /// <summary>密度。</summary>
    public TableDensity Density { get => GetValue(DensityProperty); set => SetValue(DensityProperty, value); }

    /// <summary>样式。</summary>
    public TableVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>斑马纹。</summary>
    public bool Striped { get => GetValue(StripedProperty); set => SetValue(StripedProperty, value); }

    /// <summary>显示的列（去掉隐藏的）。</summary>
    internal IReadOnlyList<DataTableColumn> Shown { get; private set; } = [];

    /// <summary>显示的行（本地排序之后）。</summary>
    internal IReadOnlyList<object> View { get; private set; } = [];

    /// <summary>各列的值类型（按全部数据推断）。</summary>
    internal ColumnType[] Types { get; private set; } = [];

    /// <summary>滚动区域（测试用）。</summary>
    internal ScrollArea Scroller => scroller;

    /// <summary>表格的内容面板（测试用）。</summary>
    internal DataTableSurface Surface => surface;

    internal Lang Lang => Provider.GetLang(this);

    internal object KeyOf(object row) => GetKey?.Invoke(row) ?? row;

    internal bool IsSelected(object row) => Selected.Contains(KeyOf(row));

    internal bool CanSelect(object row) => IsRowSelectable?.Invoke(row) ?? true;

    /// <summary>滚到 key 对应的那一行（停在吸顶表头之下）；找不到时返回 false、不滚动。</summary>
    public bool ScrollToRow(object key)
    {
        var index = View.ToList().FindIndex(row => Equals(KeyOf(row), key));
        if (index < 0) return false;
        surface.ScrollTo(index);
        return true;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            if (observed is not null) observed.CollectionChanged -= OnItemsChanged;
            observed = ItemsSource as INotifyCollectionChanged;
            if (observed is not null) observed.CollectionChanged += OnItemsChanged;
            Refresh();
        }
        else if (change.Property == ColumnsProperty || change.Property == SortProperty || change.Property == ManualSortProperty || change.Property == HiddenColumnsProperty
                 || change.Property == MultiSortProperty || change.Property == SortsProperty || change.Property == ManualFilterProperty
                 || change.Property == ColumnOrderProperty || change.Property == ColumnPinsProperty || change.Property == ColumnGroupsProperty || change.Property == Provider.LangProperty) Refresh();
        else if (change.Property == FiltersProperty) Refresh(header: false);
        else if (change.Property == SelectedProperty || change.Property == RowToneProperty || change.Property == IsRowSelectableProperty) surface.SyncRows();
        else if (change.Property == SelectionModeProperty || change.Property == IsLoadingProperty || change.Property == EmptyProperty || change.Property == SummaryLabelProperty
                 || change.Property == ResizableProperty || change.Property == ColumnMenuProperty || change.Property == CellNavigationProperty
                 || change.Property == EditTriggerProperty || change.Property == ValidateRowProperty || change.Property == RenderDetailProperty
                 || change.Property == HasDetailProperty || change.Property == DensityProperty || change.Property == StripedProperty || change.Property == GetKeyProperty) surface.Rebuild();
        if (change.Property == VariantProperty || change.Property == DensityProperty) SyncFrame();
        // 只是宽度变了：重排，不重建（拖动中的手柄要留着）
        if (change.Property == ColumnWidthsProperty) surface.Relayout();
        // 展开、收起：各行的展开按钮跟着改，详情行在量的时候建或收（不重建各行，按钮的焦点留着）
        else if (change.Property == ExpandedRowsProperty) surface.SyncDetails();
    }

    void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncFrame();
        Refresh();
    }

    /// <summary>数据、列、排序变了：重算显示的列与行，表格重建。</summary>
    void Refresh(bool header = true)
    {
        source = ItemsSource?.Cast<object>().ToList() ?? [];
        var hidden = HiddenColumns ?? [];
        Arranged = TableColumns.Arrange(Columns, ColumnOrder, ColumnPins);
        Shown = [.. Arranged.Where(column => !hidden.Contains(column.Key))];
        Types = [.. Shown.Select(column => TableColumns.TypeOf(column, source))];
        // 隐藏了的列不算进组；组里的列全隐藏时回到一行表头
        var layout = ColumnGroups is { Count: > 0 } groups ? TableColumns.HeaderLayout(Shown, groups) : [];
        HeaderCells = layout.Any(cell => cell.Group is not null) ? layout : [];
        // 筛选的选项看全部行（不是筛过的）
        FilterOptionsOf = Columns.Where(column => column.Filter is not null).ToDictionary(column => column.Key, column => TableColumns.FilterOptions(column, source, Lang.Code));
        var kept = ManualFilter ? source : TableColumns.FilterRows(source, Columns, Filters, Lang.Code);
        View = ManualSort ? kept : TableColumns.SortRows(kept, Columns, ActiveSorts, Lang.Code);
        if (header) surface.Rebuild();
        else surface.RebuildBody();
    }

    /// <summary>当前的排序：多列排序时是 Sorts，否则是 Sort（一个或零个）。</summary>
    internal IReadOnlyList<SortState> ActiveSorts => MultiSort ? Sorts : Sort is { } one ? [one] : [];

    /// <summary>按顺序与固定排好的全部列（含隐藏的）。</summary>
    internal IReadOnlyList<DataTableColumn> Arranged { get; private set; } = [];

    /// <summary>各列表头筛选的选项。</summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<ColumnFilterOption>> FilterOptionsOf { get; private set; } = new Dictionary<string, IReadOnlyList<ColumnFilterOption>>();

    /// <summary>点了列标题：多列排序时按住 Shift 是追加，否则只按这一列。</summary>
    internal void SortClicked(string key, bool additive)
    {
        if (MultiSort) SetCurrentValue(SortsProperty, TableColumns.NextSorts(Sorts, key, additive));
        else SetCurrentValue(SortProperty, TableColumns.NextSort(Sort, key));
    }

    /// <summary>列菜单里的排序：多列排序时这一列排到最前。</summary>
    void SetSort(string key, SortDirection? direction)
    {
        if (!MultiSort) SetCurrentValue(SortProperty, direction is { } one ? new SortState(key, one) : null);
        else
        {
            var rest = Sorts.Where(item => item.Key != key);
            SetCurrentValue(SortsProperty, direction is { } first ? [new SortState(key, first), .. rest] : rest.ToList());
        }
    }

    /// <summary>改了一列的表头筛选。</summary>
    internal void SetFilter(string key, IReadOnlyList<string> values)
    {
        var next = new Dictionary<string, IReadOnlyList<string>>(Filters ?? new Dictionary<string, IReadOnlyList<string>>()) { [key] = values };
        SetCurrentValue(FiltersProperty, next);
    }

    /// <summary>往前、往后移一列：与相邻的可见列交换位置，写回 ColumnOrder。</summary>
    void Move(string key, int step)
    {
        var hidden = (HiddenColumns ?? []).ToHashSet();
        var keys = Arranged.Select(column => column.Key).ToList();
        var visible = keys.Where(item => !hidden.Contains(item)).ToList();
        var at = visible.IndexOf(key) + step;
        if (at < 0 || at >= visible.Count) return;
        var neighbour = visible[at];
        var (from, to) = (keys.IndexOf(key), keys.IndexOf(neighbour));
        (keys[from], keys[to]) = (neighbour, key);
        SetCurrentValue(ColumnOrderProperty, keys);
    }

    /// <summary>列菜单选了一项：排序、移动、隐藏、固定（固定没写宽度的列时记下它现在的宽度：固定列的偏移按列宽累加）。</summary>
    internal void ColumnAction(string key, ColumnMenuAction action, double width)
    {
        switch (action)
        {
            case ColumnMenuAction.SortAscending: SetSort(key, SortDirection.Ascending); break;
            case ColumnMenuAction.SortDescending: SetSort(key, SortDirection.Descending); break;
            case ColumnMenuAction.Unsort: SetSort(key, null); break;
            case ColumnMenuAction.MoveBackward: Move(key, -1); break;
            case ColumnMenuAction.MoveForward: Move(key, 1); break;
            case ColumnMenuAction.Hide: SetCurrentValue(HiddenColumnsProperty, [.. HiddenColumns ?? [], key]); break;
            default:
                ColumnSticky? side = action == ColumnMenuAction.PinStart ? ColumnSticky.Start : action == ColumnMenuAction.PinEnd ? ColumnSticky.End : null;
                SetCurrentValue(ColumnPinsProperty, new Dictionary<string, ColumnSticky?>(ColumnPins ?? new Dictionary<string, ColumnSticky?>()) { [key] = side });
                var column = Columns.FirstOrDefault(item => item.Key == key);
                if (side is not null && column?.Width is null && ColumnWidths?.ContainsKey(key) != true && width > 0) ResizeColumn(key, width);
                break;
        }
    }

    /// <summary>外框：surface 有 border 边框、lg 圆角、surface 底；ghost 是页面底色。</summary>
    void SyncFrame()
    {
        var ghost = Variant == TableVariant.Ghost;
        // 紧凑时字号也小一档（同上游 compactTable：在表格上，外层的 32em 仍按原来的字号）
        if (Density == TableDensity.Compact) surface.Bind(TextElement.FontSizeProperty, this.GetResourceObservable(ThemeKeys.FontSize.Sm));
        else surface.ClearValue(TextElement.FontSizeProperty);
        frame.BorderThickness = ghost ? default : new Thickness(this.Resource<double>(ThemeKeys.BorderWidth.Xs));
        frame.CornerRadius = ghost ? default : new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Lg) * TextElement.GetFontSize(this));
        frame[!Border.BorderBrushProperty] = this.GetResourceObservable(ThemeKeys.Brush.Border).ToBinding();
        frame[!Border.BackgroundProperty] = this.GetResourceObservable(ghost ? ThemeKeys.Brush.Background : ThemeKeys.Brush.Surface).ToBinding();
        surface.InvalidateVisual();
    }

    /// <summary>调整了一列的宽度：写回 ColumnWidths（新的字典）。</summary>
    internal void ResizeColumn(string key, double width)
    {
        var next = new Dictionary<string, double>(ColumnWidths ?? new Dictionary<string, double>()) { [key] = width };
        SetCurrentValue(ColumnWidthsProperty, next);
    }


    /// <summary>点了一行的选择框。</summary>
    internal void ToggleRow(object row)
    {
        if (!CanSelect(row) || SelectionMode is not { } mode) return;
        var key = KeyOf(row);
        IReadOnlyList<object> next = mode == Rune.Avalonia.SelectionMode.Multiple
            ? Selected.Contains(key) ? [.. Selected.Where(item => !Equals(item, key))] : [.. Selected, key]
            : [key];
        SetCurrentValue(SelectedProperty, next);
    }

    /// <summary>点了表头的全选：能选的行都已选时取消它们，否则选上它们（不能选的行原样）。</summary>
    internal void ToggleAll()
    {
        var selectable = View.Where(CanSelect).Select(KeyOf).ToList();
        var set = Selected.ToHashSet();
        var all = selectable.Count > 0 && selectable.All(set.Contains);
        if (all) set.ExceptWith(selectable);
        else set.UnionWith(selectable);
        SetCurrentValue(SelectedProperty, set.ToList());
    }

    /// <summary>全选框的状态：全选、部分、没选（只数能选的行）。</summary>
    internal (bool All, bool Some) SelectionState()
    {
        if (Selected.Count == 0) return (false, false);
        var set = Selected.ToHashSet();
        var selectable = View.Where(CanSelect).Select(KeyOf).ToList();
        var count = selectable.Count(set.Contains);
        return (selectable.Count > 0 && count == selectable.Count, count > 0 && count < selectable.Count);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // 默认最高 32em（同上游 root 的 maxHeight）：使用方写了 MaxHeight 或 Height 时以它为准
        var cap = IsSet(MaxHeightProperty) || IsSet(HeightProperty) ? double.PositiveInfinity : 32 * TextElement.GetFontSize(this);
        frame.Measure(availableSize.WithHeight(Math.Min(availableSize.Height, cap)));
        announcer?.Measure(Size.Infinity);
        return frame.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        frame.Arrange(new Rect(finalSize));
        announcer?.Arrange(default);
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new DataTableAutomationPeer(this);

    sealed class DataTableAutomationPeer(DataTable owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

        protected override string? GetItemStatusCore() => owner.IsLoading ? "busy" : base.GetItemStatusCore();
    }
}

/// <summary>DataTable 的部件名（TS 的 propsHeaderCell、propsRow、propsCell 等）：样式写 <c>rune|TableCell#PART_Cell</c>，按行的数据写样式用行的 DataContext。</summary>
public static class DataTableParts
{
    /// <summary>表头那一行。</summary>
    public const string HeaderRow = "PART_HeaderRow";

    /// <summary>列标题。</summary>
    public const string HeaderCell = "PART_HeaderCell";

    /// <summary>调整列宽的手柄（ColumnResizer）。</summary>
    public const string Resizer = "PART_Resizer";

    /// <summary>行拖动的把手（第一个数据列的前面）。</summary>
    public const string RowHandle = "PART_RowHandle";

    /// <summary>编辑中的格里的编辑器。</summary>
    public const string Editor = "PART_Editor";

    /// <summary>编辑器下面校验没过的原因。</summary>
    public const string EditorError = "PART_EditorError";

    /// <summary>列标题里的表头筛选按钮（ColumnFilterButton）。</summary>
    public const string FilterButton = "PART_FilterButton";

    /// <summary>列标题里的列菜单按钮（ColumnMenuButton）。</summary>
    public const string ColumnMenuButton = "PART_ColumnMenuButton";

    /// <summary>多列排序时的次序数字。</summary>
    public const string SortPriority = "PART_SortPriority";

    /// <summary>可排序列标题里的按钮。</summary>
    public const string SortButton = "PART_SortButton";

    /// <summary>排序的箭头。</summary>
    public const string SortIcon = "PART_SortIcon";

    /// <summary>表体的每一行。</summary>
    public const string Row = "PART_Row";

    /// <summary>表体的每个单元格。</summary>
    public const string Cell = "PART_Cell";

    /// <summary>表头的选择列。</summary>
    public const string SelectHeaderCell = "PART_SelectHeaderCell";

    /// <summary>表头的全选框。</summary>
    public const string SelectAll = "PART_SelectAll";

    /// <summary>分组表头里的组标题。</summary>
    public const string GroupHeaderCell = "PART_GroupHeaderCell";

    /// <summary>表头的详情列。</summary>
    public const string DetailHeaderCell = "PART_DetailHeaderCell";

    /// <summary>每行的展开按钮。</summary>
    public const string DetailToggle = "PART_DetailToggle";

    /// <summary>展开的详情那一行。</summary>
    public const string DetailRow = "PART_DetailRow";

    /// <summary>每行的选择列。</summary>
    public const string SelectCell = "PART_SelectCell";

    /// <summary>每行的勾选框（多选）。</summary>
    public const string SelectCheckbox = "PART_SelectCheckbox";

    /// <summary>每行的单选框（单选）。</summary>
    public const string SelectRadio = "PART_SelectRadio";

    /// <summary>合计行。</summary>
    public const string SummaryRow = "PART_SummaryRow";

    /// <summary>合计行的每个单元格。</summary>
    public const string SummaryCell = "PART_SummaryCell";

    /// <summary>没有行时的 Empty。</summary>
    public const string Empty = "PART_Empty";
}
