using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>AsideLayout 页的示例（vue-tsx-pro 文档站 layout/AsideLayout，整页的版式：AsideLayout 与 ViewportFill）。上游列表页的 DataTable 还没做，这里用滚动的行代替。</summary>
static class AsideLayoutExamples
{
    public static Example[] All =>
    [
        new("AsideLayout", "detail", "详情页", "主栏放概览与明细，侧栏放金额与里程碑；自己不到 56rem 宽时叠成一列。", Detail),
        new("AsideLayout", "viewport-fill", "列表页：占满到窗口底边", "ViewportFill 从自己的顶边占到滚动区域的可视区底边（留出 Bottom），里面竖排：查询栏、撑满剩下高度的列表（在列表里滚动）、贴着底边的分页。这里的外框是 32rem 高的滚动区域，代替窗口。", ViewportPage),
    ];

    static Card Section(string title, Control content) => new() { Variant = CardVariant.Outline, Header = new H3 { Text = title, Size = TextSize.Sm }, Content = content };

    static Control Detail()
    {
        var overview = new Descriptions { Columns = 2, Size = TextSize.Sm };
        overview.Items.AddRange([
            new DescriptionItem { Label = "任务类别", Value = "产品改进" },
            new DescriptionItem { Label = "责任团队", Value = "技术部" },
            new DescriptionItem { Label = "主办", Value = "宋知行" },
            new DescriptionItem { Label = "计划完成", Value = "2026-10-06" },
        ]);
        return new AsideLayout
        {
            AsideLabel = "收益与里程碑",
            AsideWidth = "18rem",
            Sticky = false,
            Aside =
            {
                Section("收益（万元）", new Grid { Columns = 2, Gap = Space.Sm, Children = { new Stat { Label = "预期", Value = 17.48m }, new Stat { Label = "测算", Text = "—" } } }),
                Section("里程碑", new Timeline
                {
                    Size = TextSize.Sm,
                    Items =
                    [
                        new TimelineItem { Title = "方案确定", Time = "2026-08-13", Tone = StatusTone.Success },
                        new TimelineItem { Title = "验证样件交付", Time = "2026-09-22", Tone = StatusTone.Success },
                    ],
                    Pending = "验证中，计划 10-30 完成",
                }),
            },
            Children =
            {
                Section("概览", overview),
                Section("改进明细", new Text { Text = "卡箍改用铝合金，合并两件支架，减少紧固件。" }),
            },
        };
    }

    static Control ViewportPage()
    {
        var rows = new StackPanel();
        string[] names = ["线缆收纳夹改版", "桌面挡板流程优化", "机柜导轨第二供应商"];
        string[] teams = ["技术部", "研发中心", "运营部"];
        for (var index = 0; index < 40; index++)
            rows.Children.Add(new Flex
            {
                Gap = Space.Md,
                Children =
                {
                    new Text { Text = $"PO-2026-{index + 1:0000}", Width = 120 },
                    new Text { Text = names[index % 3], Width = 200 },
                    new Text { Text = teams[index % 3], Tone = TextTone.Muted },
                },
                Margin = new global::Avalonia.Thickness(0, 4),
            });
        var list = new Card { Variant = CardVariant.Outline, Padding = Space.Sm, Content = new ScrollArea { Content = rows } };
        Flex.SetGrow(list, true);
        var toolbar = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new TextField { Icon = Lucide.Search, PlaceholderText = "编码 / 名称" }, new Small { Text = "列表占到窗口底边，再多在列表里滚动", Tone = TextTone.Muted } } };
        var pager = new Flex { Justify = FlexJustify.End, Children = { new Pagination { Total = 400, PageSize = 40 } } };
        // 放不下时只有列表收缩（上游 DataTable 的 flexible），查询栏与分页保持自己的高度
        Flex.SetShrink(toolbar, false);
        Flex.SetShrink(pager, false);
        var fill = new ViewportFill { Gap = Space.Md, Bottom = Space.Md, Children = { toolbar, list, pager } };
        // 示例嵌在页面里：外框是一个 32rem 高的滚动区域，代替窗口
        return new ScrollArea { Height = 512, Content = new Border { Padding = new global::Avalonia.Thickness(16), Child = fill } };
    }
}
