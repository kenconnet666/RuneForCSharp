namespace Rune.WinForms.Examples;

/// <summary>
/// IconButton 的示例（vue-tsx-pro 文档站 base/IconButton）。states 要等 Tooltip、CopyButton 与 focusableWhenDisabled，presets 要等 CopyButton、TextField。
/// WinForms 里放进窗体的是 IconButtonControl：读屏名称写 AccessibleName。
/// </summary>
static class IconButtonExamples
{
    public static Example[] All =>
    [
        new("IconButton", "toolbar", "基础用法", "宽高相等，默认 ghost 样式、中性色（一排图标不会满屏品牌色）；外观选项同 Button。没有文字，必须写 AccessibleName，读屏才知道它是做什么的。", Toolbar),
        new("IconButton", "links", "图标链接", "点了去别处的图标用 IconLinkButtonControl：读屏念\"链接\"；桌面没有站内地址，Href 写绝对地址、用系统打开，External 显示在新窗口打开并告诉读屏。", Links),
    ];

    static Control Links() => Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Wrap = true },
        new TextControl { Text = "© 2026 vue-tsx-pro", TextSize = TextSize.Sm, Tone = TextTone.Muted },
        Build.Flex(new FlexControl { Gap = Space.Xs },
            Link("https://example.com/guide/getting-started", false, Lucide.BookOpen, "文档"),
            Link("https://example.com/feed.xml", true, Lucide.Rss, "订阅"),
            Link("https://youtube.com", true, Lucide.Video, "视频教程"),
            Link("mailto:hi@example.com", false, Lucide.Mail, "发邮件")));

    static IconLinkButtonControl Link(string href, bool external, IconData icon, string name) =>
        new() { Href = new Uri(href), External = external, Icon = icon, AccessibleName = name };

    static Control Toolbar() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
        // 编辑器工具栏：默认 ghost，一组按钮之间用分隔线隔开
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs, Wrap = true },
            Icon(Lucide.LetterB, "加粗"), Icon(Lucide.Italic, "斜体"), Icon(Lucide.Underline, "下划线"), Icon(Lucide.Link2, "插入链接"),
            new SeparatorControl { Orientation = SeparatorOrientation.Vertical },
            Icon(Lucide.TextAlignStart, "左对齐"), Icon(Lucide.TextAlignCenter, "居中"), Icon(Lucide.TextAlignEnd, "右对齐")),
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true },
            Label("样式"),
            Icon(Lucide.Pencil, "编辑", ButtonVariant.Solid, StatusTone.Accent),
            Icon(Lucide.Share2, "分享", ButtonVariant.Soft),
            Icon(Lucide.Settings, "设置", ButtonVariant.Surface),
            Icon(Lucide.Ellipsis, "更多", ButtonVariant.Outline),
            new IconButtonControl { Icon = Lucide.Heart, AccessibleName = "收藏", Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Radius = Radius.Full },
            Icon(Lucide.Trash, "删除", tone: StatusTone.Danger)),
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = true },
        [
            Label("尺寸"),
            .. Build.Steps.Select(size => new IconButtonControl { Icon = Lucide.Settings, TextSize = size, Variant = ButtonVariant.Surface, AccessibleName = $"设置（{size.Value}）" }),
        ]));

    static IconButtonControl Icon(IconData icon, string name, ButtonVariant variant = default, StatusTone tone = default) =>
        new() { Icon = icon, AccessibleName = name, Variant = variant, Tone = tone };

    static Control Label(string text) => new TextControl { Text = text, TextSize = TextSize.Xs, Tone = TextTone.Muted };
}
