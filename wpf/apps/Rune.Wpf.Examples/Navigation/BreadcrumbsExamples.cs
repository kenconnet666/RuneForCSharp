using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf.Examples;

/// <summary>Breadcrumbs 的示例（vue-tsx-pro 文档站 navigation/Breadcrumbs；同页的 Steps 还没移植）。</summary>
static class BreadcrumbsExamples
{
    public static Example[] All =>
    [
        new("Breadcrumbs", "breadcrumbs", "面包屑", "最后一项是当前页（读屏的项状态是\"当前页\"），不是链接。路径太长时 MaxItems 把中间几级收进菜单（同 Radix Themes），不会把布局撑乱。分隔符从右到左排版时翻转。", Paths),
    ];

    static FrameworkElement Paths()
    {
        // 示例里的链接只写锚点：点了不离开文档
        BreadcrumbItem[] deep =
        [
            new("首页", "#home", Lucide.House), new("项目", "#projects"), new("官网改版", "#redesign"),
            new("设计稿", "#designs"), new("2026 秋季", "#autumn"), new("首页 v3.fig"),
        ];
        FrameworkElement Case(string caption, Breadcrumbs breadcrumbs, string name)
        {
            AutomationProperties.SetName(breadcrumbs, name);
            return new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new Small { Tone = TextTone.Muted, Text = caption }, breadcrumbs } };
        }
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                Case("默认：最后一项是当前页，不是链接", new Breadcrumbs { Items = [new("首页", "#home", Lucide.House), new("订单", "#orders"), new("#10234")] }, "订单位置"),
                Case("MaxItems=4：中间几级收进菜单", new Breadcrumbs { Items = deep, MaxItems = 4 }, "设计稿位置"),
                Case("自定义分隔符与图标", new Breadcrumbs
                {
                    Separator = Lucide.Slash,
                    Items = [new("vue-tsx-pro", "#repo", Lucide.Folder), new("packages", "#packages", Lucide.Folder), new("ui", "#ui", Lucide.Folder), new("src")],
                }, "代码位置"),
            },
        };
    }
}
