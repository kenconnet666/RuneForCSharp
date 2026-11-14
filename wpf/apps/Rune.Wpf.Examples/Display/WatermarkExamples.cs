using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace Rune.Wpf.Examples;

/// <summary>
/// Watermark 页的示例（vue-tsx-pro 文档站 display/Watermark）。上游的 Options 用 Slider、ColorField 调参数，这里还没有这两个组件，
/// 字号、角度、间距用 NumberField，颜色不调。
/// </summary>
static class WatermarkExamples
{
    public static Example[] All =>
    [
        new("Watermark", "report", "内部报表", "Text 用换行分成几行（查看人、日期、密级）。水印是盖在内容上的一层：不挡点击与选择，读屏不念。Fullscreen 盖住整个窗口（窗口内容的装饰层）；不包内容时只有那一层，放在页面的任何地方都行。水印删得掉，只是提示，真正的保密靠服务端（导出时打水印、权限）。", Report),
        new("Watermark", "options", "文字、图片与间距", "按平铺单元画：两行两列的格子斜对角各一份，相邻两行错开半格。Image 给了就画图片；Gap 是每格之间的空隙。默认颜色是半透明的灰，亮色、暗色主题上都看得见、不抢内容。", Options),
    ];

    static readonly (string Warehouse, string Item, string Quantity, string Amount)[] Rows =
    [
        ("华东仓", "轴承 6205", "1 280 件", "¥ 38 400"),
        ("华南仓", "密封圈 O-32", "9 600 件", "¥ 11 520"),
        ("西南仓", "液压阀 HV-08", "86 件", "¥ 51 600"),
    ];

    static void OnChanged(DependencyObject target, DependencyProperty property, Action changed) =>
        DependencyPropertyDescriptor.FromProperty(property, target.GetType()).AddValueChanged(target, (_, _) => changed());

    static FrameworkElement Report()
    {
        var fullscreen = new Switch { Content = "整页水印" };
        var rows = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs };
        foreach (var (warehouse, item, quantity, amount) in Rows)
        {
            var name = new Text { Size = TextSize.Sm, Text = item };
            Flex.SetGrow(name, true);
            rows.Children.Add(new Flex
            {
                Gap = Space.Md, Wrap = FlexWrap.Wrap,
                Children =
                {
                    new Small { Tone = TextTone.Muted, Width = 56, Text = warehouse }, name,
                    new Text { Size = TextSize.Sm, Mono = true, Text = quantity },
                    new Text { Size = TextSize.Sm, Mono = true, Width = 84, TextAlignment = TextAlignment.Right, Text = amount },
                },
            });
        }
        var stats = new Flex
        {
            Gap = Space.Lg, Wrap = FlexWrap.Wrap,
            Children =
            {
                new Stat { Label = "库存总额", Value = 101520, Format = new NumericFormat { Style = NumericStyle.Currency } },
                new Stat { Label = "周转天数", Value = 23, Unit = "天", Change = -0.12m, InvertChange = true },
                new Stat { Label = "呆滞物料", Value = 7, Unit = "项", Tone = TextTone.Warning },
            },
        };
        var card = new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children =
                {
                    new Flex
                    {
                        Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm, Wrap = FlexWrap.Wrap,
                        Children = { new H4 { Text = "10 月库存周转报表" }, new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.Download, Content = "导出" } },
                    },
                    stats, rows,
                },
            },
        };
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var watermark = new Watermark { Text = $"林小满 · 采购部\n{today}\n内部资料 请勿外传", Child = card };
        OnChanged(fullscreen, ChoiceBase.IsCheckedProperty, () => watermark.Fullscreen = fullscreen.IsChecked == true);
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { new Flex { Children = { fullscreen } }, watermark } };
    }

    /// <summary>示例自带的标志图：120 × 40 的圆角框里写 ACME，同上游的那张 SVG。</summary>
    static ImageSource Logo()
    {
        var accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366f1")) { Opacity = 0.2 };
        var text = new FormattedText("ACME", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16, accent, 1);
        var image = new DrawingImage(new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing(null, new Pen(accent, 2), new RectangleGeometry(new Rect(1, 1, 118, 38), 8, 8)),
                new GeometryDrawing(accent, null, text.BuildGeometry(new Point(60 - text.Width / 2, 26 - text.Baseline))),
            },
        });
        image.Freeze();
        return image;
    }

    static FrameworkElement Options()
    {
        var kind = new ToggleGroup { IsRequired = true, Size = TextSize.Sm, Value = "text", Children = { new ToggleItem { Value = "text", Content = "文字" }, new ToggleItem { Value = "image", Content = "图片" } } };
        AutomationProperties.SetName(kind, "水印内容");
        var text = new TextField { Value = "仅供内部评审" };
        var fontSize = new NumberField { Value = 14, Min = 10, Max = 28, Suffix = "px" };
        var rotate = new NumberField { Value = -22, Min = -90, Max = 90, Suffix = "°" };
        var gap = new NumberField { Value = 120, Min = 40, Max = 240, Step = 10, Suffix = "px" };
        var textFields = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { new Field { Label = "文字", Content = text }, new Field { Label = "字号", Content = fontSize } } };
        var logo = Logo();
        var watermark = new Watermark
        {
            Child = new Card
            {
                MinHeight = 256,
                Content = new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Sm,
                    Children =
                    {
                        new Text { Weight = TextWeight.Semibold, Text = "供应商评审纪要" },
                        new Text { Size = TextSize.Sm, Tone = TextTone.Muted, Text = "参会：采购部、质量部、生产部。结论：远山精密进入 A 类名录，海川液压整改后复审。" },
                        new Text { Size = TextSize.Sm, Tone = TextTone.Muted, Text = "下次评审：11 月第二周。整改清单见附件，责任人按清单跟进，周五前反馈进度。" },
                    },
                },
            },
        };
        void Update()
        {
            var image = kind.Value == "image";
            textFields.Visibility = image ? Visibility.Collapsed : Visibility.Visible;
            watermark.Image = image ? logo : null;
            watermark.Text = text.Value;
            watermark.FontSize = (double)(fontSize.Value ?? 14);
            watermark.Rotate = (double)(rotate.Value ?? -22);
            var spacing = (double)(gap.Value ?? 120);
            watermark.Gap = new Size(spacing, Math.Round(spacing * 0.8));
        }
        OnChanged(kind, ToggleGroup.ValueProperty, Update);
        OnChanged(text, TextField.ValueProperty, Update);
        foreach (var field in new[] { fontSize, rotate, gap }) OnChanged(field, NumberField.ValueProperty, Update);
        Update();
        var controls = new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children = { kind, textFields, new Field { Label = "角度", Content = rotate }, new Field { Label = "间距", Content = gap } },
        };
        return new Grid { MinItemWidth = "18rem", Gap = Space.Lg, Children = { controls, watermark } };
    }
}
