using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Marquee 页的示例（vue-tsx-pro 文档站 display/Marquee）。</summary>
static class MarqueeExamples
{
    public static Example[] All =>
    [
        new("Marquee", "partners", "合作伙伴与标签墙", "内容后面接一份画出来的复制，平移到正好一份的长度时和开头重合，无缝循环；Speed 是每秒多少像素。Fade 让两端渐隐；Direction=right 倒着走。指针悬停、焦点在里面时暂停（PauseOnHover，默认开）。复制的那份点不到、读屏里没有，读屏只念一遍。", Partners),
        new("Marquee", "notice", "滚动公告", "Direction=up 竖着滚，适合一行高的公告栏。IsPaused 由外面控制：持续动的内容要给一个暂停按钮（WCAG 2.2.2）。系统关闭动画时不滚动，改成可以手动滚动、能用键盘滚的区域。", Notice),
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
        var partners = new Marquee { Fade = true, Gap = Space.Xl };
        AutomationProperties.SetName(partners, "合作伙伴");
        foreach (var (icon, name) in PartnerList)
            partners.Children.Add(new Flex
            {
                Align = FlexAlign.Center, Gap = Space.Sm,
                Children = { new Icon { Data = icon, Tone = TextTone.Muted }, new Text { Size = TextSize.Lg, Tone = TextTone.Muted, Weight = TextWeight.Semibold, Text = name } },
            });
        var tags = new Marquee { Fade = true, Direction = MarqueeDirection.Right, Speed = 25, Gap = Space.Sm };
        AutomationProperties.SetName(tags, "特性");
        foreach (var tag in Tags) tags.Children.Add(new Badge { Variant = StatusVariant.Soft, Radius = Radius.Full, Content = tag });
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Children = { partners, tags } };
    }

    static Control Notice()
    {
        var notices = new Marquee { Direction = MarqueeDirection.Up, Speed = 20, Gap = Space.Sm, Height = 21 };
        AutomationProperties.SetName(notices, "公告");
        foreach (var notice in Notices) notices.Children.Add(new Text { Text = notice });
        Flex.SetGrow(notices, true);
        var toggle = new IconButton { Icon = Lucide.Pause };
        AutomationProperties.SetName(toggle, "暂停滚动");
        toggle.Click += (_, _) =>
        {
            notices.IsPaused = !notices.IsPaused;
            toggle.Icon = notices.IsPaused ? Lucide.Play : Lucide.Pause;
            AutomationProperties.SetName(toggle, notices.IsPaused ? "继续滚动" : "暂停滚动");
        };
        return new Card
        {
            Padding = Space.Sm,
            Content = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Icon { Data = Lucide.Megaphone, Tone = TextTone.Accent }, notices, toggle } },
        };
    }
}
