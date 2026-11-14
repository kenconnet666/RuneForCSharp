namespace Rune.WinForms.Examples;

/// <summary>Pagination 与 Pager 的示例（vue-tsx-pro 文档站 navigation/Pagination；分页栏 PaginationBar、跳页、每页条数还没移植）。WinForms 里是 PaginationControl、PagerControl。</summary>
static class PaginationExamples
{
    public static Example[] All =>
    [
        new("Pagination", "variants", "页码的数量", "按钮数固定：翻页时省略号换位置，按钮不跳动、不会让指针下的按钮变成别的页码。当前页读屏的说明是\"当前页\"。", Variants),
        new("Pagination", "links", "链接与上一篇 / 下一篇", "Href 给出每页的地址时页码是链接按钮。PagerControl 是文章末尾的上一篇 / 下一篇两张链接卡片。", Links),
    ];

    static Control Case(string caption, Control control, string name)
    {
        control.AccessibleName = name;
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, AutoSize = true }, new SmallControl { Tone = TextTone.Muted, Text = caption }, control);
    }

    static Control Variants() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, AutoSize = true },
        Case("默认：当前页两侧各 1 个，首尾各 1 个", new PaginationControl { Page = 6, PageCount = 20 }, "默认页码数"),
        Case("Siblings=2 Boundaries=2：页码多显示几个", new PaginationControl { Page = 6, PageCount = 20, Siblings = 2, Boundaries = 2 }, "更多页码"),
        Case("TextSize=Sm，页数少时不出现省略号", new PaginationControl { Page = 2, PageCount = 5, TextSize = TextSize.Sm }, "小号分页"));

    static Control Links()
    {
        var column = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, AutoSize = true },
            Case("Href：页码是链接，可以分享", new PaginationControl { Page = 3, PageCount = 12, Href = page => $"#articles-page-{page}" }, "文章分页"),
            Case("PagerControl：文章末尾的上一篇 / 下一篇", new PagerControl
            {
                Previous = new PagerLink("/components/tabs", "Tabs 标签页"), Next = new PagerLink("/components/breadcrumbs", "Breadcrumbs 面包屑"),
            }, "文章翻页"));
        column.Align = FlexAlign.Stretch;
        foreach (Control child in column.Controls) ((FlexControl)child).Align = FlexAlign.Stretch;
        return column;
    }
}
