using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace Rune.Avalonia.Examples;

/// <summary>ScrollArea 的示例（vue-tsx-pro 文档站 layout/ScrollArea）。both（宽表格，表头与行号固定）要等 Table。</summary>
static class ScrollAreaExamples
{
    public static Example[] All =>
    [
        new("ScrollArea", "vertical", "纵向滚动", "高度（或最大高度）由使用方给。区域可以用 Tab 聚焦、用方向键滚动，名称写在 AutomationProperties.Name 上；滚动条对读屏隐藏。", Inbox),
        new("ScrollArea", "horizontal", "横向滚动", "Axis=\"x\" 横向滚动，纵向超出的裁掉。滚动条浮在内容上，底部留出一点空，免得盖住卡片。", Playlists),
        new("ScrollArea", "both", "两个方向", "宽表格、电子表格：Table 自己就有滚动区域，ScrollAxis=\"Both\" 并限高；表头吸顶（StickyHeader），行号固定在起始边（TableCell.Sticky）。", Sheet),
        new("ScrollArea", "types", "什么时候显示滚动条", "Type 三种显示时机；淡出前的停留时间是 HideDelay。", Types),
    ];

    static readonly string[] Senders = ["林小满", "周一航", "陈默", "许悠", "王晴", "赵浩然", "孙思远", "李雨桐"];

    static readonly string[] Subjects =
    [
        "首页改版的设计稿更新了，麻烦看一下", "下周一的评审会改到下午三点", "发票已经寄出，单号见附件",
        "接口文档补好了，有问题随时找我", "团建报名今天截止", "服务器续费提醒",
    ];

    static Control Inbox()
    {
        var list = new FlexList { Direction = FlexDirection.Column };
        for (var index = 0; index < 24; index++)
        {
            var sender = Senders[index % Senders.Length];
            var time = $"{18 - index / 3:00}:{index * 17 % 60:00}";
            var body = new Flex
            {
                Direction = FlexDirection.Column,
                Children =
                {
                    new Flex { Justify = FlexJustify.Between, Gap = Space.Sm, Children = { new Strong { Text = sender }, new Small { Text = time, Tone = TextTone.Muted } } },
                    new Text { Text = Subjects[index % Subjects.Length], Size = TextSize.Sm, Tone = TextTone.Muted, Truncate = true },
                },
            };
            Flex.SetGrow(body, true);
            // 每一项上下 space.sm、左右 space.md（画布字号 14px）
            list.Children.Add(new Border
            {
                Padding = new Thickness(10.5, 7),
                Child = new Flex { Gap = Space.Md, Align = FlexAlign.Center, Children = { new Avatar { Size = TextSize.Sm, DisplayName = sender }, body } },
            });
        }
        var area = new ScrollArea { Height = 320, Content = list };
        AutomationProperties.SetName(area, "收件箱");
        return new Card { Padding = Space.None, Content = area };
    }

    static readonly (string Name, int Count, double Hue)[] Shelf =
    [
        ("通勤路上", 32, 210), ("深夜写代码", 48, 265), ("周末早午餐", 21, 30), ("跑步节拍", 40, 0),
        ("雨天爵士", 27, 230), ("专注白噪音", 12, 160), ("九十年代华语", 55, 340), ("电影原声", 36, 290),
    ];

