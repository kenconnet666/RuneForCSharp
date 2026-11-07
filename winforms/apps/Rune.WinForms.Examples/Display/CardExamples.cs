namespace Rune.WinForms.Examples;

/// <summary>Card 的示例（vue-tsx-pro 文档站 display/Card）。sections 要等 Descriptions，effects 要等装饰效果。</summary>
static class CardExamples
{
    public static Example[] All =>
    [
        new("Card", "variants", "样式", "Variant 有 surface（默认）、outline、elevated、ghost，颜色取主题。WinForms 画不到控件外面：elevated 没有阴影，只有抬高的表面色与弱边框。", Variants),
        new("Card", "extra", "页头的操作", "Extra 在页头的结束边（\"全部 ↗\"、标记、按钮），和 Header 排在一行、放不下时换行。", Extra),
        new("Card", "loading", "内边距与加载中", "CardPadding 取间距档位；IsLoading 时正文换成扫光的骨架，很快完成的加载不出现骨架。用 Switch 切换。", Loading),
    ];

    static readonly (CardVariant Variant, string Description)[] Kinds =
    [
        (CardVariant.Surface, "表面色加描边（默认）：页面上最常见的卡片"),
        (CardVariant.Outline, "只有描边、没有底色：放在有底色的区域里"),
        (CardVariant.Elevated, "浮起的表面加阴影：可点击、可拖动的卡片"),
        (CardVariant.Ghost, "没有边框与底色：只用来分组与留白"),
    ];

    static Control Variants() => Build.Grid(new GridControl { MinItemWidth = "13em", Gap = Space.Md },
        [.. Kinds.Select(kind => Build.Card(new CardControl { Variant = kind.Variant },
            new StrongControl { Text = kind.Variant.Value },
            new ParagraphControl { Text = kind.Description, TextSize = TextSize.Sm, Tone = TextTone.Muted }))]);

    static Control Extra() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
        Build.Card(new CardControl { Variant = CardVariant.Outline, CardPadding = Space.Md, Header = "超时最久的项目", Extra = new LinkControl { Text = "全部超时", External = true } },
            new SmallControl { Text = "PO-2026-0276 线缆收纳夹改版 · 超时 15 天", Tone = TextTone.Muted }),
        Build.Card(new CardControl { Variant = CardVariant.Outline, CardPadding = Space.Md, Header = "结项材料", Extra = new BadgeControl { Tone = StatusTone.Warning, Text = "缺 2 项" } },
            new SmallControl { Text = "验收报告、财务复核意见、合同签订记录，缺一不能结项", Tone = TextTone.Muted }));

    static Control Loading()
    {
        var toggle = new SwitchControl { Text = "加载中", IsChecked = true };
        var cards = new[] { Space.Sm, Space.Md, Space.Lg }.Select(padding => Build.Card(
            new CardControl { CardPadding = padding, IsLoading = true, Header = $"CardPadding = {padding}" },
            new ParagraphControl { Text = "正文加载中换成几行扫光的骨架，页头照常显示，读屏状态带\"忙\"。", TextSize = TextSize.Sm },
            new SmallControl { Text = "很快完成的加载不出现骨架", Tone = TextTone.Muted })).ToArray();
        toggle.IsCheckedChanged += (_, _) =>
        {
            foreach (var card in cards) card.IsLoading = toggle.IsChecked;
        };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md }, toggle, Build.Grid(new GridControl { MinItemWidth = "11em", Gap = Space.Md }, cards));
    }
}
