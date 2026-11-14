namespace Rune.WinForms.Examples;

/// <summary>AccordionControl 的示例（vue-tsx-pro 文档站 display/Accordion）。</summary>
static class AccordionExamples
{
    public static Example[] All =>
    [
        new("Accordion", "faq", "常见问题", "默认一次只展开一项，再点可以收起（Collapsible）；标题是按钮，↑ ↓ 在标题之间移动，Home / End 到两端。", Faq),
        new("Accordion", "collapsible", "单个展开区域", "CollapsibleControl 是触发器加 CollapseControl：Trigger 放一个按钮，点它展开收起。", Disclosure),
        new("Accordion", "multiple", "多开", "SelectionMode=multiple 时每项独立展开，Values 是展开着的值；TitleContent 可以放图标、计数。", Settings),
    ];

    static AccordionItemControl Item(string value, string title, Control content, Control? rich = null) =>
        new() { Value = value, Title = title, TitleContent = rich, Content = content };

    static Control Faq()
    {
        var accordion = new AccordionControl { Value = "ship", AccessibleName = "常见问题" };
        var member = Item("member", "会员有什么权益？", new ParagraphControl { Text = "会员计划正在调整，敬请期待。" });
        member.Enabled = false;
        accordion.Controls.AddRange([
            Item("ship", "付款后多久发货？", new ParagraphControl { Text = "工作日 16:00 前付款当天发货，之后的次日发货；节假日顺延。" }),
            Item("refund", "怎么申请退款？", new ParagraphControl { Text = "在订单详情里点“申请售后”，选择退款原因。审核通过后 1–3 个工作日原路退回，进度在“我的售后”里查看。" }),
            Item("invoice", "可以开发票吗？", new ParagraphControl { Text = "可以。下单时填写抬头与税号，电子发票会在发货后发到邮箱。" }),
            member,
        ]);
        return Build.Card(new CardControl { CardPadding = Space.None }, accordion);
    }

    static Control Disclosure()
    {
        var filters = Build.Grid(new GridControl { MinItemWidth = "9em", Gap = Space.Md },
            new FieldControl { Label = "最低价", Content = new NumberFieldControl { Value = 200, Min = 0 } },
            new FieldControl { Label = "最高价", Content = new NumberFieldControl { Value = 800, Min = 0 } },
            new FieldControl { Label = "排序", Content = new SelectControl { ItemsSource = new[] { new ListboxOption("relevance", "综合"), new ListboxOption("sales", "销量"), new ListboxOption("price", "价格") }, Value = "relevance" } });
        var trigger = new ButtonControl { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Icon = Lucide.SlidersHorizontal, IconEnd = Lucide.ChevronDown, Text = "高级筛选" };
        return Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch },
            new FieldControl { Label = "搜索商品", Content = new TextFieldControl { Text = "机械键盘" } },
            new CollapsibleControl { Trigger = trigger, Content = filters }));
    }

    static Control Title(IconData icon, string label, int? count = null)
    {
        var row = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm, AutoSize = true }, new IconControl { Data = icon }, new TextControl { Weight = TextWeight.Medium, Text = label });
        if (count is { } value) row.Controls.Add(new BadgeControl { Radius = Radius.Full, TextSize = TextSize.Xs, Text = value.ToString() });
        return row;
    }

    static Control Settings()
    {
        var open = new SmallControl { Tone = TextTone.Muted };
        var accordion = new AccordionControl { SelectionMode = SelectionMode.Multiple, Values = ["notify", "privacy"], AccessibleName = "设置" };
        accordion.Controls.AddRange([
            Item("notify", "通知", Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, AutoSize = true },
                new SwitchControl { Text = "邮件通知", IsChecked = true }, new SwitchControl { Text = "推送通知" }), Title(Lucide.Bell, "通知", 2)),
            Item("privacy", "隐私", new SwitchControl { Text = "公开个人主页", IsChecked = true }, Title(Lucide.Lock, "隐私")),
            Item("appearance", "外观", new SwitchControl { Text = "紧凑模式" }, Title(Lucide.Palette, "外观")),
        ]);
        void Show() => open.Text = $"展开着：{(accordion.Values is { Count: > 0 } values ? string.Join("、", values) : "无")}";
        accordion.ValueChanged += (_, _) => Show();
        Show();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch }, accordion, open);
    }
}
