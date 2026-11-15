using System.Globalization;

namespace Rune.WinForms.Examples;

/// <summary>RangeCalendar 的示例（vue-tsx-pro 文档站 date/RangeCalendar）。WinForms 里是 RangeCalendarControl；上游 Display 示例里的 valueType="dateValue" 没有对应：值总是 IsoDateRange，要日期时读 Range。</summary>
static class RangeCalendarExamples
{
    public static Example[] All =>
    [
        new("RangeCalendar", "display", "显示与只读", "FirstDayOfWeek 一周从哪天开始、ShowWeekNumbers 周数、FixedWeeks=false 按实际行数；IsReadOnly 能翻页不能改；DefaultFocused 没有值时先显示哪个月；Picked 每选完一个区间发出。", Display),
        new("RangeCalendar", "hotel", "订酒店", "并排两个月选入住与离店，订满的晚上不能被夹在中间；右侧算出几晚、总价。", Hotel),
        new("RangeCalendar", "leave", "请假", "周末不可选，但请假区间可以跨过周末（AllowsUnavailableInRange），只算工作日。", Leave),
        new("RangeCalendar", "report", "报表的统计区间", "页脚放快捷区间（CalendarState.PresetRange 按今天与一周的第一天算，名字取语言包），今天之后不能选。", Report),
    ];

    static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");

