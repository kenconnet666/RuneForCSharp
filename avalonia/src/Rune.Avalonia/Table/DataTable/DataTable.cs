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
using Avalonia.Layout;
using Avalonia.Media;

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
/// 行高按第一行量出的高度（单元格默认一行，放不下省略号）。表头筛选、列菜单、就地编辑、行拖动、详情行、分组表头、调整列宽的把手还没做（见 docs/DESIGN.md）。
/// 部件：<see cref="DataTableParts"/>。外观同 Table：<see cref="Density"/>、<see cref="Variant"/>、<see cref="Striped"/>。
/// </summary>
public class DataTable : Control
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

    /// <summary>隐藏的列（列的 Key）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> HiddenColumnsProperty = AvaloniaProperty.Register<DataTable, IReadOnlyList<string>?>(nameof(HiddenColumns));

    /// <summary>调整过的列宽（列的 Key → 像素，双向绑定，可以存到本地设置里）。</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, double>?> ColumnWidthsProperty =
        AvaloniaProperty.Register<DataTable, IReadOnlyDictionary<string, double>?>(nameof(ColumnWidths), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>合计行第一列的内容（第一列自己没写 Summary 时），默认取语言包（合计）。</summary>
    public static readonly StyledProperty<object?> SummaryLabelProperty = AvaloniaProperty.Register<DataTable, object?>(nameof(SummaryLabel));

    /// <summary>密度，默认 standard。</summary>
    public static readonly StyledProperty<TableDensity> DensityProperty = AvaloniaProperty.Register<DataTable, TableDensity>(nameof(Density), TableDensity.Standard);

    /// <summary>样式，默认 surface。</summary>
    public static readonly StyledProperty<TableVariant> VariantProperty = AvaloniaProperty.Register<DataTable, TableVariant>(nameof(Variant), TableVariant.Surface);

    /// <summary>斑马纹：隔行浅色底。</summary>
    public static readonly StyledProperty<bool> StripedProperty = AvaloniaProperty.Register<DataTable, bool>(nameof(Striped));

    /// <summary>勾选列的宽度（像素，同上游 SELECT_WIDTH）。</summary>
    internal const double SelectWidth = 44;

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

    /// <summary>调整过的列宽。</summary>
    public IReadOnlyDictionary<string, double>? ColumnWidths { get => GetValue(ColumnWidthsProperty); set => SetValue(ColumnWidthsProperty, value); }

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
                 || change.Property == Provider.LangProperty) Refresh();
        else if (change.Property == SelectedProperty || change.Property == RowToneProperty || change.Property == IsRowSelectableProperty) surface.SyncRows();
        else if (change.Property == SelectionModeProperty || change.Property == IsLoadingProperty || change.Property == EmptyProperty || change.Property == SummaryLabelProperty
                 || change.Property == ColumnWidthsProperty || change.Property == DensityProperty || change.Property == StripedProperty || change.Property == GetKeyProperty) surface.Rebuild();
        if (change.Property == VariantProperty || change.Property == DensityProperty) SyncFrame();
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
    void Refresh()
    {
        source = ItemsSource?.Cast<object>().ToList() ?? [];
        var hidden = HiddenColumns ?? [];
        Shown = [.. Columns.Where(column => !hidden.Contains(column.Key))];
        Types = [.. Shown.Select(column => TableColumns.TypeOf(column, source))];
        View = ManualSort || Sort is null ? source : TableColumns.SortRows(source, Columns, [Sort], Lang.Code);
        surface.Rebuild();
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

    /// <summary>点了列标题：下一个排序，写回 Sort。</summary>
    internal void ToggleSort(string key) => SetCurrentValue(SortProperty, TableColumns.NextSort(Sort, key));

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
        return frame.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        frame.Arrange(new Rect(finalSize));
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

/// <summary>
/// DataTable 滚动区域里的内容（上游 DataTableView 的 table）：只建看得见的行。表头按纵向偏移挪到视口顶上（吸顶），合计行挪到视口底下（吸底），
/// 固定列的格按横向偏移挪回边上。行高按第一行量出的高度。
/// </summary>
internal sealed class DataTableSurface : Control
{
    const int Overscan = 8;
    readonly DataTable owner;
    DataTableRowView? header;
    DataTableRowView? summary;
    Control? emptyView;
    readonly Dictionary<int, DataTableRowView> realized = [];
    readonly List<DataTableRowView> pool = [];
    readonly List<DataTableRowView> skeletons = [];
    double rowHeight;
    double[] widths = [];

    public DataTableSurface(DataTable owner) => this.owner = owner;

    /// <summary>表头（测试用）。</summary>
    internal DataTableRowView? Header => header;

    /// <summary>建好的行（测试用）：下标 → 行。</summary>
    internal IReadOnlyDictionary<int, DataTableRowView> Realized => realized;

    /// <summary>骨架行（测试用）。</summary>
    internal IReadOnlyList<DataTableRowView> Skeletons => skeletons;

    /// <summary>空状态（测试用）。</summary>
    internal Control? EmptyView => emptyView;

    /// <summary>合计行（测试用）。</summary>
    internal DataTableRowView? Summary => summary;

    internal double RowHeight => rowHeight;

    internal bool HasSelection => owner.SelectionMode is not null;

    internal double Leading => HasSelection ? DataTable.SelectWidth : 0;

    internal IReadOnlyList<double> Widths => widths;

    internal DataTable Owner => owner;

    Vector Offset => owner.Scroller.Offset;

    Size Viewport => owner.Scroller.Viewport;

    void Add(Control child)
    {
        LogicalChildren.Add(child);
        VisualChildren.Add(child);
    }

    void Remove(Control child)
    {
        LogicalChildren.Remove(child);
        VisualChildren.Remove(child);
    }

    /// <summary>列、数据、外观变了：表头、合计、骨架、空状态与建好的行都重建。</summary>
    internal void Rebuild()
    {
        foreach (var child in VisualChildren.OfType<Control>().ToList()) Remove(child);
        realized.Clear();
        pool.Clear();
        skeletons.Clear();
        emptyView = null;
        header = new DataTableRowView(this, DataTableRowKind.Header) { ZIndex = 2 };
        Add(header);
        summary = owner.Shown.Any(column => column.Summary is not null) ? new DataTableRowView(this, DataTableRowKind.Summary) { ZIndex = 2 } : null;
        if (summary is not null) Add(summary);
        if (owner.IsLoading)
            for (var i = 0; i < 6; i++)
            {
                var skeleton = new DataTableRowView(this, DataTableRowKind.Skeleton, i);
                skeletons.Add(skeleton);
                Add(skeleton);
            }
        else if (owner.View.Count == 0)
        {
            emptyView = owner.Empty as Control ?? new Empty { Name = DataTableParts.Empty, Size = TextSize.Sm, Title = owner.Empty as string ?? owner.Lang.TableEmpty };
            Add(emptyView);
        }
        rowHeight = 0;
        InvalidateMeasure();
    }

    /// <summary>选择、色调变了：建好的行与表头的全选框跟着改，不重建。</summary>
    internal void SyncRows()
    {
        header?.SyncSelection();
        foreach (var row in realized.Values) row.SyncSelection();
    }

    /// <summary>
    /// 滚动或视口变了：可见的行可能变了（重新量），固定列的格要按新的横向偏移挪（行的位置没变时平台不会重排它，要自己通知）。
    /// </summary>
    internal void Scrolled()
    {
        InvalidateMeasure();
        header?.InvalidateArrange();
        summary?.InvalidateArrange();
        foreach (var row in realized.Values) row.InvalidateArrange();
    }

    internal void ScrollTo(int index)
    {
        if (rowHeight <= 0) owner.UpdateLayout();
        owner.Scroller.Offset = new Vector(Offset.X, Math.Max(0, index * rowHeight));
    }

    /// <summary>各列的宽度：调整过的、列定义的；其余平分剩下的（最小宽度不一样时按 FlexColumnWidth），放不下最小宽度时横向滚动。</summary>
    void ComputeWidths(double viewport)
    {
        var columns = owner.Shown;
        double? WidthOf(DataTableColumn column) => owner.ColumnWidths?.TryGetValue(column.Key, out var own) == true ? own : column.Width;
        var total = Math.Max(viewport, TableColumns.TableMinWidth(columns, WidthOf, Leading));
        var flexible = columns.Where(column => WidthOf(column) is null).ToList();
        var rest = Math.Max(0, total - Leading - columns.Sum(column => WidthOf(column) ?? 0));
        widths = [.. columns.Select(column => WidthOf(column) ?? TableColumns.FlexColumnWidth(column, columns, WidthOf, Leading, viewport) ?? (flexible.Count > 0 ? rest / flexible.Count : 0))];
        // 各列都写了宽度、合起来比表格窄：按比例分掉多出来的（同浏览器 table-layout: fixed 的做法）
        var sum = widths.Sum();
        if (flexible.Count == 0 && sum > 0 && Leading + sum < viewport) widths = [.. widths.Select(width => width * (viewport - Leading) / sum)];
    }

    internal double TableWidth => Leading + widths.Sum();

    double HeaderHeight => header?.DesiredSize.Height ?? 0;

    double SummaryHeight => summary?.DesiredSize.Height ?? 0;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var viewport = Viewport.Width > 0 ? Viewport.Width : double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        ComputeWidths(viewport);
        var width = TableWidth;
        var infinite = new Size(width, double.PositiveInfinity);
        header?.Measure(infinite);
        summary?.Measure(infinite);
        var rows = owner.View;
        var body = 0.0;
        if (skeletons.Count > 0)
        {
            foreach (var skeleton in skeletons) skeleton.Measure(infinite);
            body = skeletons.Sum(skeleton => skeleton.DesiredSize.Height);
        }
        else if (emptyView is not null)
        {
            emptyView.Measure(new Size(Math.Max(0, (viewport > 0 ? viewport : width) - 2 * 16), double.PositiveInfinity));
            body = emptyView.DesiredSize.Height + 2 * 16;
        }
        else if (rows.Count > 0)
        {
            if (rowHeight <= 0)
            {
                var probe = Realize(0);
                probe.Measure(infinite);
                rowHeight = Math.Max(1, probe.DesiredSize.Height);
            }
            var viewportHeight = Viewport.Height > 0 ? Viewport.Height : 32 * TextElement.GetFontSize(this);
            var top = Math.Max(0, Offset.Y - HeaderHeight);
            var first = Math.Max(0, (int)Math.Floor(top / rowHeight) - Overscan);
            var last = Math.Min(rows.Count - 1, (int)Math.Ceiling((top + viewportHeight) / rowHeight) + Overscan);
            foreach (var index in realized.Keys.Where(index => index < first || index > last).ToList())
            {
                var view = realized[index];
                realized.Remove(index);
                view.IsVisible = false;
                pool.Add(view);
            }
            for (var index = first; index <= last; index++) Realize(index).Measure(infinite);
            body = rows.Count * rowHeight;
        }
        return new Size(width, HeaderHeight + body + SummaryHeight);
    }

    /// <summary>建（或从池里拿）第 index 行。</summary>
    DataTableRowView Realize(int index)
    {
        if (realized.TryGetValue(index, out var existing)) return existing;
        DataTableRowView view;
        if (pool.Count > 0)
        {
            view = pool[^1];
            pool.RemoveAt(pool.Count - 1);
            view.IsVisible = true;
        }
        else
        {
            view = new DataTableRowView(this, DataTableRowKind.Body);
            Add(view);
        }
        view.Bind(owner.View[index], index);
        realized[index] = view;
        return view;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Max(finalSize.Width, TableWidth);
        var headerHeight = HeaderHeight;
        header?.Arrange(new Rect(0, Offset.Y, width, headerHeight));
        var y = headerHeight;
        if (skeletons.Count > 0)
            foreach (var skeleton in skeletons)
            {
                skeleton.Arrange(new Rect(0, y, width, skeleton.DesiredSize.Height));
                y += skeleton.DesiredSize.Height;
            }
        else if (emptyView is not null)
        {
            // 空状态贴着视口居中（横向滚动时不跟着滚走）
            var visible = Viewport.Width > 0 ? Viewport.Width : width;
            emptyView.Arrange(new Rect(Offset.X + 16, y + 16, Math.Max(0, visible - 32), emptyView.DesiredSize.Height));
            y += emptyView.DesiredSize.Height + 32;
        }
        else
        {
            foreach (var (index, view) in realized) view.Arrange(new Rect(0, headerHeight + index * rowHeight, width, rowHeight));
            y += owner.View.Count * rowHeight;
        }
        if (summary is not null)
        {
            var viewportBottom = Offset.Y + (Viewport.Height > 0 ? Viewport.Height : finalSize.Height);
            summary.Arrange(new Rect(0, Math.Min(y, viewportBottom - SummaryHeight), width, SummaryHeight));
        }
        foreach (var view in pool) view.Arrange(default);
        return finalSize;
    }

    /// <summary>横向偏移（固定列的格按它挪回边上）。</summary>
    internal double OffsetX => Offset.X;

    /// <summary>视口宽度。</summary>
    internal double ViewportWidth => Viewport.Width > 0 ? Viewport.Width : Bounds.Width;
}

