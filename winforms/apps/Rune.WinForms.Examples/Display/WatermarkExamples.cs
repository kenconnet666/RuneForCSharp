using System.Drawing.Drawing2D;
using System.Globalization;

namespace Rune.WinForms.Examples;

/// <summary>
/// Watermark 页的示例（vue-tsx-pro 文档站 display/Watermark）。上游的 Options 用 Slider、ColorField 调参数，这里还没有这两个组件，
/// 字号、角度、间距用 NumberFieldControl，颜色不调。水印画在窗体上的分层窗口里，inspect 的控件截图里没有它。
/// </summary>
static class WatermarkExamples
{
    public static Example[] All =>
    [
        new("Watermark", "report", "内部报表", "Text 用换行分成几行（查看人、日期、密级）。水印画在属于窗体的分层窗口里，盖住控件露出来的部分：点击穿透，不挡点击与选择，读屏里没有它。Fullscreen 盖住整个窗体；不包内容时只有那一层。水印删得掉，只是提示，真正的保密靠服务端（导出时打水印、权限）。", Report),
        new("Watermark", "options", "文字、图片与间距", "按平铺单元画：两行两列的格子斜对角各一份，相邻两行错开半格。Image 给了就画图片；Gap 是每格之间的空隙。默认颜色是半透明的灰，亮色、暗色主题上都看得见、不抢内容。", Options),
    ];

    static readonly (string Warehouse, string Item, string Quantity, string Amount)[] Rows =
    [
        ("华东仓", "轴承 6205", "1 280 件", "¥ 38 400"),
        ("华南仓", "密封圈 O-32", "9 600 件", "¥ 11 520"),
        ("西南仓", "液压阀 HV-08", "86 件", "¥ 51 600"),
    ];

    static Control Report()
    {
        var fullscreen = new SwitchControl { Text = "整页水印" };
        var rows = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch };
        foreach (var (warehouse, item, quantity, amount) in Rows)
        {
            var name = new TextControl { TextSize = TextSize.Sm, Text = item };
            // 仓库、金额两列定宽（上游的 4em、6em），按 DPI 换算
            var place = new SmallControl { Tone = TextTone.Muted, AutoSize = false, Text = warehouse };
            var sum = new TextControl { TextSize = TextSize.Sm, Mono = true, AutoSize = false, Align = TextAlign.End, Text = amount };
            place.Size = new Size(place.LogicalToDeviceUnits(56), place.GetPreferredSize(Size.Empty).Height);
            sum.Size = new Size(sum.LogicalToDeviceUnits(84), sum.GetPreferredSize(Size.Empty).Height);
            var row = Build.Flex(new FlexControl { Gap = Space.Md, Wrap = true },
                place, name, new TextControl { TextSize = TextSize.Sm, Mono = true, Text = quantity }, sum);
            row.SetGrow(name, true);
            rows.Controls.Add(row);
        }
        var stats = Build.Flex(new FlexControl { Gap = Space.Lg, Wrap = true },
            new StatControl { Label = "库存总额", Value = 101520, Format = new NumericFormat { Style = NumericStyle.Currency } },
            new StatControl { Label = "周转天数", Value = 23, Unit = "天", Change = -0.12m, InvertChange = true },
            new StatControl { Label = "呆滞物料", Value = 7, Unit = "项", Tone = TextTone.Warning });
        var header = Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm, Wrap = true },
            new H4Control { Text = "10 月库存周转报表" },
            new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.Download, Text = "导出" });
        var card = Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch }, header, stats, rows));
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var watermark = new WatermarkControl { Text = $"林小满 · 采购部\n{today}\n内部资料 请勿外传" };
        watermark.Controls.Add(card);
        fullscreen.IsCheckedChanged += (_, _) => watermark.Fullscreen = fullscreen.IsChecked;
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch }, Build.Flex(new FlexControl(), fullscreen), watermark);
    }

    /// <summary>示例自带的标志图：120 × 40 的圆角框里写 ACME，同上游的那张 SVG（透明度 0.2）。</summary>
    static Bitmap Logo()
    {
        var logo = new Bitmap(240, 80);
        using var graphics = Graphics.FromImage(logo);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(2, 2);
        var accent = Color.FromArgb(51, 0x63, 0x66, 0xf1);
        using var frame = new GraphicsPath();
        frame.AddArc(1, 1, 16, 16, 180, 90);
        frame.AddArc(103, 1, 16, 16, 270, 90);
        frame.AddArc(103, 23, 16, 16, 0, 90);
        frame.AddArc(1, 23, 16, 16, 90, 90);
        frame.CloseFigure();
        using var pen = new Pen(accent, 2);
        graphics.DrawPath(pen, frame);
        using var font = new Font("Segoe UI", 16, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(accent);
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString("ACME", font, brush, new RectangleF(0, 0, 120, 40), centered);
        return logo;
    }

    static Control Options()
    {
        var kind = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm, AccessibleName = "水印内容" };
        kind.Controls.Add(new ToggleItemControl { Value = "text", Text = "文字" });
        kind.Controls.Add(new ToggleItemControl { Value = "image", Text = "图片" });
        kind.Value = "text";
        var text = new TextFieldControl { Text = "仅供内部评审" };
        var fontSize = new NumberFieldControl { Value = 14, Min = 10, Max = 28, Suffix = "px" };
        var rotate = new NumberFieldControl { Value = -22, Min = -90, Max = 90, Suffix = "°" };
        var gap = new NumberFieldControl { Value = 120, Min = 40, Max = 240, Step = 10, Suffix = "px" };
        var textFields = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch },
            new FieldControl { Label = "文字", Content = text }, new FieldControl { Label = "字号", Content = fontSize });
        var logo = Logo();
        var note = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch },
            new TextControl { Weight = TextWeight.Semibold, Text = "供应商评审纪要" },
            new TextControl { TextSize = TextSize.Sm, Tone = TextTone.Muted, Text = "参会：采购部、质量部、生产部。结论：远山精密进入 A 类名录，海川液压整改后复审。" },
            new TextControl { TextSize = TextSize.Sm, Tone = TextTone.Muted, Text = "下次评审：11 月第二周。整改清单见附件，责任人按清单跟进，周五前反馈进度。" });
        var card = Build.Card(new CardControl(), note);
        card.MinimumSize = new Size(0, card.LogicalToDeviceUnits(256));
        var watermark = new WatermarkControl();
        watermark.Controls.Add(card);
        void Update()
        {
            var image = kind.Value == "image";
            textFields.Visible = !image;
            watermark.Image = image ? logo : null;
            watermark.Text = text.Text;
            watermark.FontSize = (float)(fontSize.Value ?? 14);
            watermark.Rotate = (float)(rotate.Value ?? -22);
            var spacing = (int)(gap.Value ?? 120);
            watermark.Gap = new Size(spacing, (int)Math.Round(spacing * 0.8));
        }
        kind.ValueChanged += (_, _) => Update();
        text.ValueChanged += (_, _) => Update();
        foreach (var field in new[] { fontSize, rotate, gap }) field.ValueChanged += (_, _) => Update();
        Update();
        var controls = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch },
            Build.Flex(new FlexControl(), kind), textFields, new FieldControl { Label = "角度", Content = rotate }, new FieldControl { Label = "间距", Content = gap });
        return Build.Grid(new GridControl { MinItemWidth = "18rem", Gap = Space.Lg }, controls, watermark);
    }
}
