using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace Rune.Avalonia.Examples;

/// <summary>DataTable 页的示例（vue-tsx-pro 文档站 table/DataTable）：这一轮是一万条订单、单选、加载与空状态；其余（表头筛选、编辑、拖动、详情行等）随功能补上。</summary>
static class DataTableExamples
{
    public static Example[] All =>
    [
        new("DataTable", "orders", "一万条订单", "列定义里 Type 决定默认的格式与对齐（Number、Currency、Percent、FileSize、Date、DateTime，跟随语言；不写时按第一个值推断，数字靠右），Cell 自定义单元格，Value 给排序用的值；点可排序的列标题在升序、降序、不排之间切换（本地按数字、日期、按语言的文字排，ManualSort 时交给服务端）。SelectionMode=\"multiple\" 多一列勾选框；Sticky 的列横向滚动时固定在两侧；Resizable 时可以拖动或用键盘调整列宽，ColumnWidths 双向绑定可以存到本地设置里。工具栏（搜索、批量操作、分页）用现有组件组合，不内置。", Orders),
        new("DataTable", "single", "单选：选一家报价", "SelectionMode=\"single\" 时每行是一个单选框：点击选中，选中的行整行高亮；没有全选，取消选择用 SelectionBar 的清空或把 Selected 设为空列表。", Single),
        new("DataTable", "states", "加载、空状态与外观", "IsLoading 时表体换成骨架行（首次加载与刷新一样，不另加进度条）；没有数据显示 Empty。Density、Variant、Striped 与静态表格相同。", States),
    ];

    record Order(string Id, string Customer, string City, decimal Amount, StatusTone Tone, string Status, DateTime PlacedAt);

    static readonly string[] Customers = ["林小满", "周一航", "陈默", "许悠", "王晴", "赵浩然", "孙思远", "李雨桐"];
    static readonly string[] Cities = ["上海", "北京", "杭州", "深圳", "成都", "武汉"];
    static readonly (StatusTone Tone, string Label)[] OrderStatuses =
        [(StatusTone.Warning, "待付款"), (StatusTone.Info, "备货中"), (StatusTone.Accent, "已发货"), (StatusTone.Success, "已完成"), (StatusTone.Danger, "已退款")];

    static readonly DateTime Latest = new(2026, 10, 20, 18, 0, 0);

    // 一万条订单：只建看得见的几十行
    static readonly Order[] Orders_ =
    [
        .. Enumerable.Range(0, 10_000).Select(index => new Order(
            $"SO{100_000 + index}", Customers[index % Customers.Length], Cities[index * 7 % Cities.Length],
            Math.Round((index * 9301L + 49297) % 233280 / 23.328m) / 100 + 9.9m,
            OrderStatuses[index * 3 % OrderStatuses.Length].Tone, OrderStatuses[index * 3 % OrderStatuses.Length].Label,
            // 越往后越早：每单相隔 37 分钟
            Latest.AddMinutes(-37 * index))),
    ];

