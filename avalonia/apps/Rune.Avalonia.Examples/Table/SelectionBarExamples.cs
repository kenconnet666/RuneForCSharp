using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>SelectionBar 的示例：上游在 DataTable 页的“台账”示例里（勾选行后表格上方出现批量操作栏），数据表格还没做，这里配静态的 Table。</summary>
static class SelectionBarExamples
{
    public static Example[] All =>
    [
        new("DataTable", "selection-bar", "批量操作栏", "SelectionBar 放在表格上方：勾选了行之后出现“已选 N 项 · 取消选择”，结束边是批量操作；Count 变成 0 时淡出、不占位。数目变化读屏会念出来；“取消选择”报告 ClearRequested，清空选择由使用方做。", Ledger),
    ];

    static readonly (string Code, string Supplier, string Amount)[] Invoices =
    [
        ("INV-2026-0418", "华辰精密", "¥12,800.00"),
        ("INV-2026-0421", "恒远五金", "¥3,460.50"),
        ("INV-2026-0427", "远航物流", "¥8,900.00"),
        ("INV-2026-0433", "北辰电子", "¥21,350.00"),
    ];

    static Control Ledger()
    {
        var checks = new List<Checkbox>();
        var bar = new SelectionBar
        {
            Actions =
            {
                new Button { Size = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Content = "批量导出" },
                new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Content = "批量删除" },
            },
        };
        void Count() => bar.Count = checks.Count(check => check.IsChecked);
        bar.ClearRequested += (_, _) =>
        {
            foreach (var check in checks) check.IsChecked = false;
        };
        var head = new TableRow();
        foreach (var header in new[] { "", "发票号", "供应商", "金额" }) head.Cells.Add(new TableHeaderCell { Content = header, Align = header == "金额" ? TableAlign.End : default });
        var body = new TableBody();
        foreach (var (code, supplier, amount) in Invoices)
        {
            var check = new Checkbox();
            global::Avalonia.Automation.AutomationProperties.SetName(check, $"选择 {code}");
            check.PropertyChanged += (_, e) => { if (e.Property == ChoiceBase.IsCheckedProperty) Count(); };
            checks.Add(check);
            body.Rows.Add(new TableRow { Cells = { new TableCell { Content = check }, new TableCell { Content = code }, new TableCell { Content = supplier }, new TableCell { Content = amount, Align = TableAlign.End } } });
        }
        checks[0].IsChecked = checks[2].IsChecked = true;
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm,
            Children = { bar, new Table { Density = TableDensity.Compact, Sections = { new TableHead { Rows = { head } }, body } } },
        };
    }
}
