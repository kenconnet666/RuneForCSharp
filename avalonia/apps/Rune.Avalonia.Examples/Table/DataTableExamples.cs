using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace Rune.Avalonia.Examples;

/// <summary>DataTable 页的示例（vue-tsx-pro 文档站 table/DataTable）：已做的功能的示例：一万条订单、表头筛选与列菜单、就地编辑、分组表头、展开行、单选、加载与空状态；其余（拖动、列虚拟化等）随功能补上。</summary>
static class DataTableExamples
{
    public static Example[] All =>
    [
        new("DataTable", "orders", "一万条订单", "列定义里 Type 决定默认的格式与对齐（Number、Currency、Percent、FileSize、Date、DateTime，跟随语言；不写时按第一个值推断，数字靠右），Cell 自定义单元格，Value 给排序用的值；点可排序的列标题在升序、降序、不排之间切换（本地按数字、日期、按语言的文字排，ManualSort 时交给服务端）。SelectionMode=\"multiple\" 多一列勾选框；Sticky 的列横向滚动时固定在两侧；Resizable 时可以拖动或用键盘调整列宽，ColumnWidths 双向绑定可以存到本地设置里。工具栏（搜索、批量操作、分页）用现有组件组合，不内置。", Orders),
        new("DataTable", "columns", "采购单：表头筛选、多列排序与列菜单", "列定义写 Filter 时列标题上有筛选按钮：ColumnFilter.Values 列出这一列出现过的值（状态列按阶段顺序、显示状态名），也可以给 Options，ColumnFilter.Keyword 是输入关键字；Filters 双向绑定各列选中的值，默认在本地筛，ManualFilter 时交给服务端。MultiSort 时点列标题只按这一列排，按住 Shift 点是追加一列（列标题上标着次序），排序写在 Sorts。ColumnMenu 在列标题结尾放 ⋮：排序、固定在开头 / 结尾、前后移动、隐藏，写回 ColumnPins、ColumnOrder、HiddenColumns，都能双向绑定、存起来（上游记在浏览器里；这里在内存里，“还原列设置”清掉）。", Columns),
        new("DataTable", "editable", "采购申请明细：就地编辑与校验", "列写 Editable（或按行判断的 IsEditable）时可以就地编辑，表格随之变成网格：方向键在格之间走，只有一格能 Tab 进入；在可编辑的格上按 Enter、F2、双击或直接打字开始编辑，Enter 提交、Tab 提交并到下一个可编辑的格、点开别处提交、Esc 放弃。默认编辑器按列选（状态列是 Select，数字类是 NumberField，其余是 TextField），Editor 自己写（这里的交期是 DateField）。列的 Validate 拦下不合理的值，CellChange 写回，返回文字时是不接受的原因（接口拒绝），编辑器留着并显示它；ValidateRow 返回整行的问题，标在对应的格上（红框，悬停与读屏都能知道原因）。", Editable),
        new("DataTable", "changes", "采购方案调整：分组表头", "ColumnGroups 写组标题与组里各列的 Key：相邻的几列上方共用一个标题，表头变成两行，没分组的列跨两行；组与相邻的列之间有竖线，固定列照常固定。列定义仍是平铺的，排序、列宽、隐藏列都按列算，组里的列全隐藏时这一组也不显示。", Changes),
        new("DataTable", "details", "供应商改进建议：展开行", "RenderDetail 给出一行的详情：多一列展开按钮（读屏念出展开状态），展开的行下面多一行跨全部列的详情，虚拟滚动按真实高度测量；HasDetail 决定哪些行能展开，ExpandedRows 可以双向绑定。详情横向滚动时贴着可见区域的起始边，宽度不超过可见区域。", Details),
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

    record PurchaseOrder(string Id, string Supplier, string Category, string Buyer, string Status, decimal Amount, DateOnly OrderedAt);

    static readonly StatusMap PurchaseStatuses = new()
    {
        ["draft"] = new("草稿", StatusTone.Neutral),
        ["approving"] = new("审批中", StatusTone.Warning),
        ["ordered"] = new("已下单", StatusTone.Info),
        ["received"] = new("已到货", StatusTone.Success),
    };

    static readonly PurchaseOrder[] PurchaseOrders =
    [
        .. Enumerable.Range(0, 240).Select(index => new PurchaseOrder(
            $"PO-2026-{1000 + index}",
            new[] { "华辰精密", "启航塑胶", "远山钣金", "恒通紧固件", "北辰电子" }[index * 3 % 5],
            new[] { "机械件", "塑料件", "钣金件", "标准件", "线缆" }[index * 7 % 5],
            new[] { "林小满", "周一航", "陈默", "许悠", "王晴", "赵浩然" }[index * 5 % 6],
            PurchaseStatuses.Keys[index * 11 % PurchaseStatuses.Keys.Count],
            index * 7919 % 90_000 + 1200,
            new DateOnly(2026, 10, 21).AddDays(-(index * 13 % 60)))),
    ];

    static Control Columns()
    {
        DataTableColumn[] columns =
        [
            new("Id", "采购单号") { Width = 160, Sticky = ColumnSticky.Start, Filter = ColumnFilter.Keyword },
            new("Supplier", "供应商") { Sortable = true, Filter = ColumnFilter.Values },
            new("Category", "品类") { Width = 130, Sortable = true, Filter = ColumnFilter.Values },
            new("Buyer", "采购员") { Width = 140, Filter = ColumnFilter.Values },
            new("Status", "状态") { Width = 140, Statuses = PurchaseStatuses, Filter = ColumnFilter.Values },
            new("Amount", "金额") { Width = 150, Type = ColumnType.Currency, Sortable = true },
            new("OrderedAt", "下单日期") { Width = 150, Type = ColumnType.Date, Sortable = true },
        ];
        var table = new DataTable
        {
            ItemsSource = PurchaseOrders, Columns = columns, GetKey = row => ((PurchaseOrder)row).Id, MultiSort = true, ColumnMenu = true, Resizable = true,
            Sorts = [new SortState("OrderedAt", SortDirection.Descending)], Height = 22 * 16,
        };
        AutomationProperties.SetName(table, "采购单");
        string Header(string key) => columns.FirstOrDefault(column => column.Key == key)?.Header as string ?? key;
        var summary = new Small { Tone = TextTone.Muted };
        var reset = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Icon = Lucide.RotateCcw, Content = "还原列设置" };
        reset.Click += (_, _) =>
        {
            table.HiddenColumns = [];
            table.ColumnOrder = [];
            table.ColumnPins = new Dictionary<string, ColumnSticky?>();
            table.ColumnWidths = new Dictionary<string, double>();
        };
        void Sync()
        {
            var parts = (table.Filters ?? new Dictionary<string, IReadOnlyList<string>>()).Where(pair => pair.Value.Count > 0)
                .Select(pair => $"{Header(pair.Key)}：{string.Join("、", pair.Key == "Status" ? pair.Value.Select(PurchaseStatuses.Label) : pair.Value)}").ToList();
            var order = table.Sorts.Select((sort, index) => $"{index + 1}. {Header(sort.Key)}{(sort.Direction == SortDirection.Ascending ? "↑" : "↓")}").ToList();
            summary.Text = $"{(parts.Count > 0 ? $"筛选 {string.Join("；", parts)}" : "没有筛选")} · {(order.Count > 0 ? $"排序 {string.Join(" ", order)}" : "没有排序")}";
            reset.IsEnabled = table.HiddenColumns is { Count: > 0 } || table.ColumnOrder is { Count: > 0 } || table.ColumnPins is { Count: > 0 } || table.ColumnWidths is { Count: > 0 };
        }
        table.PropertyChanged += (_, e) =>
        {
            if (e.Property == DataTable.FiltersProperty || e.Property == DataTable.SortsProperty || e.Property == DataTable.HiddenColumnsProperty
                || e.Property == DataTable.ColumnOrderProperty || e.Property == DataTable.ColumnPinsProperty || e.Property == DataTable.ColumnWidthsProperty) Sync();
        };
        Sync();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children = { new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Wrap = FlexWrap.Wrap, Children = { summary, reset } }, table },
        };
    }

    record Line(string Id, string Part, decimal Quantity, decimal Price, string Due, string Status);

    static Control Editable()
    {
        const string today = "2026-10-24";
        var statuses = new StatusMap { ["draft"] = new("草稿", StatusTone.Neutral), ["ordered"] = new("已下单", StatusTone.Info) };
        DataTableColumn[] columns =
        [
            new("Id", "行号") { Width = 70 },
            new("Part", "零件") { MinWidth = 180, Editable = true, Validate = (value, _) => value is string text && text.Trim().Length > 0 ? null : "零件名称不能为空" },
            new("Quantity", "数量") { Width = 100, Type = ColumnType.Number, Editable = true, Validate = (value, _) => value is decimal number && number > 0 && number == Math.Floor(number) ? null : "数量是正整数" },
            new("Price", "单价") { Width = 120, Type = ColumnType.Currency, Editable = true, Validate = (value, _) => value is decimal number && number >= 0 ? null : "单价不能为负" },
            new("Subtotal", "小计") { Width = 130, Type = ColumnType.Currency, Value = row => ((Line)row).Quantity * ((Line)row).Price, Summary = ColumnSummary.Sum },
            new("Due", "交期")
            {
                Width = 150, Type = ColumnType.Date, Editable = true,
                // 自定义编辑器：拿到草稿、提交与放弃
                Editor = context =>
                {
                    var field = new DateField { Value = context.Value as string, Size = TextSize.Sm, IsInvalid = context.Error is not null };
                    AutomationProperties.SetName(field, "交期");
                    field.PropertyChanged += (_, e) =>
                    {
                        if (e.Property == DateField.ValueProperty) context.Change(field.Value);
                    };
                    return field;
                },
            },
            new("Status", "状态") { Width = 100, Statuses = statuses },
        ];
        List<Line> lines =
        [
            new("1", "显示器支架", 40, 128, "2026-11-05", "ordered"),
            new("2", "铰链底座", 80, 76.5m, "2026-11-12", "draft"),
            new("3", "检修口盖板", 24, 212, "2026-10-20", "draft"),
            new("4", "线束卡箍", 300, 3.2m, "2026-11-30", "draft"),
        ];
        var saved = new Small { Tone = TextTone.Muted, Text = "方向键在格之间走；在可编辑的格上按 Enter、F2、双击或直接打字开始编辑，Enter 提交，Tab 到下一格，Esc 放弃" };
        AutomationProperties.SetLiveSetting(saved, global::Avalonia.Automation.AutomationLiveSetting.Polite);
        var table = new DataTable
        {
            ItemsSource = lines, Columns = columns, GetKey = row => ((Line)row).Id,
            ValidateRow = row => string.CompareOrdinal(((Line)row).Due, today) < 0 ? new Dictionary<string, string> { ["Due"] = "交期早于今天，请重新约定" } : null,
        };
        AutomationProperties.SetName(table, "采购申请明细");
        // 模拟保存：已下单的行不能改数量（接口拒绝时返回原因，编辑器留着并显示它）
        table.CellChange = async (row, key, value) =>
        {
            await Task.Delay(300);
            var line = (Line)row;
            if (line.Status == "ordered" && key == "Quantity") return "已下单的行不能改数量，请走变更单";
            lines = [.. lines.Select(item => item.Id != line.Id ? item : key switch
            {
                "Part" => item with { Part = (string)value! },
                "Quantity" => item with { Quantity = (decimal)value! },
                "Price" => item with { Price = (decimal)value! },
                _ => item with { Due = (string)value! },
            })];
            table.ItemsSource = lines;
            saved.Text = $"第 {line.Id} 行已保存";
            return null;
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Children = { table, saved } };
    }

    record Side(string Part, decimal Price, int Quantity)
    {
        public decimal Amount => Price * Quantity;
    }

    record Change(int No, string Name, Side Before, Side After)
    {
        public decimal Saving => Before.Amount - After.Amount;
    }

    static Control Changes()
    {
        Change[] changes =
        [
            new(1, "笔记本电脑", new("LT-14", 4520m, 20), new("LT-15", 3980m, 20)),
            new(2, "显示器", new("MN-27", 1260m, 40), new("MN-27S", 1080m, 40)),
            new(3, "键盘鼠标套装", new("KB-200", 129m, 60), new("KB-210", 99m, 56)),
            new(4, "办公椅", new("CH-01", 860m, 30), new("CH-01", 860m, 24)),
            new(5, "打印机", new("PR-6", 2380m, 4), new("PR-8", 2650m, 4)),
        ];
        var money = new NumericFormat { MinimumFractionDigits = 2, MaximumFractionDigits = 2 };
        var table = new DataTable
        {
            ItemsSource = changes, GetKey = row => ((Change)row).No, Density = TableDensity.Compact,
            Columns =
            [
                new("No", "序号") { Width = 64, Sticky = ColumnSticky.Start },
                new("Name", "物品") { Width = 140, Sticky = ColumnSticky.Start },
                new("BeforePart", "型号") { Width = 88, Value = row => ((Change)row).Before.Part },
                new("BeforePrice", "单价（元）") { Width = 112, Value = row => ((Change)row).Before.Price, Format = money },
                new("BeforeQuantity", "数量") { Width = 72, Value = row => ((Change)row).Before.Quantity },
                new("AfterPart", "型号") { Width = 88, Value = row => ((Change)row).After.Part },
                new("AfterPrice", "单价（元）") { Width = 112, Value = row => ((Change)row).After.Price, Format = money },
                new("AfterQuantity", "数量") { Width = 72, Value = row => ((Change)row).After.Quantity },
                new("Saving", "节省（元）")
                {
                    Width = 132, Value = row => ((Change)row).Saving, Format = money, Summary = ColumnSummary.Sum,
                    // 调整后反而更贵：节省是负数，标红（其余按格式显示）
                    Cell = (row, _) => ((Change)row).Saving < 0 ? new Text { Tone = TextTone.Danger, Text = money.Format(((Change)row).Saving, "zh-CN", "CNY") } : null,
                },
            ],
            ColumnGroups =
            [
                new DataTableColumnGroup("before", "调整前", ["BeforePart", "BeforePrice", "BeforeQuantity"]),
                new DataTableColumnGroup("after", "调整后", ["AfterPart", "AfterPrice", "AfterQuantity"]),
            ],
        };
        AutomationProperties.SetName(table, "采购方案调整");
        return table;
    }

    record Proposal(string Code, string Supplier, string Part, string Kind, decimal Saving, string Status, string? Content = null, string? Impact = null, string[]? Files = null, string? Submitted = null);

    static Control Details()
    {
        var statuses = new StatusMap
        {
            ["draft"] = new("草稿", StatusTone.Neutral),
            ["reviewing"] = new("评估中", StatusTone.Info),
            ["accepted"] = new("已采纳", StatusTone.Success),
            ["rejected"] = new("未采纳", StatusTone.Danger),
        };
        Proposal[] proposals =
        [
            new("GYS-2026-031", "供应商甲", "显示器支架", "材料替代", 5600, "reviewing",
                "支架材料由 7050 铝合金改为 2024 铝合金，加工余量减少 30%，单件工时由 6.5 h 降到 4.8 h。", "强度裕度由 1.42 降到 1.31，仍满足设计要求；需补做一项静力试验。",
                ["材料对比报告.pdf", "工时测算.xlsx"], "2026-10-08 张工"),
            new("GYS-2026-034", "供应商乙", "包装纸箱", "供应商切换", 2380.5m, "accepted",
                "引入第二供应商，年采购量 1,200 件分两家，单价由 186 元降到 162 元。", "新供应商需完成样品验收，预计 8 周。", ["报价单.pdf"], "2026-09-21 李工"),
            new("GYS-2026-037", "供应商丙", "线束支架", "流程优化", 980, "rejected", "钣金件由数控铣改为冲压成形。", "批量不足 500 件，模具费摊销后不降反升，未采纳。", Submitted: "2026-09-15 王工"),
            new("GYS-2026-040", "供应商甲", "机柜面板", "产品改进", 0, "draft"),
        ];
        Control Detail(object row)
        {
            var proposal = (Proposal)row;
            // 附件：回形针与链接，没有时写"无"
            Control files = new Small { Tone = TextTone.Muted, Text = "无" };
            if (proposal.Files is { Length: > 0 } list)
            {
                var links = new Flex { Gap = Space.Md, Wrap = FlexWrap.Wrap };
                foreach (var file in list)
                    links.Children.Add(new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Icon { Data = Lucide.Paperclip }, new Link { Href = new Uri("https://example.com/files"), Content = file } } });
                files = links;
            }
            var descriptions = new Descriptions { Columns = 2, Size = TextSize.Sm };
            descriptions.Items.AddRange(
            [
                new DescriptionItem { Label = "建议内容", Value = proposal.Content, Span = 2 },
                new DescriptionItem { Label = "影响评估", Value = proposal.Impact, Span = 2 },
                new DescriptionItem { Label = "附件", Value = files },
                new DescriptionItem { Label = "提交", Value = proposal.Submitted },
            ]);
            return descriptions;
        }
        var table = new DataTable
        {
            ItemsSource = proposals, GetKey = row => ((Proposal)row).Code, RenderDetail = Detail, HasDetail = row => ((Proposal)row).Content is not null, ExpandedRows = ["GYS-2026-031"],
            Columns =
            [
                new("Code", "建议编号") { Width = 132, Sticky = ColumnSticky.Start },
                new("Supplier", "供应商") { Width = 112 },
                new("Part", "零件") { Width = 140 },
                new("Kind", "类型") { Width = 104 },
                new("Saving", "预计单价（元）") { Width = 156, Format = new NumericFormat { MinimumFractionDigits = 2, MaximumFractionDigits = 2 } },
                new("Status", "状态") { Width = 112, Statuses = statuses },
            ],
        };
        AutomationProperties.SetName(table, "供应商改进建议");
        return table;
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
