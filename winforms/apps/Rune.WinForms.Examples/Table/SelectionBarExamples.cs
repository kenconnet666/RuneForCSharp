namespace Rune.WinForms.Examples;

/// <summary>SelectionBarControl 的示例：上游在 DataTable 页的“台账”示例里（勾选行后表格上方出现批量操作栏），数据表格还没做，这里配静态的 TableControl。</summary>
static class SelectionBarExamples
{
    public static Example[] All =>
    [
        new("DataTable", "selection-bar", "批量操作栏", "SelectionBarControl 放在表格上方：勾选了行之后出现“已选 N 项 · 取消选择”，结束边是批量操作；Count 变成 0 时不显示、不占位。数目变化读屏会念出来；“取消选择”报告 ClearRequested，清空选择由使用方做。", Ledger),
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
        var checks = new List<CheckboxControl>();
        var bar = new SelectionBarControl();
        bar.Actions.AddRange([
            new ButtonControl { Text = "批量导出", TextSize = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral },
            new ButtonControl { Text = "批量删除", TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Danger },
        ]);
        void Count() => bar.Count = checks.Count(check => check.IsChecked);
        bar.ClearRequested += (_, _) =>
        {
            foreach (var check in checks) check.IsChecked = false;
        };
        var head = new TableRowControl();
        foreach (var header in new[] { "", "发票号", "供应商", "金额" })
            head.Controls.Add(new TableHeaderCellControl { Text = header, Align = header == "金额" ? TableAlign.End : TableAlign.Start });
        var body = new TableBodyControl();
        foreach (var (code, supplier, amount) in Invoices)
        {
            var check = new CheckboxControl { AccessibleName = $"选择 {code}" };
            check.IsCheckedChanged += (_, _) => Count();
            checks.Add(check);
            var select = new TableCellControl();
            select.Controls.Add(check);
            body.Controls.Add(new TableRowControl
            {
                Controls = { select, new TableCellControl { Text = code }, new TableCellControl { Text = supplier }, new TableCellControl { Text = amount, Align = TableAlign.End } },
            });
        }
        checks[0].IsChecked = checks[2].IsChecked = true;
        var table = new TableControl { Density = TableDensity.Compact };
        table.Controls.AddRange([new TableHeadControl { Controls = { head } }, body]);
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Dock = DockStyle.Top }, bar, table);
    }
}
