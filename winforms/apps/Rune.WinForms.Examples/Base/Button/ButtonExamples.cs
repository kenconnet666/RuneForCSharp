namespace Rune.WinForms.Examples;

/// <summary>
/// Button 的示例（vue-tsx-pro 文档站 base/Button；图标按钮、预设按钮也在这一页）。types 要等 Field、TextField，group 要等 ButtonGroup，disabled 的第二行要等 Tooltip。
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
        new("Button", "icon-buttons", "图标按钮", "IconButtonControl 宽高相等，默认 ghost 样式、中性色（一排图标不会满屏品牌色）；外观选项同按钮。没有文字，必须写 AccessibleName，读屏才知道它是做什么的。", IconButtons),
        new("Button", "presets", "关闭、清空与复制", "预设按钮：CloseButtonControl 读屏念语言包的“关闭”；CopyButtonControl 复制固定的 Value，或点击时才用 ValueFactory 生成的值，复制后换成对勾，Copied 报告结果。ClearButtonControl 是 TextFieldControl、SelectControl 的 Clearable 用的清空按钮。", Presets),
        new("Button", "loading", "加载中", "IsLoading 显示旋转指示，宽度不变、点击无效、焦点保留；很快完成的加载不出现指示，出现了至少显示 0.5 秒。", Loading),
        new("Button", "full-width", "占满宽度", "FullWidth 占满一行：登录页、移动端的主要操作。放进 FlexControl 时竖排由它拉到整行宽，横排的两个各 SetGrow 平分一行。", FullWidthExample),
        new("Button", "links", "链接按钮", "点了去别处用 LinkButtonControl 而不是按钮加点击：读屏念“链接”；只有图标的链接用 IconLinkButtonControl（页脚的文档、订阅、联系）。外观同按钮；桌面上 Href 用系统打开，External 显示外链图标并告诉读屏在新窗口打开。", Links),
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
            new LinkButtonControl { Text = "Vue 文档", Href = new Uri("https://vuejs.org"), External = true, Variant = ButtonVariant.Ghost, IconEnd = Lucide.ArrowRight }),
        new SeparatorControl(),
        // 页脚：只有图标的链接用 IconLinkButtonControl
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Wrap = true },
            new SmallControl { Text = "© 2026 vue-tsx-pro", Tone = TextTone.Muted },
            Build.Flex(new FlexControl { Gap = Space.Xs },
                IconLink("https://example.com/guide/getting-started", false, Lucide.BookOpen, "文档"),
                IconLink("https://example.com/feed.xml", true, Lucide.Rss, "订阅"),
                IconLink("https://youtube.com", true, Lucide.Video, "视频教程"),
                IconLink("mailto:hi@example.com", false, Lucide.Mail, "发邮件")))));

    static IconLinkButtonControl IconLink(string href, bool external, IconData icon, string name) =>
        new() { Href = new Uri(href), External = external, Icon = icon, AccessibleName = name };

    static Control IconButtons() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
        // 编辑器工具栏：默认 ghost、中性色，一组按钮之间用分隔线隔开
        Build.Card(new CardControl { CardPadding = Space.Xs }, Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs, Wrap = true },
            IconOnly(Lucide.LetterB, "加粗"), IconOnly(Lucide.Italic, "斜体"), IconOnly(Lucide.Underline, "下划线"), IconOnly(Lucide.Link2, "插入链接"),
            new SeparatorControl { Orientation = SeparatorOrientation.Vertical },
            IconOnly(Lucide.TextAlignStart, "左对齐"), IconOnly(Lucide.TextAlignCenter, "居中"), IconOnly(Lucide.TextAlignEnd, "右对齐"))),
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true },
            IconOnly(Lucide.Pencil, "编辑", ButtonVariant.Solid, StatusTone.Accent),
            IconOnly(Lucide.Share2, "分享", ButtonVariant.Soft),
            IconOnly(Lucide.Settings, "设置", ButtonVariant.Surface),
            IconOnly(Lucide.Ellipsis, "更多", ButtonVariant.Outline),
            new IconButtonControl { Icon = Lucide.Heart, AccessibleName = "收藏", Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Radius = Radius.Full },
            IconOnly(Lucide.Trash, "删除", tone: StatusTone.Danger)));

    static IconButtonControl IconOnly(IconData icon, string name, ButtonVariant variant = default, StatusTone tone = default) =>
        new() { Icon = icon, AccessibleName = name, Variant = variant, Tone = tone };

    static Control Presets()
    {
        var root = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md };
        var close = new CloseButtonControl();
        var notice = Build.Card(new CardControl(), Build.Flex(new FlexControl { Align = FlexAlign.Start, Justify = FlexJustify.Between, Gap = Space.Sm },
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
                new StrongControl { Text = "新版本已发布" },
                new TextControl { Text = "右上角的 CloseButtonControl 读屏念语言包里的“关闭”。", TextSize = TextSize.Sm, Tone = TextTone.Muted }),
            close));
        close.Click += (_, _) =>
        {
            var index = root.Controls.IndexOf(notice);
            root.Controls.Remove(notice);
            var closed = new SmallControl { Text = "提示已关闭（刷新示例可以恢复）", Tone = TextTone.Muted };
            root.Controls.Add(closed);
            root.Controls.SetChildIndex(closed, index);
            notice.Dispose();
        };
        const string apiKey = "sk-live-3f9a7c21e8b44d0a";
        var copies = 0;
        var count = new SmallControl { Text = "已复制 0 次", Tone = TextTone.Muted };
        // 模拟向服务端申请一条一次性的邀请链接
        var invite = new CopyButtonControl
        {
            ValueFactory = async () =>
            {
                await Task.Delay(800);
                return $"https://example.com/invite/{Guid.NewGuid().ToString("N")[..8]}";
            },
            Timeout = TimeSpan.FromSeconds(3),
            AccessibleName = "复制邀请链接",
        };
        invite.Copied += (_, e) =>
        {
            if (e.Success) count.Text = $"已复制 {++copies} 次";
        };
        var copy = Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm },
            Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm },
                new IconControl { Data = Lucide.KeyRound, Tone = TextTone.Muted },
                new CodeControl { Text = apiKey },
                new CopyButtonControl { Value = apiKey, AccessibleName = "复制 API 密钥" }),
            Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm },
                new IconControl { Data = Lucide.Link, Tone = TextTone.Muted },
                new TextControl { Text = "邀请链接（点击时才生成）", TextSize = TextSize.Sm },
                invite,
                count)));
        root.Controls.AddRange([notice, copy]);
        return root;
    }

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
