using System.Globalization;

namespace Rune.WinForms.Examples;

/// <summary>Calendar 的示例（vue-tsx-pro 文档站 date/Calendar）。WinForms 里是 CalendarControl；自定义日期格是 CellText（文字、读屏）加 DrawCell（自己画）。</summary>
static class CalendarExamples
{
    public static Example[] All =>
    [
        new("Calendar", "basic", "基础用法", "机票的出发日期：今天之前不能选（Min）；旁边是选中日期的说明与几个快捷日期，外面改值时日历翻到那一天。", Basic),
        new("Calendar", "footer", "页脚与 Picked", "Footer 放快捷操作；FixedWeeks（默认）让每个月都是六行，翻页时高度不跳；Picked 在每次点选时发出（再点已选的那天也会）。", Footer),
        new("Calendar", "locales", "跟随语言", "月份名、星期名、读屏念的完整日期都按语言生成；一周从哪天开始按地区惯例，也可以用 FirstDayOfWeek 指定；从右往左的语言整个日历镜像。", Locales),
        new("Calendar", "multiple", "多选", "排班：多选要值班的日子，右侧按日期列出、可以单独移除；并排两个月、显示周数。", Multiple),
        new("Calendar", "multiple-months", "并排几个月", "排期总览：并排三个月，翻页一次翻三个月；并排时补齐的日期留空，同一天不会出现两次。", MultipleMonths),
        new("Calendar", "prices", "自定义日期格", "酒店的价格日历：CellText 给读屏念的房价，DrawCell 在格子里画日期与房价，最低价用成功色标出；周五、周六更贵，售罄的日子不可选。", Prices),
        new("Calendar", "sizes", "尺寸与状态", "TextSize 改字号，格子按 em 等比缩放；只读能翻页、不能改；不可用整个变淡。", Sizes),
        new("Calendar", "unavailable", "不可选的日期", "会议室预约：只能约今天起 30 天内的工作日，节假日与已约满的日子不可选（仍能用方向键经过，读屏念\"不可用\"）。", Unavailable),
        new("Calendar", "quarter", "季度面板", "QuarterCalendarControl：一年一页的四个季度（季度报表、考核周期），选一个季度或一段区间（先开始、再结束，可以跨年翻页）；整段在 Min–Max 之外的季度不可用。DatePicker 的季度粒度弹出的就是它。", Quarter),
        new("Calendar", "views", "月份与年份视图", "点标题从日期切到月份、再到年份，跨年份找日期很快；Granularity 让选择停在月份或年份（账期、入学年份）。", Views),
    ];

    static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");

    static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static string Full(DateOnly date) => CalendarText.FullDate(date, Chinese);

