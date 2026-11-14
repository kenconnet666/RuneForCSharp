using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>PageHeader 的示例（vue-tsx-pro 文档站 patterns/PageHeader）。示例嵌在文档页的一节里，标题降到 H3（同上游）。</summary>
static class PageHeaderExamples
{
    public static Example[] All =>
    [
        new("PageHeader", "detail", "详情页", "Breadcrumbs 是面包屑（最后一项是当前页），Meta 放标题后面的状态、标签，Actions 靠结束边、放不下时整块换到标题下面。订阅了 Back（或给了 BackCommand）就有返回按钮；给 BackHref 时返回是链接。Content 排在最下面，放摘要。", Detail),
        new("PageHeader", "simple", "列表页：标题、说明与标签页", "只写 Title 就是一个 H1；Description 是标题下面的说明。标签页放在 Content。页头嵌在页面里的一块（抽屉、分栏的右侧）时用 HeadingLevel 把标题降到 H2、H3。", Simple),
    ];

    static Control Detail()
    {
        var backs = 0;
        var count = new Text { Tone = TextTone.Muted, Size = TextSize.Sm, Text = "点了 0 次返回" };
        var summary = new Descriptions { Columns = 3 };
        summary.Items.AddRange([new DescriptionItem { Label = "采购员", Value = "林小满" }, new DescriptionItem { Label = "交货日期", Value = "2026-11-05" }, new DescriptionItem { Label = "付款方式", Value = "月结 30 天" }]);
        var header = new PageHeader
        {
            Breadcrumbs = [new BreadcrumbItem("采购", "#purchase"), new BreadcrumbItem("采购单", "#orders"), new BreadcrumbItem("PO-2026-0187")],
            Title = "PO-2026-0187",
            HeadingLevel = 3,
            Meta = new Badge { Tone = StatusTone.Success, Content = "已到货" },
            Description = "华辰精密 · 2026-10-20 下单 · 含税 86,400.00 元",
            Actions = new Flex
            {
                Wrap = FlexWrap.Wrap, Gap = Space.Sm,
                Children = { new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Pencil, Content = "编辑" }, new Button { Icon = Lucide.Truck, Content = "催单" } },
            },
            Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { summary, count } },
        };
        header.Back += (_, _) => count.Text = $"点了 {++backs} 次返回";
        return header;
    }

    static Control Simple()
    {
        var list = new TabList { Children = { new Tab { Value = "all", Content = "全部" }, new Tab { Value = "mine", Content = "我提交的" }, new Tab { Value = "todo", Content = "待我审批" } } };
        AutomationProperties.SetName(list, "采购申请");
        return new Tabs
        {
            Value = "all",
            Children =
            {
                new PageHeader
                {
                    Title = "采购申请", HeadingLevel = 3,
                    Description = "提交后先由部门经理审批，金额超过 5 万元时加签财务。",
                    Actions = new Button { Icon = Lucide.Plus, Content = "新建申请" },
                    Content = list,
                },
            },
        };
    }
}
