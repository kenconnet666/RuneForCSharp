using static Rune.WinForms.Examples.Layout;

namespace Rune.WinForms.Examples;

/// <summary>Badge 的示例（vue-tsx-pro 文档站 base/Badge）。in-context 等 Table，statuses 等 StatusBadge，tag 等 Tag。</summary>
static class BadgeExamples
{
    static readonly (StatusTone Tone, string Label)[] Tones =
    [
        (StatusTone.Neutral, "草稿"), (StatusTone.Accent, "新功能"), (StatusTone.Info, "处理中"),
        (StatusTone.Success, "已完成"), (StatusTone.Warning, "即将到期"), (StatusTone.Danger, "已失败"),
    ];

    public static Example[] All =>
    [
        new("Badge", "matrix", "样式与色调", "Variant 是 solid、soft（默认）、surface、outline，Tone 是六种状态色调；颜色都取主题 token，每套主题都过对比度检查。", Matrix),
        new("Badge", "shapes", "尺寸、形状与图标", "TextSize 默认 xs；Radius = full 是胶囊，计数常用实底；Icon 放在文字前，Dot 是同色的小圆点，表格里比整块底色更轻。", Shapes),
    ];

    /// <summary>一行一种样式、一列一种色调：第一列是样式名。</summary>
    static Control Matrix()
    {
        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 7 };
        foreach (var variant in StatusVariant.Known)
        {
            table.Controls.Add(Label(variant.Value));
            foreach (var (tone, label) in Tones)
                table.Controls.Add(new BadgeControl { Text = label, Variant = variant, Tone = tone, Margin = new Padding(Dip(6), Dip(4), Dip(6), Dip(4)) });
        }
        return table;
    }

    static Control Shapes() => Column(16,
        Row(8, [Label("字号"), .. new[] { TextSize.Xs, TextSize.Sm, TextSize.Md, TextSize.Lg }.Select(size => new BadgeControl { Text = $"size={size.Value}", TextSize = size, Tone = StatusTone.Accent, Anchor = AnchorStyles.None })]),
        Row(8, Label("胶囊与计数"),
            new BadgeControl { Text = "12", Radius = Radius.Full, Variant = StatusVariant.Solid, Tone = StatusTone.Danger },
            new BadgeControl { Text = "99+", Radius = Radius.Full, Variant = StatusVariant.Solid },
            new BadgeControl { Text = "在线", Radius = Radius.Full, Tone = StatusTone.Success },
            new BadgeControl { Text = "v2.4.0", Radius = Radius.Full, Variant = StatusVariant.Outline }),
        Row(8, Label("图标"),
            new BadgeControl { Text = "已通过", Icon = Lucide.CircleCheck, Tone = StatusTone.Success },
            new BadgeControl { Text = "待审核", Icon = Lucide.Clock, Tone = StatusTone.Warning },
            new BadgeControl { Text = "3 个错误", Icon = Lucide.TriangleExclamationPoint, Tone = StatusTone.Danger, Variant = StatusVariant.Surface },
            new BadgeControl { Text = "Beta", Icon = Lucide.Sparkles, Tone = StatusTone.Accent, Variant = StatusVariant.Solid },
            new BadgeControl { Text = "热门", Icon = Lucide.Flame, Tone = StatusTone.Warning, Variant = StatusVariant.Outline, Radius = Radius.Full }),
        Row(8, Label("圆点"),
            new BadgeControl { Text = "运行中", Dot = true, Tone = StatusTone.Success, Radius = Radius.None },
            new BadgeControl { Text = "待审核", Dot = true, Tone = StatusTone.Warning, Radius = Radius.None },
            new BadgeControl { Text = "已停止", Dot = true, Tone = StatusTone.Danger, Variant = StatusVariant.Outline }));

    static TextControl Label(string text) => new() { Text = text, TextSize = TextSize.Xs, Tone = Tone.Muted, Anchor = AnchorStyles.Left };
}