    static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static FlexControl Stack(Space gap, params Control[] children) => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = gap, Align = FlexAlign.Start }, children);

    static FlexControl Row(Space gap, params Control[] children) => Build.Flex(new FlexControl { Gap = gap, Wrap = true, Align = FlexAlign.Center }, children);

    static Control Display()
    {
        var calendar = new RangeCalendarControl { DefaultFocused = Iso(new DateOnly(Today.Year, Today.Month, 1).AddMonths(1)), FirstDayOfWeek = DayOfWeek.Monday, ShowWeekNumbers = true, AccessibleName = "活动档期" };
        var firstDay = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm, AccessibleName = "一周的第一天" };
        firstDay.Controls.AddRange([new ToggleItemControl { Value = "1", Text = "周一开始" }, new ToggleItemControl { Value = "0", Text = "周日开始" }]);
        firstDay.Value = "1";
        firstDay.ValueChanged += (_, _) => calendar.FirstDayOfWeek = firstDay.Value == "0" ? DayOfWeek.Sunday : DayOfWeek.Monday;
        SwitchControl Toggle(string label, bool on, Action<bool> apply)
        {
            var toggle = new SwitchControl { Text = label, IsChecked = on };
            toggle.IsCheckedChanged += (_, _) => apply(toggle.IsChecked);
            return toggle;
        }
        var picks = 0;
        var note = new SmallControl { Tone = TextTone.Muted };
        void Sync() => note.Text = $"{(calendar.Value is { } range ? $"{range.Start} – {range.End}" : "还没选")} · 选完 {picks} 次";
        calendar.Picked += (_, _) =>
        {
            picks++;
            Sync();
        };
        calendar.ValueChanged += (_, _) => Sync();
        Sync();
        return Stack(Space.Md,
            Row(Space.Lg, firstDay, Toggle("周数", true, on => calendar.ShowWeekNumbers = on), Toggle("固定 6 行", true, on => calendar.FixedWeeks = on), Toggle("只读", false, on => calendar.IsReadOnly = on)),
            calendar,
            note);
    }

    static Control Hotel()
    {
        const int nightly = 528;
        var today = Today;
        var booked = new HashSet<DateOnly> { today.AddDays(9), today.AddDays(10), today.AddDays(23) };
        var calendar = new RangeCalendarControl { Range = new DateRange(today.AddDays(2), today.AddDays(5)), VisibleMonths = 2, Min = Iso(today), IsDateUnavailable = booked.Contains, AccessibleName = "入住与离店" };
        var stay = new TextControl();
        var detail = new TextControl { Tone = TextTone.Muted, TextSize = TextSize.Sm };
        var total = new StrongControl { TextSize = TextSize.Lg };
        var book = new ButtonControl { Text = "预订" };
        static string Day(DateOnly date) => date.ToString("M月d日 ddd", Chinese);
        void Sync()
        {
            var nights = calendar.Range is { } range ? range.End.DayNumber - range.Start.DayNumber : 0;
            stay.Text = calendar.Range is { } picked ? $"{Day(picked.Start)} 入住 → {Day(picked.End)} 离店" : "先点入住，再点离店";
            detail.Text = $"{nights} 晚 × ¥{nightly}";
            detail.Visible = nights > 0;
            total.Text = $"¥{nights * nightly:N0}";
            book.Enabled = nights > 0;
        }
        calendar.ValueChanged += (_, _) => Sync();
        Sync();
        var side = Build.Card(new CardControl { Header = "湖景大床房", MinimumSize = new Size(15 * 14, 0) },
            Stack(Space.Sm, Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm }, new IconControl { Data = Lucide.BedDouble }, stay), detail, total, book));
        return Build.Flex(new FlexControl { Gap = Space.Xl, Wrap = true, Align = FlexAlign.Start }, calendar, side);
    }

    static Control Leave()
    {
        static bool Weekend(DateOnly date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var calendar = new RangeCalendarControl { VisibleMonths = 2, IsDateUnavailable = Weekend, AllowsUnavailableInRange = true, AccessibleName = "请假日期" };
        var text = new TextControl();
        var badge = new BadgeControl { Variant = StatusVariant.Soft };
        void Sync()
        {
            text.Text = calendar.Value is { } range ? $"{range.Start} 至 {range.End}" : "选开始与结束的工作日";
            badge.Visible = calendar.Range is not null;
            if (calendar.Range is { } dates)
                badge.Text = $"共 {Enumerable.Range(0, dates.End.DayNumber - dates.Start.DayNumber + 1).Count(offset => !Weekend(dates.Start.AddDays(offset)))} 个工作日";
        }
        calendar.ValueChanged += (_, _) => Sync();
        Sync();
        return Stack(Space.Md, calendar, Row(Space.Sm, text, badge));
    }

    static Control Report()
    {
        DateRangePreset[] presets = [DateRangePreset.Today, DateRangePreset.Last7Days, DateRangePreset.Last30Days, DateRangePreset.ThisMonth, DateRangePreset.LastMonth];
        var today = Today;
        var calendar = new RangeCalendarControl { VisibleMonths = 2, Max = Iso(today), AccessibleName = "统计区间" };
        // 快捷区间按今天与这个日历的一周第一天算；名字取语言包（放进界面才知道所在的作用域）
        DateRange Preset(DateRangePreset preset) => CalendarState.PresetRange(preset, today, CalendarText.FirstDayOf(ProviderControl.LangOf(calendar), calendar.FirstDayOfWeek));
        calendar.Range = CalendarState.PresetRange(DateRangePreset.Last7Days, today, DayOfWeek.Monday);
        var footer = new FlexControl { Gap = Space.Xs, Wrap = true };
        var buttons = presets.Select(preset =>
        {
            var button = new ButtonControl { TextSize = TextSize.Sm, Tone = StatusTone.Neutral };
            button.Click += (_, _) => calendar.Range = Preset(preset);
            footer.Controls.Add(button);
            return (button, preset);
        }).ToList();
        calendar.Footer = footer;
        var text = new TextControl { Tone = TextTone.Muted };
        void Sync()
        {
            var lang = ProviderControl.LangOf(calendar);
            foreach (var (button, preset) in buttons)
            {
                button.Text = CalendarText.PresetName(preset, lang);
                button.Variant = calendar.Range == Preset(preset) ? ButtonVariant.Soft : ButtonVariant.Ghost;
            }
            text.Text = calendar.Range is { } range ? $"统计 {range.Start.ToString("D", Chinese)} – {range.End.ToString("D", Chinese)}" : "选一个区间";
        }
        calendar.ValueChanged += (_, _) => Sync();
        calendar.HandleCreated += (_, _) => Sync();
        Sync();
        return Stack(Space.Md, calendar, text);
    }
}
