namespace Rune.WinForms.Examples;

/// <summary>
/// Figure 页的示例（vue-tsx-pro 文档站 display/Figure）。上游的插图是 Image，这里还没有 Image 组件，用迷你图代替。
/// EmbedControl、VideoControl 在可选包 Rune.WinForms.Web 里（网页内核 WebView2）。
/// </summary>
static class FigureExamples
{
    public static Example[] All =>
    [
        new("Figure", "captions", "图片与表格的说明", "FigureControl 的 Caption 是内容的说明：读屏把说明当作这块内容的名称。图片、图表的说明在下面；表格的说明习惯放上面（CaptionSide=top）。FigureAlign 让内容与说明一起对齐。", Captions),
        new("Figure", "embed", "嵌入的页面", "EmbedControl 是带外框的网页内核：Title 必填（读屏靠它知道这一块是什么），加载完之前显示骨架，加载完发 PageLoaded。示例的页面是一段内联的 HTML（data: 地址），不依赖外网；实际使用时 Source 是地图、播放器、报表的地址。", Embedded),
        new("Figure", "video", "视频", "VideoControl 用网页内核自己的控制条，外面是定比例的框（Ratio），画面完整放进去、四周黑底；Poster 是封面，Sources 给几种格式，Tracks 是字幕。Autoplay 默认静音，系统关掉动画时不自动播放并显示控制条。示例的片段是 MDN 的 CC0 样片，要联网才能播。", Clip),
        new("Figure", "player", "自绘的控制条", "VideoControl 的 ControlBar=Custom 换成跟主题走的控制条（画在页面里）：播放 / 暂停、时间与进度、静音与音量、字幕（有 Tracks 时）、倍速（Speeds）、全屏。播放中鼠标离开画面、或几秒不动就收起，暂停、移动鼠标、键盘聚焦、菜单开着时留着；点画面切换播放；焦点在播放器里时 Space / K、← →、↑ ↓、M、F。示例的片段是 MDN 的 CC0 样片（没有字幕文件），要联网才能播。", Player),
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

    const string Report = """
        <!doctype html><meta charset="utf-8">
        <style>body{margin:0;display:grid;place-items:center;height:100vh;font:14px system-ui,sans-serif;background:linear-gradient(135deg,#eef2ff,#f8fafc);color:#1f2937}strong{font-size:28px}</style>
        <div style="text-align:center"><strong>96.2%</strong><br>十月准时交付率</div>
        """;

    static Control Embedded() => Figure(new FigureControl { Caption = "嵌入的报表：外层的边框、圆角跟随主题，宽高比 16 / 9" },
        new EmbedControl { Source = new Uri("data:text/html;charset=utf-8," + Uri.EscapeDataString(Report)), Title = "十月准时交付率报表" });

    static Control Clip() => Figure(new FigureControl { Caption = "内核自己的控制条；宽高比 16 / 9，加载前就占好位置" },
        new VideoControl { Source = new Uri("https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.webm"), Loop = true, AccessibleName = "花朵绽放的片段" });

    static Control Player() => Figure(new FigureControl { Caption = "点画面播放 / 暂停；焦点在播放器里时可以用键盘操作" },
        new VideoControl { Source = new Uri("https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.webm"), ControlBar = VideoControlBar.Custom, Loop = true, AccessibleName = "花朵绽放的片段" });

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
