using System.Windows;
using System.Windows.Documents;

namespace Rune.Wpf.Examples;

/// <summary>Empty 的示例（vue-tsx-pro 文档站 feedback/Empty）。result、status 要等 Result。</summary>
static class EmptyExamples
{
    public static Example[] All =>
    [
        new("Empty", "empty", "空状态", "列表、表格、搜索结果没有内容时用 Empty：说清楚为什么空、接下来能做什么；Size 跟字号档位缩放，下拉选项里用 sm。", Empties),
    ];

    static FrameworkElement Empties() => new Grid
    {
        MinItemWidth = "14em",
        Gap = Space.Md,
        Children =
        {
            new Card
            {
                Content = new Empty
                {
                    Icon = Lucide.Inbox, Title = "还没有订单", Description = "顾客下单后会显示在这里，也可以先手动录入一张。",
                    Action = new Button { Content = "新建订单", Size = TextSize.Sm, Icon = Lucide.Plus },
                },
            },
            new Card
            {
                Content = new Empty
                {
                    Icon = Lucide.SearchX, Size = TextSize.Sm, Title = "没有找到“vue-tsx”", Description = "换个关键词，或者检查拼写。",
                    Action = new Text { Size = TextSize.Sm, Inlines = { new Run("试试 "), new Link { Inlines = { new Run("button") } } } },
                },
            },
            new Card { Content = new Empty { Icon = Lucide.FolderOpen, Size = TextSize.Sm, Title = "文件夹是空的" } },
        },
    };
}
