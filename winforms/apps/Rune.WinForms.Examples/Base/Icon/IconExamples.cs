using static Rune.WinForms.Examples.Layout;

namespace Rune.WinForms.Examples;

/// <summary>
/// Icon 的示例（vue-tsx-pro 文档站 base/Icon）。文档站的 with-text 要等 Text、Button、Badge，draw 等描线动画，到时补上。
/// WinForms 里放进窗体的是 <see cref="IconControl"/>：大小档位叫 IconSize，名称写 AccessibleName。
/// </summary>
static class IconExamples
{
    public static Example[] All =>
    [
        new("Icon", "basic", "基础用法", "从 Lucide 类按名取图标交给 Data；设计器的属性窗口里按名字选。全部图标都在 Lucide 类里，第一次用到时才创建。", Basic),
        new("Icon", "sizes", "尺寸与线宽", "不写 IconSize 时是 1em，跟随控件的字体；档位对应主题字号，数字是像素。StrokeWidth 调线宽，大图标用细一点的线更精致。", Sizes),
        new("Icon", "tones", "色调", "不写 Tone 时跟随 ForeColor；状态图标用 success、warning、danger 等色调，和提示条、标记的颜色一致。", Tones),
        new("Icon", "direction", "从右到左", "FlipInRtl 让图标在从右到左（RightToLeft）的排版里水平翻转：箭头、chevron、“返回”这类有方向的图标要翻，时钟、勾号不翻。", Direction),
        new("Icon", "accessibility", "装饰与含义", "旁边已有文字时图标只是装饰，不写名称；单独出现、表达含义的图标要给 AccessibleName。", Accessibility),
        new("Icon", "custom", "自定义图标", "按 Lucide 的格式写 24 格画布上的路径（绝对坐标的 M、L、C、Z）；实心的部分写在 fillPath 里，颜色仍跟随文字。", Custom),
    ];

    static Control Basic() => Wrap(12,
    [
        .. new (string Name, IconData Data)[]
        {
            ("Search", Lucide.Search), ("Bell", Lucide.Bell), ("Heart", Lucide.Heart), ("Settings", Lucide.Settings),
            ("House", Lucide.House), ("User", Lucide.User), ("Mail", Lucide.Mail), ("Calendar", Lucide.Calendar),
            ("Camera", Lucide.Camera), ("Cloud", Lucide.Cloud), ("Download", Lucide.Download), ("Folder", Lucide.Folder),
            ("MapPin", Lucide.MapPin), ("ShoppingCart", Lucide.ShoppingCart), ("Star", Lucide.Star), ("Zap", Lucide.Zap),
        }.Select(icon => Cell(new IconControl { Data = icon.Data, IconSize = TextSize.Xl }, icon.Name, width: 88)),
    ]);

    static Control Sizes()
    {
        var followText = Row(4, new IconControl { Data = Lucide.Star, Anchor = AnchorStyles.None }, Text("星标"));
        followText.Font = new Font(Fonts.Installed(RuneTheme.Current.Peek().Fonts.Sans), Tokens.FontSize.Xl * 72 / 96);
        var sizes = Wrap(24,
        [
            .. TextSize.Known.Select(size => Cell(new IconControl { Data = Lucide.Star, IconSize = size }, size.Value)),
            Cell(followText, "跟随文字"),
        ]);
        var pixels = Wrap(24, [.. new[] { 16f, 24, 32, 48 }.Select(size => Cell(new IconControl { Data = Lucide.Star, IconSize = size }, $"{size}px"))]);
        var strokes = Wrap(24, [.. new[] { 1f, 1.5f, 2, 2.5f, 3 }.Select(width => Cell(new IconControl { Data = Lucide.Star, IconSize = 32, StrokeWidth = width }, $"线宽 {width}"))]);
        return Stack(sizes, pixels, strokes);
    }

