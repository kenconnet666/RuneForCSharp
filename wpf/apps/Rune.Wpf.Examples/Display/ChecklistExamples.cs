using System.Windows;
using System.Windows.Automation;
using System.Windows.Documents;

namespace Rune.Wpf.Examples;

/// <summary>Checklist 页的示例（vue-tsx-pro 文档站 display/Checklist）。compact 的表格用静态 Table（还没有 ColumnTable）。</summary>
static class ChecklistExamples
{
    public static Example[] All =>
    [
        new("Checklist", "closing", "里程碑与结项材料", "每项的 Status 决定图标与颜色：Done 完成、Todo 待办（默认）、Overdue 超时（日期标红）、Blocked 受阻、Skipped 不适用（划掉，不算进进度）。名称下面是 Description 与一行 Owner · Date · Evidence，Action 在结束边（催办、上传）。Summary 显示完成数与进度条。状态写在读屏的名称里。", Closing),
        new("Checklist", "launch", "可以勾选：上线检查", "订阅了 CheckedChange 时状态图标是复选按钮（读屏里是复选框，名称是这一项），点了报告新的完成状态，状态由使用方保存（换掉 Items）；不适用、受阻的不能勾。", Launch),
        new("Checklist", "compact", "表格里与一行排开", "Layout=Icons 只有一排状态图标，放在表格的格子里，悬停与读屏有每一项的说明；Layout=Inline 一行排开、放不下换行，显示名称、日期与证据。", Compact),
    ];

    // WPF 的 Link 是行内的超链接：放进一段文字里，图标是行内的元素
    static FrameworkElement Evidence(string name) => new Text { Inlines = { new Link { Inlines = { new InlineUIContainer(new Icon { Data = Lucide.Paperclip }) { BaselineAlignment = BaselineAlignment.Center }, new Run(" " + name) } } } };

    static FrameworkElement Closing()
    {
        ChecklistItem[] milestones =
        [
            new() { Label = "方案确定", Status = ChecklistStatus.Done, Owner = "方圆", Date = "2026-08-12 完成", Evidence = Evidence("方案评审纪要.pdf") },
            new() { Label = "样品验收", Status = ChecklistStatus.Done, Owner = "林小满", Date = "2026-09-20 完成", Evidence = Evidence("样品验收报告.pdf") },
            new()
            {
                Label = "批量切换", Status = ChecklistStatus.Overdue, Description = "生产线切换到新供应商的首批零件，附切换单", Owner = "周宁", Date = "计划 2026-10-15，已超 9 天",
                Action = new Button { Content = "催办", Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Icon = Lucide.BellRing },
            },
        ];
        ChecklistItem[] closing =
        [
            new() { Label = "验收报告", Status = ChecklistStatus.Done, Date = "2026-10-08", Evidence = Evidence("验收报告.pdf") },
            new() { Label = "财务复核意见", Description = "测算锁定后出具", Owner = "由财务部出具", Action = new Button { Content = "上传", Size = TextSize.Sm, Variant = ButtonVariant.Surface, Icon = Lucide.Upload } },
            new() { Label = "合同签订记录", Status = ChecklistStatus.Blocked, Description = "等财务复核意见：合同价格要按复核后的金额", Owner = "由采购部出具" },
            new() { Label = "海关备案", Status = ChecklistStatus.Skipped, Description = "国内供应商，不涉及" },
        ];
        return new Grid
        {
            MinItemWidth = "22em", Gap = Space.Lg,
            Children =
            {
                new Card { Header = new H3 { Text = "里程碑与交付证据", Size = TextSize.Sm }, Content = new Checklist { Items = milestones, Summary = true } },
                new Card { Header = new H3 { Text = "结项材料", Size = TextSize.Sm }, Extra = new Badge { Content = "缺 2 项", Tone = StatusTone.Warning }, Content = new Checklist { Items = closing, Summary = true } },
            },
        };
    }

    static FrameworkElement Launch()
    {
        var items = new List<ChecklistItem>
        {
            new() { Key = "backup", Label = "数据库备份", Description = "上线前全量备份，保留 7 天", Status = ChecklistStatus.Done },
            new() { Key = "migration", Label = "执行迁移脚本", Description = "V42__add_order_index.sql" },
            new() { Key = "config", Label = "核对生产配置", Description = "对照配置清单逐项确认", Owner = "运维" },
            new() { Key = "smoke", Label = "冒烟测试", Owner = "测试", Status = ChecklistStatus.Blocked, Date = "等迁移完成" },
            new() { Key = "cdn", Label = "刷新 CDN", Status = ChecklistStatus.Skipped, Description = "这次没有静态资源改动" },
        };
        var checklist = new Checklist { Items = items, Summary = true };
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
        return new Card { Content = checklist };
    }

    static FrameworkElement Compact()
    {
        static ChecklistItem Node(string label, ChecklistStatus status, string? date = null) => new() { Label = label, Status = status, Date = date };
        (string Code, string Name, ChecklistItem[] Nodes)[] projects =
        [
            ("PO-2026-0187", "官网首页改版", [Node("评审", ChecklistStatus.Done, "08-12"), Node("样品", ChecklistStatus.Done, "09-20"), Node("切换", ChecklistStatus.Overdue, "计划 10-15"), Node("关闭", ChecklistStatus.Todo)]),
            ("PO-2026-0203", "线缆收纳夹国产化", [Node("评审", ChecklistStatus.Done, "09-02"), Node("样品", ChecklistStatus.Todo, "计划 11-05"), Node("切换", ChecklistStatus.Todo), Node("关闭", ChecklistStatus.Todo)]),
            ("PO-2026-0211", "门店照明改造", [Node("评审", ChecklistStatus.Done, "07-30"), Node("样品", ChecklistStatus.Done, "08-28"), Node("切换", ChecklistStatus.Done, "09-30"), Node("关闭", ChecklistStatus.Blocked, "等财务复核")]),
        ];
        var head = new TableRow { Cells = { new TableHeaderCell { Content = "编号" }, new TableHeaderCell { Content = "名称" }, new TableHeaderCell { Content = "节点" } } };
        var body = new TableBody();
        foreach (var (code, name, nodes) in projects)
            body.Rows.Add(new TableRow { Cells = { new TableCell { Content = code }, new TableCell { Content = name }, new TableCell { Content = new Checklist { Layout = ChecklistLayout.Icons, Items = nodes } } } });
        var table = new Table { Sections = { new TableHead { Rows = { head } }, body } };
        AutomationProperties.SetName(table, "项目");
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                table,
                new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Xs,
                    Children = { new Small { Text = "PO-2026-0187 的实施节点", Tone = TextTone.Muted }, new Checklist { Layout = ChecklistLayout.Inline, Items = projects[0].Nodes } },
                },
            },
        };
    }
}
