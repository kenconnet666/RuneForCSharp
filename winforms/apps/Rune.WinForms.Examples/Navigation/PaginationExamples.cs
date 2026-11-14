namespace Rune.WinForms.Examples;

/// <summary>Pagination、PaginationBar、PageSizeSelect、PageJump 与 Pager 的示例（vue-tsx-pro 文档站 navigation/Pagination）。WinForms 里控件名带 Control。</summary>
static class PaginationExamples
{
    public static Example[] All =>
    [
        new("Pagination", "bar", "分页栏", "表格、列表下方的一整行：起始边是“第 1–5 条，共 1,286 条”，结束边是页码、每页条数与跳页。翻页、换条数发 PageChanged（换条数回到第 1 页）；只有一页时不显示跳页。分页导航的名称写在 PaginationPart 上。", Bar),
        new("Pagination", "variants", "页码的数量", "按钮数固定：翻页时省略号换位置，按钮不跳动、不会让指针下的按钮变成别的页码。当前页读屏的说明是\"当前页\"。", Variants),
        new("Pagination", "bar-variants", "分页栏的宽度与取舍", "分页栏按自己的宽度取舍：窄于 36em 时不显示跳页，窄于 24em 时总数换到单独一行，放在卡片、侧栏里也不挤。TotalText 自己写总数（拿到 From、To、Total）；ShowSizeSelect、ShowJump 关掉不要的部分。", BarVariants),
        new("Pagination", "links", "链接与上一篇 / 下一篇", "Href 给出每页的地址时页码是链接按钮。PagerControl 是文章末尾的上一篇 / 下一篇两张链接卡片。", Links),
        new("Pagination", "full", "自己组合", "给了 Total 与 PageSize 就自动算页数，也可以直接给 PageCount。ShowTotal 显示总条数；每页条数变了时，在 PageSizeSelectControl 的 PageSizeChanged 里回到第 1 页。三个组件各自独立，排法与分页栏不同时自己并排。", Full),
    ];

    static Control Case(string caption, Control control, string name)
    {
        control.AccessibleName = name;
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, AutoSize = true }, new SmallControl { Tone = TextTone.Muted, Text = caption }, control);
    }

    static readonly string[] Customers = ["林小满", "周一航", "陈默", "许悠", "宋知远", "顾青", "沈遥", "韩笑"];

    static readonly (StatusTone Tone, string Label)[] Statuses =
        [(StatusTone.Success, "已发货"), (StatusTone.Warning, "待付款"), (StatusTone.Info, "备货中"), (StatusTone.Neutral, "已完成")];

    static Control Bar()
    {
        // 1,286 条假订单：按序号算出来，不占内存
        const int total = 1286;
        var body = new TableBodyControl();
        var head = new TableRowControl
        {
            Controls = { new TableHeaderCellControl { Text = "订单号" }, new TableHeaderCellControl { Text = "客户" }, new TableHeaderCellControl { Align = TableAlign.End, Text = "金额" }, new TableHeaderCellControl { Text = "状态" } },
        };
        var table = new TableControl { AccessibleName = "订单", Controls = { new TableHeadControl { Controls = { head } }, body } };
        var bar = new PaginationBarControl { PageSize = 5, Total = total, PageSizeOptions = [5, 10, 20, 50] };
        bar.PaginationPart.AccessibleName = "订单表格分页";
        void Rows()
        {
            body.SuspendLayout();
            body.Controls.Cast<Control>().ToList().ForEach(row => row.Dispose());
            var start = (bar.Page - 1) * bar.PageSize;
            for (var index = start; index < Math.Min(start + bar.PageSize, total); index++)
            {
                var amount = index * 7919 % 50_000 / 10m + 39;
                var status = Statuses[index * 3 % Statuses.Length];
                var badge = new TableCellControl();
                badge.Controls.Add(new BadgeControl { Tone = status.Tone, Text = status.Label });
                body.Controls.Add(new TableRowControl
                {
                    Controls =
                    {
                        new TableCellControl { Text = $"#{10_000 + total - index}" }, new TableCellControl { Text = Customers[index * 5 % Customers.Length] },
                        new TableCellControl { Align = TableAlign.End, Text = $"¥{amount:N2}" }, badge,
                    },
                });
            }
            body.ResumeLayout();
        }
        bar.PageChanged += (_, _) => Rows();
        Rows();
        var column = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, AutoSize = true }, table, bar);
        column.Align = FlexAlign.Stretch;
        return column;
    }

    static Control BarVariants()
    {
        var narrow = new PaginationBarControl { Page = 4, Total = 86, ShowSizeSelect = false };
        narrow.PaginationPart.Siblings = 0;
        narrow.PaginationPart.AccessibleName = "窄分页";
        var custom = new PaginationBarControl { PageSize = 20, Total = 342, TotalText = range => $"供应商 {range.From}–{range.To} / {range.Total} 家 · 勾选跨页保留" };
        custom.PaginationPart.AccessibleName = "供应商分页";
        var simple = new PaginationBarControl { Page = 2, Total = 48, ShowSizeSelect = false, ShowJump = false };
        simple.PaginationPart.AccessibleName = "简单分页";
        var card = Build.Card(new CardControl(), narrow);
        card.MinimumSize = card.MaximumSize = new Size(card.LogicalToDeviceUnits(20 * 16), 0);
        Control Labeled(string caption, Control control)
        {
            var flex = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, AutoSize = true }, new SmallControl { Tone = TextTone.Muted, Text = caption }, control);
            flex.Align = control == card ? FlexAlign.Start : FlexAlign.Stretch;
            return flex;
        }
        var column = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, AutoSize = true },
            Labeled("放在 20rem 宽的卡片里：不显示跳页，总数单独一行", card),
            Labeled("TotalText：自己写总数", custom),
            Labeled("只要页码与总数：ShowSizeSelect、ShowJump 关掉", simple));
        column.Align = FlexAlign.Stretch;
        return column;
    }

    static Control Full()
    {
        const int total = 1286;
        var pagination = new PaginationControl { Total = total, PageSize = 20, ShowTotal = true, AccessibleName = "订单分页" };
        var size = new PageSizeSelectControl { PageSize = 20 };
        var jump = new PageJumpControl { PageCount = pagination.Count };
        var range = new SmallControl { Tone = TextTone.Muted };
        void Sync()
        {
            var first = (pagination.Page - 1) * pagination.PageSize + 1;
            range.Text = $"第 {first}–{Math.Min(first + pagination.PageSize - 1, total)} 条，共 {total:N0} 条";
            jump.PageCount = pagination.Count;
            jump.Page = pagination.Page;
        }
        pagination.PageChanged += (_, _) => Sync();
        jump.PageChanged += (_, _) => pagination.Page = jump.Page;
        size.PageSizeChanged += (_, value) =>
        {
            pagination.PageSize = value;
            pagination.Page = 1;
            Sync();
        };
        Sync();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, AutoSize = true },
            Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Md, Wrap = FlexWrap.Wrap, AutoSize = true }, pagination, size, jump), range);
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
