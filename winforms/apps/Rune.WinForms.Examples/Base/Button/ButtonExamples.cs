using static Rune.WinForms.Examples.Layout;

namespace Rune.WinForms.Examples;

/// <summary>
/// Button 的示例（vue-tsx-pro 文档站 base/Button）。ButtonGroup、Card、Kbd 做出来后补上其余几节。
/// WinForms 里放进窗体的是 ButtonControl：字号档位叫 TextSize，文字是 Text。
/// </summary>
static class ButtonExamples
{
    static readonly (string Variant, string Usage)[] Variants =
    [
        ("solid", "主要操作，每组最多一个"), ("soft", "需要突出的次要操作"), ("surface", "表单、设置页里的次要操作"),
        ("outline", "次要操作"), ("ghost", "工具栏、行内操作"), ("link", "句子中间的操作"),
    ];

    public static Example[] All =>
    [
        new("Button", "variants", "样式", "solid 是一组操作里的主要操作，每组最多一个；其余按强调程度递减。", VariantsExample),
        new("Button", "tones", "色调", "Tone 与 Badge、Alert 同一套：neutral 用于取消这类不需要强调的操作，danger 用于删除等破坏性操作。", Tones),
        new("Button", "sizes", "尺寸", "TextSize 是字号档位，内边距、高度、图标都按 em 随之缩放；不写时跟随控件的字体。", Sizes),
        new("Button", "radius", "圆角", "Radius 默认 md；full 是胶囊形。", RadiusExample),
        new("Button", "icons", "图标", "Icon 在文字前、IconEnd 在文字后，大小跟随按钮的字号。", Icons),
        new("Button", "loading", "加载中", "IsLoading 显示旋转指示，宽度不变、点击无效、焦点保留；很快完成的加载不出现指示，出现了至少显示 0.5 秒。", Loading),
        new("Button", "links", "链接按钮", "点了去别处用 LinkButtonControl 而不是按钮加点击：读屏念“链接”。外观同按钮；桌面上 Href 用系统打开，External 显示外链图标并告诉读屏在新窗口打开。", Links),
    ];

    static Control VariantsExample() => Wrap(16,
    [
        .. Variants.Select(v =>
        {
            var cell = Column(4, new ButtonControl { Text = v.Variant, Variant = new ButtonVariant(v.Variant) }, new SmallControl { Text = v.Usage, Tone = TextTone.Muted });
            cell.MinimumSize = new Size(Dip(160), 0);
            return cell;
        }),
    ]);

    static Control Tones() => Column(0,
    [
        .. new (StatusTone Tone, string Label)[]
        {
            (StatusTone.Neutral, "取消"), (StatusTone.Accent, "确定"), (StatusTone.Info, "详情"),
            (StatusTone.Success, "完成"), (StatusTone.Warning, "注意"), (StatusTone.Danger, "删除"),
        }.Select(row => Row(0, [.. Variants.Select(v => new ButtonControl { Text = row.Label, Tone = row.Tone, Variant = new ButtonVariant(v.Variant), Anchor = AnchorStyles.None })])),
    ]);

    static Control Sizes() => Column(4,
        Row(0, [.. TextSize.Known.Select(size => new ButtonControl { Text = size.Value, TextSize = size, Icon = Lucide.Plus, Anchor = AnchorStyles.None })]),
        Row(0, [.. TextSize.Known.Select(size => new ButtonControl { Text = size.Value, TextSize = size, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Anchor = AnchorStyles.None })]),
        Row(0, [.. TextSize.Known.Select(size => new IconButtonControl { Icon = Lucide.Settings, TextSize = size, Variant = ButtonVariant.Soft, AccessibleName = $"设置（{size.Value}）", Anchor = AnchorStyles.None })]));

    static Control RadiusExample() => Column(4,
        Row(0, [.. Radius.Known.Select(radius => new ButtonControl { Text = radius.Value, Radius = radius })]),
        Row(0, [.. Radius.Known.Select(radius => new ButtonControl { Text = radius.Value, Radius = radius, Variant = ButtonVariant.Outline, Icon = Lucide.Plus })]));

    /// <summary>第二行的"搜索 Ctrl K"等 Kbd 做出来后补上；内容里放 Badge 要等多元素宿主（HANDOFF §4.1）。</summary>
    static Control Icons() => Column(4,
        Row(0,
            new ButtonControl { Text = "新建", Icon = Lucide.Plus },
            new ButtonControl { Text = "下载", Variant = ButtonVariant.Soft, Icon = Lucide.Download },
            new ButtonControl { Text = "更多", Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, IconEnd = Lucide.ChevronDown },
            new ButtonControl { Text = "下一步", Variant = ButtonVariant.Ghost, IconEnd = Lucide.ArrowRight },
            new ButtonControl { Text = "查看文档", Variant = ButtonVariant.Link, IconEnd = Lucide.ExternalLink }),
        Row(0,
            new IconButtonControl { Icon = Lucide.Pencil, AccessibleName = "编辑" },
            new IconButtonControl { Icon = Lucide.Share2, AccessibleName = "分享" },
            new IconButtonControl { Icon = Lucide.Trash, Tone = StatusTone.Danger, AccessibleName = "删除" }));

    /// <summary>Card 做出来后包一层卡片；桌面没有站内地址，链接都是绝对地址，用系统打开。</summary>
    static Control Links() => Column(16,
        Column(4, new StrongControl { Text = "还没有订单" }, new ParagraphControl { Text = "新建第一张订单，或者先看看怎么批量导入。", TextSize = TextSize.Sm, Tone = TextTone.Muted }),
        Row(0,
            new LinkButtonControl { Text = "新建订单", Href = new Uri("https://example.com/orders/new"), Icon = Lucide.Plus },
            new LinkButtonControl { Text = "导入指南", Href = new Uri("https://example.com/guide/import"), Variant = ButtonVariant.Soft, Icon = Lucide.BookOpen },
            new LinkButtonControl { Text = "Vue 文档", Href = new Uri("https://vuejs.org"), External = true, Variant = ButtonVariant.Ghost, IconEnd = Lucide.ArrowRight }));

    /// <summary>点击后模拟一次请求：保存 1.5 秒；很快完成（0.1 秒）的不出现旋转指示。</summary>
    static Control Loading()
    {
        var save = new ButtonControl { Text = "保存", Icon = Lucide.Save };
        var quick = new ButtonControl { Text = "很快完成（0.1 秒）", Variant = ButtonVariant.Outline };
        save.Click += async (_, _) => await Request(save, TimeSpan.FromSeconds(1.5));
        quick.Click += async (_, _) => await Request(quick, TimeSpan.FromSeconds(0.1));
        return Row(0, save, quick, new ButtonControl { Text = "一直在加载", Variant = ButtonVariant.Soft, IsLoading = true });

        static async Task Request(ButtonControl button, TimeSpan duration)
        {
            button.IsLoading = true;
            await Task.Delay(duration);
            button.IsLoading = false;
        }
    }
}
