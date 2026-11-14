namespace Rune.WinForms.Examples;

/// <summary>Marquee 页的示例（vue-tsx-pro 文档站 display/Marquee）。WinForms 没有两端渐隐（盖不了子控件）。</summary>
static class MarqueeExamples
{
    public static Example[] All =>
    [
        new("Marquee", "partners", "合作伙伴与标签墙", "内容后面接一份复制（内容的快照，画在背景上），平移到正好一份的长度时和开头重合，无缝循环；Speed 是每秒多少像素；Direction=right 倒着走。指针悬停、焦点在里面时暂停（PauseOnHover，默认开）。复制的那份点不到、读屏里没有，读屏只念一遍。", Partners),
        new("Marquee", "notice", "滚动公告", "Direction=up 竖着滚，适合一行高的公告栏（竖着滚时自己给高度）。IsPaused 由外面控制：持续动的内容要给一个暂停按钮（WCAG 2.2.2）。系统关闭动画时不滚动，改成可以手动滚动的区域。", Notice),
    ];

    static readonly (IconData Icon, string Name)[] PartnerList =
    [
        (Lucide.Cloud, "云杉科技"), (Lucide.Rocket, "星舰物流"), (Lucide.Leaf, "青禾农业"), (Lucide.Mountain, "远山户外"),
        (Lucide.WavesHorizontal, "潮汐传媒"), (Lucide.Zap, "闪电支付"), (Lucide.Gem, "晶石珠宝"), (Lucide.Coffee, "拾光咖啡"),
    ];

    static readonly string[] Tags = ["组件库", "主题定制", "无障碍", "虚拟滚动", "StyleX", "TSX", "日期组件", "3D 效果", "表单校验", "图表"];

    static readonly string[] Notices =
    [
        "10 月 25 日 02:00–04:00 系统维护，期间暂停下单",
        "双十一预售今晚 20:00 开始，定金立减 50 元",
        "新增顺丰次日达，覆盖 120 个城市",
        "客服热线工作时间调整为 8:00–22:00",
    ];

    static Control Partners()
    {
        var partners = new MarqueeControl { Gap = Space.Xl, AccessibleName = "合作伙伴" };
        foreach (var (icon, name) in PartnerList)
            partners.Items.Add(Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm },
                new IconControl { Data = icon, Tone = TextTone.Muted, IconSize = TextSize.Lg },
                new TextControl { TextSize = TextSize.Lg, Tone = TextTone.Muted, Weight = TextWeight.Semibold, Text = name }));
        var tags = new MarqueeControl { Direction = MarqueeDirection.Right, Speed = 25, Gap = Space.Sm, AccessibleName = "特性" };
        foreach (var tag in Tags) tags.Items.Add(new BadgeControl { Variant = StatusVariant.Soft, Radius = Radius.Full, Text = tag });
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch }, partners, tags);
    }

    static Control Notice()
    {
        var notices = new MarqueeControl { Direction = MarqueeDirection.Up, Speed = 20, Gap = Space.Sm, AccessibleName = "公告", AutoSize = false };
        foreach (var notice in Notices) notices.Items.Add(new TextControl { Text = notice });
        notices.Height = notices.LogicalToDeviceUnits(21);
        var toggle = new IconButtonControl { Icon = Lucide.Pause, AccessibleName = "暂停滚动" };
        toggle.Click += (_, _) =>
        {
            notices.IsPaused = !notices.IsPaused;
            toggle.Icon = notices.IsPaused ? Lucide.Play : Lucide.Pause;
            toggle.AccessibleName = notices.IsPaused ? "继续滚动" : "暂停滚动";
        };
        var row = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm }, new IconControl { Data = Lucide.Megaphone, Tone = TextTone.Accent }, notices, toggle);
        row.SetGrow(notices, true);
        return Build.Card(new CardControl { CardPadding = Space.Sm }, row);
    }
}
