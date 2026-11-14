using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>
/// Figure 页的示例（vue-tsx-pro 文档站 display/Figure）。上游的插图是 Image，这里还没有 Image 组件，用迷你图代替；
/// embed、video 要等 Embed、Video。
/// </summary>
static class FigureExamples
{
    public static Example[] All =>
    [
        new("Figure", "captions", "图片与表格的说明", "Figure 的 Caption 是内容的说明：读屏把说明当作这块内容的名称。图片、图表的说明在下面；表格的说明习惯放上面（CaptionSide=top）。Align 让内容与说明一起对齐。", Captions),
        new("Figure", "ratio", "固定宽高比", "AspectRatio 的宽度跟着容器，高度按比例算，里面的内容铺满它（Image 按 UniformToFill 裁切）。封面、地图、播放器在加载前就占好位置，加载完不把下面的内容顶开。Ratio 是宽 ÷ 高（默认 16 / 9）。", Ratio),
    ];

    static readonly decimal?[] Purchases = [86, 92, 78, 105, 118, 96, 124, 131, 112, 140, 152, 147];

    static readonly (string Supplier, int Days)[] Leads = [("华辰精密", 12), ("远山钣金", 18), ("北辰电子", 9)];

    static FrameworkElement Captions()
    {
        var head = new TableRow { Cells = { new TableHeaderCell { Content = "供应商" }, new TableHeaderCell { Align = TableAlign.End, Content = "平均交付（天）" } } };
        var body = new TableBody();
        foreach (var (supplier, days) in Leads) body.Rows.Add(new TableRow { Cells = { new TableCell { Content = supplier }, new TableCell { Align = TableAlign.End, Content = days.ToString() } } });
        var table = new Table { Sections = { new TableHead { Rows = { head } }, body } };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Xl, VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                new Figure { Caption = "图 1：近 12 个月的采购额（万元）", Width = 320, HorizontalAlignment = HorizontalAlignment.Left, Content = new Sparkline { Data = Purchases, Type = SparklineType.Area, Height = 96, Label = "近 12 个月的采购额" } },
                new Figure { Caption = "表 1：各供应商的平均交付周期", CaptionSide = FigureCaptionSide.Top, Content = table },
                new Figure { Caption = "图 2：居中的插图，说明跟着居中", Align = FigureAlign.Center, Content = new Icon { Data = Lucide.ChartColumn, FontSize = 48, Tone = TextTone.Muted } },
            },
        };
    }

    static FrameworkElement Ratio()
    {
        FrameworkElement Case(string label, double ratio)
        {
            var fill = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = new Text { Tone = TextTone.Muted, Text = "任意内容", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            fill.SetResourceReference(Border.BackgroundProperty, "Rune.Brush.Fill");
            var box = new AspectRatio { Ratio = ratio, Child = fill };
            AutomationProperties.SetName(box, label);
            return new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { box, new Small { Tone = TextTone.Muted, Text = $"Ratio：{label}" } } };
        }
        return new Grid { MinItemWidth = "10rem", Gap = Space.Lg, VerticalAlignment = VerticalAlignment.Top, Children = { Case("16 / 9（默认）", 16d / 9), Case("4 / 3", 4d / 3), Case("1", 1) } };
    }
}
