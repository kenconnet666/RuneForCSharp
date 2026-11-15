using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Examples;

/// <summary>Calendar 的示例（vue-tsx-pro 文档站 date/Calendar）。日期的格式化用 .NET 的区域格式（上游是 Intl 的 useFormat）。</summary>
static class CalendarExamples
{
    public static Example[] All =>
    [
        new("Calendar", "basic", "基础用法", "机票的出发日期：今天之前不能选（Min）；旁边是选中日期的说明与几个快捷日期，外面改值时日历翻到那一天。", Basic),
        new("Calendar", "footer", "页脚与 Picked", "Footer 放快捷操作；FixedWeeks（默认）让每个月都是六行，翻页时高度不跳；Picked 在每次点选时发出（再点已选的那天也会）。", Footer),
        new("Calendar", "locales", "跟随语言", "月份名、星期名、读屏念的完整日期都按语言生成；一周从哪天开始按地区惯例，也可以用 FirstDayOfWeek 指定；从右往左的语言整个日历镜像。", Locales),
        new("Calendar", "multiple", "多选", "排班：多选要值班的日子，右侧按日期列出、可以单独移除；并排两个月、显示周数。", Multiple),
        new("Calendar", "multiple-months", "并排几个月", "排期总览：并排三个月，翻页一次翻三个月；并排时补齐的日期留空，同一天不会出现两次。", MultipleMonths),
        new("Calendar", "prices", "自定义日期格", "酒店的价格日历：CellContent 在日期格里加上当天的房价，最低价用成功色标出；周五、周六更贵，售罄的日子不可选。", Prices),
        new("Calendar", "sizes", "尺寸与状态", "Size 改字号，格子按 em 等比缩放；只读能翻页、不能改；不可用整个变淡。", Sizes),
        new("Calendar", "unavailable", "不可选的日期", "会议室预约：只能约今天起 30 天内的工作日，节假日与已约满的日子不可选（仍能用方向键经过，读屏念\"不可用\"）。", Unavailable),
        new("Calendar", "views", "月份与年份视图", "点标题从日期切到月份、再到年份，跨年份找日期很快；Granularity 让选择停在月份或年份（账期、入学年份）。", Views),
    ];

    static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");

    static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static string Full(DateOnly date) => CalendarText.FullDate(date, Chinese);

    static void OnValue(Calendar calendar, Action changed) => calendar.PropertyChanged += (_, e) =>
    {
        if (e.Property == Calendar.ValueProperty || e.Property == Calendar.ValuesProperty) changed();
    };

    static Control Labeled(string label, Control content) => new Flex
    {
        Direction = FlexDirection.Column,
        Gap = Space.Xs,
        Children = { new Small { Tone = TextTone.Muted, Text = label }, content },
    };

    static Grid Columns(string minWidth, IEnumerable<Control> children)
    {
        var grid = new Grid { MinItemWidth = new Length(minWidth), Gap = Space.Xl, Align = GridAlign.Start };
        grid.Children.AddRange(children);
        return grid;
    }

