using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>QueryTable 页的示例（vue-tsx-pro 文档站 patterns/QueryTable）。</summary>
static class QueryTableExamples
{
    public static Example[] All =>
    [
        new("QueryTable", "orders", "采购单列表", "Query 是 TableQuery：页码、每页条数、排序（ManualSort，交给服务端；MultiSort 时按住 Shift 点列标题追加一列，写在 Query.Sorts）、已应用的条件、总数与加载都在里面。Filters 拿到草稿，控件改它的字段，点“查询”（或在输入框里回车）才交给 Query.Search；“重置”回到 BlankFilters（不写时是一开始的条件）并查询；MoreFilters 是不常用的条件，第一排放不下的收进“展开”。有查询栏时标题与工具栏并进查询栏的第一排：起始边是 Title（也是表格的名称），重置、查询后面是 Toolbar（自己的按钮），结尾是刷新、行高、列设置（勾选显示、拖动排序、重置）。列标题带列菜单（固定、前后移动、隐藏），列宽可以拖动；列设置与行高在 Table 上，要记住时调用方存下来。SelectionMode=\"multiple\" 时勾选后出现批量操作栏，BatchActions 拿到已选的 key 给出按钮。", Orders),
    ];

    record PurchaseOrder(string Id, string Supplier, string Category, string Buyer, string Status, decimal Amount, DateOnly OrderedAt);

    /// <summary>查询栏的条件：控件直接改这些字段（草稿），点“查询”时交出一份拷贝。</summary>
    sealed class OrderFilters
    {
        public string Keyword { get; set; } = "";
        public string? Category { get; set; }
        public string? Status { get; set; }
        public string Buyer { get; set; } = "";
    }

    static readonly StatusMap Statuses = new()
    {
        ["draft"] = new("草稿", StatusTone.Neutral),
        ["approving"] = new("审批中", StatusTone.Warning),
        ["ordered"] = new("已下单", StatusTone.Info),
        ["received"] = new("已到货", StatusTone.Success),
    };

    static readonly string[] Suppliers = ["华辰精密", "启航塑胶", "远山钣金", "恒通紧固件", "北辰电子", "青禾线缆"];
    static readonly string[] Categories = ["机械件", "塑料件", "钣金件", "标准件", "线缆"];
    static readonly string[] Buyers = ["林小满", "周一航", "陈默", "许悠", "王晴", "赵浩然"];

    // 假的服务端数据：368 张采购单
    static readonly PurchaseOrder[] AllOrders =
    [
        .. Enumerable.Range(0, 368).Select(index => new PurchaseOrder(
            $"PO-2026-{2000 + index}", Suppliers[index * 5 % Suppliers.Length], Categories[index * 7 % Categories.Length], Buyers[index * 11 % Buyers.Length],
            Statuses.Keys[index * 3 % Statuses.Keys.Count], index * 7919 % 90_000 + 1200, new DateOnly(2026, 10, 21).AddDays(-(index * 13 % 90)))),
    ];

    /// <summary>假的查询接口：按查询栏与表头的条件筛、按几列排序（前面的优先）、分页，300–600 ms 后返回；取消时不返回。</summary>
    static async Task<TablePage<PurchaseOrder>> FetchOrders(TableQueryRequest<OrderFilters> query, CancellationToken cancellation)
    {
        await Task.Delay(300 + Random.Shared.Next(300), cancellation);
        var filters = query.Filters;
        // 表头筛选：品类是多选（值在其中），供应商是关键字（文字包含）
        var categoryIn = query.ColumnFilters.GetValueOrDefault("Category") ?? [];
        var supplierHas = query.ColumnFilters.GetValueOrDefault("Supplier")?.FirstOrDefault()?.Trim();
        IEnumerable<PurchaseOrder> matched = AllOrders.Where(order =>
            (filters.Keyword is "" || order.Id.Contains(filters.Keyword) || order.Supplier.Contains(filters.Keyword))
            && (filters.Category is null || order.Category == filters.Category)
            && (filters.Status is null || order.Status == filters.Status)
            && (filters.Buyer is "" || order.Buyer.Contains(filters.Buyer))
            && (categoryIn.Count == 0 || categoryIn.Contains(order.Category))
            && (string.IsNullOrEmpty(supplierHas) || order.Supplier.Contains(supplierHas)));
        var rows = TableColumns.SortRows([.. matched], Columns, query.Sorts, "zh-CN").Cast<PurchaseOrder>().ToList();
        return new TablePage<PurchaseOrder>([.. rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)], rows.Count);
    }

    static readonly DataTableColumn[] Columns =
    [
        new("Id", "采购单号") { Width = 150, Sticky = ColumnSticky.Start, Sortable = true },
        new("Supplier", "供应商") { MinWidth = 160, Sortable = true, Filter = ColumnFilter.Keyword },
        new("Category", "品类") { Width = 110, Filter = new ColumnFilter { Options = [.. Categories.Select(category => new ColumnFilterOption(category))] } },
        new("Buyer", "采购员") { Width = 110 },
        new("Status", "状态") { Width = 110, Statuses = Statuses },
        new("Amount", "金额") { Width = 140, Type = ColumnType.Currency, Sortable = true },
        new("OrderedAt", "下单日期") { Width = 140, Type = ColumnType.Date, Sortable = true },
    ];

    static Control Orders()
    {
        var orders = new TableQuery<PurchaseOrder, OrderFilters>(FetchOrders, new OrderFilters(), pageSize: 10);
        _ = orders.Refresh();
        return new QueryTable
        {
            Query = orders, Columns = Columns, GetKey = row => ((PurchaseOrder)row).Id, Title = "采购单", MultiSort = true,
            Filters = draft =>
            {
                var filters = (OrderFilters)draft;
                var keyword = new TextField { PlaceholderText = "单号或供应商", Clearable = true, Text = filters.Keyword };
                keyword.TextChanged += (_, _) => filters.Keyword = keyword.Text ?? "";
                var category = new Select { ItemsSource = Categories, Placeholder = "全部", Clearable = true, Value = filters.Category };
                category.PropertyChanged += (_, e) => { if (e.Property == SelectBase.ValueProperty) filters.Category = category.Value; };
                var status = new Select { ItemsSource = Statuses.Options(), Placeholder = "全部", Clearable = true, Value = filters.Status };
                status.PropertyChanged += (_, e) => { if (e.Property == SelectBase.ValueProperty) filters.Status = status.Value; };
                return [new Field { Label = "关键字", Content = keyword }, new Field { Label = "品类", Content = category }, new Field { Label = "状态", Content = status }];
            },
            MoreFilters = draft =>
            {
                var filters = (OrderFilters)draft;
                var buyer = new TextField { PlaceholderText = "姓名", Clearable = true, Text = filters.Buyer };
                buyer.TextChanged += (_, _) => filters.Buyer = buyer.Text ?? "";
                return [new Field { Label = "采购员", Content = buyer }];
            },
            Toolbar = new Flex
            {
                Gap = Space.Sm,
                Children = { new Button { Icon = Lucide.Plus, Content = "新建采购单" }, new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Download, Content = "导出" } },
            },
            SelectionMode = SelectionMode.Multiple,
            BatchActions = keys =>
            [
                new Button { Size = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Content = $"批量催单（{keys.Count}）" },
                new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Content = "批量作废" },
            ],
        };
    }
}
