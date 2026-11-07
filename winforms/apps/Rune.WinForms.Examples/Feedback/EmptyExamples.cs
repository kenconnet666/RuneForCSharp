namespace Rune.WinForms.Examples;

/// <summary>Empty 的示例（vue-tsx-pro 文档站 feedback/Empty）。result、status 要等 Result。</summary>
static class EmptyExamples
{
    public static Example[] All =>
    [
        new("Empty", "empty", "空状态", "列表、表格、搜索结果没有内容时用 EmptyControl：说清楚为什么空、接下来能做什么；TextSize 跟字号档位缩放，下拉选项里用 sm。", Empties),
    ];

    static Control Empties() => Build.Grid(new GridControl { MinItemWidth = "14em", Gap = Space.Md },
        Build.Card(new CardControl(), new EmptyControl
        {
            Icon = Lucide.Inbox, Title = "还没有订单", Description = "顾客下单后会显示在这里，也可以先手动录入一张。",
            Action = new ButtonControl { Text = "新建订单", TextSize = TextSize.Sm, Icon = Lucide.Plus },
        }),
        Build.Card(new CardControl(), new EmptyControl
        {
            Icon = Lucide.SearchX, TextSize = TextSize.Sm, Title = "没有找到“vue-tsx”", Description = "换个关键词，或者检查拼写。",
            Action = Build.Flex(new FlexControl { Align = FlexAlign.Baseline, Gap = Space.Xs }, new TextControl { Text = "试试", TextSize = TextSize.Sm }, new LinkControl { Text = "button" }),
        }),
        Build.Card(new CardControl(), new EmptyControl { Icon = Lucide.FolderOpen, TextSize = TextSize.Sm, Title = "文件夹是空的" }));
}
