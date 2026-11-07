using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Card 的示例（vue-tsx-pro 文档站 display/Card）。sections 要等 Descriptions，effects 要等装饰效果。</summary>
static class CardExamples
{
    public static Example[] All =>
    [
        new("Card", "variants", "样式", "Variant 有 surface（默认）、outline、elevated、ghost，颜色与阴影都取主题；可以用 Provider 的样式统一改默认值。", Variants),
        new("Card", "extra", "页头的操作", "Extra 在页头的结束边（\"全部 ↗\"、标记、按钮），和 Header 排在一行、放不下时换行，不用自己在 Header 里写两端对齐。", Extra),
        new("Card", "loading", "内边距与加载中", "Padding 取间距档位；IsLoading 时正文换成扫光的骨架（SkeletonText），很快完成的加载不出现骨架。上游用 Switch 切换，Switch 做出来之前用 CheckBox。", Loading),
    ];

    static readonly (CardVariant Variant, string Description)[] Kinds =
    [
        (CardVariant.Surface, "表面色加描边（默认）：页面上最常见的卡片"),
        (CardVariant.Outline, "只有描边、没有底色：放在有底色的区域里"),
        (CardVariant.Elevated, "浮起的表面加阴影：可点击、可拖动的卡片"),
        (CardVariant.Ghost, "没有边框与底色：只用来分组与留白"),
    ];

    static Control Variants()
    {
        var grid = new Grid { MinItemWidth = "13em", Gap = Space.Md };
        grid.Children.AddRange(Kinds.Select(kind => new Card
        {
            Variant = kind.Variant,
            Content = new StackPanel { Children = { new Strong { Text = kind.Variant.Value }, new Paragraph { Text = kind.Description, Size = TextSize.Sm, Tone = TextTone.Muted } } },
        }));
        return grid;
    }

    static Control Extra() => new Flex
    {
        Direction = FlexDirection.Column,
        Gap = Space.Md,
        Children =
        {
            new Card
            {
                Variant = CardVariant.Outline, Padding = Space.Md, Header = "超时最久的项目",
                Extra = new Link { Content = "全部超时", External = true },
                Content = new Small { Text = "PO-2026-0276 线缆收纳夹改版 · 超时 15 天", Tone = TextTone.Muted },
            },
            new Card
            {
                Variant = CardVariant.Outline, Padding = Space.Md, Header = "结项材料",
                Extra = new Badge { Tone = StatusTone.Warning, Content = "缺 2 项" },
                Content = new Small { Text = "验收报告、财务复核意见、合同签订记录，缺一不能结项", Tone = TextTone.Muted },
            },
        },
    };

    static Control Loading()
    {
        var toggle = new CheckBox { Content = "加载中", IsChecked = true };
        var grid = new Grid { MinItemWidth = "11em", Gap = Space.Md };
        foreach (var padding in new[] { Space.Sm, Space.Md, Space.Lg })
        {
            var card = new Card
            {
                Padding = padding, IsLoading = true,
                Header = new H3 { Text = $"Padding=\"{padding}\"", Size = TextSize.Sm },
                Content = new StackPanel
                {
                    Children =
                    {
                        new Paragraph { Text = "正文加载中换成几行扫光的骨架，页头照常显示，读屏的项状态是加载中。", Size = TextSize.Sm },
                        new Small { Text = "很快完成的加载不出现骨架", Tone = TextTone.Muted },
                    },
                },
            };
            toggle.IsCheckedChanged += (_, _) => card.IsLoading = toggle.IsChecked == true;
            grid.Children.Add(card);
        }
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { toggle, grid } };
    }
}
