using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf.Examples;

/// <summary>Table 的示例（vue-tsx-pro 文档站 table/Table）。invoice 要等 ToggleGroup；column-table 要等 ColumnTable。</summary>
static class TableExamples
{
    public static Example[] All =>
    [
        new("Table", "plans", "套餐对比", "TableHead、TableBody 里放 TableRow，行里放 TableHeaderCell 与 TableCell；表体里的 TableHeaderCell 是行标题。TableCaption 是表格的标题，也是读屏名称。勾与叉是图标，旁边放一份给读屏的名称。", Plans),
    ];

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

    static FrameworkElement Plans()
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
