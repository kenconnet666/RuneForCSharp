using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;

namespace Rune.Wpf.Examples;

/// <summary>Stat 页的示例（vue-tsx-pro 文档站 display/Stat）。</summary>
static class StatExamples
{
    public static Example[] All =>
    [
        new("Stat", "basic", "基础用法", "Stat 只是内容，放进 Card、Grid 里组合。数字按当前语言格式化，Format 控制小数位与缩放：金额以元给出、按万元显示时写 Scale = 10000 并配 Unit=\"万元\"，decimal 不丢精度。读屏里是一组，名称是指标名，图标对读屏隐藏。", Basic),
        new("Stat", "change", "变化量", "Change 默认按百分比显示，上升为成功色、下降为危险色；成本、耗时这类越低越好的指标写 InvertChange。变化量是绝对数时用 ChangeFormat 换格式。", Changes),
        new("Stat", "live", "数值变化", "数值变化时从旧值滚动到新值（AnimatedNumber），读屏只读终值；系统关闭动画时直接跳到新值。下方的迷你趋势是示例自己拼的最近 12 次取值（Sparkline 的柱）。", Live),
        new("Stat", "animated-number", "AnimatedNumber 单独使用", "值变化时从旧值滚到新值，等宽数字不抖动。Format 是 Intl.NumberFormat 的选项加上缩放 Scale（NumericFormat），金额不写币种时用语言包的币种；值是 decimal，中间帧按浮点数算，停下时按原值格式化，不丢精度。Trend 按正负着色；Prefix、Suffix 放在数字前后（图标、单位）。", Numbers),
        new("Stat", "group", "一排指标", "StatGroup 把几个 Stat 排成一排，按宽度换行（MinItemWidth，默认 9rem），同一行里之间一条竖线。超时、缺漏这类要引起注意的数写 Tone=\"danger\"，数值换成危险色。", Group),
        new("Stat", "loading", "加载中", "IsLoading 时数值与变化量换成骨架，一道光带从起始边扫过，指标名照常显示；Card 的 IsLoading 把正文换成几行骨架。骨架对读屏隐藏，读屏的状态是“加载中”；很快完成的加载不出现骨架，系统关闭动画时不扫光。", Loading),
    ];

    // 接口给的金额以元为单位（decimal 不丢精度），卡片上按万元显示
    static readonly NumericFormat WanYuan = new() { Scale = 10_000, MinimumFractionDigits = 2, MaximumFractionDigits = 2 };

    static Grid Cards(string minItemWidth, params FrameworkElement[] contents)
    {
        var grid = new Grid { MinItemWidth = minItemWidth, Balanced = true, Gap = Space.Md };
        foreach (var content in contents) grid.Children.Add(content is Card ? content : new Card { Content = content });
        return grid;
    }

    static FrameworkElement Basic()
    {
        const decimal expected = 10875000m, actual = 9103600.35m;
        return Cards("14em",
            new Stat { Label = "项目总数", Value = 6, Unit = "项", Icon = Lucide.ClipboardList },
            new Stat { Label = "预期销售额", Value = expected, Format = WanYuan, Unit = "万元", Icon = Lucide.JapaneseYen },
            new Stat { Label = "实际销售额", Value = actual, Format = WanYuan, Unit = "万元", Icon = Lucide.ChartColumn },
            new Stat { Label = "销售达成率", Value = actual / expected, Format = new NumericFormat { Style = NumericStyle.Percent, MaximumFractionDigits = 1 }, Icon = Lucide.TrendingUp });
    }

    static FrameworkElement Changes() => Cards("12em",
        new Stat { Label = "订单数", Value = 1284, Change = 0.123m, ChangeLabel = "较上月" },
        new Stat { Label = "客单价", Value = 86.4m, Unit = "元", Change = -0.042m, ChangeLabel = "较上月" },
        new Stat { Label = "平均响应时间", Value = 212, Unit = "ms", Change = -0.18m, ChangeLabel = "较上周", InvertChange = true, Description = "越低越好：下降显示为改善" },
        // 变化量是绝对数而不是比例：ChangeFormat 换成带符号的整数
        new Stat { Label = "新增会员", Value = 342, Unit = "人", Change = 58, ChangeFormat = new NumericFormat { SignDisplay = NumericSign.ExceptZero }, ChangeLabel = "较昨日" });

    // 一排指标：按宽度换行，同一行里之间一条竖线；要引起注意的数用 Tone 标红
    static FrameworkElement Group()
    {
        (string Label, IconData Icon, int Value, bool Urgent)[] items =
        [
            ("超时", Lucide.AlarmClock, 10, true), ("7 天内到期", Lucide.CalendarClock, 6, false),
            ("待验收", Lucide.BadgeCheck, 5, false), ("可关闭", Lucide.PackageCheck, 0, false),
        ];
        var group = new StatGroup();
        foreach (var stat in items.Select(item => new Stat
        {
            Label = item.Label, Icon = item.Icon, Value = item.Value, Unit = "项",
            Tone = item.Urgent && item.Value > 0 ? TextTone.Danger : default,
            Footer = item.Value > 0 ? new Text { Inlines = { new Link { Href = new Uri("https://example.com/todo"), Inlines = { new Run("去处理") } } } } : new Small { Tone = TextTone.Muted, Text = "没有" },
        })) group.Children.Add(stat);
        return new Card { Variant = CardVariant.Outline, Padding = Space.Md, Header = "需要处理", Content = group };
    }

