using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf.Examples;

/// <summary>Accordion 的示例（vue-tsx-pro 文档站 display/Accordion）。</summary>
static class AccordionExamples
{
    public static Example[] All =>
    [
        new("Accordion", "faq", "常见问题", "默认一次只展开一项，再点可以收起（Collapsible）；标题是按钮，读屏念出标题层级，↑ ↓ 在标题之间移动，Home / End 到两端。", Faq),
        new("Accordion", "collapsible", "单个展开区域", "Collapsible 是触发器加 Collapse：Trigger 放一个按钮，点它展开收起，读屏带上展开状态。", Disclosure),
        new("Accordion", "multiple", "多开", "SelectionMode=multiple 时每项独立展开，Values 是展开着的值；Title 可以放图标、计数；HeadingLevel 按所在页面的大纲选。", Settings),
    ];

    static AccordionItem Item(string value, object title, UIElement content, bool enabled = true) =>
        new() { Value = value, Title = title, Content = content, IsEnabled = enabled };

    static FrameworkElement Faq()
    {
        var accordion = new Accordion
        {
            Value = "ship",
            Children =
            {
                Item("ship", "付款后多久发货？", new Paragraph { Text = "工作日 16:00 前付款当天发货，之后的次日发货；节假日顺延。" }),
                Item("refund", "怎么申请退款？", new Paragraph { Text = "在订单详情里点“申请售后”，选择退款原因。审核通过后 1–3 个工作日原路退回，进度在“我的售后”里查看。" }),
                Item("invoice", "可以开发票吗？", new Paragraph { Text = "可以。下单时填写抬头与税号，电子发票会在发货后发到邮箱。" }),
                Item("member", "会员有什么权益？", new Paragraph { Text = "会员计划正在调整，敬请期待。" }, enabled: false),
            },
        };
        AutomationProperties.SetName(accordion, "常见问题");
        return new Card { Padding = Space.None, Content = accordion };
    }

    static FrameworkElement Disclosure()
    {
        var filters = new Grid
        {
            MinItemWidth = "9em", Gap = Space.Md,
            Children =
            {
                new Field { Label = "最低价", Content = new NumberField { Value = 200, Min = 0 } },
                new Field { Label = "最高价", Content = new NumberField { Value = 800, Min = 0 } },
                new Field { Label = "排序", Content = new Select { ItemsSource = new[] { new ListboxOption("relevance", "综合"), new ListboxOption("sales", "销量"), new ListboxOption("price", "价格") }, Value = "relevance" } },
            },
        };
        var trigger = new Button { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Icon = Lucide.SlidersHorizontal, IconEnd = Lucide.ChevronDown, Content = "高级筛选" };
        return new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children =
                {
                    new Field { Label = "搜索商品", Content = new TextField { Text = "机械键盘" } },
                    new Collapsible { Trigger = trigger, Content = filters },
                },
            },
        };
    }

    static FrameworkElement Title(IconData icon, string label, int? count = null)
    {
        var row = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Icon { Data = icon }, new Text { Weight = TextWeight.Medium, Text = label } } };
        if (count is { } value) row.Children.Add(new Badge { Radius = Radius.Full, Size = TextSize.Xs, Content = value.ToString() });
        return row;
    }

    static FrameworkElement Settings()
    {
        var open = new Small { Tone = TextTone.Muted };
        var accordion = new Accordion
        {
            SelectionMode = SelectionMode.Multiple, Values = ["notify", "privacy"], HeadingLevel = 3,
            Children =
            {
                Item("notify", Title(Lucide.Bell, "通知", 2), new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { new Switch { IsChecked = true, Content = "邮件通知" }, new Switch { Content = "推送通知" } } }),
                Item("privacy", Title(Lucide.Lock, "隐私"), new Switch { IsChecked = true, Content = "公开个人主页" }),
                Item("appearance", Title(Lucide.Palette, "外观"), new Switch { Content = "紧凑模式" }),
            },
        };
        AutomationProperties.SetName(accordion, "设置");
        void Show() => open.Text = $"展开着：{(accordion.Values is { Count: > 0 } values ? string.Join("、", values) : "无")}";
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Accordion.ValuesProperty, typeof(Accordion)).AddValueChanged(accordion, (_, _) => Show());
        Show();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { accordion, open } };
    }
}