    static Control Basic()
    {
        var today = Today;
        (string Label, DateOnly Date)[] quick =
        [
            ("今天", today),
            ("明天", today.AddDays(1)),
            ("下周一", today.AddDays(8 - ((int)today.DayOfWeek is 0 ? 7 : (int)today.DayOfWeek))),
            ("一个月后", today.AddMonths(1)),
        ];
        var calendar = new Calendar { Date = today.AddDays(3), Min = Iso(today) };
        AutomationProperties.SetName(calendar, "出发日期");
        var summary = new Text();
        var days = new Text { Tone = TextTone.Muted, Size = TextSize.Sm };
        var buttons = new Flex { Gap = Space.Sm, Wrap = true };
        var choices = quick.Select(entry =>
        {
            var button = new Button { Size = TextSize.Sm, Content = entry.Label };
            button.Click += (_, _) => calendar.Date = entry.Date;
            buttons.Children.Add(button);
            return (button, entry.Date);
        }).ToList();
        void Sync()
        {
            summary.Text = calendar.Date is { } date ? Full(date) : "还没选出发日期";
            var between = calendar.Date is { } picked ? picked.DayNumber - today.DayNumber : (int?)null;
            days.Text = between is 0 ? "今天出发" : between is { } n ? $"{n} 天后出发" : "";
            days.IsVisible = between is not null;
            foreach (var (button, quickDate) in choices) button.Variant = calendar.Date == quickDate ? ButtonVariant.Solid : ButtonVariant.Soft;
        }
        OnValue(calendar, Sync);
        Sync();
        var side = new Card
        {
            MinWidth = 16 * 14,
            Header = new Strong { Text = "上海 → 成都" },
            Content = new Flex
            {
                Direction = FlexDirection.Column,
                Gap = Space.Md,
                Children =
                {
                    new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Icon { Data = Lucide.PlaneTakeoff }, summary } },
                    days,
                    buttons,
                },
            },
        };
        return new Flex { Gap = Space.Xl, Wrap = true, Align = FlexAlign.Start, Children = { calendar, side } };
    }

    static Control Footer()
    {
        var picks = 0;
        var calendar = new Calendar { DefaultFocused = "2026-12-01" };
        AutomationProperties.SetName(calendar, "提醒日期");
        var today = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Content = "今天" };
        var clear = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Content = "清空" };
        today.Click += (_, _) => calendar.Date = Today;
        clear.Click += (_, _) => calendar.Value = null;
        calendar.Footer = new Flex { Gap = Space.Xs, Justify = FlexJustify.Between, Children = { today, clear } };
        var note = new Small { Tone = TextTone.Muted };
        void Sync()
        {
            clear.IsEnabled = calendar.Value is not null;
            note.Text = $"{calendar.Value ?? "没有选择（一开始显示 2026 年 12 月）"} · 点选了 {picks} 次";
        }
        calendar.Picked += (_, _) =>
        {
            picks++;
            Sync();
        };
        OnValue(calendar, Sync);
        Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Start, Children = { new Card { Padding = Space.Sm, Content = calendar }, note } };
    }

    static Control Locales()
    {
        var german = Lang.EnUs with { Code = "de-DE" };
        (string Label, Lang Lang, DayOfWeek? First)[] cases =
        [
            ("简体中文：周一开始", Lang.ZhCn, null),
            ("English (US)：周日开始", Lang.EnUs, null),
            ("Deutsch：周一开始，显示周数", german, null),
            ("日本語：周日开始", Lang.EnUs with { Code = "ja-JP" }, null),
            ("简体中文，FirstDayOfWeek=Sunday", Lang.ZhCn, DayOfWeek.Sunday),
            ("阿拉伯语（从右往左）", Lang.EnUs with { Code = "ar-EG", Direction = "rtl" }, null),
        ];
        return Columns("15em", cases.Select(entry =>
        {
            var calendar = new Calendar { Date = Today, FirstDayOfWeek = entry.First, ShowWeekNumbers = entry.Lang == german };
            AutomationProperties.SetName(calendar, entry.Label);
            Provider.SetLang(calendar, entry.Lang);
            // 从右往左：整个日历镜像，翻页箭头跟着翻转
            if (entry.Lang.Direction == "rtl") calendar.FlowDirection = FlowDirection.RightToLeft;
            return Labeled(entry.Label, calendar);
        }));
    }

    static Control Multiple()
    {
        var calendar = new Calendar { SelectionMode = CalendarSelectionMode.Multiple, VisibleMonths = 2, ShowWeekNumbers = true };
        AutomationProperties.SetName(calendar, "值班日期");
        var count = new Strong();
        var clear = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Content = "清空" };
        clear.Click += (_, _) => calendar.Values = [];
        var side = new Card
        {
            MinWidth = 14 * 14,
            Header = new Flex { Justify = FlexJustify.Between, Align = FlexAlign.Center, Children = { count, clear } },
        };
        void Sync()
        {
            var sorted = calendar.Values.Order(StringComparer.Ordinal).ToList();
            count.Text = $"值班 {sorted.Count} 天";
            clear.IsEnabled = sorted.Count > 0;
            if (sorted.Count == 0)
            {
                side.Content = new Text { Tone = TextTone.Muted, Text = "点日历上的日子加入排班，再点一次取消" };
                return;
            }
            var tags = new Flex { Gap = Space.Xs, Wrap = true };
            foreach (var iso in sorted)
            {
                var date = DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var tag = new Tag { Content = date.ToString("M/d ddd", Chinese) };
                tag.RemoveRequested += (_, _) => calendar.Values = [.. calendar.Values.Where(item => item != iso)];
                tags.Children.Add(tag);
            }
            side.Content = tags;
        }
        OnValue(calendar, Sync);
        Sync();
        return new Flex { Gap = Space.Xl, Wrap = true, Align = FlexAlign.Start, Children = { calendar, side } };
    }

    static Control MultipleMonths()
    {
        var calendar = new Calendar { Date = Today, VisibleMonths = 3 };
        AutomationProperties.SetName(calendar, "上线日期");
        var text = new Text { Tone = TextTone.Muted };
        void Sync() => text.Text = $"上线日期：{(calendar.Date is { } date ? Full(date) : "未定")}";
        OnValue(calendar, Sync);
        Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { calendar, text } };
    }

    static Control Prices()
    {
        var today = Today;
        var soldOut = new HashSet<DateOnly> { today.AddDays(5), today.AddDays(6) };
        // 周五、周六更贵；按日期给一点起伏，演示数据确定不变
        static int PriceOf(DateOnly date) => (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? 699 : 459) + date.Day % 4 * 20;
        var lowest = Enumerable.Range(0, 35).Min(days => PriceOf(today.AddDays(days)));
        var calendar = new Calendar
        {
            Min = Iso(today),
            Max = Iso(today.AddDays(34)),
            IsDateUnavailable = soldOut.Contains,
            CellContent = (date, cell) =>
            {
                var day = new TextBlock { Text = date.Day.ToString(CultureInfo.InvariantCulture), HorizontalAlignment = HorizontalAlignment.Center };
                // 读屏已经念了完整日期，数字不重复念；价格照常念
                AutomationProperties.SetAccessibilityView(day, AccessibilityView.Raw);
                var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Children = { day } };
                if (cell.Disabled || cell.Outside) return stack;
                var price = PriceOf(date);
                var label = new Text { Text = cell.Unavailable ? "满" : $"¥{price}", FontSize = 0.7 * 14, HorizontalAlignment = HorizontalAlignment.Center };
                // 选中的格子是品牌色实底：价格跟着用实底上的文字色
                if (!cell.Selected)
                {
                    label.Tone = price == lowest ? TextTone.Success : TextTone.Muted;
                    if (price == lowest) label.Weight = TextWeight.Semibold;
                }
                stack.Children.Add(label);
                return stack;
            },
        };
        AutomationProperties.SetName(calendar, "入住日期");
        // 格子放得下价格：比默认的 2.25em 宽、高一些
        var size = new EmExtension(3.25).ProvideValue(null!);
        calendar.Styles.Add(new Style(x => x.OfType<ButtonBase>().Name(CalendarViewParts.CellButton))
        {
            Setters = { new Setter(Layoutable.WidthProperty, size), new Setter(Layoutable.HeightProperty, size) },
        });
        var text = new Text { Tone = TextTone.Muted };
        void Sync() => text.Text = calendar.Date is { } night
            ? $"{night.ToString("M月d日 ddd", Chinese)} 入住，一晚 ¥{PriceOf(night)}"
            : $"最低 ¥{lowest} 起，周五、周六更贵";
        OnValue(calendar, Sync);
        Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { calendar, text } };
    }

    static Control Sizes()
    {
        (string Label, Action<Calendar> Apply)[] cases =
        [
            ("Size=Sm：侧栏、筛选面板", calendar => calendar.Size = TextSize.Sm),
            ("Size=Md", calendar => calendar.Size = TextSize.Md),
            ("Size=Lg：触屏、大屏", calendar => calendar.Size = TextSize.Lg),
            ("IsReadOnly：能翻页、不能改", calendar => calendar.IsReadOnly = true),
            ("IsEnabled=False", calendar => calendar.IsEnabled = false),
        ];
        return Columns("16em", cases.Select(entry =>
        {
            var calendar = new Calendar { Date = Today };
            entry.Apply(calendar);
            AutomationProperties.SetName(calendar, entry.Label);
            return Labeled(entry.Label, calendar);
        }));
    }

    static Control Unavailable()
    {
        var today = Today;
        var last = today.AddDays(30);
        // 演示数据：从今天往后数的第 3、4、11 个日子已约满，第 8 天是节假日
        var fullyBooked = new HashSet<DateOnly> { today.AddDays(3), today.AddDays(4), today.AddDays(11) };
        var holiday = today.AddDays(8);
        string? Reason(DateOnly date) =>
            date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "周末" : date == holiday ? "节假日" : fullyBooked.Contains(date) ? "已约满" : null;
        var calendar = new Calendar { Min = Iso(today), Max = Iso(last), IsDateUnavailable = date => Reason(date) is not null };
        AutomationProperties.SetName(calendar, "预约日期");
        var picked = new Text();
        void Sync() => picked.Text = calendar.Date is { } date ? $"已选：{Full(date)}" : "选一个可预约的工作日";
        OnValue(calendar, Sync);
        Sync();
        var side = new Card
        {
            MinWidth = 16 * 14,
            Header = new Strong { Text = "3 号会议室 · 12 人" },
            Content = new Flex
            {
                Direction = FlexDirection.Column,
                Gap = Space.Sm,
                Children =
                {
                    picked,
                    new Text { Tone = TextTone.Muted, Size = TextSize.Sm, Text = $"可预约：{today.ToString("M月d日", Chinese)} 至 {last.ToString("M月d日", Chinese)}" },
                    new Flex
                    {
                        Gap = Space.Sm,
                        Wrap = true,
                        Children =
                        {
                            new Badge { Variant = StatusVariant.Soft, Tone = StatusTone.Neutral, Content = "周末不开放" },
                            new Badge { Variant = StatusVariant.Soft, Tone = StatusTone.Warning, Content = $"{holiday.ToString("M/d", Chinese)} 节假日" },
                            new Badge { Variant = StatusVariant.Soft, Tone = StatusTone.Danger, Content = $"{fullyBooked.Count} 天已约满" },
                        },
                    },
                },
            },
        };
        return new Flex { Gap = Space.Xl, Wrap = true, Align = FlexAlign.Start, Children = { calendar, side } };
    }

    static Control Views()
    {
        Control Case(string label, Calendar calendar, Func<DateOnly?, string> describe)
        {
            AutomationProperties.SetName(calendar, label);
            var text = new Text { Size = TextSize.Sm };
            void Sync() => text.Text = describe(calendar.Date);
            OnValue(calendar, Sync);
            Sync();
            return new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new Small { Tone = TextTone.Muted, Text = label }, calendar, text } };
        }
        return Columns("15em",
        [
            Case("生日：点标题\"1995年6月\"切到月份、年份视图", new Calendar { Value = "1995-06-18", Max = "2026-12-31" }, date => date?.ToString("D", Chinese) ?? ""),
            Case("账期：Granularity=Month", new Calendar { Granularity = DateUnit.Month }, date => date?.ToString("Y", Chinese) ?? "选一个月"),
            Case("入学年份：Granularity=Year", new Calendar { Granularity = DateUnit.Year, Min = "2000-01-01" }, date => date is { } year ? $"{year.Year}年" : "选一年"),
        ]);
    }
}
