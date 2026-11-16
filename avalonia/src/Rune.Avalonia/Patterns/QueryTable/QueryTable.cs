using System.Collections;
using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;

namespace Rune.Avalonia;

/// <summary>
/// 查询表格（vue-tsx-pro 的 QueryTable，同 Ant Design ProComponents 的 ProTable）：查询栏（FilterBar）+ 工具栏（标题、自己的按钮、刷新、行高、列设置）
/// + 批量操作栏 + 数据表格 + 分页栏，状态都在 <see cref="TableQuery{T, TFilters}"/> 里（服务端分页、排序、条件）。
/// <code>
/// var tasks = new TableQuery&lt;TaskRow, TaskFilters&gt;(FetchTasks, new TaskFilters(), pageSize: 20);
/// var page = new QueryTable
/// {
///     Query = tasks, Columns = columns, GetKey = row =&gt; ((TaskRow)row).Id, Title = "任务台账",
///     Filters = draft =&gt; [new Field { Label = "关键字", Content = KeywordBox((TaskFilters)draft) }],
///     Toolbar = new Button { Icon = Lucide.Plus, Content = "新建" },
/// };
/// _ = tasks.Refresh();
/// </code>
/// <list type="bullet">
/// <item>查询栏的条件改的是草稿（<see cref="Filters"/> 拿到的对象，是已应用条件的一份浅拷贝）：点"查询"（或回车）才交给 Query.Search；
/// "重置"回到 <see cref="BlankFilters"/>（不写时是一开始的条件），重新建出条件控件并查询。筛选条件要是可写属性的类（或字典），控件改它的字段。</item>
/// <item>有查询栏时标题与工具栏并进查询栏的第一排：标题 常用条件 … 展开 重置 查询 │ 自己的按钮 刷新 行高 列设置；不常用的条件放进 <see cref="MoreFilters"/>，
/// 点"展开"后排在第二排起。没有查询栏时工具栏是表格上方单独的一行。</item>
/// <item>表头排序、表头筛选交给服务端（ManualSort、ManualFilter，写回 Query 的 Sort / Sorts、ColumnFilters），分页栏绑定 Query 的 Page、PageSize、Total。</item>
/// <item><see cref="SelectionMode"/> 为 multiple 时勾选行，表格上方出现批量操作栏，<see cref="BatchActions"/> 拿到已选的 key 给出按钮。</item>
/// <item><see cref="Fill"/> 时整块占满到窗口底边（ViewportFill），行多了在表格里滚动，分页留在一屏里。加载失败时表格上方有提示条与"重试"，保留上一次的行。</item>
/// </list>
/// 桌面上与上游不同的：表格、查询栏、分页栏是 <see cref="Table"/>、<see cref="FilterBar"/>、<see cref="Pagination"/>，详情行、行的色调等直接写在它们上面（上游的 propsTable 等）；
/// 列设置与行高是 Table 的 HiddenColumns、ColumnOrder、ColumnPins、ColumnWidths、Density，要记住时调用方存下来（上游 persistKey 记在浏览器里）；
/// 全屏（上游浏览器的 Fullscreen API）没有：窗口自己可以最大化。
/// 部件：<see cref="QueryTableParts"/>。
/// </summary>
public class QueryTable : Control
{
    /// <summary>列表状态（TableQuery）。</summary>
    public static readonly StyledProperty<ITableQuery?> QueryProperty = AvaloniaProperty.Register<QueryTable, ITableQuery?>(nameof(Query));

    /// <summary>列定义。</summary>
    public static readonly StyledProperty<IReadOnlyList<DataTableColumn>> ColumnsProperty = AvaloniaProperty.Register<QueryTable, IReadOnlyList<DataTableColumn>>(nameof(Columns), []);

    /// <summary>行的 key。</summary>
    public static readonly StyledProperty<Func<object, object>?> GetKeyProperty = AvaloniaProperty.Register<QueryTable, Func<object, object>?>(nameof(GetKey));