    static FrameworkElement Loading()
    {
        var toggle = new Switch { Content = "加载中", IsChecked = true };
        var count = new Stat { Label = "项目总数", Value = 6, Unit = "项", Icon = Lucide.ClipboardList, IsLoading = true };
        var sales = new Stat
        {
            Label = "实际销售额", Value = 910.36m, Format = new NumericFormat { MinimumFractionDigits = 2 }, Unit = "万元", Icon = Lucide.JapaneseYen,
            Change = 0.123m, ChangeLabel = "较上月", IsLoading = true,
        };
        var note = new Card { Header = "本月说明", IsLoading = true, Content = new Text { Text = "实际销售额按财务口径统计，每月 5 日更新上月数据。" } };
        DependencyPropertyDescriptor.FromProperty(ChoiceBase.IsCheckedProperty, typeof(Switch)).AddValueChanged(toggle, (_, _) =>
            count.IsLoading = sales.IsLoading = note.IsLoading = toggle.IsChecked == true);
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { new Flex { Children = { toggle } }, Cards("12em", count, sales, note) } };
    }


    // 实时指标：每 2 秒推一个新值，数值滚动到新值，变化量与上一次比较；下面是最近 12 次的柱，最新的一根实色
    static FrameworkElement Live()
    {
        var random = new Random(3);
        (string Label, IconData Icon, string Unit, int Spread, int Base)[] metrics =
        [
            ("在线人数", Lucide.Users, "人", 180, 3260), ("每分钟订单", Lucide.ShoppingCart, "单", 12, 86),
            ("接口 QPS", Lucide.Activity, "次/秒", 90, 1420), ("平均延迟", Lucide.Gauge, "ms", 14, 118),
        ];
        var running = new Switch { Content = "每 2 秒刷新", IsChecked = true };
        var grid = new Grid { MinItemWidth = "15em", Balanced = true, Gap = Space.Md };
        var updates = new List<Action>();
        foreach (var (label, icon, unit, spread, start) in metrics)
        {
            var history = Enumerable.Range(0, 12).Select(index => (decimal)Math.Round(start + Math.Sin(index) * spread)).ToList();
            var trend = new Sparkline { Type = SparklineType.Bar, Label = $"最近 12 次{label}" };
            var stat = new Stat { Label = label, Icon = icon, Unit = unit, ChangeLabel = "较上次", InvertChange = label == "平均延迟", Content = trend };
            void Show()
            {
                var (value, previous) = (history[^1], history[^2]);
                stat.Value = value;
                stat.Change = previous == 0 ? 0 : Math.Round((value - previous) / previous, 4);
                // 柱默认从 0 起；值都在几千上下时从略低于最小值的地方起，起伏才看得出
                var (min, max) = ((double)history.Min(), (double)history.Max());
                trend.Domain = (min - (max - min) / 3, null);
                trend.Data = [.. history.Select(point => (decimal?)point)];
            }
            Show();
            updates.Add(() =>
            {
                history.RemoveAt(0);
                history.Add(Math.Max(0, Math.Round(history[^1] + (decimal)((random.NextDouble() - 0.5) * spread))));
                Show();
            });
            grid.Children.Add(new Card { Content = stat });
        }
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            if (running.IsChecked == true) updates.ForEach(update => update());
        };
        grid.Loaded += (_, _) => timer.Start();
        grid.Unloaded += (_, _) => timer.Stop();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children = { new Flex { Gap = Space.Md, Align = FlexAlign.Center, Wrap = true, Children = { running, new Small { Tone = TextTone.Muted, Text = "数值滚动到新值，变化量与上一次比较" } } }, grid },
        };
    }

    static FrameworkElement Numbers()
    {
        var random = new Random(7);
        decimal Between(double min, double max) => (decimal)(min + random.NextDouble() * (max - min));
        var balance = new AnimatedNumber { Value = 12880.35m, Format = new NumericFormat { Style = NumericStyle.Currency } };
        var growth = new AnimatedNumber { Value = 0.124m, Format = new NumericFormat { Style = NumericStyle.Percent, MaximumFractionDigits = 1 }, Trend = true };
        var downloads = new AnimatedNumber { Value = 9_007_199_254_740_993m };
        var rating = new AnimatedNumber
        {
            Value = 4.6m, Format = new NumericFormat { MinimumFractionDigits = 1 }, Suffix = " / 5",
            Prefix = new Icon { Data = Lucide.Star, Tone = TextTone.Warning }, Duration = TimeSpan.FromMilliseconds(500),
        };
        var shuffle = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.RefreshCw, Content = "换一组数" };
        shuffle.Click += (_, _) =>
        {
            balance.Value += Math.Round(Between(-2000, 2000), 2);
            growth.Value = Math.Round(Between(-0.2, 0.3), 4);
            downloads.Value += Math.Round(Between(1000, 50_000));
            rating.Value = Math.Round(Between(3, 5), 1);
        };
        var values = new Descriptions { Columns = 2 };
        foreach (var item in new DescriptionItem[]
        {
            new DescriptionItem { Label = "余额（decimal）", Value = balance },
            new DescriptionItem { Label = "环比", Value = growth },
            new DescriptionItem { Label = "累计下载", Value = downloads },
            new DescriptionItem { Label = "评分", Value = rating },
        }) values.Items.Add(item);
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { values, shuffle } };
    }
}
