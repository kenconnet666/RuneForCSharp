namespace Rune.WinForms.Examples;

/// <summary>Breadcrumbs 的示例（vue-tsx-pro 文档站 navigation/Breadcrumbs；同页的 Steps 还没移植）。WinForms 里是 BreadcrumbsControl。</summary>
static class BreadcrumbsExamples
{
    public static Example[] All =>
    [
        new("Breadcrumbs", "breadcrumbs", "面包屑", "最后一项是当前页（读屏的说明是\"当前页\"），不是链接。路径太长时 MaxItems 把中间几级收进菜单（同 Radix Themes），不会把布局撑乱。分隔符从右到左排版时翻转。", Paths),
    ];

    static Control Paths()
    {
        // 示例里的链接只写锚点：点了不离开文档
        BreadcrumbItem[] deep =
        [
            new("首页", "#home", Lucide.House), new("项目", "#projects"), new("官网改版", "#redesign"),
            new("设计稿", "#designs"), new("2026 秋季", "#autumn"), new("首页 v3.fig"),
        ];
        Control Case(string caption, BreadcrumbsControl breadcrumbs, string name)
        {
            breadcrumbs.AccessibleName = name;
            return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, AutoSize = true }, new SmallControl { Tone = TextTone.Muted, Text = caption }, breadcrumbs);
        }
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, AutoSize = true },
            Case("默认：最后一项是当前页，不是链接", new BreadcrumbsControl { Items = [new("首页", "#home", Lucide.House), new("订单", "#orders"), new("#10234")] }, "订单位置"),
            Case("MaxItems=4：中间几级收进菜单", new BreadcrumbsControl { Items = deep, MaxItems = 4 }, "设计稿位置"),
            Case("自定义分隔符与图标", new BreadcrumbsControl
            {
                Separator = Lucide.Slash,
                Items = [new("vue-tsx-pro", "#repo", Lucide.Folder), new("packages", "#packages", Lucide.Folder), new("ui", "#ui", Lucide.Folder), new("src")],
            }, "代码位置"));
    }
}