    static Control Playlists()
    {
        // 底部留 space.md：滚动条浮在内容上，不盖住卡片
        var row = new FlexList { Gap = Space.Md, Margin = new Thickness(0, 0, 0, 10.5) };
        foreach (var (name, count, hue) in Shelf)
        {
            // 封面：歌单的色相做 135° 的渐变（同上游的 oklch(70% 0.14 h) → oklch(45% 0.12 h+40)），宽 9rem 的卡片里正方形
            var cover = new Border
            {
                Height = 144 - 2 * 7 - 2, Margin = new Thickness(0, 0, 0, 7), CornerRadius = new CornerRadius(0.5 * 14),
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Oklch(0.70, 0.14, hue), 0), new GradientStop(Oklch(0.45, 0.12, hue + 40), 1) },
                },
            };
            row.Children.Add(new Card
            {
                Width = 144, Padding = Space.Sm,
                Content = new StackPanel { Children = { cover, new Strong { Text = name }, new Small { Text = $"{count} 首", Tone = TextTone.Muted } } },
            });
        }
        var area = new ScrollArea { Axis = ScrollAreaAxis.X, Type = ScrollbarType.Always, Content = row };
        AutomationProperties.SetName(area, "推荐歌单");
        return area;
    }

    static readonly (ScrollbarType Type, string Description)[] Kinds =
    [
        (ScrollbarType.Hover, "指针在上面或正在滚动时显示（默认）"),
        (ScrollbarType.Scroll, "只在滚动时显示，停下后淡出"),
        (ScrollbarType.Always, "内容超出时一直显示"),
    ];

    // 同上游的文字（"浏览器原生"换成"平台原生"）：文字的长度决定滑块的大小
    const string Words = "滚动仍是平台原生的：触控板惯性、键盘、页内搜索、读屏都不变。只是把原生滚动条藏起来，换成半透明、浮在内容上方的滚动条，出现和消失都不占位，内容不会因此左右跳动。滚动条可以拖动，点击轨道翻一页。";

    static Control Types()
    {
        var grid = new Grid { MinItemWidth = "10rem", Gap = Space.Md };
        foreach (var (type, description) in Kinds)
        {
            var area = new ScrollArea { Type = type, Height = 144, Content = new Paragraph { Text = Words + Words, Size = TextSize.Sm, Margin = new Thickness(0, 0, 0.75 * 13, 0) } };
            AutomationProperties.SetName(area, type.Value);
            grid.Children.Add(new Card
            {
                Padding = Space.Sm,
                Content = new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Sm,
                    Children = { CodeLine($"Type=\"{type.Value}\""), new Small { Text = description, Tone = TextTone.Muted }, area },
                },
            });
        }
        return grid;
    }

    /// <summary>一行行内代码（Code 是行内元素，放进文字里）。</summary>
    static Text CodeLine(string code)
    {
        var text = new Text();
        text.Inlines!.Add(new Code { Inlines = { new global::Avalonia.Controls.Documents.Run(code) } });
        return text;
    }

    /// <summary>OKLCH → sRGB（CSS Color 4 的换算），超出色域的通道截断。</summary>
    static Color Oklch(double lightness, double chroma, double hue)
    {
        var radians = hue * Math.PI / 180;
        var (a, b) = (chroma * Math.Cos(radians), chroma * Math.Sin(radians));
        var l = Math.Pow(lightness + 0.3963377774 * a + 0.2158037573 * b, 3);
        var m = Math.Pow(lightness - 0.1055613458 * a - 0.0638541728 * b, 3);
        var s = Math.Pow(lightness - 0.0894841775 * a - 1.2914855480 * b, 3);
        static byte Encode(double linear)
        {
            var value = Math.Clamp(linear, 0, 1);
            var srgb = value <= 0.0031308 ? 12.92 * value : 1.055 * Math.Pow(value, 1 / 2.4) - 0.055;
            return (byte)Math.Round(srgb * 255);
        }
        return Color.FromRgb(
            Encode(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s),
            Encode(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s),
            Encode(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s));
    }

    /// <summary>两个方向都能滚的明细表：表头吸顶，行号那一列固定。</summary>
    static Control Sheet()
    {
        var head = new TableRow();
        var corner = new TableHeaderCell { Sticky = true, ColumnWidth = 48 };
        AutomationProperties.SetName(corner, "行号");
        head.Cells.Add(corner);
        for (var c = 0; c < 14; c++) head.Cells.Add(new TableHeaderCell { Align = TableAlign.Center, ColumnWidth = 88, Content = ((char)('A' + c)).ToString() });
        var body = new TableBody();
        for (var r = 1; r <= 30; r++)
        {
            var row = new TableRow { Cells = { new TableHeaderCell { Scope = "row", Sticky = true, Content = r.ToString(CultureInfo.InvariantCulture) } } };
            for (var c = 0; c < 14; c++) row.Cells.Add(new TableCell { Align = TableAlign.End, Nowrap = true, Content = Value(r, c).ToString("F2", CultureInfo.InvariantCulture) });
            body.Rows.Add(row);
        }
        var table = new Table { Density = TableDensity.Compact, StickyHeader = true, ScrollAxis = ScrollAreaAxis.Both, Height = 288, Sections = { new TableHead { Rows = { head } }, body } };
        AutomationProperties.SetName(table, "销售明细表");
        return table;
    }

    /// <summary>每格的数（同上游 demo 的算法：看起来随机、每次一样）。</summary>
    static double Value(int row, int column) => (row * 7919 + column * 104_729) % 100_000 / 100d;
}