    /// <summary>表格的名称：工具栏起始边的标题，也是表格的读屏名称。</summary>
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<QueryTable, string>(nameof(Title), "");

    /// <summary>查询栏的条件：拿到草稿，返回各个条件（一般是 Field）；不写时没有查询栏。</summary>
    public static readonly StyledProperty<Func<object, IEnumerable<Control>>?> FiltersProperty = AvaloniaProperty.Register<QueryTable, Func<object, IEnumerable<Control>>?>(nameof(Filters));

    /// <summary>收起的条件（不常用的），点"展开"才显示。</summary>
    public static readonly StyledProperty<Func<object, IEnumerable<Control>>?> MoreFiltersProperty = AvaloniaProperty.Register<QueryTable, Func<object, IEnumerable<Control>>?>(nameof(MoreFilters));

    /// <summary>"重置"回到的条件；不写时是一开始的条件。</summary>
    public static readonly StyledProperty<object?> BlankFiltersProperty = AvaloniaProperty.Register<QueryTable, object?>(nameof(BlankFilters));

    /// <summary>占满到窗口底边（列表页的整页）：行多了在表格里滚动，分页一直在一屏里。</summary>
    public static readonly StyledProperty<bool> FillProperty = AvaloniaProperty.Register<QueryTable, bool>(nameof(Fill));

    /// <summary>工具栏上自己的按钮（新建、导出），在标题之后、内置按钮之前。</summary>
    public static readonly StyledProperty<object?> ToolbarProperty = AvaloniaProperty.Register<QueryTable, object?>(nameof(Toolbar));

    /// <summary>多列排序：按住 Shift 点列标题追加一列，排序写在 Query.Sorts。</summary>
    public static readonly StyledProperty<bool> MultiSortProperty = AvaloniaProperty.Register<QueryTable, bool>(nameof(MultiSort));

    /// <summary>勾选行：多选时表格上方出现批量操作栏。</summary>
    public static readonly StyledProperty<SelectionMode?> SelectionModeProperty = AvaloniaProperty.Register<QueryTable, SelectionMode?>(nameof(SelectionMode));

