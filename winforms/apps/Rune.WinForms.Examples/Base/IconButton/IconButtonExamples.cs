using static Rune.WinForms.Examples.Layout;

namespace Rune.WinForms.Examples;

/// <summary>
/// IconButton 的示例（vue-tsx-pro 文档站 base/IconButton）。focusableWhenDisabled、Tooltip、CopyButton、CloseButton、IconLinkButton 做出来后补上其余几节。
/// WinForms 里放进窗体的是 IconButtonControl：读屏名称写 AccessibleName。
/// </summary>
static class IconButtonExamples
{
    public static Example[] All =>
    [
        new("IconButton", "toolbar", "基础用法", "宽高相等，默认 ghost 样式、中性色（一排图标不会满屏品牌色）；外观选项同 Button。没有文字，必须写 AccessibleName，读屏才知道它是做什么的。", Toolbar),
    ];

    static Control Toolbar() => Column(16,
        // 编辑器工具栏：默认 ghost，一组按钮之间用分隔线隔开
        Row(0,
            Icon(Lucide.LetterB, "加粗"), Icon(Lucide.Italic, "斜体"), Icon(Lucide.Underline, "下划线"), Icon(Lucide.Link2, "插入链接"),
            Separator(),
            Icon(Lucide.TextAlignStart, "左对齐"), Icon(Lucide.TextAlignCenter, "居中"), Icon(Lucide.TextAlignEnd, "右对齐")),
        Row(4,
            Label("样式"),
            Icon(Lucide.Pencil, "编辑", ButtonVariant.Solid, StatusTone.Accent),
            Icon(Lucide.Share2, "分享", ButtonVariant.Soft),
            Icon(Lucide.Settings, "设置", ButtonVariant.Surface),
            Icon(Lucide.Ellipsis, "更多", ButtonVariant.Outline),
            new IconButtonControl { Icon = Lucide.Heart, AccessibleName = "收藏", Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Radius = Radius.Full, Anchor = AnchorStyles.None },
            Icon(Lucide.Trash, "删除", tone: StatusTone.Danger)),
        Row(4,
            [
                Label("尺寸"),
                .. TextSize.Known.Select(size => new IconButtonControl { Icon = Lucide.Settings, TextSize = size, Variant = ButtonVariant.Surface, AccessibleName = $"设置（{size.Value}）", Anchor = AnchorStyles.None }),
            ]));

    static IconButtonControl Icon(IconData icon, string name, ButtonVariant variant = default, StatusTone tone = default) =>
        new() { Icon = icon, AccessibleName = name, Variant = variant, Tone = tone, Anchor = AnchorStyles.None };

    static Control Label(string text) => new SmallControl { Text = text, Tone = TextTone.Muted, Anchor = AnchorStyles.None };

    /// <summary>分隔线：Separator 做出来后换掉。</summary>
    static Control Separator() => new Panel
    {
        Width = Dip(1), Height = Dip(20), Anchor = AnchorStyles.None,
        BackColor = RuneTheme.Current.Peek().Colors.Border,
    };
}
