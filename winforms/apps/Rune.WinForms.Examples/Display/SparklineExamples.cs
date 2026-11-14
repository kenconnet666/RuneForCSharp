namespace Rune.WinForms.Examples;

/// <summary>Sparkline 页的示例（vue-tsx-pro 文档站 charts/Sparkline）。in-table 要等 ColumnTable，Gauge 的示例等 Gauge。</summary>
static class SparklineExamples
{
    public static Example[] All =>
    [
        new("Sparkline", "cards", "迷你图：指标卡里", "放进 StatControl 的 Content：占满数值下面的一行。Type 是 Line（默认）、Area（下面填一层浅色）或 Bar（每个值一根柱，从 0 起），Tone 是色调。默认标出最后一个点（ShowLast）。读屏念成一句话：几个值、从哪到哪、最低最高，按 ValueFormat 格式化，Label 写在前面。", Cards),
        new("Sparkline", "proportion-bar", "比例条：一个总数的构成", "Segments 互斥、加起来是总数；颜色用 Tone（状态色）或 Color（任意颜色）。横条对读屏是一张图，名称后面自动带上各段的数量；图例是画的文字，ValueText 改数量的写法，LegendExtra 在图例项后面补一句（次要色）。数量为 0 的段不画但留在图例里。", Proportions),
    ];

    static readonly decimal?[] Savings = [40, 70, 150, 205, 180, 150, 185, 150, 190, 205, 270, 360];
    static readonly decimal?[] Visits = [820, 932, 901, 934, 1290, 1330, 1320, 1180, 1420, 1510, 1490, 1680];
    static readonly decimal?[] Overdue = [6, 4, 7, 3, 2, 5, 1, 0, 2, 1, 3, 1];

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
