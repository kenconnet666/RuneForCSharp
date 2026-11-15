namespace Rune.WinForms.Examples;

/// <summary>AffixControl 的示例（vue-tsx-pro 文档站 layout/Affix）。</summary>
static class AffixExamples
{
    public static Example[] All =>
    [
        new("Affix", "affix", "滚动面板里的操作条", "AffixControl 放在 ScrollAreaControl 的内容里：滚过时贴在滚动区域的顶部（OffsetTop），原位留着空位；StuckChanged 报告贴住、离开。滚动区域默认是外层最近的 ScrollAreaControl，也可以用 Target 指定。面板滚下去之后右下角出现“回到顶部”。", Toolbar),
        new("Affix", "bottom", "贴在底部的提交栏", "OffsetBottom 贴在滚动区域的底边，滚到表单末尾时回到原位；IsStuck 时说明下面还有内容。WinForms 的控件画不出阴影，贴住时没有阴影。", SubmitBar),
    ];

    static readonly string[] Titles = ["来料检验报告补录", "模具保养计划确认", "供应商对账", "工艺变更评审", "月度销售复盘"];
    static readonly string[] Owners = ["林小满", "周一航", "许悠", "韩笑"];

    /// <summary>示例框：固定高度的卡片，里面一个 Panel 装滚动区域与浮动按钮。</summary>
    static CardControl Frame(params Control[] layers)
    {
        var page = new Panel();
        page.Height = page.LogicalToDeviceUnits(320);
        page.Controls.AddRange(layers);
        return Build.Card(new CardControl { CardPadding = Space.None, Dock = DockStyle.Top }, page);
    }

    static FlexControl Column(Space gap)
    {
        var column = new FlexControl { Direction = FlexDirection.Column, Gap = gap, Align = FlexAlign.Stretch };
        column.Padding = new Padding(column.LogicalToDeviceUnits(12));
        return column;
    }

    static Control Toolbar()
    {
        var hint = new SmallControl { Text = "已贴住", Tone = TextTone.Muted, Visible = false };
        var bar = Build.Card(new CardControl { CardPadding = Space.Sm },
            Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm, Wrap = true },
                Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm },
                    new TextControl { Text = "待办任务", Weight = TextWeight.Semibold }, new BadgeControl { Text = "30", TextSize = TextSize.Xs, Variant = StatusVariant.Soft }, hint),
                Build.Flex(new FlexControl { Gap = Space.Xs },
                    new ButtonControl { Text = "导出", TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Icon = Lucide.Download },
                    new ButtonControl { Text = "新建任务", TextSize = TextSize.Sm, Icon = Lucide.Plus })));
        var affix = new AffixControl { Content = bar };
        affix.StuckChanged += (_, _) => hint.Visible = affix.IsStuck;
        var list = Column(Space.Xs);
        list.Controls.AddRange([new SmallControl { Text = "往下滚：操作条会贴在面板顶部", Tone = TextTone.Muted }, affix]);
        for (var index = 0; index < 30; index++)
        {
            var title = new TextControl { Text = Titles[index % 5], TextSize = TextSize.Sm };
            var row = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Md },
                new TextControl { Text = $"T-{101 + index:000}", Mono = true, TextSize = TextSize.Sm, Tone = TextTone.Muted }, title, new SmallControl { Text = Owners[index % 4], Tone = TextTone.Muted });
            row.SetGrow(title, true);
            list.Controls.Add(row);
        }
        var scroller = new ScrollAreaControl { Dock = DockStyle.Fill, Content = list };
        return Frame(new BackTopControl { Target = scroller, Threshold = 200 }, scroller);
    }

    static Control SubmitBar()
    {
        var status = new SmallControl { Text = "下面还有内容，往下滚", Tone = TextTone.Muted };
        var saved = false;
        var form = Column(Space.Md);
        foreach (var label in new[] { "供应商名称", "统一社会信用代码", "开户银行", "银行账号", "联系人", "联系电话", "收货地址", "发票抬头" })
            form.Controls.Add(new FieldControl { Label = label, Content = new TextFieldControl() });
        form.Controls.Add(new FieldControl { Label = "备注", Content = new TextareaControl { Rows = 3 } });
        var cancel = new ButtonControl { Text = "取消", TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };
        var save = new ButtonControl { Text = "保存", TextSize = TextSize.Sm };
        var affix = new AffixControl
        {
            OffsetBottom = 0,
            Content = Build.Card(new CardControl { CardPadding = Space.Sm },
                Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm, Wrap = true }, status, Build.Flex(new FlexControl { Gap = Space.Xs }, cancel, save))),
        };
        void Sync() => status.Text = saved ? "已保存" : affix.IsStuck ? "下面还有内容，往下滚" : "到底了";
        affix.StuckChanged += (_, _) => Sync();
        cancel.Click += (_, _) => { saved = false; Sync(); };
        save.Click += (_, _) => { saved = true; Sync(); };
        form.Controls.Add(affix);
        return Frame(new ScrollAreaControl { Dock = DockStyle.Fill, Content = form });
    }
}