    static Control Tones() => Wrap(12,
    [
        .. new (TextTone Tone, IconData Data, string Label)[]
        {
            (TextTone.Neutral, Lucide.CircleUser, "中性：正文色"), (TextTone.Muted, Lucide.CircleUser, "次要"), (TextTone.Accent, Lucide.Sparkles, "品牌色"),
            (TextTone.Info, Lucide.Info, "信息"), (TextTone.Success, Lucide.CircleCheck, "成功"), (TextTone.Warning, Lucide.TriangleExclamationPoint, "警告"),
            (TextTone.Danger, Lucide.CircleExclamationPoint, "危险"),
        }.Select(item =>
        {
            var row = Row(8, new IconControl { Data = item.Data, Tone = item.Tone, IconSize = TextSize.Xl, Anchor = AnchorStyles.None }, Text(item.Label, Tokens.FontSize.Sm));
            row.MinimumSize = new Size(row.LogicalToDeviceUnits(144), 0);
            return row;
        }),
    ]);

    static Control Direction() => Wrap(12, Card("从左到右（中文、英文）", RightToLeft.No), Card("从右到左（阿拉伯语、希伯来语）", RightToLeft.Yes));

    /// <summary>一张卡片：有方向的图标（箭头、chevron）设 FlipInRtl，时钟、勾号不翻。Card 做出来后换成 Card。</summary>
    static Control Card(string caption, RightToLeft direction)
    {
        var card = Column(8,
            Caption(caption),
            Row(4, Inline(Lucide.ChevronLeft, flip: true), Text("返回")),
            Row(4, Text("下一步"), Inline(Lucide.ArrowRight, flip: true)),
            Row(4, Inline(Lucide.Clock), Text("刚刚更新")),
            Row(4, Inline(Lucide.Check), Text("已读")));
        card.RightToLeft = direction;
        card.Padding = new Padding(card.LogicalToDeviceUnits(12));
        card.MinimumSize = new Size(card.LogicalToDeviceUnits(240), 0);
        card.BackColor = RuneTheme.Current.Peek().Colors.Surface;
        card.BorderStyle = BorderStyle.FixedSingle;
        return card;
    }

    static Control Accessibility() => Wrap(32,
        Column(4,
            Row(4, Inline(Lucide.CircleCheck, TextTone.Success), Text("已保存")),
            Row(4, Inline(Lucide.TriangleExclamationPoint, TextTone.Warning), Text("3 项待确认")),
            Row(4, Inline(Lucide.CircleX, TextTone.Danger), Text("同步失败"))),
        Row(12,
            new IconControl { Data = Lucide.TriangleExclamationPoint, Tone = TextTone.Warning, IconSize = TextSize.Xl, AccessibleName = "有未保存的修改" },
            new IconControl { Data = Lucide.Lock, Tone = TextTone.Muted, IconSize = TextSize.Xl, AccessibleName = "仅自己可见" },
            new IconControl { Data = Lucide.Wifi, Tone = TextTone.Success, IconSize = TextSize.Xl, AccessibleName = "已连接" }));

    static Control Custom() => Row(32,
        new IconControl { Data = CustomIcons.Logo, IconSize = 40, AccessibleName = "站点标志" },
        new IconControl { Data = CustomIcons.Verified, IconSize = 40, Tone = TextTone.Accent, AccessibleName = "已认证" },
        new IconControl { Data = CustomIcons.Wave, IconSize = 40, Tone = TextTone.Info, AccessibleName = "信号" });

    /// <summary>与文字同一行的图标：跟随字体（1em），竖直居中。</summary>
    static IconControl Inline(IconData data, TextTone tone = default, bool flip = false) => new() { Data = data, Tone = tone, FlipInRtl = flip, Anchor = AnchorStyles.None };

    /// <summary>几块竖着叠起来（宽度随宿主，块内放不下时换行）。</summary>
    static Control Stack(params Control[] blocks)
    {
        var stack = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        // Dock = Top 的子控件后加的在上面：倒着加
        foreach (var block in blocks.Reverse())
        {
            block.Margin = Padding.Empty;
            stack.Controls.Add(block);
        }
        return stack;
    }
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