    /// <summary>已选行的 key（双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<object>> SelectedProperty =
        AvaloniaProperty.Register<QueryTable, IReadOnlyList<object>>(nameof(Selected), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>批量操作栏里的按钮：拿到已选的 key。</summary>
    public static readonly StyledProperty<Func<IReadOnlyList<object>, IEnumerable<Control>>?> BatchActionsProperty =
        AvaloniaProperty.Register<QueryTable, Func<IReadOnlyList<object>, IEnumerable<Control>>?>(nameof(BatchActions));

    readonly Flex root = new() { Name = QueryTableParts.Root, Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch };
    readonly Flex panel = new() { Name = QueryTableParts.Panel, Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch };
    readonly Flex toolbarRow = new() { Name = QueryTableParts.ToolbarRow, Align = FlexAlign.Center, Wrap = true, Gap = Space.Sm };
    readonly Flex toolbar = new() { Name = QueryTableParts.Toolbar, Align = FlexAlign.Center, Wrap = true, Gap = Space.Sm, HorizontalAlignment = HorizontalAlignment.Right };
    readonly ContentControl custom = new();
    readonly Flex tools = new() { Name = QueryTableParts.Tools, Align = FlexAlign.Center, Gap = Space.Xs };
    readonly IconButton refresh = ToolButton(Lucide.RefreshCw);
    readonly MenuRadioGroup density = new() { Value = nameof(TableDensity.Standard) };
    readonly Menu densityMenu;
    readonly ColumnSettings settings = new() { Name = QueryTableParts.ColumnSettings };
    readonly SelectionBar selectionBar = new() { Name = QueryTableParts.SelectionBar };
    readonly Alert error = new() { Name = QueryTableParts.Error, Tone = StatusTone.Danger, Live = AlertLive.Assertive };
    readonly Button retry = new() { Name = QueryTableParts.Retry, Size = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Danger };
    ViewportFill? fill;
    ITableQuery? observed;
    object? draft;
    object? initial;
    // 正在把 Query 的状态写到表格、分页上：表格随即发的变化不再写回 Query
    bool syncing;

    /// <summary>新建：查询栏、工具栏、批量操作栏、表格、分页栏，等 Query 接上。</summary>
    public QueryTable()
    {
        FilterBar = new FilterBar { Name = QueryTableParts.FilterBar };
        Table = new DataTable { Name = QueryTableParts.Table, Resizable = true, ColumnMenu = true, ManualSort = true, ManualFilter = true };
        Pagination = new PaginationBar { Name = QueryTableParts.Pagination };
        FilterBar.SearchRequested += (_, _) => Search();
        FilterBar.ResetRequested += (_, _) => Reset();
        refresh.Click += (_, _) => _ = Query?.Refresh();
        retry.Click += (_, _) => _ = Query?.Refresh();
        error.Action = retry;
        foreach (var level in new[] { TableDensity.Compact, TableDensity.Standard, TableDensity.Comfortable })
            density.Items.Add(new MenuRadioItem { Value = level.ToString() });
        density.PropertyChanged += (_, e) =>
        {
            if (e.Property == MenuRadioGroup.ValueProperty && Enum.TryParse<TableDensity>(density.Value as string, out var level)) Table.Density = level;
        };
        densityMenu = new Menu { Trigger = ToolButton(Lucide.Rows3), Placement = FloatingPlacement.BottomEnd, Items = { density } };
        settings.ResetRequested += (_, _) =>
        {
            Table.ColumnPins = null;
            Table.ColumnWidths = null;
        };
        // 列设置与表格绑同一份隐藏列、列顺序
        (Table.HiddenColumns, Table.ColumnOrder) = ([], []);
        settings.Bind(ColumnSettings.HiddenProperty, new Binding(nameof(DataTable.HiddenColumns)) { Source = Table, Mode = BindingMode.TwoWay });
        settings.Bind(ColumnSettings.OrderProperty, new Binding(nameof(DataTable.ColumnOrder)) { Source = Table, Mode = BindingMode.TwoWay });
        tools.Children.AddRange([refresh, densityMenu, settings]);
        toolbar.Children.AddRange([custom, tools]);
        selectionBar.ClearRequested += (_, _) => SetCurrentValue(SelectedProperty, Array.Empty<object>());
        Table.PropertyChanged += OnTableChanged;
        Pagination.PropertyChanged += (_, e) =>
        {
            if (syncing || Query is not { } query) return;
            if (e.Property == PaginationBar.PageProperty) query.Page = Pagination.Page;
            else if (e.Property == PaginationBar.PageSizeProperty) query.PageSize = Pagination.PageSize;
        };
        panel.Children.AddRange([toolbarRow, selectionBar, error, Table, Pagination]);
        root.Children.AddRange([FilterBar, panel]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        SyncLang();
        SyncLayout();
        SyncSelection();
        SyncQuery();
    }

    /// <summary>列表状态。</summary>
    public ITableQuery? Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }

    /// <summary>列定义。</summary>
    public IReadOnlyList<DataTableColumn> Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>行的 key。</summary>
    public Func<object, object>? GetKey { get => GetValue(GetKeyProperty); set => SetValue(GetKeyProperty, value); }

    /// <summary>表格的名称。</summary>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>查询栏的条件。</summary>
    public Func<object, IEnumerable<Control>>? Filters { get => GetValue(FiltersProperty); set => SetValue(FiltersProperty, value); }

    /// <summary>收起的条件。</summary>
    public Func<object, IEnumerable<Control>>? MoreFilters { get => GetValue(MoreFiltersProperty); set => SetValue(MoreFiltersProperty, value); }

    /// <summary>"重置"回到的条件。</summary>
    public object? BlankFilters { get => GetValue(BlankFiltersProperty); set => SetValue(BlankFiltersProperty, value); }

    /// <summary>占满到窗口底边。</summary>
    public bool Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <summary>工具栏上自己的按钮。</summary>
    public object? Toolbar { get => GetValue(ToolbarProperty); set => SetValue(ToolbarProperty, value); }

    /// <summary>多列排序。</summary>
    public bool MultiSort { get => GetValue(MultiSortProperty); set => SetValue(MultiSortProperty, value); }

    /// <summary>勾选行。</summary>
    public SelectionMode? SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>已选行的 key。</summary>
    public IReadOnlyList<object> Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }

