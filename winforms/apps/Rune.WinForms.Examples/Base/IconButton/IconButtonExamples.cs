namespace Rune.WinForms.Examples;

/// <summary>
/// IconButton 的示例（vue-tsx-pro 文档站 base/IconButton）。states 要等 Tooltip、CopyButton，presets 要等 TextField、CopyButton，links（IconLinkButton 已有）还没写。
/// WinForms 里放进窗体的是 IconButtonControl：读屏名称写 AccessibleName。
/// </summary>
static class IconButtonExamples
{
    public static Example[] All =>
    [
        new("IconButton", "toolbar", "基础用法", "宽高相等，默认 ghost 样式、中性色（一排图标不会满屏品牌色）；外观选项同 Button。没有文字，必须写 AccessibleName，读屏才知道它是做什么的。", Toolbar),
    ];

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
