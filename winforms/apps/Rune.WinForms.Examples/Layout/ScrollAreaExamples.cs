using System.Globalization;
using System.Drawing.Drawing2D;

namespace Rune.WinForms.Examples;

/// <summary>
/// ScrollArea 的示例（vue-tsx-pro 文档站 layout/ScrollArea）。both（宽表格，表头与行号固定）要等 Table。
/// WinForms 里是 ScrollAreaControl：放一个内容控件（Content），滚动条是叠在内容上的滑块。
/// </summary>
static class ScrollAreaExamples
{
    public static Example[] All =>
    [
        new("ScrollArea", "vertical", "纵向滚动", "高度由使用方给，内容放进 Content。区域可以用 Tab 聚焦、用方向键滚动，名称写在 AccessibleName 上。", Inbox),
        new("ScrollArea", "horizontal", "横向滚动", "Axis = x 横向滚动，纵向超出的裁掉。滚动条浮在内容上，底部留出一点空，免得盖住卡片。", Playlists),
        new("ScrollArea", "both", "两个方向", "宽表格、电子表格：TableControl 放进 Axis = Both、限高的滚动区域；表头吸顶（StickyHeader），行号固定在起始边（TableCellControl.Sticky）。", Sheet),
        new("ScrollArea", "types", "什么时候显示滚动条", "Type 三种显示时机；隐藏前的停留时间是 HideDelay。", Types),
    ];

    static readonly string[] Senders = ["林小满", "周一航", "陈默", "许悠", "王晴", "赵浩然", "孙思远", "李雨桐"];

    static readonly string[] Subjects =
    [
        "首页改版的设计稿更新了，麻烦看一下", "下周一的评审会改到下午三点", "发票已经寄出，单号见附件",
        "接口文档补好了，有问题随时找我", "团建报名今天截止", "服务器续费提醒",
    ];

    static Control Inbox()
    {
        var list = new FlexListControl { Direction = FlexDirection.Column, AutoSize = true };
        for (var index = 0; index < 24; index++)
        {
            var sender = Senders[index % Senders.Length];
            var time = $"{18 - index / 3:00}:{index * 17 % 60:00}";
            var body = Build.Flex(new FlexControl { Direction = FlexDirection.Column },
                Build.Flex(new FlexControl { Justify = FlexJustify.Between, Gap = Space.Sm },
                    new StrongControl { Text = sender }, new SmallControl { Text = time, Tone = TextTone.Muted }),
                new TextControl { Text = Subjects[index % Subjects.Length], TextSize = TextSize.Sm, Tone = TextTone.Muted, Truncate = true });
            // 每一项上下 space.sm、左右 space.md（画布字号 14px）
            var item = Build.Flex(new FlexControl { Gap = Space.Md, Align = FlexAlign.Center },
                new AvatarControl { TextSize = TextSize.Sm, DisplayName = sender }, body);
            item.SetGrow(body, true);
            item.Padding = new Padding(item.LogicalToDeviceUnits(10), item.LogicalToDeviceUnits(7), item.LogicalToDeviceUnits(10), item.LogicalToDeviceUnits(7));
            list.Controls.Add(item);
        }
        var area = new ScrollAreaControl { Content = list, AccessibleName = "收件箱", Dock = DockStyle.Top };
        area.Height = area.LogicalToDeviceUnits(320);
        return Build.Card(new CardControl { CardPadding = Space.None }, area);
    }

    static readonly (string Name, int Count, double Hue)[] Shelf =
    [
        ("通勤路上", 32, 210), ("深夜写代码", 48, 265), ("周末早午餐", 21, 30), ("跑步节拍", 40, 0),
        ("雨天爵士", 27, 230), ("专注白噪音", 12, 160), ("九十年代华语", 55, 340), ("电影原声", 36, 290),
    ];

    static Control Playlists()
    {
        // 底部留 space.md：滚动条浮在内容上，不盖住卡片
        var row = new FlexListControl { Gap = Space.Md, AutoSize = true };
        row.Padding = new Padding(0, 0, 0, row.LogicalToDeviceUnits(10));
        foreach (var (name, count, hue) in Shelf)
        {
            var cover = new Cover(Oklch(0.70, 0.14, hue), Oklch(0.45, 0.12, hue + 40));
            cover.Size = new Size(cover.LogicalToDeviceUnits(128), cover.LogicalToDeviceUnits(128));
            cover.Margin = new Padding(0, 0, 0, cover.LogicalToDeviceUnits(7));
            // 宽 9rem 的卡片
            var width = cover.LogicalToDeviceUnits(144);
            var card = Build.Card(new CardControl { CardPadding = Space.Sm, MinimumSize = new Size(width, 0), MaximumSize = new Size(width, 0) },
                cover, new StrongControl { Text = name }, new SmallControl { Text = $"{count} 首", Tone = TextTone.Muted });
            row.Controls.Add(card);
        }
        var area = new ScrollAreaControl { Axis = ScrollAreaAxis.X, Type = ScrollbarType.Always, Content = row, AccessibleName = "推荐歌单" };
        area.Height = row.GetPreferredSize(Size.Empty).Height;
        return area;
    }