    /// <summary>批量操作栏里的按钮。</summary>
    public Func<IReadOnlyList<object>, IEnumerable<Control>>? BatchActions { get => GetValue(BatchActionsProperty); set => SetValue(BatchActionsProperty, value); }

    /// <summary>
    /// 表格（上游的 propsTable）：详情行、行的色调、编辑等直接写在它上面；数据、排序、筛选、勾选、加载由这里接到 Query 上。
    /// 列设置与行高也在它上面（HiddenColumns、ColumnOrder、ColumnPins、ColumnWidths、Density），要记住时存下来、下次设回去。
    /// </summary>
    public DataTable Table { get; }

    /// <summary>查询栏（上游的 propsFilterBar）：标签宽度、条件的最小宽度等写在它上面。</summary>
    public FilterBar FilterBar { get; }

    /// <summary>分页栏（上游的 propsPagination）：每页条数的选项等写在它上面。</summary>
    public PaginationBar Pagination { get; }

    /// <summary>查询栏改的草稿（测试用）。</summary>
    internal object? Draft => draft;

    /// <summary>刷新按钮（测试用）。</summary>
    internal IconButton RefreshButton => refresh;

    /// <summary>列设置（测试用）。</summary>
    internal ColumnSettings Settings => settings;

    /// <summary>工具栏（测试用）。</summary>
    internal Flex ToolbarPanel => toolbar;