/// <summary>DataTable 的一行是哪种。</summary>
internal enum DataTableRowKind
{
    Header,
    Body,
    Skeleton,
    Summary,
}

/// <summary>
/// DataTable 的一行：自己画底色（表头 fill；表体随样式，悬停 fillHover，已选 accentSoft，色调的浅色底，斑马纹）与下边线，排各格（左右 space.md、上下按密度）。
/// 固定列的格画上行的底色、挪到视口的边上。
/// </summary>
internal sealed class DataTableRowView : Control
{
    readonly DataTableSurface surface;
    readonly List<(Control Cell, DataTableColumn? Column)> cells = [];
    Control? selector;
    bool hovered;

    public DataTableRowView(DataTableSurface surface, DataTableRowKind kind, int index = -1)
    {
        this.surface = surface;
        Kind = kind;
        Index = index;
        Name = kind switch
        {
            DataTableRowKind.Header => DataTableParts.HeaderRow,
            DataTableRowKind.Summary => DataTableParts.SummaryRow,
            _ => DataTableParts.Row,
        };
        if (kind is DataTableRowKind.Header or DataTableRowKind.Summary or DataTableRowKind.Skeleton) Build(null, index);
        if (kind == DataTableRowKind.Body)
        {
            PointerEntered += (_, _) => SetHovered(true);
            PointerExited += (_, _) => SetHovered(false);
        }
    }

