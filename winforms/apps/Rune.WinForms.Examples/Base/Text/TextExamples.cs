namespace Rune.WinForms.Examples;

/// <summary>
/// Text 的示例（vue-tsx-pro 文档站 base/Text）。
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
        new("Text", "inline", "行内元素", "Kbd 写按键，Code 写行内代码，Mark 标出要强调的词；Sub、Sup 是上下标；Strike 是不再有效的内容（原价），文档的增删用 Del、Ins（加进来的是浅绿底，不用下划线）。Abbr 是缩写：全称在提示里，悬停、键盘聚焦、点按都看得到。整段引用用 BlockquoteControl（Source 写出处）。WinForms 的文字是整段画的，Code、Mark、Sub、Sup、Del、Ins、Strike 是单独的控件，不跟着一段文字换行。", Inline),
        new("Text", "headings", "标题", "H1–H6 各是一个控件，默认字号按级别递减，TextSize 能单独改：视觉层级与文档结构分开。GDI 画不出收紧的字间距。", Headings),
        new("Text", "links", "链接", "LinkControl 默认带下划线：正文里的链接不能只靠颜色区分。External 加外链图标并告诉读屏在新窗口打开；Underline 的 hover、none 用在一眼看得出是链接的地方。WinForms 的链接是单独的控件，不跟着一段文字换行。", Links),
        new("Text", "overflow", "截断与换行", "Truncate 单行省略，LineClamp 限制行数（都要有宽度：放在 Flex、Grid、Card 里由它们给，或设 MaximumSize）；Wrap 的 Pretty、Balance 在桌面上同普通换行。", Overflow),
    ];

    static Control Scale() => Build.Grid(new GridControl { MinItemWidth = "18em", Gap = Space.Xl },
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm },
            [.. Build.Steps.Select(size => Build.Flex(new FlexControl { Align = FlexAlign.Baseline, Gap = Space.Md },
                Name($"size={size.Value}"), new TextControl { Text = "界面里的文字 Typography", TextSize = size }))]),
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm },
            [.. TextWeight.Known.Select(weight => Build.Flex(new FlexControl { Align = FlexAlign.Baseline, Gap = Space.Md },
                Name(weight.Value), new TextControl { Text = "季度营收 ¥1,280,000", Weight = weight, TextSize = TextSize.Lg }))]));

    /// <summary>左边一列的名字：xs、muted、宽 5em（TS 的 width: 5em），右边的样例对齐。</summary>
    static TextControl Name(string text)
    {
        var name = new TextControl { Text = text, TextSize = TextSize.Xs, Tone = TextTone.Muted };
        var height = name.GetPreferredSize(Size.Empty).Height;
        name.AutoSize = false;
        name.Size = new Size(name.LogicalToDeviceUnits((int)MathF.Round(5 * Tokens.FontSize.Xs)), height);
        return name;
    }

    static Control Tones() => Build.Grid(new GridControl { MinItemWidth = "16em", Gap = Space.Md },
        Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
            new StrongControl { Text = "部署完成" },
            new TextControl { Text = "production · 3 分钟前 · 由 @lin 触发", TextSize = TextSize.Sm, Tone = TextTone.Muted },
            new TextControl { Text = "全部 128 项检查通过", TextSize = TextSize.Sm, Tone = TextTone.Success })),
        Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
            new StrongControl { Text = "账单即将到期" },
            new TextControl { Text = "还有 2 天自动续费 ¥299", TextSize = TextSize.Sm, Tone = TextTone.Warning },
            new TextControl { Text = "查看账单详情", TextSize = TextSize.Sm, Tone = TextTone.Accent })),
        Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
            new StrongControl { Text = "同步失败" },
            new TextControl { Text = "网络连接中断，已暂停上传", TextSize = TextSize.Sm, Tone = TextTone.Danger },
            new ParagraphControl { Text = "恢复联网后会自动重试，不用重新选择文件。", TextSize = TextSize.Sm, Tone = TextTone.Info })));

    static Control Numeric() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
    [
        .. new[] { ("PO-2026-0276", "1,117.48"), ("PO-2026-0233", "9.10"), ("PO-2026-02114", "211.06") }.Select(row => Build.Flex(new FlexControl { Justify = FlexJustify.Between, Gap = Space.Xl },
            new TextControl { Text = row.Item1, Mono = true, TextSize = TextSize.Sm },
            new TextControl { Text = $"{row.Item2} 万元", Numeric = true })),
        new TextControl { Text = "超时 11 天", Numeric = true, Tone = TextTone.Danger, Weight = TextWeight.Semibold },
    ]);

    static Control Overflow() => Build.Grid(new GridControl { MinItemWidth = "18em", Gap = Space.Md, Balanced = true },
        Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
            new SmallControl { Text = "Truncate：单行省略", Tone = TextTone.Muted },
            new StrongControl { Text = "vue-tsx-pro / packages / ui / src / display / VirtualList.tsx", Truncate = true },
            new TextControl { Text = Long, Truncate = true, Tone = TextTone.Muted })),
        Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
            new SmallControl { Text = "LineClamp = 2：最多两行", Tone = TextTone.Muted },
            new ParagraphControl { Text = Long, LineClamp = 2 })),
        Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
            new SmallControl { Text = "Wrap = Pretty：桌面上同普通换行", Tone = TextTone.Muted },
            new ParagraphControl { Text = "验收完成后 10 个工作日内付款；逾期按日万分之五计收违约金，由采购部统一结算。" })),
        Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
            new SmallControl { Text = "Wrap = Balance：桌面上同普通换行", Tone = TextTone.Muted },
            new ParagraphControl { Text = "用更自然的 TSX 写 Vue 组件，符文让状态读写像普通变量", Wrap = TextWrap.Balance, TextSize = TextSize.Lg, Weight = TextWeight.Semibold, Align = TextAlign.Center })));

    /// <summary>一段话一段话拼出来：文字、按键、代码、标记各是一个控件，排成一行（放不下时换行）。</summary>
    static Control Inline() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Wrap = true },
            new TextControl { Text = "在任意页面按 " }, new KbdControl { Text = "Ctrl" }, new TextControl { Text = " + " }, new KbdControl { Text = "K" },
            new TextControl { Text = " 打开搜索，输入 " }, new CodeControl { Text = "button" }, new TextControl { Text = " 就能找到" },
            new MarkControl { Text = "Button 按钮" }, new TextControl { Text = "与它的全部示例。" },
            new StrongControl { Text = "Strong" }, new TextControl { Text = " 表示重要，" }, new EmControl { Text = "Em" }, new TextControl { Text = " 表示语气上的强调，" },
            new SmallControl { Text = "Small 用于附注与版权信息" }, new TextControl { Text = "。" }),
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Wrap = true },
            new TextControl { Text = "仓库面积 860 m" }, new SupControl { Text = "2" }, new TextControl { Text = "，冷却液的主要成分是 H" }, new SubControl { Text = "2" },
            new TextControl { Text = "O；" }, new AbbrControl { Text = "SLA", Title = "服务等级协议" }, new TextControl { Text = " 承诺可用率 99.9%。会员价 " }, new StrikeControl { Text = "¥199", Tone = TextTone.Muted }, new TextControl { Text = " ¥149。交期从 " },
            new DelControl { Text = "10 月 20 日" }, new TextControl { Text = " 改到 " }, new InsControl { Text = "11 月 5 日" }, new TextControl { Text = "。" }),
        Quote("Vue 文档", new ParagraphControl { Text = "渐进式框架：可以只在页面的一小块里用它，也可以用它搭建完整的单页应用。" }));

    /// <summary>引用：竖线在起始边，里面的内容竖着排。</summary>
    static BlockquoteControl Quote(string? source, params Control[] content)
    {
        var quote = new BlockquoteControl { Dock = DockStyle.Top, Source = source };
        quote.Controls.Add(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Dock = DockStyle.Top }, content));
        return quote;
    }

    static Control Headings() => Build.Grid(new GridControl { MinItemWidth = "20em", Gap = Space.Xl },
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm },
            new H1Control { Text = "H1 页面标题" }, new H2Control { Text = "H2 一节" }, new H3Control { Text = "H3 小节" },
            new H4Control { Text = "H4 段落标题" }, new H5Control { Text = "H5 卡片标题" }, new H6Control { Text = "H6 最小的标题" }),
        // 文档结构与视觉层级分开：级别要连续，字号用 TextSize 改
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm },
            new H2Control { Text = "账户设置", TextSize = TextSize.Lg },
            new ParagraphControl { Text = "H2 但用 lg 字号：放在卡片里不至于太大。", Tone = TextTone.Muted },
            new H3Control { Text = "危险操作", Tone = TextTone.Danger },
            new ParagraphControl { Text = "注销账户后数据不可恢复。", Tone = TextTone.Muted }));

    static Control Links() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Wrap = true },
            new TextControl { Text = "正文里的链接默认带下划线：" }, new LinkControl { Text = "按钮", Href = new Uri("https://example.com/components/button") },
            new TextControl { Text = "、" }, new LinkControl { Text = "主题定制", Href = new Uri("https://example.com/guide/theming") },
            new TextControl { Text = "，不只靠颜色和周围的文字区分。去别的站点写 " }, new LinkControl { Text = "Vue 文档", Href = new Uri("https://vuejs.org"), External = true },
            new TextControl { Text = "：带图标，读屏会念出“在新窗口打开”。" }),
        Build.Flex(new FlexControl { Gap = Space.Lg, Wrap = true },
            Build.Styled(new LinkControl { Text = "悬停时才有下划线", Href = new Uri("https://example.com/components/text"), Underline = LinkUnderline.Hover }, TextSize.Sm),
            Build.Styled(new LinkControl { Text = "导航里的次要链接", Href = new Uri("https://example.com/components/text"), Underline = LinkUnderline.None, Tone = TextTone.Muted }, TextSize.Sm),
            Build.Styled(new LinkControl { Text = "注销账户", Href = new Uri("https://example.com/account"), Tone = TextTone.Danger }, TextSize.Sm)));
}