    static IconButton ToolButton(IconData icon) => new() { Name = QueryTableParts.ToolbarButton, Icon = icon, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == QueryProperty)
        {
            if (observed is not null) observed.PropertyChanged -= OnQueryChanged;
            observed = Query;
            if (observed is not null) observed.PropertyChanged += OnQueryChanged;
            initial = CloneFilters(observed?.AppliedFilters);
            draft = CloneFilters(initial);
            SyncFilters();
            SyncQuery();
        }
        else if (change.Property == ColumnsProperty)
        {
            Table.Columns = Columns;
            settings.Columns = Columns;
        }
        else if (change.Property == GetKeyProperty) Table.GetKey = GetKey;
        else if (change.Property == TitleProperty || change.Property == Provider.LangProperty) SyncLang();
        else if (change.Property == FiltersProperty || change.Property == MoreFiltersProperty)
        {
            SyncFilters();
            SyncLayout();
        }
        else if (change.Property == FillProperty) SyncLayout();
        else if (change.Property == ToolbarProperty) custom.Content = Toolbar;
        else if (change.Property == MultiSortProperty) Table.MultiSort = MultiSort;
        else if (change.Property == SelectionModeProperty || change.Property == SelectedProperty || change.Property == BatchActionsProperty) SyncSelection();
    }

    void OnQueryChanged(object? sender, PropertyChangedEventArgs e) => SyncQuery();

    /// <summary>表格上的排序、表头筛选、勾选写回 Query 与 Selected。</summary>
    void OnTableChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (syncing) return;
        if (e.Property == DataTable.SelectedProperty) SetCurrentValue(SelectedProperty, Table.Selected);
        if (Query is not { } query) return;
        if (e.Property == DataTable.SortProperty) query.Sort = Table.Sort;
        else if (e.Property == DataTable.SortsProperty) query.Sorts = Table.Sorts;
        else if (e.Property == DataTable.FiltersProperty) query.ColumnFilters = Table.Filters ?? new Dictionary<string, IReadOnlyList<string>>();
        else if (e.Property == DataTable.ColumnPinsProperty || e.Property == DataTable.ColumnWidthsProperty)
            settings.Changed = Table.ColumnPins is { Count: > 0 } || Table.ColumnWidths is { Count: > 0 };
    }

    /// <summary>Query 的状态写到表格、分页栏、出错提示上。</summary>
    void SyncQuery()
    {
        syncing = true;
        try
        {
            var query = Query;
            Table.ItemsSource = query?.RowItems ?? [];
            Table.IsLoading = query?.IsLoading ?? false;
            Table.Sort = query?.Sort;
            Table.Sorts = query?.Sorts ?? [];
            Table.Filters = query?.ColumnFilters;
            Pagination.Page = query?.Page ?? 1;
            Pagination.PageSize = query?.PageSize ?? 10;
            Pagination.Total = query?.Total ?? 0;
            var lang = Provider.GetLang(this);
            error.IsVisible = query?.Error is not null;
            error.Content = query?.Error is { } failure ? ErrorText(failure, lang) : null;
            // 加载失败时没有行不等于没有数据：不写"暂无数据"
            Table.Empty = query?.Error is not null ? lang.ListLoadFailed : null;
        }
        finally
        {
            syncing = false;
        }
    }

    /// <summary>给人看的错误文案（上游的 errorMessage）：错误自己的说明，没有时用语言包的"请求失败"。桌面上没有 HTTP 的业务错误码。</summary>
    static string ErrorText(Exception failure, Lang lang) => failure.Message is { Length: > 0 } message ? message : lang.RequestFailed;

    /// <summary>标题、按钮的名称、地标的名称跟着语言与 Title。</summary>
    void SyncLang()
    {
        var messages = PatternsMessages.For(Provider.GetLang(this));
        AutomationProperties.SetName(Table, Title);
        AutomationProperties.SetName(FilterBar, messages.QueryTableSearch(Title));
        AutomationProperties.SetName(Pagination.PaginationPart, messages.QueryTablePagination(Title));
        AutomationProperties.SetName(refresh, messages.QueryTableRefresh);
        AutomationProperties.SetName(densityMenu.Trigger!, messages.QueryTableDensity);
        string[] labels = [messages.QueryTableDensityCompact, messages.QueryTableDensityStandard, messages.QueryTableDensityComfortable];
        for (var at = 0; at < labels.Length; at++) ((MenuRadioItem)density.Items[at]).Content = labels[at];
        retry.Content = messages.QueryTableRetry;
        SyncLayout();
    }

    /// <summary>标题与工具栏放在查询栏的第一排（有查询栏时）或表格上方单独的一行；Fill 时外面包一层 ViewportFill。</summary>
    void SyncLayout()
    {
        var withFilters = Filters is not null;
        FilterBar.IsVisible = withFilters;
        FilterBar.Start = null;
        FilterBar.End = null;
        toolbarRow.Children.Clear();
        if (withFilters)
        {
            FilterBar.Start = Heading(Title);
            FilterBar.End = toolbar;
        }
        else toolbarRow.Children.AddRange([Heading(Title), toolbar]);
        toolbarRow.IsVisible = !withFilters;
        // 占满时表格不再限 32em：按行数自己的高度，最多占到剩下的高度（上游 flexible）
        if (Fill) Table.MaxHeight = double.PositiveInfinity;
        else Table.ClearValue(MaxHeightProperty);
        SyncFill();

        static Text Heading(string text) => new() { Name = QueryTableParts.Title, Text = text, Weight = TextWeight.Semibold, Size = TextSize.Lg };
    }

    void SyncFill()
    {
        var host = (Control?)fill ?? root;
        var wanted = Fill;
        if (wanted == fill is not null) return;
        LogicalChildren.Remove(host);
        VisualChildren.Remove(host);
        if (fill is not null)
        {
            fill.Children.Remove(root);
            fill = null;
            LogicalChildren.Add(root);
            VisualChildren.Add(root);
            return;
        }
        Flex.SetGrow(root, true);
        fill = new ViewportFill { Children = { root } };
        LogicalChildren.Add(fill);
        VisualChildren.Add(fill);
    }

    /// <summary>查询栏的条件按草稿重新建（Query、条件的写法换了，或"重置"之后）。</summary>
    void SyncFilters()
    {
        FilterBar.Fields.Clear();
        FilterBar.More.Clear();
        if (draft is null && Query is null) return;
        if (Filters is { } fields) FilterBar.Fields.AddRange(fields(draft!));
        if (MoreFilters is { } more) FilterBar.More.AddRange(more(draft!));
    }

    void SyncSelection()
    {
        Table.SelectionMode = SelectionMode;
        Table.Selected = Selected;
        selectionBar.IsVisible = SelectionMode == Rune.Avalonia.SelectionMode.Multiple;
        selectionBar.Count = Selected.Count;
        selectionBar.Actions.Clear();
        if (selectionBar.IsVisible && BatchActions is { } actions) selectionBar.Actions.AddRange(actions(Selected));
    }

    /// <summary>查询栏的"查询"：草稿的一份拷贝交给 Query（之后再改草稿不影响已应用的条件）。</summary>
    void Search() => Query?.Search(CloneFilters(draft));

    /// <summary>"重置"：草稿回到 BlankFilters（不写时是一开始的条件），条件控件按它重建，再查询。</summary>
    void Reset()
    {
        draft = CloneFilters(BlankFilters ?? initial);
        SyncFilters();
        Search();
    }

    /// <summary>筛选条件的浅拷贝：字典复制一份，对象按字段复制（条件里的值是字符串、日期、数字、条件树这类不可变的值）。</summary>
    internal static object? CloneFilters(object? filters) => filters switch
    {
        null => null,
        IDictionary dictionary when filters.GetType().GetConstructor(Type.EmptyTypes) is { } create && create.Invoke(null) is IDictionary copy => Copy(dictionary, copy),
        _ => MemberwiseCloneMethod.Invoke(filters, null),
    };

    static IDictionary Copy(IDictionary from, IDictionary to)
    {
        foreach (DictionaryEntry entry in from) to[entry.Key] = entry.Value;
        return to;
    }

    static readonly MethodInfo MemberwiseCloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var host = (Control?)fill ?? root;
        host.Measure(availableSize);
        return host.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        ((Control?)fill ?? root).Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>QueryTable 的部件名（TS 的 propsPanel、propsToolbar 等）。</summary>