    public DataTableRowKind Kind { get; }

    /// <summary>这一行的数据（表体）。</summary>
    public object? Row { get; private set; }

    /// <summary>整行的色调（RowTone）；没有时 null。</summary>
    internal StatusTone? Tone => Row is { } row ? Owner.RowTone?.Invoke(row) : null;

    /// <summary>在显示的行里的下标（表体）。</summary>
    public int Index { get; private set; } = -1;

    /// <summary>各格（测试用）：选择列（有时）在前，再是各数据列。</summary>
    internal IReadOnlyList<Control> Cells => [.. (selector is null ? [] : new[] { selector }), .. cells.Select(cell => cell.Cell)];

    /// <summary>这一行的选择框（测试用）。</summary>
    internal ChoiceBase? Choice => (selector as Panel)?.Children.OfType<ChoiceBase>().FirstOrDefault();

    DataTable Owner => surface.Owner;

    void Attach(Control child)
    {
        LogicalChildren.Add(child);
        VisualChildren.Add(child);
    }

    void Detach(Control child)
    {
        LogicalChildren.Remove(child);
        VisualChildren.Remove(child);
    }

    void SetHovered(bool value)
    {
        hovered = value;
        InvalidateVisual();
    }

    /// <summary>表体的行换成另一行的数据（回收）。</summary>
    public void Bind(object row, int index)
    {
        if (Equals(Row, row) && Index == index) return;
        Row = row;
        Index = index;
        DataContext = row;
        Build(row, index);
    }

