using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Empty 的示例（vue-tsx-pro 文档站 feedback/Empty）。result、status 要等 Result。</summary>
static class EmptyExamples
{
    public static Example[] All =>
    [
        new("Empty", "empty", "空状态", "列表、表格、搜索结果没有内容时用 Empty：说清楚为什么空、接下来能做什么；Size 跟字号档位缩放，下拉选项里用 sm。", Empties),
    ];

    static Control Empties()
    {
        var grid = new Grid { MinItemWidth = "14em", Gap = Space.Md };
        grid.Children.AddRange(
        [
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
                    Action = TryButton(),
                },
            },
            new Card { Content = new Empty { Icon = Lucide.FolderOpen, Size = TextSize.Sm, Title = "文件夹是空的" } },
        ]);
        return grid;
    }

    /// <summary>"试试 button"：链接放进段落时用 InlineUIContainer（DESIGN §14 Link）。</summary>
    static Text TryButton()
    {
        var text = new Text { Size = TextSize.Sm, Inlines = [] };
        text.Inlines!.Add(new global::Avalonia.Controls.Documents.Run("试试 "));
        text.Inlines.Add(new global::Avalonia.Controls.Documents.InlineUIContainer(new Link { Content = "button" }) { BaselineAlignment = global::Avalonia.Media.BaselineAlignment.Bottom });
        return text;
    }
}