    static FlexControl Stack(Space gap, params Control[] children) => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = gap, Align = FlexAlign.Start }, children);

    static FlexControl Row(params Control[] children) => Build.Flex(new FlexControl { Gap = Space.Xl, Wrap = true, Align = FlexAlign.Start }, children);

    static Control Labeled(string label, Control content) => Stack(Space.Xs, new SmallControl { Tone = TextTone.Muted, Text = label }, content);

    static Control Columns(string minWidth, IEnumerable<Control> children) => Build.Grid(new GridControl { MinItemWidth = minWidth, Gap = Space.Xl, Align = GridAlign.Start }, [.. children]);

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
        var calendar = new CalendarControl { Date = today.AddDays(3), Min = Iso(today), AccessibleName = "出发日期" };
        var summary = new TextControl();
        var days = new TextControl { Tone = TextTone.Muted, TextSize = TextSize.Sm };
        var buttons = new FlexControl { Gap = Space.Sm, Wrap = true };
        var choices = quick.Select(entry =>
        {
            var button = new ButtonControl { TextSize = TextSize.Sm, Text = entry.Label };
            button.Click += (_, _) => calendar.Date = entry.Date;
            buttons.Controls.Add(button);
            return (button, entry.Date);
        }).ToList();
        void Sync()
        {
            summary.Text = calendar.Date is { } date ? Full(date) : "还没选出发日期";
            var between = calendar.Date is { } picked ? picked.DayNumber - today.DayNumber : (int?)null;
            days.Text = between is 0 ? "今天出发" : between is { } n ? $"{n} 天后出发" : "";
            days.Visible = between is not null;
            foreach (var (button, quickDate) in choices) button.Variant = calendar.Date == quickDate ? ButtonVariant.Solid : ButtonVariant.Soft;
        }
        calendar.ValueChanged += (_, _) => Sync();
        Sync();
        var side = Build.Card(new CardControl { Header = "上海 → 成都", MinimumSize = new Size(16 * 14, 0) },
            Stack(Space.Md, Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm }, new IconControl { Data = Lucide.PlaneTakeoff }, summary), days, buttons));
        return Row(calendar, side);
    }

    static Control Footer()
    {
        var picks = 0;
        var calendar = new CalendarControl { DefaultFocused = "2026-12-01", AccessibleName = "提醒日期" };
        var today = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Text = "今天" };
        var clear = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Text = "清空" };
        today.Click += (_, _) => calendar.Date = Today;
        clear.Click += (_, _) => calendar.Value = null;
        calendar.Footer = Build.Flex(new FlexControl { Gap = Space.Xs, Justify = FlexJustify.Between }, today, clear);
        var note = new SmallControl { Tone = TextTone.Muted };
        void Sync()
        {
            clear.Enabled = calendar.Value is not null;
            note.Text = $"{calendar.Value ?? "没有选择（一开始显示 2026 年 12 月）"} · 点选了 {picks} 次";
        }
        calendar.Picked += (_, _) =>
        {
            picks++;
            Sync();
        };
        calendar.ValueChanged += (_, _) => Sync();
        Sync();
        return Stack(Space.Sm, Build.Card(new CardControl { CardPadding = Space.Sm }, calendar), note);
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
        return Columns("15rem", cases.Select(entry =>
        {
            var calendar = new CalendarControl { Date = Today, FirstDayOfWeek = entry.First, ShowWeekNumbers = entry.Lang == german, AccessibleName = entry.Label };
            // 从右往左：整个日历镜像，翻页箭头跟着翻转
            if (entry.Lang.Direction == "rtl") calendar.RightToLeft = RightToLeft.Yes;
            var provider = new ProviderControl { Lang = entry.Lang, AutoSize = true };
            provider.Controls.Add(calendar);
            return Labeled(entry.Label, provider);
        }));
    }

    static Control Multiple()
    {
        var calendar = new CalendarControl { SelectionMode = CalendarSelectionMode.Multiple, VisibleMonths = 2, ShowWeekNumbers = true, AccessibleName = "值班日期" };
        var clear = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Text = "清空" };
        clear.Click += (_, _) => calendar.Values = [];
        var tags = new FlexControl { Gap = Space.Xs, Wrap = true };
        var empty = new TextControl { Tone = TextTone.Muted, Text = "点日历上的日子加入排班，再点一次取消" };
        var side = Build.Card(new CardControl { Extra = clear, MinimumSize = new Size(14 * 14, 0) }, Stack(Space.Xs, tags, empty));
        void Sync()
        {
            var sorted = calendar.Values.Order(StringComparer.Ordinal).ToList();
            side.Header = $"值班 {sorted.Count} 天";
            clear.Enabled = sorted.Count > 0;
            empty.Visible = sorted.Count == 0;
            foreach (var old in tags.Controls.OfType<TagControl>().ToList()) old.Dispose();
            foreach (var iso in sorted)
            {
                var date = DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var tag = new TagControl { Text = date.ToString("M/d ddd", Chinese) };
                tag.RemoveRequested += (_, _) => calendar.Values = [.. calendar.Values.Where(item => item != iso)];
                tags.Controls.Add(tag);
            }
        }
        calendar.ValueChanged += (_, _) => Sync();
        Sync();
        return Row(calendar, side);
    }

    static Control MultipleMonths()
    {
        var calendar = new CalendarControl { Date = Today, VisibleMonths = 3, AccessibleName = "上线日期" };
        var text = new TextControl { Tone = TextTone.Muted };
        void Sync() => text.Text = $"上线日期：{(calendar.Date is { } date ? Full(date) : "未定")}";
        calendar.ValueChanged += (_, _) => Sync();
        Sync();
        return Stack(Space.Md, calendar, text);
    }

    static Control Prices()
    {
        var today = Today;
        var soldOut = new HashSet<DateOnly> { today.AddDays(5), today.AddDays(6) };
        // 周五、周六更贵；按日期给一点起伏，演示数据确定不变
        static int PriceOf(DateOnly date) => (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? 699 : 459) + date.Day % 4 * 20;
        var lowest = Enumerable.Range(0, 35).Min(days => PriceOf(today.AddDays(days)));
        var calendar = new CalendarControl
        {
            Min = Iso(today),
            Max = Iso(today.AddDays(34)),
            IsDateUnavailable = soldOut.Contains,
            // 格子放得下价格：比默认的 2.25em 宽、高一些
            CellSizeEm = 3.25f,
            // 读屏已经念了完整日期，数字不重复念；价格照常念
            CellText = (date, cell) => cell.Disabled || cell.Outside ? "" : cell.Unavailable ? "满" : $"¥{PriceOf(date)}",
            AccessibleName = "入住日期",
        };
        calendar.DrawCell += (_, e) =>
        {
            e.Handled = true;
            var context = ElementContext.Of(calendar);
            var colors = context.Theme.Colors;
            var day = e.Date.Day.ToString(CultureInfo.InvariantCulture);
            var off = e.State.Disabled || e.State.Outside;
            var small = new Font(e.Font.FontFamily, e.Font.SizeInPoints * 0.7f, e.State.Selected || PriceOf(e.Date) != lowest ? FontStyle.Regular : FontStyle.Bold);
            var top = e.Bounds with { Height = e.Bounds.Height * 3 / 5 };
            var bottom = new Rectangle(e.Bounds.X, top.Bottom - e.Bounds.Height / 10, e.Bounds.Width, e.Bounds.Height * 2 / 5);
            TextRenderer.DrawText(e.Graphics, day, e.Font, off ? e.Bounds : top, e.ForeColor, TextFormatFlags.HorizontalCenter | (off ? TextFormatFlags.VerticalCenter : TextFormatFlags.Bottom));
            if (off) return;
            // 选中的格子是品牌色实底：价格跟着用实底上的文字色
            var color = e.State.Selected ? e.ForeColor : PriceOf(e.Date) == lowest ? colors.SuccessText : colors.TextMuted;
            TextRenderer.DrawText(e.Graphics, e.State.Unavailable ? "满" : $"¥{PriceOf(e.Date)}", small, bottom, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
            small.Dispose();
        };
        var text = new TextControl { Tone = TextTone.Muted };
        void Sync() => text.Text = calendar.Date is { } night
            ? $"{night.ToString("M月d日 ddd", Chinese)} 入住，一晚 ¥{PriceOf(night)}"
            : $"最低 ¥{lowest} 起，周五、周六更贵";
        calendar.ValueChanged += (_, _) => Sync();
        Sync();
        return Stack(Space.Md, calendar, text);
    }

    static Control Sizes()
    {
        (string Label, Action<CalendarControl> Apply)[] cases =
        [
            ("TextSize=Sm：侧栏、筛选面板", calendar => calendar.TextSize = TextSize.Sm),
            ("TextSize=Md", calendar => calendar.TextSize = TextSize.Md),
            ("TextSize=Lg：触屏、大屏", calendar => calendar.TextSize = TextSize.Lg),
            ("IsReadOnly：能翻页、不能改", calendar => calendar.IsReadOnly = true),
            ("Enabled=false", calendar => calendar.Enabled = false),
        ];
        return Columns("16rem", cases.Select(entry =>
        {
            var calendar = new CalendarControl { Date = Today, AccessibleName = entry.Label };
            entry.Apply(calendar);
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
        var calendar = new CalendarControl { Min = Iso(today), Max = Iso(last), IsDateUnavailable = date => Reason(date) is not null, AccessibleName = "预约日期" };
        var picked = new TextControl();
        void Sync() => picked.Text = calendar.Date is { } date ? $"已选：{Full(date)}" : "选一个可预约的工作日";
        calendar.ValueChanged += (_, _) => Sync();
        Sync();
        var side = Build.Card(new CardControl { Header = "3 号会议室 · 12 人", MinimumSize = new Size(16 * 14, 0) },
            Stack(Space.Sm,
                picked,
                new TextControl { Tone = TextTone.Muted, TextSize = TextSize.Sm, Text = $"可预约：{today.ToString("M月d日", Chinese)} 至 {last.ToString("M月d日", Chinese)}" },
                Build.Flex(new FlexControl { Gap = Space.Sm, Wrap = true },
                    new BadgeControl { Variant = StatusVariant.Soft, Tone = StatusTone.Neutral, Text = "周末不开放" },
                    new BadgeControl { Variant = StatusVariant.Soft, Tone = StatusTone.Warning, Text = $"{holiday.ToString("M/d", Chinese)} 节假日" },
                    new BadgeControl { Variant = StatusVariant.Soft, Tone = StatusTone.Danger, Text = $"{fullyBooked.Count} 天已约满" })));
        return Row(calendar, side);
    }

    static Control Quarter()
    {
        var single = new QuarterCalendarControl { Value = QuarterState.Of(Today), Min = "2024-01-01", AccessibleName = "考核季度" };
        var span = new QuarterCalendarControl { SelectionMode = CalendarSelectionMode.Range, AccessibleName = "对比区间" };
        var singleText = new TextControl { TextSize = TextSize.Sm };
        var spanText = new TextControl { TextSize = TextSize.Sm };
        void Sync()
        {
            singleText.Text = single.Value ?? "选一个季度";
            spanText.Text = span.RangeValue is { } range ? $"{range.Start} 至 {range.End}" : "先选开始、再选结束";
        }
        single.ValueChanged += (_, _) => Sync();
        span.ValueChanged += (_, _) => Sync();
        Sync();
        return Columns("16rem",
        [
            Stack(Space.Xs, new SmallControl { Tone = TextTone.Muted, Text = "单选（2024 年以前不可选）" }, single, singleText),
            Stack(Space.Xs, new SmallControl { Tone = TextTone.Muted, Text = "区间：SelectionMode=Range" }, span, spanText),
        ]);
    }

    static Control Views()
    {
        Control Case(string label, CalendarControl calendar, Func<DateOnly?, string> describe)
        {
            calendar.AccessibleName = label;
            var text = new TextControl { TextSize = TextSize.Sm };
            void Sync() => text.Text = describe(calendar.Date);
            calendar.ValueChanged += (_, _) => Sync();
            Sync();
            return Stack(Space.Xs, new SmallControl { Tone = TextTone.Muted, Text = label }, calendar, text);
        }
        return Columns("15rem",
        [
            Case("生日：点标题\"1995年6月\"切到月份、年份视图", new CalendarControl { Value = "1995-06-18", Max = "2026-12-31" }, date => date?.ToString("D", Chinese) ?? ""),
            Case("账期：Granularity=Month", new CalendarControl { Granularity = DateUnit.Month }, date => date?.ToString("Y", Chinese) ?? "选一个月"),
            Case("入学年份：Granularity=Year", new CalendarControl { Granularity = DateUnit.Year, Min = "2000-01-01" }, date => date is { } year ? $"{year.Year}年" : "选一年"),
        ]);
    }
}
