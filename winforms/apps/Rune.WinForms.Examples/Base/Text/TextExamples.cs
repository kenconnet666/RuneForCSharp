using static Rune.WinForms.Examples.Layout;

namespace Rune.WinForms.Examples;

/// <summary>
/// Text 的示例（vue-tsx-pro 文档站 base/Text）。inline 要等 Kbd、Code、Mark、Blockquote，links 等 Link，headings 等 Heading。
/// WinForms 里放进窗体的是 TextControl、ParagraphControl……：字号档位叫 TextSize，文字内容是 Text。
/// </summary>
static class TextExamples
{
    const string Long = "符文（rune）是编译期的标记：组件里写 count++，编译后变成对响应式引用的读写；模板里直接用 count，不需要 .value，也不会丢失响应性。";

    public static Example[] All =>
    [
        new("Text", "scale", "字号与字重", "TextSize 对应主题的字号档位，Weight 对应字重；不写时跟随字体，SmallControl 默认 xs，StrongControl 默认 semibold。GDI 的字体只有常规与粗体。", Scale),
        new("Text", "tones", "色调", "Tone 取主题的文字色，与 Button、Badge 的色调同名：neutral 是正文色，muted 用于次要说明，其余是品牌色与提示色的文字色。", Tones),
        new("Text", "numeric", "编号与数字", "Mono 用等宽字体（编号、件号）；界面字体的数字本来就等宽，Numeric 只记下意图。", Numeric),
        new("Text", "overflow", "截断与换行", "Truncate 单行省略，LineClamp 限制行数（都要给宽度：MaximumSize 或关掉 AutoSize）；Wrap 的 Pretty、Balance 在桌面上同普通换行。", Overflow),
    ];

    static Control Scale() => Wrap(32,
        Column(8, [.. TextSize.Known.Select(size => Row(12, Name($"size={size.Value}"), new TextControl { Text = "界面里的文字 Typography", TextSize = size, Anchor = AnchorStyles.Left }))]),
        Column(8, [.. TextWeight.Known.Select(weight => Row(12, Name(weight.Value), new TextControl { Text = "季度营收 ¥1,280,000", Weight = weight, TextSize = TextSize.Lg, Anchor = AnchorStyles.Left }))]));

    /// <summary>左边一列的名字：xs、muted、宽 5em。</summary>
    static TextControl Name(string text) => new()
    {
        Text = text, TextSize = TextSize.Xs, Tone = TextTone.Muted, AutoSize = false, Anchor = AnchorStyles.Left,
        Size = new Size(Dip(70), Dip(18)),
    };

    static Control Tones() => Wrap(12,
        Card(new StrongControl { Text = "部署完成" }, Small("production · 3 分钟前 · 由 @lin 触发", TextTone.Muted), Small("全部 128 项检查通过", TextTone.Success)),
        Card(new StrongControl { Text = "账单即将到期" }, Small("还有 2 天自动续费 ¥299", TextTone.Warning), Small("查看账单详情", TextTone.Accent)),
        Card(new StrongControl { Text = "同步失败" }, Small("网络连接中断，已暂停上传", TextTone.Danger),
            new ParagraphControl { Text = "恢复联网后会自动重试，不用重新选择文件。", TextSize = TextSize.Sm, Tone = TextTone.Info, MaximumSize = new Size(Dip(208), 0) }));

    static TextControl Small(string text, TextTone tone) => new() { Text = text, TextSize = TextSize.Sm, Tone = tone };

    static Control Numeric()
    {
        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, MinimumSize = new Size(Dip(320), 0) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach (var (code, amount) in new[] { ("PO-2026-0276", "1,117.48"), ("PO-2026-0233", "9.10"), ("PO-2026-02114", "211.06") })
        {
            table.Controls.Add(new TextControl { Text = code, Mono = true, TextSize = TextSize.Sm, Anchor = AnchorStyles.Left });
            table.Controls.Add(new TextControl { Text = $"{amount} 万元", Numeric = true, Anchor = AnchorStyles.Right });
        }
        return Column(4, table, new TextControl { Text = "超时 11 天", Numeric = true, Tone = TextTone.Danger, Weight = TextWeight.Semibold });
    }

    static Control Overflow() => Wrap(12,
        Card(new SmallControl { Text = "Truncate：单行省略", Tone = TextTone.Muted },
            new StrongControl { Text = "vue-tsx-pro / packages / ui / src / display / VirtualList.tsx", Truncate = true, MaximumSize = new Size(Dip(208), 0) },
            new TextControl { Text = Long, Truncate = true, Tone = TextTone.Muted, MaximumSize = new Size(Dip(208), 0) }),
        Card(new SmallControl { Text = "LineClamp = 2：最多两行", Tone = TextTone.Muted },
            new ParagraphControl { Text = Long, LineClamp = 2, MaximumSize = new Size(Dip(208), 0) }),
        Card(new SmallControl { Text = "Wrap = Balance：桌面上同普通换行", Tone = TextTone.Muted },
            new ParagraphControl { Text = "用更自然的 TSX 写 Vue 组件，符文让状态读写像普通变量", Wrap = TextWrap.Balance, TextSize = TextSize.Lg, Weight = TextWeight.Semibold, Align = TextAlign.Center, AutoSize = false, Size = new Size(Dip(208), Dip(72)) }));

    /// <summary>一张卡片（Card 做出来后换掉）。</summary>
    static Control Card(params Control[] children)
    {
        var card = Column(4, children);
        card.Padding = new Padding(Dip(16));
        card.MinimumSize = new Size(Dip(240), 0);
        card.BackColor = RuneTheme.Current.Peek().Colors.Surface;
        card.BorderStyle = BorderStyle.FixedSingle;
        return card;
    }
}
