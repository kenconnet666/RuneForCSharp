using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// QueryTable：查询栏改草稿、点"查询"才交给 Query.Search，"重置"回到一开始的条件；工具栏的刷新、列设置；表格名称是 Title，分页绑定 Query 的总数；
/// 勾选后出现批量操作栏；有查询栏时工具栏并进第一排；MoreFilters 收起；Fill 时表格不再限高。
/// </summary>
public class QueryTableTests
{
    record Order(string Id, string Supplier);

    sealed class OrderFilters
    {
        public string Keyword { get; set; } = "";
    }

    static readonly DataTableColumn[] Columns = [new("Id", "单号"), new("Supplier", "供应商")];

    /// <summary>假接口：两行、共 42 条；记下每次的查询条件。</summary>
    static (TableQuery<Order, OrderFilters> Query, List<TableQueryRequest<OrderFilters>> Calls) FakeQuery()
    {
        var calls = new List<TableQueryRequest<OrderFilters>>();
        var query = new TableQuery<Order, OrderFilters>((request, _) =>
        {
            calls.Add(request);
            return Task.FromResult(new TablePage<Order>([new("o1", "华辰"), new("o2", "启航")], 42));
        }, new OrderFilters());
        _ = query.Refresh();
        return (query, calls);
    }

    static QueryTable New(ITableQuery query) => new() { Query = query, Columns = Columns, GetKey = row => ((Order)row).Id, Title = "采购单", Width = 1100 };