    /// <summary>建各格。</summary>
    void Build(object? row, int index)
    {
        foreach (var child in VisualChildren.OfType<Control>().ToList()) Detach(child);
        cells.Clear();
        selector = null;
        var owner = Owner;
        var lang = owner.Lang;
        if (surface.HasSelection)
        {
            selector = SelectCell(row, index, lang);
            Attach(selector);
        }
        var columns = owner.Shown;
        for (var position = 0; position < columns.Count; position++)
        {
            var column = columns[position];
            var align = ColumnTable.AlignOf(column, owner.Types, position);
            TableCell cell = Kind switch
            {
                DataTableRowKind.Header => HeaderCell(column, align),
                DataTableRowKind.Summary => new TableCell { Name = DataTableParts.SummaryCell, Align = align, Nowrap = true, Content = SummaryContent(column, position, lang) },
                DataTableRowKind.Skeleton => new TableCell { Name = DataTableParts.Cell, Content = new Skeleton { HorizontalAlignment = HorizontalAlignment.Left } },
                _ => new TableCell
                {
                    Name = DataTableParts.Cell, Align = align, Nowrap = !column.Wrap,
                    Content = column.Cell?.Invoke(row!, index) ?? ColumnTable.Display(column, owner.Types[position], TableColumns.ValueOf(column, row), lang),
                },
            };
            cell.ApplyRole(head: Kind == DataTableRowKind.Header, foot: Kind == DataTableRowKind.Summary);
            TextBlock.SetTextTrimming(cell, TextTrimming.CharacterEllipsis);
            if (column.Sticky is not null) cell.ZIndex = 1;
            cells.Add((cell, column));
            Attach(cell);
        }
        SyncSelection();
        InvalidateMeasure();
    }