public static class QueryTableParts
{
    /// <summary>外层（竖排的 Flex）。</summary>
    public const string Root = "PART_Root";

    /// <summary>查询栏（FilterBar）。</summary>
    public const string FilterBar = "PART_FilterBar";

    /// <summary>工具栏与表格所在的区域（Flex）。</summary>
    public const string Panel = "PART_Panel";

    /// <summary>没有查询栏时表格上方单独的一行（Flex）。</summary>
    public const string ToolbarRow = "PART_ToolbarRow";

    /// <summary>工具栏：自己的按钮与内置按钮（Flex）。</summary>
    public const string Toolbar = "PART_Toolbar";

    /// <summary>内置按钮的一组（Flex）。</summary>
    public const string Tools = "PART_Tools";

    /// <summary>内置按钮（刷新、行高，IconButton；TS 的 propsToolbarButton）。</summary>
    public const string ToolbarButton = "PART_ToolbarButton";

    /// <summary>列设置（ColumnSettings）。</summary>
    public const string ColumnSettings = "PART_ColumnSettings";

    /// <summary>标题（Text）。</summary>
    public const string Title = "PART_Title";

    /// <summary>表格（DataTable）。</summary>
    public const string Table = "PART_Table";

    /// <summary>批量操作栏（SelectionBar）。</summary>
    public const string SelectionBar = "PART_SelectionBar";

    /// <summary>加载失败的提示条（Alert；TS 的 propsError）。</summary>
    public const string Error = "PART_Error";

    /// <summary>提示条里的"重试"（Button；TS 的 propsRetry）。</summary>
    public const string Retry = "PART_Retry";

    /// <summary>分页栏（PaginationBar）。</summary>
    public const string Pagination = "PART_Pagination";
}
