namespace Rune.WinForms.Examples;

/// <summary>Checklist 页的示例（vue-tsx-pro 文档站 display/Checklist）。compact 的表格用静态 TableControl（还没有 ColumnTable）。</summary>
static class ChecklistExamples
{
    public static Example[] All =>
    [
        new("Checklist", "closing", "里程碑与结项材料", "每项的 Status 决定图标与颜色：Done 完成、Todo 待办（默认）、Overdue 超时（日期标红）、Blocked 受阻、Skipped 不适用（划掉，不算进进度）。名称下面是 Description 与一行 Owner · Date · Evidence，Action 在结束边（催办、上传）。Summary 显示完成数与进度条。状态写在读屏的名称里。", Closing),
        new("Checklist", "launch", "可以勾选：上线检查", "订阅了 CheckedChange 时状态图标是复选按钮（读屏里是复选框，名称是这一项），点了报告新的完成状态，状态由使用方保存（换掉 Items）；不适用、受阻的不能勾。", Launch),
        new("Checklist", "compact", "表格里与一行排开", "ItemLayout=Icons 只有一排状态图标，放在表格的格子里，悬停与读屏有每一项的说明；ItemLayout=Inline 一行排开、放不下换行，显示名称、日期与证据。", Compact),
    ];

    static Control Evidence(string name) => Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xxs }, new IconControl { Data = Lucide.Paperclip, Tone = TextTone.Accent }, new LinkControl { Text = name });

    static Control Closing()
    {
        ChecklistItem[] milestones =
        [
            new() { Label = "方案确定", Status = ChecklistStatus.Done, Owner = "方圆", Date = "2026-08-12 完成", Evidence = Evidence("方案评审纪要.pdf") },
            new() { Label = "样品验收", Status = ChecklistStatus.Done, Owner = "林小满", Date = "2026-09-20 完成", Evidence = Evidence("样品验收报告.pdf") },
            new()
            {
                Label = "批量切换", Status = ChecklistStatus.Overdue, Description = "生产线切换到新供应商的首批零件，附切换单", Owner = "周宁", Date = "计划 2026-10-15，已超 9 天",
                Action = new ButtonControl { Text = "催办", TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Icon = Lucide.BellRing },
            },
        ];
        ChecklistItem[] closing =
        [
            new() { Label = "验收报告", Status = ChecklistStatus.Done, Date = "2026-10-08", Evidence = Evidence("验收报告.pdf") },
            new() { Label = "财务复核意见", Description = "测算锁定后出具", Owner = "由财务部出具", Action = new ButtonControl { Text = "上传", TextSize = TextSize.Sm, Variant = ButtonVariant.Surface, Icon = Lucide.Upload } },
            new() { Label = "合同签订记录", Status = ChecklistStatus.Blocked, Description = "等财务复核意见：合同价格要按复核后的金额", Owner = "由采购部出具" },
            new() { Label = "海关备案", Status = ChecklistStatus.Skipped, Description = "国内供应商，不涉及" },
        ];
        return Build.Grid(new GridControl { MinItemWidth = "22em", Gap = Space.Lg },
            Build.Card(new CardControl { Header = "里程碑与交付证据" }, new ChecklistControl { Items = milestones, Summary = true }),
            Build.Card(new CardControl { Header = "结项材料", Extra = new BadgeControl { Text = "缺 2 项", Tone = StatusTone.Warning } }, new ChecklistControl { Items = closing, Summary = true }));
    }

    static Control Launch()
    {
        var items = new List<ChecklistItem>
        {
            new() { Key = "backup", Label = "数据库备份", Description = "上线前全量备份，保留 7 天", Status = ChecklistStatus.Done },
            new() { Key = "migration", Label = "执行迁移脚本", Description = "V42__add_order_index.sql" },
            new() { Key = "config", Label = "核对生产配置", Description = "对照配置清单逐项确认", Owner = "运维" },
            new() { Key = "smoke", Label = "冒烟测试", Owner = "测试", Status = ChecklistStatus.Blocked, Date = "等迁移完成" },
            new() { Key = "cdn", Label = "刷新 CDN", Status = ChecklistStatus.Skipped, Description = "这次没有静态资源改动" },
        };
        var checklist = new ChecklistControl { Items = items, Summary = true };
        checklist.CheckedChange += (_, e) =>
        {
            // 状态由使用方保存：换成新的一项，再换掉 Items
            var index = items.FindIndex(item => item.Key == e.Item.Key);
            items[index] = new ChecklistItem
            {
                Key = e.Item.Key, Label = e.Item.Label, Description = e.Item.Description, Owner = e.Item.Owner, Date = e.Item.Date,
                Status = e.Done ? ChecklistStatus.Done : ChecklistStatus.Todo,
            };
            checklist.Items = [.. items];
        };
        return Build.Card(new CardControl(), checklist);
    }

    static Control Compact()
    {
        static ChecklistItem Node(string label, ChecklistStatus status, string? date = null) => new() { Label = label, Status = status, Date = date };
        (string Code, string Name, ChecklistItem[] Nodes)[] projects =
        [
            ("PO-2026-0187", "官网首页改版", [Node("评审", ChecklistStatus.Done, "08-12"), Node("样品", ChecklistStatus.Done, "09-20"), Node("切换", ChecklistStatus.Overdue, "计划 10-15"), Node("关闭", ChecklistStatus.Todo)]),
            ("PO-2026-0203", "线缆收纳夹国产化", [Node("评审", ChecklistStatus.Done, "09-02"), Node("样品", ChecklistStatus.Todo, "计划 11-05"), Node("切换", ChecklistStatus.Todo), Node("关闭", ChecklistStatus.Todo)]),
            ("PO-2026-0211", "门店照明改造", [Node("评审", ChecklistStatus.Done, "07-30"), Node("样品", ChecklistStatus.Done, "08-28"), Node("切换", ChecklistStatus.Done, "09-30"), Node("关闭", ChecklistStatus.Blocked, "等财务复核")]),
        ];
        var head = new TableRowControl { Controls = { new TableHeaderCellControl { Text = "编号" }, new TableHeaderCellControl { Text = "名称" }, new TableHeaderCellControl { Text = "节点" } } };
        var body = new TableBodyControl();
        foreach (var (code, name, nodes) in projects)
        {
            var cell = new TableCellControl();
            cell.Controls.Add(new ChecklistControl { ItemLayout = ChecklistLayout.Icons, Items = nodes });
            body.Controls.Add(new TableRowControl { Controls = { new TableCellControl { Text = code }, new TableCellControl { Text = name }, cell } });
        }
        var table = new TableControl { AccessibleName = "项目", Controls = { new TableHeadControl { Controls = { head } }, body } };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch },
            table,
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
                new SmallControl { Text = "PO-2026-0187 的实施节点", Tone = TextTone.Muted }, new ChecklistControl { ItemLayout = ChecklistLayout.Inline, Items = projects[0].Nodes }));
    }
}
