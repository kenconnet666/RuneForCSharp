using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace Rune.Avalonia.Examples;

/// <summary>DataTable 页的示例（vue-tsx-pro 文档站 table/DataTable）：一万条订单、表头筛选与列菜单、就地编辑、数据修正、行拖动、分组表头、宽表、展开行、台账、定位、主从视图、导入校验、组织架构、单选、滚动加载、加载与空状态。</summary>
static class DataTableExamples
{
    public static Example[] All =>
    [
        new("DataTable", "orders", "一万条订单", "列定义里 Type 决定默认的格式与对齐（Number、Currency、Percent、FileSize、Date、DateTime，跟随语言；不写时按第一个值推断，数字靠右），Cell 自定义单元格，Value 给排序用的值；点可排序的列标题在升序、降序、不排之间切换（本地按数字、日期、按语言的文字排，ManualSort 时交给服务端）。SelectionMode=\"multiple\" 多一列勾选框；Sticky 的列横向滚动时固定在两侧；Resizable 时可以拖动或用键盘调整列宽，ColumnWidths 双向绑定可以存到本地设置里。工具栏（搜索、批量操作、分页）用现有组件组合，不内置。", Orders),
        new("DataTable", "columns", "采购单：表头筛选、多列排序与列菜单", "列定义写 Filter 时列标题上有筛选按钮：ColumnFilter.Values 列出这一列出现过的值（状态列按阶段顺序、显示状态名），也可以给 Options，ColumnFilter.Keyword 是输入关键字；Filters 双向绑定各列选中的值，默认在本地筛，ManualFilter 时交给服务端。MultiSort 时点列标题只按这一列排，按住 Shift 点是追加一列（列标题上标着次序），排序写在 Sorts。ColumnMenu 在列标题结尾放 ⋮：排序、固定在开头 / 结尾、前后移动、隐藏，写回 ColumnPins、ColumnOrder、HiddenColumns，都能双向绑定、存起来（上游记在浏览器里；这里在内存里，“还原列设置”清掉）。", Columns),
        new("DataTable", "editable", "采购申请明细：就地编辑与校验", "列写 Editable（或按行判断的 IsEditable）时可以就地编辑，表格随之变成网格：方向键在格之间走，只有一格能 Tab 进入；在可编辑的格上按 Enter、F2、双击或直接打字开始编辑，Enter 提交、Tab 提交并到下一个可编辑的格、点开别处提交、Esc 放弃。默认编辑器按列选（状态列是 Select，数字类是 NumberField，其余是 TextField），Editor 自己写（这里的交期是 DateField）。列的 Validate 拦下不合理的值，CellChange 写回，返回文字时是不接受的原因（接口拒绝），编辑器留着并显示它；ValidateRow 返回整行的问题，标在对应的格上（红框，悬停与读屏都能知道原因）。", Editable),
        new("DataTable", "corrections", "数据修正：单击就改，看得到改了什么", "主要用来改数据的表写 EditTrigger=\"Click\"，单击就进入编辑，格上的光标也提示能改；点开别的格时前一格照常提交，异步保存也不会收掉新打开的编辑器。FieldComparison.DiffRows 按 key 和原来的数据比，给出新增、修改（带 ChangedFields）、删除的行，配合 RowTone 着色、ValueChange 写出原值，删除的行留在最后。", Corrections),
        new("DataTable", "reorder", "需求排期：拖动行", "订阅了 Reordered 时第一列前面有把手：按下拖动，或聚焦后空格拿起、↑↓ 移动、空格放下（每一步读屏都会播报），拖完得到新的顺序。排序、筛选时显示的顺序不是数据的顺序，把手收起，取消后又能拖。", Reorder),
        new("DataTable", "ledger", "项目台账：合计、状态与批量操作", "列的 Summary 写 ColumnSummary.Sum 时底部多一行吸底的合计（decimal 精确相加，按列的格式显示）；Statuses 是状态映射，单元格显示成 StatusBadge；没有值时显示列的 Empty（默认 —，这里是“待签约”）。IsRowSelectable 决定哪些行能勾选，全选跳过不能勾选的；勾选后上方的 SelectionBar 显示已选几项与批量操作。HiddenColumns 隐藏列，这里用 Menu 的 MenuCheckboxItem 做列显示设置（也可以开 ColumnMenu）；固定列与滚动的列之间有一条竖线。", Ledger),
        new("DataTable", "changes", "采购方案调整：分组表头", "ColumnGroups 写组标题与组里各列的 Key：相邻的几列上方共用一个标题，表头变成两行，没分组的列跨两行；组与相邻的列之间有竖线，固定列照常固定。列定义仍是平铺的，排序、列宽、隐藏列都按列算，组里的列全隐藏时这一组也不显示。", Changes),
        new("DataTable", "wide", "宽表：列也虚拟滚动", "VirtualColumns 时只建看得见的列（前后各多几列），列宽、固定列、分组表头、列宽调整照常。几十上百列的宽表、透视结果打开它；行本来就是虚拟滚动的。没写宽度的列按 150px；两端的固定列总在，固定列夹在中间时不虚拟化。", Wide),
        new("DataTable", "details", "供应商改进建议：展开行", "RenderDetail 给出一行的详情：多一列展开按钮（读屏念出展开状态），展开的行下面多一行跨全部列的详情，虚拟滚动按真实高度测量；HasDetail 决定哪些行能展开，ExpandedRows 可以双向绑定。详情横向滚动时贴着可见区域的起始边，宽度不超过可见区域。", Details),
        new("DataTable", "locate", "定位到行", "从通知、搜索结果跳过来时 ScrollToRow 滚到那一行（ScrollAlign 可以居中），再用 RowTone 闪一下。两千行里还没建的行也能滚到，停在吸顶表头之下；找不到时返回 false。", Locate),
        new("DataTable", "master-detail", "主从视图：点一行看详情", "ActiveKey 高亮正在看的那一行（品牌色浅底、起始边一条强调色的竖线，读屏念“当前”）；RowClicked 是点了一行（点在行里的按钮上不算）。编号是链接按钮，键盘也能打开。", MasterDetail),
        new("DataTable", "import", "导入校验", "RowTone 给整行着色：错误行浅红、提醒行浅黄，底色不透明，固定列也盖得住；配合 IsRowSelectable 只让校验通过的行能勾选导入。", ImportCheck),
        new("DataTable", "tree", "组织架构", "TreeDataTable 建在 DataTable 上，列定义、排序、勾选都一样；第一列带缩进与展开按钮，同级之间按排序排（ManualSort 时按数据的顺序）。多选默认父子联动：勾父行连带子孙，只勾一部分时父行半选；CheckStrictly 时各自勾选。LoadChildren 懒加载：展开时取回子项放进数据，期间按钮显示加载中；服务端在每行带上有没有子项，交给 HasChildren。", Org),
        new("DataTable", "single", "单选：选一家报价", "SelectionMode=\"single\" 时每行是一个单选框：点击选中，选中的行整行高亮；没有全选，取消选择用 SelectionBar 的清空或把 Selected 设为空列表。", Single),
        new("DataTable", "infinite", "滚动加载", "RangeChanged 报告可见行的范围（不含多建的行），离末尾不远时取下一页；取的时候 IsLoading，已有数据所以骨架接在末尾。页码、去重与失败重试交给 InfiniteList，它的 OnRangeChanged 直接接上。内容换行的行按量出的高度排。", Infinite),
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

    record Supplier(string Id, string Name, string City, decimal Rating);

    static Control Corrections()
    {
        Supplier[] original = [new("S-101", "华东精密", "苏州", 92), new("S-102", "北辰机械", "天津", 78), new("S-103", "南方电子", "深圳", 85), new("S-104", "西部材料", "成都", 70)];
        var suppliers = original.ToList();
        var tones = new Dictionary<RowChange, StatusTone> { [RowChange.Inserted] = StatusTone.Success, [RowChange.Updated] = StatusTone.Warning, [RowChange.Deleted] = StatusTone.Danger };
        var table = new DataTable { Density = TableDensity.Compact, EditTrigger = EditTrigger.Click, GetKey = row => ((RowDiff<Supplier>)row).Key };
        AutomationProperties.SetName(table, "供应商");
        var count = new Small { Tone = TextTone.Muted };
        var restore = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.RotateCcw, Content = "还原" };
        void Sync()
        {
            var rows = FieldComparison.DiffRows(original, suppliers, item => item.Id);
            table.ItemsSource = rows;
            var changed = rows.Count(row => row.Change is not null);
            count.Text = changed > 0 ? $"改动了 {changed} 行" : "单击单元格修改";
            restore.IsEnabled = changed > 0;
        }
        // 一格：改过的写出原值
        object? Field(object row, string key, Func<Supplier, object> value)
        {
            var diff = (RowDiff<Supplier>)row;
            return diff.Before is { } before && diff.ChangedFields?.Contains(key) == true ? new ValueChange { Before = value(before), After = value(diff.Row) } : null;
        }
        bool Editable(object row) => ((RowDiff<Supplier>)row).Change != RowChange.Deleted;
        table.Columns =
        [
            new("Id", "编号") { Width = 110, Value = row => ((RowDiff<Supplier>)row).Row.Id },
            new("Name", "供应商")
            {
                Value = row => ((RowDiff<Supplier>)row).Row.Name, Cell = (row, _) => Field(row, "Name", item => item.Name), IsEditable = Editable,
                Validate = (value, _) => value is string { Length: > 0 } ? null : "名称不能为空",
            },
            new("City", "城市") { Value = row => ((RowDiff<Supplier>)row).Row.City, Cell = (row, _) => Field(row, "City", item => item.City), IsEditable = Editable },
            new("Rating", "评分")
            {
                Type = ColumnType.Number, Value = row => ((RowDiff<Supplier>)row).Row.Rating, Cell = (row, _) => Field(row, "Rating", item => item.Rating), IsEditable = Editable,
                Validate = (value, _) => value is decimal rating && rating >= 0 && rating <= 100 ? null : "评分是 0 到 100",
            },
            new("Remove", "操作")
            {
                Width = 72,
                Cell = (row, _) =>
                {
                    var diff = (RowDiff<Supplier>)row;
                    if (diff.Change == RowChange.Deleted) return "";
                    var remove = new IconButton { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Icon = Lucide.Trash };
                    AutomationProperties.SetName(remove, $"删除 {diff.Row.Name}");
                    remove.Click += (_, _) =>
                    {
                        suppliers = [.. suppliers.Where(item => item.Id != diff.Row.Id)];
                        Sync();
                    };
                    return remove;
                },
            },
        ];
        table.RowTone = row => ((RowDiff<Supplier>)row).Change is { } change ? tones[change] : null;
        table.CellChange = (row, key, value) =>
        {
            var id = ((RowDiff<Supplier>)row).Row.Id;
            suppliers = [.. suppliers.Select(item => item.Id != id ? item : key switch
            {
                "Name" => item with { Name = (string)value! },
                "City" => item with { City = (string)value! },
                _ => item with { Rating = (decimal)value! },
            })];
            Sync();
            return Task.FromResult<string?>(null);
        };
        restore.Click += (_, _) =>
        {
            suppliers = [.. original];
            Sync();
        };
        Sync();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children = { new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm, Children = { count, restore } }, table },
        };
    }

    record Requirement(string Id, string Title, string Owner, int Hours, string Status);

    static Control Reorder()
    {
        var statuses = new StatusMap { ["todo"] = new("未开始", StatusTone.Neutral), ["doing"] = new("进行中", StatusTone.Info), ["review"] = new("待验收", StatusTone.Warning) };
        IReadOnlyList<object> rows =
        [
            new Requirement("R-201", "台账导出按分组表头合并单元格", "林小满", 16, "doing"),
            new Requirement("R-198", "供应商报价单批量导入", "周一航", 24, "todo"),
            new Requirement("R-205", "审批流超时自动提醒", "陈默", 8, "review"),
            new Requirement("R-187", "移动端看板只读视图", "许悠", 32, "todo"),
            new Requirement("R-210", "验收单打印模板", "王晴", 6, "todo"),
            new Requirement("R-193", "项目编码规则可配置", "赵浩然", 12, "doing"),
        ];
        var table = new DataTable
        {
            ItemsSource = rows, GetKey = row => ((Requirement)row).Id,
            Columns =
            [
                // 排期是数据里的位置（不是显示的位置）
                new("Rank", "排期") { Width = 88, Cell = (row, _) => (rows.ToList().IndexOf(row) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) },
                new("Title", "需求") { MinWidth = 240 },
                new("Id", "编号") { Width = 90 },
                new("Owner", "负责人") { Width = 100 },
                new("Hours", "预计工时") { Width = 110, Type = ColumnType.Number, Sortable = true },
                new("Status", "状态") { Width = 100, Statuses = statuses },
            ],
        };
        AutomationProperties.SetName(table, "需求排期");
        table.Reordered += (_, next) => table.ItemsSource = rows = next;
        var hint = new Small { Tone = TextTone.Muted };
        void Sync() => hint.Text = table.Sort is null ? "拖动把手调整排期，排在前面的先做" : "按工时排序中：取消排序后才能拖动调整排期";
        table.PropertyChanged += (_, e) =>
        {
            if (e.Property == DataTable.SortProperty) Sync();
        };
        Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Children = { table, hint } };
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

    record StoreSales(string Store, int[] Days);

    static Control Wide()
    {
        const int days = 120;
        var start = new DateOnly(2026, 7, 1);
        string[] cities = ["杭州", "宁波", "苏州", "南京", "合肥", "上海"];
        StoreSales[] stores = [.. Enumerable.Range(0, 60).Select(store => new StoreSales($"{cities[store % cities.Length]} {store + 1:00} 店", [.. Enumerable.Range(0, days).Select(day => 40 + (store * 37 + day * 13) % 90)]))];
        List<DataTableColumn> columns = [new("Store", "门店") { Width = 120, Sticky = ColumnSticky.Start }];
        columns.AddRange(Enumerable.Range(0, days).Select(day => new DataTableColumn($"d{day}", $"{start.AddDays(day).Day} 日") { Width = 64, Align = TableAlign.End, Value = row => ((StoreSales)row).Days[day] }));
        columns.Add(new("Total", "合计") { Width = 96, Sticky = ColumnSticky.End, Align = TableAlign.End, Value = row => ((StoreSales)row).Days.Sum() });
        // 按月份分组：同一个月的日期列放在一组下面
        var groups = Enumerable.Range(0, days).GroupBy(day => start.AddDays(day).Month)
            .Select(month => new DataTableColumnGroup($"2026-{month.Key}", $"{month.Key} 月", [.. month.Select(day => $"d{day}")])).ToList();
        var table = new DataTable { ItemsSource = stores, Columns = columns, ColumnGroups = groups, GetKey = row => ((StoreSales)row).Store, Density = TableDensity.Compact, VirtualColumns = true, Height = 22 * 16 };
        AutomationProperties.SetName(table, "门店日销量");
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children = { table, new Small { Tone = TextTone.Muted, Text = $"{stores.Length} 行 × {columns.Count} 列，只建看得见的几十格" } },
        };
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

    record TodoItem(string Id, string Code, string Name, string Node, DateOnly Due);

    static Control MasterDetail()
    {
        var today = new DateOnly(2026, 10, 21);
        TodoItem[] todos =
        [
            new("t1", "PO-2026-0187", "办公椅供应商切换", "资料审核", new DateOnly(2026, 10, 23)),
            new("t2", "PO-2026-0214", "卡箍改用铝合金", "方案设计", new DateOnly(2026, 10, 16)),
            new("t3", "PO-2026-0233", "机柜导轨第二供应商", "财务复核", new DateOnly(2026, 10, 30)),
            new("t4", "FA-2026-0142", "密封件国产化", "方案评审", today),
        ];
        var table = new DataTable { ItemsSource = todos, GetKey = row => ((TodoItem)row).Id, ActiveKey = "t2", Density = TableDensity.Compact, Height = 18 * 16 };
        AutomationProperties.SetName(table, "待办");
        var detail = new ContentControl();
        void Show()
        {
            if (todos.FirstOrDefault(todo => todo.Id == table.ActiveKey as string) is not { } current)
            {
                detail.Content = new Empty { Size = TextSize.Sm, Title = "选一条看详情", VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
                return;
            }
            var descriptions = new Descriptions { Columns = 1, Size = TextSize.Sm, Margin = new global::Avalonia.Thickness(12) };
            descriptions.Items.AddRange(
            [
                new DescriptionItem { Label = "编号", Value = current.Code },
                new DescriptionItem { Label = "名称", Value = current.Name },
                new DescriptionItem { Label = "环节", Value = current.Node },
                new DescriptionItem { Label = "截止", Value = current.Due.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) },
            ]);
            detail.Content = descriptions;
        }
        table.Columns =
        [
            new("Code", "编号")
            {
                Width = 140,
                Cell = (row, _) =>
                {
                    var open = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Link, Content = ((TodoItem)row).Code };
                    open.Click += (_, _) => table.ActiveKey = ((TodoItem)row).Id;
                    return open;
                },
            },
            new("Node", "环节") { Width = 110 },
            new("Due", "剩余") { Width = 140, Cell = (row, _) => new DeadlineBadge { Due = ((TodoItem)row).Due, Today = today } },
        ];
        table.RowClicked += (_, row) => table.ActiveKey = ((TodoItem)row).Id;
        table.PropertyChanged += (_, e) =>
        {
            if (e.Property == DataTable.ActiveKeyProperty) Show();
        };
        Show();
        return new Card { Variant = CardVariant.Outline, Padding = Space.None, Radius = Radius.Md, Content = new Splitter { Height = 18 * 16, Size = 55, Start = table, End = detail } };
    }

    /// <summary>部门：还没加载的部门 Children 为 null、Lazy 为 true。</summary>
    sealed class Unit(string id, string name, string lead, int headcount, decimal budget)
    {
        public string Id => id;
        public string Name => name;
        public string Lead => lead;
        public int Headcount => headcount;
        public decimal Budget => budget;
        public Unit[]? Children { get; set; }
        public bool Lazy { get; set; }
    }

    static Control Org()
    {
        Unit[] org =
        [
            new("rd", "研发部", "林小满", 46, 3_200_000)
            {
                Children = [new("fe", "前端组", "赵浩然", 12, 820_000), new("be", "后端组", "陈默", 18, 1_300_000), new("qa", "测试组", "孙思远", 8, 480_000)],
            },
            new("mk", "市场部", "许悠", 21, 1_800_000) { Lazy = true },
            new("ds", "设计中心", "周一航", 9, 650_000),
        ];
        var table = new TreeDataTable
        {
            ItemsSource = org, GetKey = row => ((Unit)row).Id, MaxHeight = 20 * 16, Expanded = ["rd"],
            GetChildren = row => ((Unit)row).Children,
            HasChildren = row => ((Unit)row).Children is { Length: > 0 } || ((Unit)row).Lazy,
            // 懒加载：展开还没有子项的部门时取回来、放进数据里；任务结束前展开按钮显示加载中
            LoadChildren = async row =>
            {
                await Task.Delay(900);
                var unit = (Unit)row;
                unit.Children = [new("brand", "品牌组", "王晴", 7, 900_000), new("channel", "渠道组", "李雨桐", 14, 900_000)];
                unit.Lazy = false;
            },
            Columns =
            [
                new("Name", "部门") { Width = 200, Sortable = true },
                new("Lead", "负责人")
                {
                    Width = 140,
                    Cell = (row, _) => new Flex
                    {
                        Align = FlexAlign.Center, Gap = Space.Sm,
                        Children = { new Avatar { Size = TextSize.Xs, DisplayName = ((Unit)row).Lead }, new Text { Text = ((Unit)row).Lead } },
                    },
                },
                // 数字列：按第一个值推断成数字，靠右对齐
                new("Headcount", "人数") { Width = 80, Sortable = true },
                // 预算以元存、按万元显示：Scale 缩放，单位写在表头里，一列数字更整齐
                new("Budget", "年度预算（万元）") { Width = 150, Sortable = true, Format = new NumericFormat { Scale = 10_000, MinimumFractionDigits = 1, MaximumFractionDigits = 1 } },
            ],
        };
        AutomationProperties.SetName(table, "组织架构");
        return table;
    }

    /// <summary>台账的一行：金额以元保存（测算、合同价），合同没签时合同价为 null。</summary>
    record LedgerTask(string Code, string Name, string Category, string Team, decimal Estimate, decimal? Agreed, decimal? Dmc, string Status);

    static Control Ledger()
    {
        var statuses = new StatusMap
        {
            ["designing"] = new("设计中", StatusTone.Info),
            ["verifying"] = new("测试中", StatusTone.Info),
            ["awaiting"] = new("待验收", StatusTone.Warning),
            ["settling"] = new("结算中", StatusTone.Warning),
            ["closed"] = new("关闭", StatusTone.Success),
        };
        LedgerTask[] tasks =
        [
            new("PO-2026-0301", "华东门店陈列改造", "产品改进", "研发中心", 155353.20m, null, null, "verifying"),
            new("PO-2026-0302", "仓库拣货流程优化", "流程优化", "产品部", 109600.00m, 98000.00m, 3.20m, "closed"),
            new("PO-2026-0303", "包装纸箱第二供应商", "供应商切换", "采购", 143000.00m, 150500.00m, null, "negotiating"),
            new("PO-2026-0304", "会员小程序改版", "产品改进", "研发中心", 178800.00m, null, 1.75m, "awaiting"),
            new("PO-2026-0305", "仓库盘点流程优化", "流程优化", "产品部", 217000.00m, null, null, "designing"),
            new("PO-2026-0306", "门店物料供应商切换", "供应商切换", "采购", 257600.00m, 240000.00m, 0.80m, "closed"),
        ];
        // 以元保存、按万元显示：表头写单位，数字列更整齐
        var wan = new NumericFormat { Scale = 10_000, MinimumFractionDigits = 2, MaximumFractionDigits = 2 };
        DataTableColumn[] columns =
        [
            new("Code", "项目编码") { Width = 132, Sticky = ColumnSticky.Start, Sortable = true },
            new("Name", "项目名称") { Width = 240, Sortable = true },
            new("Category", "类别") { Width = 96 },
            new("Team", "责任团队") { Width = 120 },
            new("Estimate", "测算（万元）") { Width = 128, Format = wan, Sortable = true, Summary = ColumnSummary.Sum },
            new("Agreed", "合同价（万元）") { Width = 128, Format = wan, Empty = "待签约", Summary = ColumnSummary.Sum },
            new("Dmc", "运费节省（元/单）") { Width = 140, Format = new NumericFormat { MinimumFractionDigits = 2 } },
            new("Status", "状态") { Width = 120, Statuses = statuses },
            new("Actions", "操作")
            {
                Width = 112, Sticky = ColumnSticky.End,
                Cell = (row, _) =>
                {
                    var more = new IconButton { Size = TextSize.Sm, Icon = Lucide.Ellipsis };
                    AutomationProperties.SetName(more, $"更多：{((LedgerTask)row).Code}");
                    return new Flex
                    {
                        Align = FlexAlign.Center, Gap = Space.Xs,
                        Children =
                        {
                            new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Content = "查看" },
                            new Menu { Trigger = more, Items = { new MenuItem { Icon = Lucide.FileText, Content = "任务卡" }, new MenuItem { Icon = Lucide.Send, Content = "提交验收" } } },
                        },
                    };
                },
            },
        ];
        var table = new DataTable
        {
            ItemsSource = tasks, Columns = columns, GetKey = row => ((LedgerTask)row).Code, SelectionMode = SelectionMode.Multiple,
            IsRowSelectable = row => ((LedgerTask)row).Status != "closed", HiddenColumns = ["Dmc"], MaxHeight = 24 * 16,
        };
        AutomationProperties.SetName(table, "项目台账");
        // 可以隐藏的列（编码、名称、操作一直显示）
        var columnsMenu = new Menu { Trigger = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Columns3, Content = "列" } };
        foreach (var column in columns.Where(column => column.Key is not ("Code" or "Name" or "Actions")))
        {
            var item = new MenuCheckboxItem { Content = column.Header, IsChecked = !table.HiddenColumns!.Contains(column.Key) };
            item.PropertyChanged += (_, e) =>
            {
                if (e.Property != MenuCheckboxItem.IsCheckedProperty) return;
                var hidden = table.HiddenColumns ?? [];
                table.HiddenColumns = item.IsChecked ? [.. hidden.Where(key => key != column.Key)] : [.. hidden, column.Key];
            };
            columnsMenu.Items.Add(item);
        }
        var bar = new SelectionBar
        {
            Actions =
            {
                new Button { Size = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Download, Content = "导出所选" },
                new Button { Size = TextSize.Sm, Icon = Lucide.Send, Content = "批量提交验收" },
            },
        };
        bar.ClearRequested += (_, _) => table.Selected = [];
        table.PropertyChanged += (_, e) =>
        {
            if (e.Property == DataTable.SelectedProperty) bar.Count = table.Selected.Count;
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children =
            {
                new Flex
                {
                    Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm, Wrap = true,
                    Children = { new Small { Tone = TextTone.Muted, Text = $"共 {tasks.Length} 项 · 已关闭的不能勾选" }, new Flex { Gap = Space.Sm, Children = { columnsMenu } } },
                },
                bar,
                table,
            },
        };
    }

    /// <summary>导入校验的一行：校验的问题（错误或提醒）。</summary>
    record ImportLine(int Row, string Part, string Name, int? Quantity, string? Price, (bool Error, string Message)? Issue = null);

    static Control ImportCheck()
    {
        ImportLine[] lines =
        [
            new(2, "G26", "笔记本电脑", 2, "450.00"),
            new(3, "G29", "笔记本电脑（更改后）", 2, "480.00"),
            new(4, "", "紧固件组件", 36, "12.50", (true, "件号不能为空")),
            new(5, "G43", "支架", null, "96.00", (true, "数量必须大于 0")),
            new(6, "G44", "支架（更改后）", 1, "1960.00", (false, "单价比更改前高 20 倍，请核对")),
            new(7, "G73", "密封条", 4, "38.80"),
        ];
        DataTableColumn[] columns =
        [
            new("Row", "行") { Width = 56, Sticky = ColumnSticky.Start },
            new("Issue", "校验结果")
            {
                Width = 240, Wrap = true,
                Cell = (row, _) => ((ImportLine)row).Issue is { } issue
                    ? new Flex
                    {
                        Align = FlexAlign.Center, Gap = Space.Xs,
                        Children =
                        {
                            new Icon { Data = issue.Error ? Lucide.CircleExclamationPoint : Lucide.TriangleExclamationPoint, Tone = issue.Error ? TextTone.Danger : TextTone.Warning },
                            new TextBlock { Text = issue.Message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap },
                        },
                    }
                    : new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Icon { Data = Lucide.CircleCheck, Tone = TextTone.Success }, new TextBlock { Text = "通过" } } },
            },
            new("Part", "件号") { Width = 96 },
            new("Name", "零件名称") { Width = 180 },
            new("Quantity", "数量") { Width = 96 },
            new("Price", "材料单价（元）") { Width = 128, Type = ColumnType.Number, Format = new NumericFormat { MinimumFractionDigits = 2 } },
        ];
        var table = new DataTable
        {
            ItemsSource = lines, Columns = columns, GetKey = row => ((ImportLine)row).Row.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RowTone = row => ((ImportLine)row).Issue is { } issue ? issue.Error ? StatusTone.Danger : StatusTone.Warning : null,
            SelectionMode = SelectionMode.Multiple, Selected = ["2", "3", "7"], IsRowSelectable = row => ((ImportLine)row).Issue is not { Error: true },
            Density = TableDensity.Compact,
        };
        AutomationProperties.SetName(table, "导入校验结果");
        var errors = lines.Count(line => line.Issue is { Error: true });
        var warnings = lines.Count(line => line.Issue is { Error: false });
        var submit = new Button { Size = TextSize.Sm };
        void Sync()
        {
            submit.Content = $"导入所选 {table.Selected.Count} 行";
            submit.IsEnabled = table.Selected.Count > 0;
        }
        table.PropertyChanged += (_, e) =>
        {
            if (e.Property == DataTable.SelectedProperty) Sync();
        };
        Sync();
        var spacer = new Control();
        Flex.SetGrow(spacer, true);
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children =
            {
                new Flex
                {
                    Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true,
                    Children =
                    {
                        new Badge { Tone = StatusTone.Danger, Dot = true, Content = $"{errors} 行错误" },
                        new Badge { Tone = StatusTone.Warning, Dot = true, Content = $"{warnings} 行提醒" },
                        new Small { Tone = TextTone.Muted, Text = "错误行改好后重新上传；提醒行可以照常导入" },
                        spacer,
                        submit,
                    },
                },
                table,
            },
        };
    }

    record ProjectTaskRow(string Code, string Name, string Owner, string Due, string Status);

    static Control Locate()
    {
        var statuses = new StatusMap { ["doing"] = new("进行中", StatusTone.Info), ["overdue"] = new("已超期", StatusTone.Danger), ["done"] = new("已完成", StatusTone.Success) };
        string[] owners = ["张工", "李工", "王工", "赵工", "刘工"];
        string[] parts = ["显示器", "支架", "纸箱", "线缆", "键盘", "鼠标"];
        ProjectTaskRow[] tasks =
        [
            .. Enumerable.Range(0, 2000).Select(index => new ProjectTaskRow(
                $"PO-2026-{index + 1:0000}", $"{parts[index % parts.Length]}改进方案 {index + 1}", owners[index * 3 % owners.Length],
                $"2026-{index % 12 + 1:00}-{index % 28 + 1:00}", index % 17 == 0 ? "overdue" : index % 3 == 0 ? "done" : "doing")),
        ];
        string? flashing = null;
        var table = new DataTable
        {
            ItemsSource = tasks, GetKey = row => ((ProjectTaskRow)row).Code, Density = TableDensity.Compact, MaxHeight = 22 * 16,
            RowTone = row => ((ProjectTaskRow)row).Code == flashing ? StatusTone.Info : null,
            Columns =
            [
                new("Code", "任务编码") { Width = 136, Sticky = ColumnSticky.Start },
                new("Name", "任务名称"),
                new("Owner", "责任人") { Width = 96 },
                new("Due", "截止日期") { Width = 120 },
                new("Status", "状态") { Width = 104, Statuses = statuses },
            ],
        };
        AutomationProperties.SetName(table, "项目任务");
        var code = new TextField { Text = "PO-2026-1360", PlaceholderText = "任务编码", Width = 14 * 16 };
        AutomationProperties.SetName(code, "任务编码");
        var missing = new Small { Tone = TextTone.Danger, Text = "没有这个任务", IsVisible = false };
        var timer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        void Flash(string? key)
        {
            flashing = key;
            // 色调由 RowTone 给：重设一次让各行重画
            var tone = table.RowTone;
            table.RowTone = null;
            table.RowTone = tone;
        }
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Flash(null);
        };
        void Go(string key)
        {
            missing.IsVisible = !table.ScrollToRow(key, ScrollAlign.Center);
            if (missing.IsVisible) return;
            // 滚到之后闪一下：浅色底停 1.5 秒
            Flash(key);
            timer.Stop();
            timer.Start();
        }
        code.KeyDown += (_, e) =>
        {
            if (e.Key == global::Avalonia.Input.Key.Enter) Go((code.Text ?? "").Trim());
        };
        var locate = new Button { Size = TextSize.Sm, Icon = Lucide.LocateFixed, Content = "定位" };
        locate.Click += (_, _) => Go((code.Text ?? "").Trim());
        var first = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Content = "第一条" };
        first.Click += (_, _) => Go(tasks[0].Code);
        var last = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Content = "最后一条" };
        last.Click += (_, _) => Go(tasks[^1].Code);
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children = { new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { code, locate, first, last, missing } }, table },
        };
    }

    record FeedEvent(int Id, string At, string Message);

    static Control Infinite()
    {
        const int page = 40, total = 400;
        string[] messages = ["用户登录成功", "导出报表：2026 年 9 月销售明细（含退款与优惠券核销）", "支付回调重试第 2 次", "库存同步完成"];
        var events = new InfiniteList<FeedEvent>(async (cursor, cancellation) =>
        {
            var number = (int?)cursor ?? 0;
            await Task.Delay(400, cancellation);
            FeedEvent[] items = [.. Enumerable.Range(0, page).Select(index =>
            {
                var id = number * page + index + 1;
                return new FeedEvent(id, $"10:{id % 60:00}", messages[id % messages.Length]);
            })];
            return new InfinitePage<FeedEvent>(items, (number + 1) * page < total ? number + 1 : null);
        }) { KeyOf = item => item.Id, Threshold = 10 };
        var table = new DataTable
        {
            GetKey = row => ((FeedEvent)row).Id, Height = 18 * 16,
            Columns = [new("Id", "#") { Width = 64 }, new("At", "时间") { Width = 80 }, new("Message", "事件") { Width = 280, Wrap = true }],
        };
        AutomationProperties.SetName(table, "事件流");
        var loaded = new Small { Tone = TextTone.Muted };
        void Sync()
        {
            table.ItemsSource = events.Items;
            table.IsLoading = events.IsLoading;
            loaded.Text = $"已加载 {events.Count} / {total} 条";
        }
        events.PropertyChanged += (_, _) => Sync();
        table.RangeChanged += (_, range) => events.OnRangeChanged(range);
        Sync();
        _ = events.Refresh();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Children = { table, new InfiniteListStatus { List = events }, loaded } };
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
