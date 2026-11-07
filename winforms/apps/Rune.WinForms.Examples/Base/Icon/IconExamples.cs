namespace Rune.WinForms.Examples;

/// <summary>
/// Icon 的示例（vue-tsx-pro 文档站 base/Icon）。文档站的 draw 等描线动画，到时补上。
/// WinForms 里放进窗体的是 <see cref="IconControl"/>：大小档位叫 IconSize，名称写 AccessibleName。
/// </summary>
static class IconExamples
{
    public static Example[] All =>
    [
        new("Icon", "basic", "基础用法", "从 Lucide 类按名取图标交给 Data；设计器的属性窗口里按名字选。全部图标都在 Lucide 类里，第一次用到时才创建。", Basic),
        new("Icon", "sizes", "尺寸与线宽", "不写 IconSize 时是 1em，跟随控件的字体；档位对应主题字号，数字是像素。StrokeWidth 调线宽，大图标用细一点的线更精致。", Sizes),
        new("Icon", "tones", "色调", "不写 Tone 时跟随 ForeColor；状态图标用 success、warning、danger 等色调，和提示条、标记的颜色一致。", Tones),
        new("Icon", "custom", "自定义图标", "按 Lucide 的格式写 24 格画布上的路径（绝对坐标的 M、L、C、Z）；实心的部分写在 fillPath 里，颜色仍跟随文字。", Custom),
        new("Icon", "with-text", "与文字、按钮搭配", "图标与文字基线对齐，不撑高行距；按钮、标记、链接里的图标由组件排好间距。WinForms 的文字是整段画的，句子里的图标是一行 FlexControl：IconControl 与 TextControl 按基线（FlexAlign.Baseline）对齐。", WithText),
        new("Icon", "direction", "从右到左", "FlipInRtl 让图标在从右到左（RightToLeft）的排版里水平翻转：箭头、chevron、“返回”这类有方向的图标要翻，时钟、勾号不翻。", Direction),
        new("Icon", "accessibility", "装饰与含义", "旁边已有文字时图标只是装饰，不写名称；单独出现、表达含义的图标要给 AccessibleName。", Accessibility),
    ];

