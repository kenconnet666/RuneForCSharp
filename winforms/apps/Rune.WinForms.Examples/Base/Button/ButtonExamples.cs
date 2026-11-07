namespace Rune.WinForms.Examples;

/// <summary>
/// Button 的示例（vue-tsx-pro 文档站 base/Button）。types 要等 Field、TextField，group 要等 ButtonGroup，disabled 的第二行要等 Tooltip。
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
        new("Button", "basic", "基础用法", "一组操作里只有一个主要操作用默认的实底样式，其余按重要程度依次减弱；破坏性操作放在另一侧。", Basic),
        new("Button", "variants", "样式", "solid 是一组操作里的主要操作，每组最多一个；其余按强调程度递减。", VariantsExample),
        new("Button", "tones", "色调", "Tone 与 Badge、Alert 同一套：neutral 用于取消这类不需要强调的操作，danger 用于删除等破坏性操作。", Tones),
        new("Button", "sizes", "尺寸", "TextSize 是字号档位，内边距、高度、图标都按 em 随之缩放；不写时跟随控件的字体。", Sizes),
        new("Button", "radius", "圆角", "Radius 默认 md；full 是胶囊形。", RadiusExample),
        new("Button", "icons", "图标", "Icon 在文字前、IconEnd 在文字后，大小跟随按钮的字号。WinForms 的按钮里只有一段文字，快捷键的 Kbd 放在按钮旁边。", Icons),
        new("Button", "loading", "加载中", "IsLoading 显示旋转指示，宽度不变、点击无效、焦点保留；很快完成的加载不出现指示，出现了至少显示 0.5 秒。", Loading),
        new("Button", "full-width", "占满宽度", "FullWidth 占满一行：登录页、移动端的主要操作。放进 FlexControl 时竖排由它拉到整行宽，横排的两个各 SetGrow 平分一行。", FullWidthExample),
        new("Button", "links", "链接按钮", "点了去别处用 LinkButtonControl 而不是按钮加点击：读屏念“链接”。外观同按钮；桌面上 Href 用系统打开，External 显示外链图标并告诉读屏在新窗口打开。", Links),
        new("Button", "disabled", "不可用", "Enabled=false 时整体变淡、不响应点击。", Disabled),
    ];

    /// <summary>一行按钮：放不下时换行，竖直居中。</summary>
    static FlexControl Row(params Control[] items) => Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true }, items);

    /// <summary>几行竖着排。</summary>
    static FlexControl Rows(params Control[] rows) => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md }, rows);

    /// <summary>卡片的页头在上、操作在页脚：页脚放一行两端对齐的 FlexControl（布局容器撑满页脚），删除放到另一侧。</summary>
    static Control Basic()
    {
        var published = false;
        var state = new TextControl { Text = "草稿 · 3 分钟前保存", TextSize = TextSize.Sm, Tone = TextTone.Muted };
        var publish = new ButtonControl { Text = "发布", Icon = Lucide.Send };
        publish.Click += (_, _) =>
        {
            published = !published;
            publish.Text = published ? "撤回" : "发布";
            state.Text = published ? "已发布" : "草稿 · 3 分钟前保存";
        };
        var footer = Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm, Wrap = true },
            new ButtonControl { Text = "删除", Variant = ButtonVariant.Ghost, Tone = StatusTone.Danger, Icon = Lucide.Trash },
            Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true },
                new ButtonControl { Text = "取消", Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral },
                new ButtonControl { Text = "预览", Variant = ButtonVariant.Outline, Icon = Lucide.Eye },
                publish));
        return Build.Card(new CardControl { Header = "Vue 3.5 的响应式解构", Extra = state, Footer = footer },
            new ParagraphControl { Text = "一组操作里只有“发布”用实底；“预览”“取消”依次减弱，“删除”放在另一侧，离主要操作远一点。", Tone = TextTone.Muted });
    }

    static Control VariantsExample() => Build.Grid(new GridControl { MinItemWidth = "9rem", Gap = Space.Lg },
        [.. Variants.Select(v => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Start, Gap = Space.Xs },
            new ButtonControl { Text = v.Variant, Variant = new ButtonVariant(v.Variant) },
            new SmallControl { Text = v.Usage, Tone = TextTone.Muted }))]);

    static Control Tones() => Rows(
    [
        .. new (StatusTone Tone, string Label)[]
        {
            (StatusTone.Neutral, "取消"), (StatusTone.Accent, "确定"), (StatusTone.Info, "详情"),
            (StatusTone.Success, "完成"), (StatusTone.Warning, "注意"), (StatusTone.Danger, "删除"),
        }.Select(row => Row([.. Variants.Select(v => new ButtonControl { Text = row.Label, Tone = row.Tone, Variant = new ButtonVariant(v.Variant) })])),
    ]);

    static Control Sizes() => Rows(
        Row([.. Build.Steps.Select(size => new ButtonControl { Text = size.Value, TextSize = size, Icon = Lucide.Plus })]),
        Row([.. Build.Steps.Select(size => new ButtonControl { Text = size.Value, TextSize = size, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral })]),
        Row([.. Build.Steps.Select(size => new IconButtonControl { Icon = Lucide.Settings, TextSize = size, Variant = ButtonVariant.Soft, AccessibleName = $"设置（{size.Value}）" })]));

    /// <summary>圆角档位与 Badge、Card、输入框共用一套（Radius），对应主题的 radii。</summary>
    static Control RadiusExample() => Rows(
        Row([.. Radius.Known.Select(radius => new ButtonControl { Text = radius.Value, Radius = radius })]),
        Row([.. Radius.Known.Select(radius => new ButtonControl { Text = radius.Value, Radius = radius, Variant = ButtonVariant.Outline, Icon = Lucide.Plus })]),
        Row([.. Radius.Known.Select(radius => new IconButtonControl { Icon = Lucide.Heart, Radius = radius, Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, AccessibleName = $"收藏（{radius.Value}）" })]));

    /// <summary>"搜索"的 Kbd 与"未读消息"里的 Badge：按钮里只有文字，要等多元素宿主（HANDOFF §4.1）才能放进按钮；现在 Kbd 放在按钮旁边，Badge 不放。</summary>
    static Control Icons() => Rows(
        Row(
            new ButtonControl { Text = "新建", Icon = Lucide.Plus },
            new ButtonControl { Text = "下载", Variant = ButtonVariant.Soft, Icon = Lucide.Download },
            new ButtonControl { Text = "更多", Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, IconEnd = Lucide.ChevronDown },
            new ButtonControl { Text = "下一步", Variant = ButtonVariant.Ghost, IconEnd = Lucide.ArrowRight },
            new ButtonControl { Text = "查看文档", Variant = ButtonVariant.Link, IconEnd = Lucide.ExternalLink }),
        Row(
            Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs },
                new ButtonControl { Text = "搜索", Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Icon = Lucide.Search },
                new KbdControl { Text = "Ctrl K" }),
            new ButtonControl { Text = "未读消息", Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral },
            new IconButtonControl { Icon = Lucide.Pencil, AccessibleName = "编辑" },
            new IconButtonControl { Icon = Lucide.Share2, AccessibleName = "分享" },
            new IconButtonControl { Icon = Lucide.Trash, Tone = StatusTone.Danger, AccessibleName = "删除" }));

    static Control Links() => Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
            new StrongControl { Text = "还没有订单" },
            new ParagraphControl { Text = "新建第一张订单，或者先看看怎么批量导入。", TextSize = TextSize.Sm, Tone = TextTone.Muted }),
        Build.Flex(new FlexControl { Gap = Space.Sm, Wrap = true },
            new LinkButtonControl { Text = "新建订单", Href = new Uri("https://example.com/orders/new"), Icon = Lucide.Plus },
            new LinkButtonControl { Text = "导入指南", Href = new Uri("https://example.com/guide/import"), Variant = ButtonVariant.Soft, Icon = Lucide.BookOpen },
            new LinkButtonControl { Text = "Vue 文档", Href = new Uri("https://vuejs.org"), External = true, Variant = ButtonVariant.Ghost, IconEnd = Lucide.ArrowRight })));

    /// <summary>点击后模拟一次请求；很快完成（0.1 秒）的不出现旋转指示。</summary>
    static Control Loading()
    {
        var save = new ButtonControl { Text = "保存" };
        var submit = new ButtonControl { Text = "提交审核", Variant = ButtonVariant.Outline, Icon = Lucide.Send };
        var delete = new ButtonControl { Text = "删除", Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Icon = Lucide.Trash };
        var refresh = new IconButtonControl { Icon = Lucide.RefreshCw, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, AccessibleName = "刷新" };
        var quick = new ButtonControl { Text = "很快完成（0.1 秒）", Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral };
        save.Click += async (_, _) => await Request(loading => save.IsLoading = loading, TimeSpan.FromSeconds(1.5));
        submit.Click += async (_, _) => await Request(loading => submit.IsLoading = loading, TimeSpan.FromSeconds(2));
        delete.Click += async (_, _) => await Request(loading => delete.IsLoading = loading, TimeSpan.FromSeconds(1.2));
        refresh.Click += async (_, _) => await Request(loading => refresh.IsLoading = loading, TimeSpan.FromSeconds(1));
        quick.Click += async (_, _) => await Request(loading => quick.IsLoading = loading, TimeSpan.FromSeconds(0.1));
        return Rows(
            Row(save, submit, delete, refresh),
            Row(quick, new SmallControl { Text = "不到 0.2 秒的操作不换成加载指示，按钮不会闪一下", Tone = TextTone.Muted }));

        static async Task Request(Action<bool> loading, TimeSpan duration)
        {
            loading(true);
            await Task.Delay(duration);
            loading(false);
        }
    }

    /// <summary>占满宽度：竖排的 Flex 把按钮拉到整行；并排的两个各自分剩下的空间。</summary>
    static Control FullWidthExample()
    {
        var phone = new ButtonControl { Text = "手机验证码", FullWidth = true, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Icon = Lucide.Smartphone };
        var mail = new ButtonControl { Text = "邮箱验证码", FullWidth = true, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Icon = Lucide.Mail };
        var pair = Build.Flex(new FlexControl { Gap = Space.Sm }, phone, mail);
        pair.SetGrow(phone, true);
        pair.SetGrow(mail, true);
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm },
            new ButtonControl { Text = "登录", FullWidth = true, TextSize = TextSize.Lg, Icon = Lucide.LogIn },
            new SmallControl { Text = "或者", Tone = TextTone.Muted, Align = TextAlign.Center },
            pair,
            new ButtonControl { Text = "忘记密码？", FullWidth = true, Variant = ButtonVariant.Link, Tone = StatusTone.Neutral });
    }

    /// <summary>Tooltip 做出来后补上第二行：不可用但能聚焦（focusableWhenDisabled），悬停说明为什么不能点。</summary>
    static Control Disabled() => Row([.. Variants.Select(v => new ButtonControl { Text = v.Variant, Variant = new ButtonVariant(v.Variant), Enabled = false })]);
}