    /// <summary>封面：歌单的色相做 135° 的渐变（同上游的 oklch(70% 0.14 h) → oklch(45% 0.12 h+40)），圆角 radii.md。</summary>
    sealed class Cover(Color from, Color to) : Control
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            var radius = 0.5f * Font.SizeInPoints * DeviceDpi / 72f;
            using var path = new GraphicsPath();
            var diameter = 2 * radius;
            path.AddArc(0, 0, diameter, diameter, 180, 90);
            path.AddArc(Width - diameter, 0, diameter, diameter, 270, 90);
            path.AddArc(Width - diameter, Height - diameter, diameter, diameter, 0, 90);
            path.AddArc(0, Height - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            using var brush = new LinearGradientBrush(new Point(0, 0), new Point(Width, Height), from, to);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }
    }

    static readonly (ScrollbarType Type, string Description)[] Kinds =
    [
        (ScrollbarType.Hover, "指针在上面或正在滚动时显示（默认）"),
        (ScrollbarType.Scroll, "只在滚动时显示，停下后隐藏"),
        (ScrollbarType.Always, "内容超出时一直显示"),
    ];

    // 同上游的文字（"浏览器原生"换成"平台原生"）：文字的长度决定滑块的大小
    const string Words = "滚动仍是平台原生的：触控板惯性、键盘、页内搜索、读屏都不变。只是把原生滚动条藏起来，换成半透明、浮在内容上方的滚动条，出现和消失都不占位，内容不会因此左右跳动。滚动条可以拖动，点击轨道翻一页。";

    static Control Types() => Build.Grid(new GridControl { MinItemWidth = "10rem", Gap = Space.Md },
        [.. Kinds.Select(kind => Build.Card(new CardControl { CardPadding = Space.Sm },
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm },
                new CodeControl { Text = $"Type=\"{kind.Type.Value}\"" },
                new SmallControl { Text = kind.Description, Tone = TextTone.Muted },
                Area(kind.Type))))]);

    static ScrollAreaControl Area(ScrollbarType type)
    {
        var area = new ScrollAreaControl { Type = type, AccessibleName = type.Value, Content = new ParagraphControl { Text = Words + Words, TextSize = TextSize.Sm } };
        area.Height = area.LogicalToDeviceUnits(144);
        area.Content.Padding = new Padding(0, 0, area.LogicalToDeviceUnits(10), 0);
        return area;
    }

    /// <summary>OKLCH → sRGB（CSS Color 4 的换算），超出色域的通道截断。</summary>
    static Color Oklch(double lightness, double chroma, double hue)
    {
        var radians = hue * Math.PI / 180;
        var (a, b) = (chroma * Math.Cos(radians), chroma * Math.Sin(radians));
        var l = Math.Pow(lightness + 0.3963377774 * a + 0.2158037573 * b, 3);
        var m = Math.Pow(lightness - 0.1055613458 * a - 0.0638541728 * b, 3);
        var s = Math.Pow(lightness - 0.0894841775 * a - 1.2914855480 * b, 3);
        static int Encode(double linear)
        {
            var value = Math.Clamp(linear, 0, 1);
            var srgb = value <= 0.0031308 ? 12.92 * value : 1.055 * Math.Pow(value, 1 / 2.4) - 0.055;
            return (int)Math.Round(srgb * 255);
        }
        return Color.FromArgb(
            Encode(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s),
            Encode(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s),
            Encode(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s));
    }

    /// <summary>两个方向都能滚的明细表：表格放进限高的滚动区域，表头吸顶，行号那一列固定。</summary>
    static Control Sheet()
    {
        var head = new TableRowControl();
        head.Controls.Add(new TableHeaderCellControl { Sticky = true, ColumnWidth = 48, AccessibleName = "行号" });
        for (var c = 0; c < 14; c++) head.Controls.Add(new TableHeaderCellControl { Align = TableAlign.Center, ColumnWidth = 88, Text = ((char)('A' + c)).ToString() });
        var body = new TableBodyControl();
        for (var r = 1; r <= 30; r++)
        {
            var row = new TableRowControl();
            row.Controls.Add(new TableHeaderCellControl { Scope = "row", Sticky = true, Text = r.ToString(CultureInfo.InvariantCulture) });
            for (var c = 0; c < 14; c++) row.Controls.Add(new TableCellControl { Align = TableAlign.End, Nowrap = true, Text = Value(r, c).ToString("F2", CultureInfo.InvariantCulture) });
            body.Controls.Add(row);
        }
        var table = new TableControl { Density = TableDensity.Compact, StickyHeader = true, AccessibleName = "销售明细表", Controls = { new TableHeadControl { Controls = { head } }, body } };
        return new ScrollAreaControl { Axis = ScrollAreaAxis.Both, Height = 288, Width = 560, Content = table, AccessibleName = "销售明细表" };
    }

    /// <summary>每格的数（同上游 demo 的算法：看起来随机、每次一样）。</summary>
    static double Value(int row, int column) => (row * 7919 + column * 104_729) % 100_000 / 100d;
}
