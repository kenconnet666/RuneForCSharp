using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Pagination、PaginationBar、PageSizeSelect、PageJump 与 Pager 的示例（vue-tsx-pro 文档站 navigation/Pagination）。</summary>
static class PaginationExamples
{
    public static Example[] All =>
    [
        new("Pagination", "bar", "分页栏", "表格、列表下方的一整行：起始边是“第 1–5 条，共 1,286 条”，结束边是页码、每页条数与跳页。Page、PageSize 都双向绑定，换每页条数回到第 1 页；只有一页时不显示跳页。分页导航的名称写在 PaginationPart 上。", Bar),
        new("Pagination", "variants", "页码的数量", "按钮数固定：翻页时省略号换位置，按钮不跳动、不会让指针下的按钮变成别的页码。当前页读屏的项状态是\"当前页\"。", Variants),
        new("Pagination", "bar-variants", "分页栏的宽度与取舍", "分页栏按自己的宽度取舍：窄于 36em 时不显示跳页，窄于 24em 时总数换到单独一行，放在卡片、侧栏里也不挤。TotalText 自己写总数（拿到 From、To、Total）；ShowSizeSelect、ShowJump 关掉不要的部分。", BarVariants),
        new("Pagination", "links", "链接与上一篇 / 下一篇", "Href 给出每页的地址时页码是链接按钮。Pager 是文章末尾的上一篇 / 下一篇两张链接卡片。", Links),
        new("Pagination", "full", "自己组合", "给了 Total 与 PageSize 就自动算页数，也可以直接给 PageCount。ShowTotal 显示总条数；每页条数变了时，在 PageSizeSelect 的 PageSizeChanged 里回到第 1 页。三个组件各自独立，排法与分页栏不同时自己并排。", Full),
    ];