    static Control Orders()
    {
        DataTableColumn[] columns =
        [
            new("Id", "订单号") { Width = 120, Sortable = true, Sticky = ColumnSticky.Start },
            new("Customer", "客户") { Width = 110, Sortable = true },
            new("City", "城市") { Width = 90 },
            // 不写 Cell：按 Type 与当前语言格式化（金额用语言的币种，数字类靠右）
            new("Amount", "金额") { Width = 130, Type = ColumnType.Currency, Sortable = true },
            new("Status", "状态") { Width = 100, Sortable = true, Cell = (row, _) => new Badge { Tone = ((Order)row).Tone, Content = ((Order)row).Status } },
            new("PlacedAt", "下单时间") { Width = 180, Type = ColumnType.DateTime, Sortable = true },
            new("Actions", "操作")
            {
                Width = 64, Sticky = ColumnSticky.End, Resizable = false,
                Cell = (row, _) =>
                {
                    var more = new IconButton { Icon = Lucide.Ellipsis };
                    AutomationProperties.SetName(more, $"{((Order)row).Id} 的更多操作");
                    return more;
                },
            },
        ];
        var table = new DataTable
        {
            ItemsSource = Orders_, Columns = columns, GetKey = row => ((Order)row).Id, SelectionMode = SelectionMode.Multiple,
            Sort = new SortState("PlacedAt", SortDirection.Descending), MaxHeight = 26 * 16, Resizable = true,
        };
        AutomationProperties.SetName(table, "订单");
        var keyword = new TextField { Icon = Lucide.Search, PlaceholderText = "订单号或客户", Width = 16 * 16 };
        AutomationProperties.SetName(keyword, "搜索订单");
        var count = new Small { Tone = TextTone.Muted };
        var export = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral, Icon = Lucide.Download, Content = "导出" };
        var cancel = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Icon = Lucide.Trash, Content = "取消订单" };
        cancel.Click += (_, _) => table.Selected = [];
        void Sync()
        {
            var text = keyword.Text ?? "";
            var rows = text.Length == 0 ? Orders_ : [.. Orders_.Where(order => order.Id.Contains(text) || order.Customer.Contains(text))];
            if (!ReferenceEquals(table.ItemsSource, rows) && (text.Length > 0 || table.ItemsSource != Orders_)) table.ItemsSource = rows;
            count.Text = table.Selected.Count > 0 ? $"已选 {table.Selected.Count} 单" : $"共 {rows.Length:N0} 单";
            export.IsEnabled = cancel.IsEnabled = table.Selected.Count > 0;
        }
        keyword.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) Sync();
        };
        table.PropertyChanged += (_, e) =>
        {
            if (e.Property == DataTable.SelectedProperty) Sync();
        };
        Sync();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children =
            {
                new Flex
                {
                    Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Wrap = FlexWrap.Wrap,
                    Children = { keyword, new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { count, export, cancel } } },
                },
                table,
            },
        };
    }

    record Quote(string Id, string Supplier, decimal Price, int Days, string Rating);

    static Control Single()
    {
        Quote[] quotes = [new("q1", "青岩精密", 182_400, 45, "A"), new("q2", "北辰研发", 169_900, 60, "B"), new("q3", "远航生产", 175_000, 38, "A"), new("q4", "星河紧固件", 158_600, 75, "C")];
        var table = new DataTable
        {
            ItemsSource = quotes, GetKey = row => ((Quote)row).Id, SelectionMode = SelectionMode.Single, Selected = ["q3"], Density = TableDensity.Compact,
            Columns =
            [
                new("Supplier", "供应商"),
                new("Price", "报价（元）") { Width = 128, Format = new NumericFormat { MaximumFractionDigits = 0 } },
                new("Days", "交付（天）") { Width = 104 },
                new("Rating", "评级") { Width = 72, Align = TableAlign.Center },
            ],
        };
        AutomationProperties.SetName(table, "供应商报价");
        var picked = new Small { Tone = TextTone.Muted };
        void Sync() => picked.Inlines = [new Run("选中："), new Code { Inlines = { new Run(quotes.FirstOrDefault(quote => table.Selected.Contains(quote.Id))?.Supplier ?? "无") } }];
        table.PropertyChanged += (_, e) =>
        {
            if (e.Property == DataTable.SelectedProperty) Sync();
        };
        Sync();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children = { table, picked },
        };
    }

    record Log(int Id, string Level, string Message);

    static Control States()
    {
        Log[] sample = [.. Enumerable.Range(0, 6).Select(index => new Log(index, new[] { "INFO", "WARN", "ERROR" }[index % 3], new[] { "服务健康检查通过", "支付回调重试第 2 次", "库存同步超时" }[index % 3]))];
        var load = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Content = "载入示例数据" };
        var table = new DataTable
        {
            GetKey = row => ((Log)row).Id, IsLoading = true, Density = TableDensity.Compact, Variant = TableVariant.Ghost, Striped = true, Height = 16 * 16,
            Columns = [new("Id", "#") { Width = 56 }, new("Level", "级别") { Width = 90 }, new("Message", "内容") { Width = 260, Wrap = true }],
            Empty = new Flex { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Sm, Children = { new TextBlock { Text = "今天还没有日志" }, load } },
        };
        AutomationProperties.SetName(table, "运行日志");
        var mode = new ToggleGroup { IsRequired = true, Value = "loading", Size = TextSize.Sm };
        AutomationProperties.SetName(mode, "状态");
        foreach (var (value, label) in new[] { ("data", "有数据"), ("loading", "首次加载"), ("refreshing", "刷新中"), ("empty", "没有数据") }) mode.Children.Add(new ToggleItem { Value = value, Content = label });
        var density = new ToggleGroup { IsRequired = true, Value = "compact", Size = TextSize.Sm };
        AutomationProperties.SetName(density, "密度");
        foreach (var (value, label) in new[] { ("compact", "紧凑"), ("standard", "标准"), ("comfortable", "宽松") }) density.Children.Add(new ToggleItem { Value = value, Content = label });
        void Sync()
        {
            var state = mode.Value;
            table.ItemsSource = state is "empty" or "loading" ? Array.Empty<Log>() : sample;
            table.IsLoading = state is "loading" or "refreshing";
            table.Density = TableDensity.Parse(density.Value ?? "compact");
        }
        mode.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleGroup.ValueProperty) Sync();
        };
        density.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleGroup.ValueProperty) Sync();
        };
        load.Click += (_, _) => mode.Value = "data";
        Sync();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children = { new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { mode, density } }, table },
        };
    }
}
