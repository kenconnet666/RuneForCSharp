namespace Rune.WinForms.Examples;

/// <summary>PageHeaderControl 的示例（vue-tsx-pro 文档站 patterns/PageHeader）。示例嵌在文档页的一节里，标题降到 H3（同上游）。</summary>
static class PageHeaderExamples
{
    public static Example[] All =>
    [
        new("PageHeader", "detail", "详情页", "Breadcrumbs 是面包屑（最后一项是当前页），Meta 放标题后面的状态、标签，Actions 靠结束边、放不下时整块换到标题下面。订阅了 Back 就有返回按钮；给 BackHref 时返回是链接。放进去的子控件排在最下面，放摘要。", Detail),
        new("PageHeader", "simple", "列表页：标题、说明与标签页", "只写 Title 就是一个 H1；Description 是标题下面的说明。标签页放在子控件里。页头嵌在页面里的一块（抽屉、分栏的右侧）时用 HeadingLevel 把标题降到 H2、H3。", Simple),
    ];

    static Control Detail()
    {
        var backs = 0;
        var count = new TextControl { Tone = TextTone.Muted, TextSize = TextSize.Sm, Text = "点了 0 次返回" };
        var summary = new DescriptionsControl { Columns = 3 };
        foreach (var (label, value) in new[] { ("采购员", "林小满"), ("交货日期", "2026-11-05"), ("付款方式", "月结 30 天") })
            summary.Items.Add(new DescriptionItem { Label = label, Value = new TextControl { Text = value } });
        var header = new PageHeaderControl
        {
            Breadcrumbs = [new BreadcrumbItem("采购", "#purchase"), new BreadcrumbItem("采购单", "#orders"), new BreadcrumbItem("PO-2026-0187")],
            Title = "PO-2026-0187",
            HeadingLevel = 3,
            Meta = new BadgeControl { Tone = StatusTone.Success, Text = "已到货" },
            Description = "华辰精密 · 2026-10-20 下单 · 含税 86,400.00 元",
            Actions = Build.Flex(new FlexControl { Wrap = FlexWrap.Wrap, Gap = Space.Sm, AutoSize = true },
                new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Pencil, Text = "编辑" },
                new ButtonControl { Icon = Lucide.Truck, Text = "催单" }),
        };
        header.Back += (_, _) => count.Text = $"点了 {++backs} 次返回";
        header.Controls.AddRange([summary, count]);
        return header;
    }

    static Control Simple()
    {
        var tabs = new TabsControl { Value = "all" };
        var list = new TabListControl { AccessibleName = "采购申请" };
        foreach (var (value, label) in new[] { ("all", "全部"), ("mine", "我提交的"), ("todo", "待我审批") })
        {
            var tab = new TabItemControl { Value = value };
            tab.Controls.Add(new TextControl { Text = label });
            list.Controls.Add(tab);
        }
        var header = new PageHeaderControl
        {
            Title = "采购申请", HeadingLevel = 3,
            Description = "提交后先由部门经理审批，金额超过 5 万元时加签财务。",
            Actions = new ButtonControl { Icon = Lucide.Plus, Text = "新建申请" },
        };
        header.Controls.Add(list);
        tabs.Controls.Add(header);
        return tabs;
    }
}