    TableHeaderCell HeaderCell(DataTableColumn column, TableAlign align)
    {
        var cell = new TableHeaderCell { Name = DataTableParts.HeaderCell, Align = align, Scope = "col" };
        var sort = Owner.Sort?.Key == column.Key ? Owner.Sort.Direction : (SortDirection?)null;
        if (!column.Sortable)
        {
            cell.Content = column.Header;
            return cell;
        }
        var icon = new Icon
        {
            Name = DataTableParts.SortIcon,
            Data = sort switch { SortDirection.Ascending => Lucide.ArrowUp, SortDirection.Descending => Lucide.ArrowDown, _ => Lucide.ArrowUpDown },
        };
        if (sort is not null) icon[!Icon.ForegroundProperty] = this.GetResourceObservable(ThemeKeys.Brush.AccentText).ToBinding();
        var button = new ButtonBase
        {
            Name = DataTableParts.SortButton,
            Content = new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new TextBlock { Text = column.Header as string, TextTrimming = TextTrimming.CharacterEllipsis }, icon } },
            HorizontalAlignment = align == TableAlign.End ? HorizontalAlignment.Right : align == TableAlign.Center ? HorizontalAlignment.Center : HorizontalAlignment.Left,
        };
        if (column.Header is not string) ((Flex)button.Content).Children[0] = new ContentControl { Content = column.Header };
        button.Click += (_, _) => Owner.ToggleSort(column.Key);
        cell.Content = button;
        // 上游 th 的 aria-sort：读屏念出排序的方向
        AutomationProperties.SetItemStatus(cell, sort switch { SortDirection.Ascending => "ascending", SortDirection.Descending => "descending", _ => "none" });
        return cell;
    }

    object? SummaryContent(DataTableColumn column, int position, Lang lang) => column.Summary switch
    {
        { Compute: { } compute } => compute(Owner.View),
        { } => TableColumns.Sum(column, Owner.View) is { } total ? ColumnTable.Display(column, Owner.Types[position], total, lang) : column.Empty ?? lang.EmptyValue,
        null when position == 0 => Owner.SummaryLabel ?? lang.TableSummary,
        _ => null,
    };

    /// <summary>选择列的格：表头是全选框（多选时），表体是勾选框或单选框，骨架行是空的。</summary>
    Control SelectCell(object? row, int index, Lang lang)
    {
        var host = new Panel { Name = Kind == DataTableRowKind.Header ? DataTableParts.SelectHeaderCell : DataTableParts.SelectCell };
        var owner = Owner;
        ChoiceBase? choice = null;
        if (Kind == DataTableRowKind.Header && owner.SelectionMode == SelectionMode.Multiple)
        {
            choice = new Checkbox { Name = DataTableParts.SelectAll };
            AutomationProperties.SetName(choice, lang.TableSelectAll);
            choice.Toggling += (_, e) =>
            {
                e.Cancel = true;
                owner.ToggleAll();
            };
        }
        else if (Kind == DataTableRowKind.Body)
        {
            choice = owner.SelectionMode == SelectionMode.Multiple ? new Checkbox { Name = DataTableParts.SelectCheckbox } : new Radio { Name = DataTableParts.SelectRadio };
            AutomationProperties.SetName(choice, lang.TableSelectRow(index + 1));
            choice.Toggling += (_, e) =>
            {
                e.Cancel = true;
                if (Row is { } current) owner.ToggleRow(current);
            };
        }
        if (choice is not null)
        {
            choice.HorizontalAlignment = HorizontalAlignment.Center;
            choice.VerticalAlignment = VerticalAlignment.Top;
            host.Children.Add(choice);
        }
        return host;
    }

    /// <summary>选择框的状态与这一行是否已选（整行底色）。</summary>
    public void SyncSelection()
    {
        var owner = Owner;
        if (Choice is Checkbox { Name: DataTableParts.SelectAll } all)
        {
            var (every, some) = owner.SelectionState();
            all.IsChecked = every;
            all.IsIndeterminate = some;
        }
        else if (Choice is { } choice && Row is { } row)
        {
            choice.IsChecked = owner.IsSelected(row);
            choice.IsEnabled = owner.CanSelect(row);
        }
        InvalidateVisual();
    }

    double Em => TextElement.GetFontSize(this);

    /// <summary>格的内边距：左右 space.md、上下按密度（同静态表格）。</summary>
    Thickness PaddingOf(Control cell)
    {
        var em = TextElement.GetFontSize(cell);
        var density = Owner.Density;
        var block = density == TableDensity.Compact ? 0.25 : density == TableDensity.Comfortable ? 0.75 : 0.5;
        return new Thickness(0.75 * em, block * em);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var widths = surface.Widths;
        var height = 0.0;
        // 选择框不撑高这一行（上游 choiceInCell：24px 的点击区域在行里叠放），有选择列的行与纯文字的行一样高
        selector?.Measure(new Size(DataTable.SelectWidth, double.PositiveInfinity));
        for (var i = 0; i < cells.Count && i < widths.Count; i++)
        {
            var padding = PaddingOf(cells[i].Cell);
            // 骨架：高 1em，宽度按行与列错开（90%、75%、60%、45%，同上游）
            if (cells[i].Cell is TableCell { Content: Skeleton bar })
                (bar.Width, bar.Height) = (Math.Max(0, widths[i] - padding.Left - padding.Right) * (90 - (Index + i) % 4 * 15) / 100, TextElement.GetFontSize(cells[i].Cell));
            cells[i].Cell.Measure(new Size(Math.Max(0, widths[i] - padding.Left - padding.Right), double.PositiveInfinity));
            height = Math.Max(height, cells[i].Cell.DesiredSize.Height + padding.Top + padding.Bottom);
        }
        return new Size(surface.TableWidth, height + 1);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var widths = surface.Widths;
        var offsets = TableColumns.StickyOffsetsOf(Owner.Shown, column => widths[IndexOf(column)], surface.Leading);
        var height = finalSize.Height - 1;
        var x = 0.0;
        if (selector is not null)
        {
            // 选择框的第一行（1.5em，框画在它的正中）对着这一行的正中；24px 的点击区域多出的部分在下面，不撑高这一行
            var top = (height - 1.5 * TextElement.GetFontSize(selector)) / 2;
            selector.Arrange(new Rect(0, top, DataTable.SelectWidth, Math.Max(selector.DesiredSize.Height, height - top)));
            x = DataTable.SelectWidth;
        }
        var scroll = surface.OffsetX;
        var view = surface.ViewportWidth;
        for (var i = 0; i < cells.Count && i < widths.Count; i++)
        {
            var (cell, column) = cells[i];
            var left = x;
            // 固定列：挪到视口的边上（起始边不早于偏移，结束边不晚于视口的右边）
            if (column?.Sticky == ColumnSticky.Start && offsets.Start.TryGetValue(column.Key, out var start)) left = Math.Max(x, scroll + start);
            else if (column?.Sticky == ColumnSticky.End && offsets.End.TryGetValue(column.Key, out var end)) left = Math.Min(x, scroll + view - end - widths[i]);
            var padding = PaddingOf(cell);
            cell.Arrange(new Rect(left + padding.Left, padding.Top, Math.Max(0, widths[i] - padding.Left - padding.Right), Math.Max(0, height - padding.Top - padding.Bottom)));
            x += widths[i];
        }
        return finalSize;
    }

    int IndexOf(DataTableColumn column)
    {
        for (var i = 0; i < Owner.Shown.Count; i++)
            if (ReferenceEquals(Owner.Shown[i], column)) return i;
        return 0;
    }

    /// <summary>行的底色：表头、合计 fill；已选 accentSoft；色调的浅色底；悬停 fillHover；斑马纹隔行 fill；其余随表格的样式。</summary>
    IBrush? RowBrush()
    {
        var owner = Owner;
        string key;
        if (Kind is DataTableRowKind.Header or DataTableRowKind.Summary) key = ThemeKeys.Brush.Fill;
        else if (Row is { } row && owner.IsSelected(row)) key = hovered ? ThemeKeys.Brush.AccentSoftHover : ThemeKeys.Brush.AccentSoft;
        else if (Row is { } toned && owner.RowTone?.Invoke(toned) is { } tone && ToneBrush(tone) is { } soft) key = soft;
        else if (hovered) key = ThemeKeys.Brush.FillHover;
        else if (owner.Striped && Index % 2 == 1) key = ThemeKeys.Brush.Fill;
        else key = owner.Variant == TableVariant.Ghost ? ThemeKeys.Brush.Background : ThemeKeys.Brush.Surface;
        return this.Resource<IBrush?>(key);

        string? ToneBrush(StatusTone tone) => tone == StatusTone.Info ? ThemeKeys.Brush.InfoSoft
            : tone == StatusTone.Success ? ThemeKeys.Brush.SuccessSoft
            : tone == StatusTone.Warning ? ThemeKeys.Brush.WarningSoft
            : tone == StatusTone.Danger ? ThemeKeys.Brush.DangerSoft : null;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var edges = TableColumns.StickyOffsetsOf(Owner.Shown, column => surface.Widths[IndexOf(column)], surface.Leading);
        if (RowBrush() is { } fill)
        {
            context.FillRectangle(fill, bounds);
            // 固定列的格盖住滚过去的内容：在它们的位置再铺一层底色（格本身透明）
            foreach (var (cell, column) in cells)
                if (column?.Sticky is not null)
                {
                    var padding = PaddingOf(cell);
                    context.FillRectangle(fill, new Rect(cell.Bounds.X - padding.Left, 0, cell.Bounds.Width + padding.Left + padding.Right, bounds.Height));
                }
        }
        // 固定列与滚动的列之间一条竖线：横向滚动时看得出哪几列是固定的
        if (this.Resource<IBrush?>(ThemeKeys.Brush.BorderMuted) is { } divider)
            foreach (var (cell, column) in cells)
            {
                if (column is null) continue;
                var padding = PaddingOf(cell);
                if (column.Key == edges.StartEdge) context.FillRectangle(divider, new Rect(cell.Bounds.Right + padding.Right - 1, 0, 1, bounds.Height));
                else if (column.Key == edges.EndEdge) context.FillRectangle(divider, new Rect(cell.Bounds.X - padding.Left, 0, 1, bounds.Height));
            }
        // 下边线：表头 border，表体 borderMuted；合计行的上边线是 border
        var line = Kind == DataTableRowKind.Header || Kind == DataTableRowKind.Summary ? ThemeKeys.Brush.Border : ThemeKeys.Brush.BorderMuted;
        if (this.Resource<IBrush?>(line) is { } stroke)
        {
            if (Kind == DataTableRowKind.Summary) context.FillRectangle(stroke, new Rect(0, 0, bounds.Width, 1));
            else context.FillRectangle(stroke, new Rect(0, bounds.Height - 1, bounds.Width, 1));
        }
    }
}

/// <summary>DataTable 的部件名（TS 的 propsHeaderCell、propsRow、propsCell 等）：样式写 <c>rune|TableCell#PART_Cell</c>，按行的数据写样式用行的 DataContext。</summary>
public static class DataTableParts
{
    /// <summary>表头那一行。</summary>
    public const string HeaderRow = "PART_HeaderRow";

    /// <summary>列标题。</summary>
    public const string HeaderCell = "PART_HeaderCell";

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
