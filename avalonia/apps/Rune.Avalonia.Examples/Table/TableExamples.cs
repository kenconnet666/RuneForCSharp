using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Table 的示例（vue-tsx-pro 文档站 table/Table）。invoice 要等 ToggleGroup。</summary>
static class TableExamples
{
    public static Example[] All =>
    [
        new("Table", "plans", "套餐对比", "TableHead、TableBody 里放 TableRow，行里放 TableHeaderCell 与 TableCell；表体里的 TableHeaderCell 是行标题。TableCaption 是表格的标题，也是读屏名称。勾与叉是图标，旁边放一份给读屏的名称。", Plans),
        new("Table", "column-table", "列定义与合并单元格：询价比价单", "ColumnTable 按列定义渲染静态表格：列定义与 DataTable 相同（金额按语言、状态、空值）。供应商与品类上下相同的格合并（MergeRepeated，层层分组）；没报价的行用 GetCellSpan 把“单价”“小计”合成一格写原因；底部合计（ColumnSummary.Sum 精确相加）。", Quotes),
        new("Table", "column-settings", "列设置：显示哪些列、什么顺序", "ColumnSettings 弹出全部的列：勾选显示哪些、拖动把手（或聚焦后用键盘）调整顺序，“重置”回到列定义。Hidden、Order 写回新的列表；表格用 TableColumns.Arrange 按顺序排列、去掉隐藏的列。上游只在 QueryTable 里用它，这个示例配的是 ColumnTable。", Settings),
    ];

    // 列设置配静态表格：两边看同一份 Hidden、Order
    static Control Settings()
    {
        DataTableColumn[] columns =
        [
            new("Part", "零件"),
            new("Supplier", "供应商"),
            new("Quantity", "数量") { Type = ColumnType.Number },
            new("Price", "单价") { Type = ColumnType.Currency },
            new("Status", "状态") { Statuses = QuoteStatuses },
        ];
        var table = new ColumnTable { ItemsSource = QuoteLines, Columns = columns, Density = TableDensity.Compact };
        AutomationProperties.SetName(table, "报价");
        var settings = new ColumnSettings { Columns = columns };
        settings.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColumnSettings.HiddenProperty || e.Property == ColumnSettings.OrderProperty)
                table.Columns = [.. TableColumns.Arrange(columns, settings.Order).Where(column => !settings.Hidden.Contains(column.Key))];
        };
        var bar = new Flex { Justify = FlexJustify.End, Children = { settings } };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Children = { bar, table } };
    }

    sealed record QuoteLine(string Supplier, string Category, string Part, int Quantity, decimal? Price, string Status, string? Note = null);

    static readonly StatusMap QuoteStatuses = new()
    {
        ["valid"] = new("有效", StatusTone.Success),
        ["expiring"] = new("即将过期", StatusTone.Warning),
        ["missing"] = new("未报价", StatusTone.Neutral),
    };

    static readonly QuoteLine[] QuoteLines =
    [
        new("华辰精密", "机械件", "显示器支架", 40, 128, "valid"),
        new("华辰精密", "机械件", "铰链底座", 80, 76.5m, "valid"),
        new("华辰精密", "钣金件", "检修口盖板", 24, 212, "expiring"),
        new("启航塑胶", "塑料件", "笔记本电脑", 12, 1860, "valid"),
        new("启航塑胶", "塑料件", "加强筋", 60, null, "missing", "工艺评审中，预计 10 月 30 日报价"),
        new("远山钣金", "钣金件", "检修口盖板", 24, 198, "valid"),
    ];

    // 询价比价单：没报价的行，单价占两列写原因（Cell 返回 null 的行照类型显示），小计那一格被合并掉
    static Control Quotes()
    {
        var table = new ColumnTable
        {
            ItemsSource = QuoteLines,
            Density = TableDensity.Compact,
            MergeRepeated = ["Supplier", "Category"],
            Columns =
            [
                new("Supplier", "供应商") { Summary = ColumnSummary.Of(rows => $"{rows.Count} 项") },
                new("Category", "品类"),
                new("Part", "零件"),
                new("Quantity", "数量") { Type = ColumnType.Number },
                new("Price", "单价")
                {
                    Type = ColumnType.Currency,
                    Align = TableAlign.End,
                    Cell = (row, _) => row is QuoteLine { Price: null, Note: var note } ? new Small { Text = note, Tone = TextTone.Muted } : null,
                },
                new("Subtotal", "小计") { Type = ColumnType.Currency, Value = row => ((QuoteLine)row).Price * ((QuoteLine)row).Quantity, Summary = ColumnSummary.Sum },
                new("Status", "状态") { Statuses = QuoteStatuses },
            ],
            GetCellSpan = (row, column, _) => row is QuoteLine { Price: null } ? column.Key switch
            {
                "Price" => new CellSpan(ColumnSpan: 2),
                "Subtotal" => new CellSpan(ColumnSpan: 0),
                _ => null,
            } : null,
        };
        AutomationProperties.SetName(table, "询价比价单");
        return table;
    }

    static readonly string[] Names = ["免费版", "团队版", "企业版"];

    static readonly (string Feature, object[] Values)[] Features =
    [
        ("成员数", ["3", "50", "不限"]),
        ("存储空间", ["5 GB", "200 GB", "2 TB"]),
        ("单点登录（SSO）", [false, false, true]),
        ("操作审计", [false, true, true]),
        ("专属客服", [false, false, true]),
        ("价格（每月）", ["¥0", "¥299", "¥1,999"]),
    ];

    static Control Plans()
    {
        var head = new TableRow { Cells = { new TableHeaderCell { Content = "功能" } } };
        foreach (var name in Names)
        {
            object content = name == "团队版"
                ? new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Text { Text = name }, new Badge { Tone = StatusTone.Accent, Radius = Radius.Full, Content = "推荐" } } }
                : name;
            head.Cells.Add(new TableHeaderCell { Align = TableAlign.Center, Content = content });
        }
        var body = new TableBody();
        foreach (var (feature, values) in Features)
        {
            var row = new TableRow { Cells = { new TableHeaderCell { Scope = "row", Content = feature } } };
            foreach (var value in values) row.Cells.Add(new TableCell { Align = TableAlign.Center, Content = Mark(value) });
            body.Rows.Add(row);
        }
        var table = new Table
        {
            Sections = { new TableCaption { Content = "三档套餐的功能对比，价格按年付折算" }, new TableHead { Rows = { head } }, body },
        };
        AutomationProperties.SetName(table, "套餐对比");
        return table;
    }

    /// <summary>勾与叉是图标：读屏名称是"包含""不包含"。</summary>
    static object Mark(object value)
    {
        if (value is not bool included) return value;
        var icon = new Icon { Data = included ? Lucide.Check : Lucide.Minus };
        AutomationProperties.SetName(icon, included ? "包含" : "不包含");
        return icon;
    }
}