    /// <summary>关键字条件：输入框改草稿的字段。</summary>
    static IEnumerable<Control> KeywordField(object draft)
    {
        var filters = (OrderFilters)draft;
        var box = new TextField { Text = filters.Keyword };
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) filters.Keyword = box.Text ?? "";
        };
        return [new Field { Label = "关键字", Content = box }];
    }

    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static string Texts(Control root) => string.Concat(root.GetLogicalDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text));

    [Fact]
    [Upstream("QueryTable", "查询栏改草稿，点“查询”才交给 query.search；“重置”回到一开始的条件并查询")]
    public Task Filters_edit_a_draft_until_search_and_reset() => HeadlessUi.Run(() =>
    {
        var (query, calls) = FakeQuery();
        var table = New(query);
        table.Filters = KeywordField;
        var window = HeadlessUi.Show(table);
        var box = table.FilterBar.GetLogicalDescendants().OfType<TextField>().Single();
        box.Text = "华辰";
        var before = calls.Count;
        Assert.Equal(before, calls.Count);
        Click(table.FilterBar.Part<Button>(FilterBarParts.Search));
        Assert.Equal("华辰", calls[^1].Filters.Keyword);
        // 已应用的是拷贝：再改草稿不影响
        box.Text = "启航";
        Assert.Equal("华辰", query.Filters.Keyword);
        Click(table.FilterBar.Part<Button>(FilterBarParts.Reset));
        HeadlessUi.Flush(window);
        Assert.Equal("", calls[^1].Filters.Keyword);
        Assert.Equal("", table.FilterBar.GetLogicalDescendants().OfType<TextField>().Single().Text);
    });

    [Fact]
    [Upstream("QueryTable", "表格名称是 title，分页显示 query 的总数；刷新按钮调 query.refresh；列设置隐藏列")]
    public Task Title_pagination_refresh_and_column_settings() => HeadlessUi.Run(() =>
    {
        var (query, calls) = FakeQuery();
        var table = New(query);
        var window = HeadlessUi.Show(table);
        Assert.Equal("采购单", AutomationProperties.GetName(table.Table));
        Assert.Contains("第 1–10 条，共 42 条", Texts(table.Pagination));
        Assert.Equal("采购单分页", AutomationProperties.GetName(table.Pagination.PaginationPart));
        Assert.Equal("刷新", AutomationProperties.GetName(table.RefreshButton));
        var before = calls.Count;
        Click(table.RefreshButton);
        Assert.Equal(before + 1, calls.Count);

        table.Settings.Popover.IsOpen = true;
        HeadlessUi.Flush(window);
        var supplier = ((Control)table.Settings.Popover.Content!).GetVisualDescendants().OfType<Checkbox>().Single(box => Texts(box).Contains("供应商"));
        supplier.Toggle();
        HeadlessUi.Flush(window);
        Assert.Equal(["Supplier"], table.Table.HiddenColumns);
        Assert.Single(table.Table.Shown);
    });

    [Fact]
    [Upstream("QueryTable", "多选：勾选后出现批量操作栏，batchActions 拿到已选的 key")]
    public Task Selection_shows_the_batch_bar() => HeadlessUi.Run(() =>
    {
        var (query, _) = FakeQuery();
        var table = New(query);
        table.SelectionMode = SelectionMode.Multiple;
        table.Selected = ["o2"];
        table.BatchActions = keys => [new Button { Content = $"批量作废 {string.Join(',', keys)}" }];
        HeadlessUi.Show(table);
        var bar = table.Part<SelectionBar>(QueryTableParts.SelectionBar);
        Assert.True(bar.IsVisible);
        Assert.Contains("已选 1 项", Texts(bar));
        Assert.Equal("批量作废 o2", bar.Actions.OfType<Button>().Single().Content);
        // 表格上勾选写回 Selected，批量操作栏跟着变
        table.Table.Selected = ["o1", "o2"];
        Assert.Equal(["o1", "o2"], table.Selected);
        Assert.Equal("批量作废 o1,o2", bar.Actions.OfType<Button>().Single().Content);
    });

    [Fact]
    [Upstream("QueryTable", "有查询栏时标题、自己的按钮、刷新并进查询栏的第一排，按钮都在查询后面；表格上方没有单独的工具栏")]
    public Task Toolbar_joins_the_filter_bar_row() => HeadlessUi.Run(() =>
    {
        var (query, _) = FakeQuery();
        var table = New(query);
        table.Filters = KeywordField;
        var create = new Button { Content = "新建" };
        table.Toolbar = create;
        HeadlessUi.Show(table);
        Assert.Same(table.ToolbarPanel, table.FilterBar.End);
        Assert.Contains("采购单", Texts(table.FilterBar));
        Assert.True(create.IsEffectivelyVisible);
        Assert.Contains(table.RefreshButton, table.FilterBar.GetLogicalDescendants());
        Assert.False(table.Part<Flex>(QueryTableParts.ToolbarRow).IsVisible);
        // 按钮区：重置、查询，再是自己的按钮、刷新
        var buttons = table.FilterBar.Part<Flex>(FilterBarParts.Actions).GetLogicalDescendants().OfType<ButtonBase>().Where(button => button.IsEffectivelyVisible).ToList();
        var order = buttons.Select(button => button.Name switch
        {
            FilterBarParts.Reset => "重置",
            FilterBarParts.Search => "查询",
            QueryTableParts.ToolbarButton => AutomationProperties.GetName(button),
            _ => button.Content as string,
        }).ToList();
        Assert.Equal(["重置", "查询", "新建", "刷新"], order.Take(4));
    });

    [Fact]
    [Upstream("QueryTable", "没有查询栏时，工具栏是表格上方单独的一行")]
    public Task Without_filters_the_toolbar_is_its_own_row() => HeadlessUi.Run(() =>
    {
        var (query, _) = FakeQuery();
        var table = New(query);
        table.Toolbar = new Button { Content = "新建" };
        HeadlessUi.Show(table);
        Assert.False(table.FilterBar.IsVisible);
        var row = table.Part<Flex>(QueryTableParts.ToolbarRow);
        Assert.True(row.IsVisible);
        Assert.Contains("采购单", Texts(row));
        Assert.Contains(row.GetLogicalDescendants().OfType<Button>(), button => button.Content as string == "新建");
    });

    [Fact]
    [Upstream("QueryTable", "条件多时 moreFilters 收起，点“展开”后更多条件排在下面")]
    public Task More_filters_are_collapsed_until_expanded() => HeadlessUi.Run(() =>
    {
        var (query, _) = FakeQuery();
        var table = New(query);
        table.Width = 640;
        table[TextElement.FontSizeProperty] = 14d;
        table.Filters = KeywordField;
        var supplier = new Field { Label = "供应商", Content = new TextField() };
        table.MoreFilters = _ => [supplier];
        var window = HeadlessUi.Show(table);
        Assert.False(supplier.IsEffectivelyVisible);
        Click(table.FilterBar.Part<Button>(FilterBarParts.Toggle));
        HeadlessUi.Flush(window);
        Assert.True(supplier.IsEffectivelyVisible);
    });

    [Fact]
    [Upstream("QueryTable", "fill：整块占满到窗口底边，表格不再限高")]
    public Task Fill_wraps_in_viewport_fill_and_lifts_the_height_cap() => HeadlessUi.Run(() =>
    {
        var (query, _) = FakeQuery();
        var table = New(query);
        table.Fill = true;
        HeadlessUi.Show(table);
        Assert.IsType<ViewportFill>(table.GetVisualChildren().Single());
        Assert.Equal(double.PositiveInfinity, table.Table.MaxHeight);
        table.Fill = false;
        Assert.IsNotType<ViewportFill>(table.GetVisualChildren().Single());
        Assert.False(table.Table.IsSet(global::Avalonia.Layout.Layoutable.MaxHeightProperty));
    });

    [Fact]
    public Task Failure_shows_an_alert_with_retry_and_keeps_rows() => HeadlessUi.Run(() =>
    {
        var fail = false;
        var query = new TableQuery<Order, OrderFilters>((_, _) => fail
            ? Task.FromException<TablePage<Order>>(new InvalidOperationException("网关超时"))
            : Task.FromResult(new TablePage<Order>([new("o1", "华辰")], 1)), new OrderFilters());
        _ = query.Refresh();
        var table = New(query);
        HeadlessUi.Show(table);
        var alert = table.Part<Alert>(QueryTableParts.Error);
        Assert.False(alert.IsVisible);
        fail = true;
        _ = query.Refresh();
        Assert.True(alert.IsVisible);
        Assert.Equal("网关超时", alert.Content);
        Assert.Single(table.Table.View);
        fail = false;
        Click((Button)alert.Action!);
        Assert.False(alert.IsVisible);
    });

    [Fact]
    public Task Header_sort_and_pagination_write_back_to_the_query() => HeadlessUi.Run(() =>
    {
        var (query, calls) = FakeQuery();
        var table = New(query);
        HeadlessUi.Show(table);
        table.Table.Sort = new SortState("Supplier", SortDirection.Descending);
        Assert.Equal(new SortState("Supplier", SortDirection.Descending), calls[^1].Sorts.Single());
        table.Pagination.Page = 3;
        Assert.Equal(3, calls[^1].Page);
        table.Table.Filters = new Dictionary<string, IReadOnlyList<string>> { ["Supplier"] = ["华辰"] };
        Assert.Equal((1, "华辰"), (calls[^1].Page, calls[^1].ColumnFilters["Supplier"][0]));
        Assert.Equal(1, table.Pagination.Page);
    });
}
