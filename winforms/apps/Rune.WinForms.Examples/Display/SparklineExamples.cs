namespace Rune.WinForms.Examples;

/// <summary>Sparkline 页的示例（vue-tsx-pro 文档站 charts/Sparkline）。in-table 要等 ColumnTable。</summary>
static class SparklineExamples
{
    public static Example[] All =>
    [
        new("Sparkline", "cards", "迷你图：指标卡里", "放进 StatControl 的 Content：占满数值下面的一行。Type 是 Line（默认）、Area（下面填一层浅色）或 Bar（每个值一根柱，从 0 起），Tone 是色调。默认标出最后一个点（ShowLast）。读屏念成一句话：几个值、从哪到哪、最低最高，按 ValueFormat 格式化，Label 写在前面。", Cards),
        new("Sparkline", "gauge", "仪表盘：一个量落在哪个区间", "Value 在 Min–Max（默认 0–100）之间；Bands 是外圈的区间（每段到 To 为止，带色调），数值弧取它所在区间的色调（Tone 可以指定）；Target 在外圈上画一道刻度。ValueFormat、Unit 决定中间与读屏的数值写法，读屏是进度条（只读）。和 Progress 的分工：Progress 是做到哪了，Gauge 是一个量处在好坏的哪一段。", Gauges),
        new("Sparkline", "gauge-sizes", "仪表盘：大小与中间的文字", "宽 10em，随字号缩放：TextSize 档位或外层的字体。ValueLabel 换掉中间的文字；读屏仍念格式化的数值与目标。", GaugeSizes),
        new("Sparkline", "proportion-bar", "比例条：一个总数的构成", "Segments 互斥、加起来是总数；颜色用 Tone（状态色）或 Color（任意颜色）。横条对读屏是一张图，名称后面自动带上各段的数量；图例是画的文字，ValueText 改数量的写法，LegendExtra 在图例项后面补一句（次要色）。数量为 0 的段不画但留在图例里。", Proportions),
    ];

    static readonly decimal?[] Savings = [40, 70, 150, 205, 180, 150, 185, 150, 190, 205, 270, 360];
    static readonly decimal?[] Visits = [820, 932, 901, 934, 1290, 1330, 1320, 1180, 1420, 1510, 1490, 1680];
    static readonly decimal?[] Overdue = [6, 4, 7, 3, 2, 5, 1, 0, 2, 1, 3, 1];

    // 仪表盘在卡片里居中
    static Control Centered(GaugeControl gauge) => Build.Card(new CardControl(), Build.Flex(new FlexControl { Justify = FlexJustify.Center }, gauge));

    // 工作台的一排仪表盘：完成率（分三段，目标 90%）、产能利用率（越过 85% 就是提醒）、供应商风险分（越低越好，区间倒过来）
    static Control Gauges()
    {
        var percent = new NumericFormat { Style = NumericStyle.Percent };
        return Build.Grid(new GridControl { MinItemWidth = "12em", Balanced = true, Gap = Space.Md },
            Centered(new GaugeControl { Value = 0.82m, Max = 1, ValueFormat = percent, Label = "年度销售完成率", Target = 0.9m, Bands = [new(0.6, StatusTone.Danger), new(0.9, StatusTone.Warning), new(1, StatusTone.Success)] }),
            Centered(new GaugeControl { Value = 0.91m, Max = 1, ValueFormat = percent, Label = "产线利用率", Bands = [new(0.7, StatusTone.Info), new(0.85, StatusTone.Success), new(1, StatusTone.Warning)] }),
            Centered(new GaugeControl { Value = 38, Label = "供应商风险分", Bands = [new(40, StatusTone.Success), new(70, StatusTone.Warning), new(100, StatusTone.Danger)] }),
            Centered(new GaugeControl { Value = 2.4m, Max = 5, ValueFormat = new NumericFormat { MaximumFractionDigits = 1 }, Unit = "天", Label = "平均处理时长", Tone = StatusTone.Accent }));
    }

    static readonly GaugeBand[] Grades = [new(60, StatusTone.Danger), new(80, StatusTone.Warning), new(100, StatusTone.Success)];

    // 大小随字号：TextSize 档位或外层的字体；中间的文字可以用 ValueLabel 换成自己的（“82 分”）
    static Control GaugeSizes() => Build.Flex(new FlexControl { Gap = Space.Lg, Align = FlexAlign.End, Wrap = true },
        new GaugeControl { Value = 82, Bands = Grades, TextSize = TextSize.Xs, Label = "xs" },
        new GaugeControl { Value = 82, Bands = Grades, TextSize = TextSize.Sm, Label = "sm" },
        new GaugeControl { Value = 82, Bands = Grades, Label = "默认" },
        new GaugeControl { Value = 82, Bands = Grades, TextSize = TextSize.Lg, Label = "质量评分", ValueLabel = "82 分" });

    // 工单的三种状态：互斥、加起来是总数；图例后面可以补一句（“等外部回复”）
    static Control Proportions() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch },
        new ProportionBarControl
        {
            Label = "工单构成",
            Segments = [new("done", "已完成", 25, StatusTone.Success), new("blocked", "受阻", 4, StatusTone.Warning), new("open", "进行中", 35, StatusTone.Info)],
            ValueText = segment => $"{segment.Value} 项",
            LegendExtra = segment => segment.Key == "blocked" ? "等外部回复" : null,
        },
        new ProportionBarControl
        {
            Label = "存储空间", BarSize = ProportionBarSize.Sm,
            Segments = [new("docs", "文档", 42, Color: ColorTranslator.FromHtml("#3e63dd")), new("images", "图片", 28, Color: ColorTranslator.FromHtml("#12a594")), new("free", "剩余", 30, StatusTone.Neutral)],
            ValueText = segment => $"{segment.Value} GB",
        });

    // 指标卡里的迷你图：StatControl 的 Content 占满数值下面的一行；折线看走势、面积看总量的起伏、柱看每月的多少
    static Control Cards() => Build.Grid(new GridControl { MinItemWidth = "14em", Balanced = true, Gap = Space.Md },
        Build.Card(new CardControl(), new StatControl { Label = "实际销售", Value = 360, Unit = "万元", Change = 0.333m, ChangeLabel = "较上月", Content = new SparklineControl { Data = Savings, Label = "近 12 个月实际销售", ValueFormat = new NumericFormat { Suffix = "万元" } } }),
        Build.Card(new CardControl(), new StatControl { Label = "门户访问", Value = 1680, Unit = "次", Change = 0.127m, ChangeLabel = "较上月", Content = new SparklineControl { Data = Visits, Type = SparklineType.Area, Tone = StatusTone.Info, Label = "近 12 个月门户访问" } }),
        Build.Card(new CardControl(), new StatControl { Label = "超时任务", Value = 1, Unit = "项", Tone = TextTone.Danger, Change = -0.667m, InvertChange = true, Content = new SparklineControl { Data = Overdue, Type = SparklineType.Bar, Tone = StatusTone.Danger, Label = "近 12 个月超时任务" } }));
}