    static Control Case(string caption, Control control, string name)
    {
        AutomationProperties.SetName(control, name);
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new Small { Tone = TextTone.Muted, Text = caption }, control } };
    }

    static readonly string[] Customers = ["林小满", "周一航", "陈默", "许悠", "宋知远", "顾青", "沈遥", "韩笑"];

    static readonly (StatusTone Tone, string Label)[] Statuses =
        [(StatusTone.Success, "已发货"), (StatusTone.Warning, "待付款"), (StatusTone.Info, "备货中"), (StatusTone.Neutral, "已完成")];

    static Control Bar()
    {
        // 1,286 条假订单：按序号算出来，不占内存
        const int total = 1286;
        var body = new TableBody();
        var table = new Table
        {
            Sections =
            {
                new TableHead { Rows = { new TableRow { Cells = { new TableHeaderCell { Content = "订单号" }, new TableHeaderCell { Content = "客户" }, new TableHeaderCell { Align = TableAlign.End, Content = "金额" }, new TableHeaderCell { Content = "状态" } } } } },
                body,
            },
        };
        global::Avalonia.Automation.AutomationProperties.SetName(table, "订单");
        var bar = new PaginationBar { PageSize = 5, Total = total, PageSizeOptions = [5, 10, 20, 50] };
        global::Avalonia.Automation.AutomationProperties.SetName(bar.PaginationPart, "订单表格分页");
        void Rows()
        {
            body.Rows.Clear();
            var start = (bar.Page - 1) * bar.PageSize;
            for (var index = start; index < Math.Min(start + bar.PageSize, total); index++)
            {
                var amount = index * 7919 % 50_000 / 10m + 39;
                var status = Statuses[index * 3 % Statuses.Length];
                body.Rows.Add(new TableRow
                {
                    Cells =
                    {
                        new TableCell { Content = $"#{10_000 + total - index}" }, new TableCell { Content = Customers[index * 5 % Customers.Length] },
                        new TableCell { Align = TableAlign.End, Content = $"¥{amount:N2}" }, new TableCell { Content = new Badge { Tone = status.Tone, Content = status.Label } },
                    },
                });
            }
        }
        bar.PropertyChanged += (_, e) =>
        {
            if (e.Property == PaginationBar.PageProperty || e.Property == PaginationBar.PageSizeProperty) Rows();
        };
        Rows();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { table, bar } };
    }

    static Control BarVariants()
    {
        var narrow = new PaginationBar { Page = 4, Total = 86, ShowSizeSelect = false };
        narrow.PaginationPart.Siblings = 0;
        var custom = new PaginationBar { PageSize = 20, Total = 342, TotalText = range => $"供应商 {range.From}–{range.To} / {range.Total} 家 · 勾选跨页保留" };
        var simple = new PaginationBar { Page = 2, Total = 48, ShowSizeSelect = false, ShowJump = false };
        global::Avalonia.Automation.AutomationProperties.SetName(narrow.PaginationPart, "窄分页");
        global::Avalonia.Automation.AutomationProperties.SetName(custom.PaginationPart, "供应商分页");
        global::Avalonia.Automation.AutomationProperties.SetName(simple.PaginationPart, "简单分页");
        Control Labeled(string caption, Control control) =>
            new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new Small { Tone = TextTone.Muted, Text = caption }, control } };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                Labeled("放在 20rem 宽的卡片里：不显示跳页，总数单独一行", new Card { MaxWidth = 20 * 16, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left, Content = narrow }),
                Labeled("TotalText：自己写总数", custom),
                Labeled("只要页码与总数：ShowSizeSelect、ShowJump 关掉", simple),
            },
        };
    }

    static Control Full()
    {
        const int total = 1286;
        var pagination = new Pagination { Total = total, PageSize = 20, ShowTotal = true };
        global::Avalonia.Automation.AutomationProperties.SetName(pagination, "订单分页");
        var size = new PageSizeSelect { PageSize = 20 };
        var jump = new PageJump { PageCount = pagination.Count };
        var range = new Small { Tone = TextTone.Muted };
        void Sync()
        {
            var first = (pagination.Page - 1) * pagination.PageSize + 1;
            range.Text = $"第 {first}–{Math.Min(first + pagination.PageSize - 1, total)} 条，共 {total:N0} 条";
            jump.Page = pagination.Page;
            jump.PageCount = pagination.Count;
        }
        pagination.PropertyChanged += (_, e) =>
        {
            if (e.Property == Pagination.PageProperty || e.Property == Pagination.PageSizeProperty) Sync();
        };
        jump.PropertyChanged += (_, e) =>
        {
            if (e.Property == PageJump.PageProperty) pagination.Page = jump.Page;
        };
        size.PageSizeChanged += (_, value) =>
        {
            pagination.PageSize = value;
            pagination.Page = 1;
        };
        Sync();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children = { new Flex { Align = FlexAlign.Center, Gap = Space.Md, Wrap = FlexWrap.Wrap, Children = { pagination, size, jump } }, range },
        };
    }

    static Control Variants() => new Flex
    {
        Direction = FlexDirection.Column, Gap = Space.Lg,
        Children =
        {
            Case("默认：当前页两侧各 1 个，首尾各 1 个", new Pagination { Page = 6, PageCount = 20 }, "默认页码数"),
            Case("Siblings=2 Boundaries=2：页码多显示几个", new Pagination { Page = 6, PageCount = 20, Siblings = 2, Boundaries = 2 }, "更多页码"),
            Case("Size=Sm，页数少时不出现省略号", new Pagination { Page = 2, PageCount = 5, Size = TextSize.Sm }, "小号分页"),
        },
    };

    static Control Links() => new Flex
    {
        Direction = FlexDirection.Column, Gap = Space.Lg,
        Children =
        {
            Case("Href：页码是链接，可以分享", new Pagination { Page = 3, PageCount = 12, Href = page => $"#articles-page-{page}" }, "文章分页"),
            Case("Pager：文章末尾的上一篇 / 下一篇", new Pager
            {
                Previous = new PagerLink("/components/tabs", "Tabs 标签页"), Next = new PagerLink("/components/breadcrumbs", "Breadcrumbs 面包屑"),
            }, "文章翻页"),
        },
    };
}
