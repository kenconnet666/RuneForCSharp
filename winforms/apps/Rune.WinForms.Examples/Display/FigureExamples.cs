namespace Rune.WinForms.Examples;

/// <summary>
/// Figure 页的示例（vue-tsx-pro 文档站 display/Figure）。上游的插图是 Image，这里还没有 Image 组件，用迷你图代替；
/// embed、video 要等 Embed、Video。
/// </summary>
static class FigureExamples
{
    public static Example[] All =>
    [
        new("Figure", "captions", "图片与表格的说明", "FigureControl 的 Caption 是内容的说明：读屏把说明当作这块内容的名称。图片、图表的说明在下面；表格的说明习惯放上面（CaptionSide=top）。FigureAlign 让内容与说明一起对齐。", Captions),
        new("Figure", "ratio", "固定宽高比", "AspectRatioControl 的宽度跟着容器，高度按比例算，里面的子控件铺满它。封面、地图在加载前就占好位置，加载完不把下面的内容顶开。Ratio 是宽 ÷ 高（默认 16 / 9）。", Ratio),
    ];

    static readonly decimal?[] Purchases = [86, 92, 78, 105, 118, 96, 124, 131, 112, 140, 152, 147];

    static readonly (string Supplier, int Days)[] Leads = [("华辰精密", 12), ("远山钣金", 18), ("北辰电子", 9)];

    static FigureControl Figure(FigureControl figure, Control content)
    {
        figure.Controls.Add(content);
        return figure;
    }

    static Control Captions()
    {
        var head = new TableRowControl { Controls = { new TableHeaderCellControl { Text = "供应商" }, new TableHeaderCellControl { Align = TableAlign.End, Text = "平均交付（天）" } } };
        var body = new TableBodyControl();
        foreach (var (supplier, days) in Leads)
            body.Controls.Add(new TableRowControl { Controls = { new TableCellControl { Text = supplier }, new TableCellControl { Align = TableAlign.End, Text = days.ToString() } } });
        var table = new TableControl { AccessibleName = "交付周期", Controls = { new TableHeadControl { Controls = { head } }, body } };
        // 迷你图默认 2em 高：放进定比例的框里，高度跟着插图的宽度
        var plot = new AspectRatioControl { Ratio = 10d / 3 };
        plot.Controls.Add(new SparklineControl { Data = Purchases, Type = SparklineType.Area, Label = "近 12 个月的采购额" });
        var chart = Figure(new FigureControl { Caption = "图 1：近 12 个月的采购额（万元）" }, plot);
        chart.MaximumSize = new Size(chart.LogicalToDeviceUnits(320), 0);
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xl, Align = FlexAlign.Stretch },
            chart,
            Figure(new FigureControl { Caption = "表 1：各供应商的平均交付周期", CaptionSide = FigureCaptionSide.Top }, table),
            Figure(new FigureControl { Caption = "图 2：居中的插图，说明跟着居中", FigureAlign = FigureAlign.Center },
                new IconControl { Data = Lucide.ChartColumn, Tone = TextTone.Muted, IconSize = TextSize.Xxxl }));
    }

    static Control Ratio()
    {
        Control Case(string label, double ratio)
        {
            var box = new AspectRatioControl { Ratio = ratio, AccessibleName = label };
            var fill = new CardControl { Variant = CardVariant.Outline };
            box.Controls.Add(Build.Card(fill, new TextControl { Text = "任意内容", Tone = TextTone.Muted, Align = TextAlign.Center }));
            return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch }, box, new SmallControl { Tone = TextTone.Muted, Text = $"Ratio：{label}" });
        }
        return Build.Grid(new GridControl { MinItemWidth = "10rem", Gap = Space.Lg }, Case("16 / 9（默认）", 16d / 9), Case("4 / 3", 4d / 3), Case("1", 1));
    }
}
