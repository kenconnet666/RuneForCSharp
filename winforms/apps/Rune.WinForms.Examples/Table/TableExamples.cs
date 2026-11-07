namespace Rune.WinForms.Examples;

/// <summary>
/// Table 的示例（vue-tsx-pro 文档站 table/Table）。invoice 要等 ToggleGroup；column-table 要等 ColumnTable。
/// WinForms 里是 TableControl、TableHeadControl、TableBodyControl、TableRowControl、TableCellControl 这一族。
/// </summary>
static class TableExamples
{
    public static Example[] All =>
    [
        new("Table", "plans", "套餐对比", "TableHeadControl、TableBodyControl 里放 TableRowControl，行里放 TableHeaderCellControl 与 TableCellControl；表体里的 TableHeaderCellControl 是行标题。TableCaptionControl 是表格的标题，也是读屏名称。勾与叉是图标，旁边放一份给读屏的名称。", Plans),
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

    static Control Plans()
    {
        var head = new TableRowControl { Controls = { new TableHeaderCellControl { Text = "功能" } } };
        foreach (var name in Names)
        {
            var cell = new TableHeaderCellControl { Align = TableAlign.Center };
            if (name == "团队版")
                cell.Controls.Add(new FlexControl
                {
                    Align = FlexAlign.Center, Gap = Space.Xs, AutoSize = true,
                    Controls = { new TextControl { Text = name }, new BadgeControl { Tone = StatusTone.Accent, Radius = Radius.Full, Text = "推荐" } },
                });
            else cell.Text = name;
            head.Controls.Add(cell);
        }
        var body = new TableBodyControl();
        foreach (var (feature, values) in Features)
        {
            var row = new TableRowControl { Controls = { new TableHeaderCellControl { Scope = "row", Text = feature } } };
            foreach (var value in values) row.Controls.Add(Mark(value));
            body.Controls.Add(row);
        }
        return new TableControl
        {
            AccessibleName = "套餐对比",
            Controls = { new TableCaptionControl { Text = "三档套餐的功能对比，价格按年付折算" }, new TableHeadControl { Controls = { head } }, body },
        };
    }

    /// <summary>勾与叉是图标：读屏名称是"包含""不包含"。</summary>
    static TableCellControl Mark(object value)
    {
        var cell = new TableCellControl { Align = TableAlign.Center };
        if (value is bool included) cell.Controls.Add(new IconControl { Data = included ? Lucide.Check : Lucide.Minus, AccessibleName = included ? "包含" : "不包含" });
        else cell.Text = (string)value;
        return cell;
    }
}