    /// <summary>一格：图标（或别的内容）在上，说明在下。</summary>
    static FlexControl Cell(Control content, string label) =>
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Xs }, content, new SmallControl { Text = label, Tone = TextTone.Muted });

    /// <summary>与文字同一行的图标：同一条基线。</summary>
    static FlexControl Line(params Control[] children) => Build.Flex(new FlexControl { Align = FlexAlign.Baseline, Gap = Space.Xs }, children);

    static Control Basic() => Build.Grid(new GridControl { MinItemWidth = "5.5rem", Gap = Space.Md },
    [
        .. new (string Name, IconData Data)[]
        {
            ("Search", Lucide.Search), ("Bell", Lucide.Bell), ("Heart", Lucide.Heart), ("Settings", Lucide.Settings),
            ("House", Lucide.House), ("User", Lucide.User), ("Mail", Lucide.Mail), ("Calendar", Lucide.Calendar),
            ("Camera", Lucide.Camera), ("Cloud", Lucide.Cloud), ("Download", Lucide.Download), ("Folder", Lucide.Folder),
            ("MapPin", Lucide.MapPin), ("ShoppingCart", Lucide.ShoppingCart), ("Star", Lucide.Star), ("Zap", Lucide.Zap),
        }.Select(icon => Cell(new IconControl { Data = icon.Data, IconSize = TextSize.Xl }, icon.Name)),
    ]);

    static Control Sizes() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
        Build.Flex(new FlexControl { Align = FlexAlign.End, Gap = Space.Lg, Wrap = true },
        [
            .. Build.Steps.Select(size => Cell(new IconControl { Data = Lucide.Star, IconSize = size }, size.Value)),
            Cell(Build.Styled(Line(new IconControl { Data = Lucide.Star }, new TextControl { Text = "星标" }), TextSize.Xl), "跟随文字"),
        ]),
        Build.Flex(new FlexControl { Align = FlexAlign.End, Gap = Space.Lg, Wrap = true },
            [.. new[] { 16f, 24, 32, 48 }.Select(size => Cell(new IconControl { Data = Lucide.Star, IconSize = size }, $"{size}px"))]),
        Build.Flex(new FlexControl { Align = FlexAlign.End, Gap = Space.Lg, Wrap = true },
            [.. new[] { 1f, 1.5f, 2, 2.5f, 3 }.Select(width => Cell(new IconControl { Data = Lucide.Star, IconSize = 32, StrokeWidth = width }, $"线宽 {width}"))]));

    static Control Tones() => Build.Grid(new GridControl { MinItemWidth = "9rem", Gap = Space.Md },
    [
        .. new (TextTone Tone, IconData Data, string Label)[]
        {
            (TextTone.Neutral, Lucide.CircleUser, "中性：正文色"), (TextTone.Muted, Lucide.CircleUser, "次要"), (TextTone.Accent, Lucide.Sparkles, "品牌色"),
            (TextTone.Info, Lucide.Info, "信息"), (TextTone.Success, Lucide.CircleCheck, "成功"), (TextTone.Warning, Lucide.TriangleExclamationPoint, "警告"),
            (TextTone.Danger, Lucide.CircleExclamationPoint, "危险"),
        }.Select(item => Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm },
            new IconControl { Data = item.Data, Tone = item.Tone, IconSize = TextSize.Xl },
            new TextControl { Text = item.Label, TextSize = TextSize.Sm })),
    ]);

    static Control Custom() => Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xl },
        new IconControl { Data = CustomIcons.Logo, IconSize = 40, AccessibleName = "站点标志" },
        new IconControl { Data = CustomIcons.Verified, IconSize = 40, Tone = TextTone.Accent, AccessibleName = "已认证" },
        new IconControl { Data = CustomIcons.Wave, IconSize = 40, Tone = TextTone.Info, AccessibleName = "信号" });

    static Control WithText() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
        Build.Flex(new FlexControl { Gap = Space.Lg, Wrap = true },
            Build.Styled(Line(new IconControl { Data = Lucide.MapPin }, new TextControl { Text = "上海·徐汇" }), tone: TextTone.Muted),
            Build.Styled(Line(new IconControl { Data = Lucide.Clock }, new TextControl { Text = "3 分钟前" }), tone: TextTone.Muted),
            Build.Styled(Line(new IconControl { Data = Lucide.Users }, new TextControl { Text = "128 人参与" }), tone: TextTone.Muted)),
        // 一句话里的图标：链接自带外链图标（External）。LinkControl 还没有基线，按基线对齐时会比前后的文字高一点，所以这里居中
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Wrap = true },
            new TextControl { Text = "图标跟随文字的字号与颜色，和文字基线对齐；" },
            new TextControl { Text = "放在句子中间也不会撑高行距，" },
            new LinkControl { Text = "查看设计规范", Href = new Uri("https://example.com/guide/design"), External = true },
            new TextControl { Text = "。" }),
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true },
            new ButtonControl { Text = "新建项目", Icon = Lucide.Plus },
            new ButtonControl { Text = "继续", Variant = ButtonVariant.Ghost, IconEnd = Lucide.ArrowRight },
            new BadgeControl { Text = "精选", Tone = StatusTone.Warning, Icon = Lucide.Star }));

    static Control Direction() => Build.Grid(new GridControl { MinItemWidth = "14rem", Gap = Space.Md },
        DirectionCard("从左到右（中文、英文）", RightToLeft.No),
        DirectionCard("从右到左（阿拉伯语、希伯来语）", RightToLeft.Yes));

    /// <summary>一张卡片：有方向的图标（箭头、chevron）设 FlipInRtl，时钟、勾号不翻。</summary>
    static CardControl DirectionCard(string caption, RightToLeft direction) => Build.Card(new CardControl { CardPadding = Space.Sm, RightToLeft = direction },
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm },
            new SmallControl { Text = caption, Tone = TextTone.Muted },
            Line(new IconControl { Data = Lucide.ChevronLeft, FlipInRtl = true }, new TextControl { Text = "返回" }),
            Line(new TextControl { Text = "下一步" }, new IconControl { Data = Lucide.ArrowRight, FlipInRtl = true }),
            Line(new IconControl { Data = Lucide.Clock }, new TextControl { Text = "刚刚更新" }),
            Line(new IconControl { Data = Lucide.Check }, new TextControl { Text = "已读" })));

    static Control Accessibility() => Build.Flex(new FlexControl { Gap = Space.Xl, Wrap = true },
        // 旁边已有文字：图标只是装饰，读屏跳过
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
            Line(new IconControl { Data = Lucide.CircleCheck, Tone = TextTone.Success }, new TextControl { Text = "已保存" }),
            Line(new IconControl { Data = Lucide.TriangleExclamationPoint, Tone = TextTone.Warning }, new TextControl { Text = "3 项待确认" }),
            Line(new IconControl { Data = Lucide.CircleX, Tone = TextTone.Danger }, new TextControl { Text = "同步失败" })),
        // 单独出现、表达含义：给名称
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Md },
            new IconControl { Data = Lucide.TriangleExclamationPoint, Tone = TextTone.Warning, IconSize = TextSize.Xl, AccessibleName = "有未保存的修改" },
            new IconControl { Data = Lucide.Lock, Tone = TextTone.Muted, IconSize = TextSize.Xl, AccessibleName = "仅自己可见" },
            new IconControl { Data = Lucide.Wifi, Tone = TextTone.Success, IconSize = TextSize.Xl, AccessibleName = "已连接" }));
}

/// <summary>Icon/custom 的图标，三个库写法相同（绝对坐标的 M、L、C、Z）。</summary>
static class CustomIcons
{
    /// <summary>站点标志：一个 V 加上面的点。</summary>
    public static readonly IconData Logo = new("logo", "M4 6L12 18L20 6M10.5 4C10.5 3.172 11.172 2.5 12 2.5C12.828 2.5 13.5 3.172 13.5 4C13.5 4.828 12.828 5.5 12 5.5C11.172 5.5 10.5 4.828 10.5 4Z");

    /// <summary>认证标记：圆圈、勾，右上角的实心点颜色仍跟随文字。</summary>
    public static readonly IconData Verified = new(
        "verified",
        "M3 12C3 7.029 7.029 3 12 3C16.971 3 21 7.029 21 12C21 16.971 16.971 21 12 21C7.029 21 3 16.971 3 12ZM8 12L11 15L16 9",
        "M16 5.5C16 4.119 17.119 3 18.5 3C19.881 3 21 4.119 21 5.5C21 6.881 19.881 8 18.5 8C17.119 8 16 6.881 16 5.5Z");

    /// <summary>波形：原稿画在 32 格上，按 24/32 换算到 24 格。</summary>
    public static readonly IconData Wave = new("wave", "M1.5 12C4.5 6 7.5 6 10.5 12C13.5 18 16.5 18 19.5 12C22.5 6 22.5 9 22.5 9");
}
