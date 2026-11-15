using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>RangeCalendar 的示例（vue-tsx-pro 文档站 date/RangeCalendar）。上游 Display 示例里的 valueType="dateValue" 没有对应：值总是 IsoDateRange，要日期时读 Range。</summary>
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

    static void OnValue(RangeCalendar calendar, Action changed) =>
        DependencyPropertyDescriptor.FromProperty(RangeCalendar.ValueProperty, typeof(RangeCalendar)).AddValueChanged(calendar, (_, _) => changed());

    static Flex Stack(Space gap, params UIElement[] children)
    {
        var flex = new Flex { Direction = FlexDirection.Column, Gap = gap };
        foreach (var child in children) flex.Children.Add(child);
        return flex;
    }

    static Flex Row(Space gap, params UIElement[] children)
    {
        var flex = new Flex { Gap = gap, Wrap = true, Align = FlexAlign.Center };
        foreach (var child in children) flex.Children.Add(child);
        return flex;
    }

    static FrameworkElement Display()
    {
        var calendar = new RangeCalendar { DefaultFocused = Iso(new DateOnly(Today.Year, Today.Month, 1).AddMonths(1)), FirstDayOfWeek = DayOfWeek.Monday, ShowWeekNumbers = true };
        AutomationProperties.SetName(calendar, "活动档期");
        var firstDay = new ToggleGroup
        {
            IsRequired = true,
            Value = "1",
            Size = TextSize.Sm,
            Children = { new ToggleItem { Value = "1", Content = "周一开始" }, new ToggleItem { Value = "0", Content = "周日开始" } },
        };
        AutomationProperties.SetName(firstDay, "一周的第一天");
        DependencyPropertyDescriptor.FromProperty(ToggleGroup.ValueProperty, typeof(ToggleGroup)).AddValueChanged(firstDay, (_, _) =>
            calendar.FirstDayOfWeek = firstDay.Value == "0" ? DayOfWeek.Sunday : DayOfWeek.Monday);
        Switch Toggle(string label, bool on, Action<bool> apply)
        {
            var toggle = new Switch { Content = label, IsChecked = on };
            DependencyPropertyDescriptor.FromProperty(ChoiceBase.IsCheckedProperty, typeof(Switch)).AddValueChanged(toggle, (_, _) => apply(toggle.IsChecked));
            return toggle;
        }
        var picks = 0;
        var note = new Small { Tone = TextTone.Muted };
        void Sync() => note.Text = $"{(calendar.Value is { } range ? $"{range.Start} – {range.End}" : "还没选")} · 选完 {picks} 次";
        calendar.Picked += (_, _) =>
        {
            picks++;
            Sync();
        };
        OnValue(calendar, Sync);
        Sync();
        return Stack(Space.Md,
            Row(Space.Lg, firstDay, Toggle("周数", true, on => calendar.ShowWeekNumbers = on), Toggle("固定 6 行", true, on => calendar.FixedWeeks = on), Toggle("只读", false, on => calendar.IsReadOnly = on)),
            calendar,
            note);
    }

    static FrameworkElement Hotel()
    {
        const int nightly = 528;
        var today = Today;
        var booked = new HashSet<DateOnly> { today.AddDays(9), today.AddDays(10), today.AddDays(23) };
        var calendar = new RangeCalendar { Range = new DateRange(today.AddDays(2), today.AddDays(5)), VisibleMonths = 2, Min = Iso(today), IsDateUnavailable = booked.Contains };
        AutomationProperties.SetName(calendar, "入住与离店");
        var stay = new Text();
        var detail = new Text { Tone = TextTone.Muted, Size = TextSize.Sm };
        var total = new Strong { Size = TextSize.Lg };
        var book = new Button { Content = "预订" };
        static string Day(DateOnly date) => date.ToString("M月d日 ddd", Chinese);
        void Sync()
        {
            var nights = calendar.Range is { } range ? range.End.DayNumber - range.Start.DayNumber : 0;
            stay.Text = calendar.Range is { } picked ? $"{Day(picked.Start)} 入住 → {Day(picked.End)} 离店" : "先点入住，再点离店";
            detail.Text = $"{nights} 晚 × ¥{nightly}";
            detail.Visibility = nights > 0 ? Visibility.Visible : Visibility.Collapsed;
            total.Text = $"¥{nights * nightly:N0}";
            book.IsEnabled = nights > 0;
        }
        OnValue(calendar, Sync);
        Sync();
        var side = new Card
        {
            MinWidth = 15 * 14,
            Header = new Strong { Text = "湖景大床房" },
            Content = Stack(Space.Sm, new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Icon { Data = Lucide.BedDouble }, stay } }, detail, total, book),
        };
        var row = Row(Space.Xl, calendar, side);
        row.Align = FlexAlign.Start;
        return row;
    }

    static FrameworkElement Leave()
    {
        static bool Weekend(DateOnly date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var calendar = new RangeCalendar { VisibleMonths = 2, IsDateUnavailable = Weekend, AllowsUnavailableInRange = true };
        AutomationProperties.SetName(calendar, "请假日期");
        var text = new Text();
        var badge = new Badge { Variant = StatusVariant.Soft };
        void Sync()
        {
            text.Text = calendar.Value is { } range ? $"{range.Start} 至 {range.End}" : "选开始与结束的工作日";
            badge.Visibility = calendar.Range is null ? Visibility.Collapsed : Visibility.Visible;
            if (calendar.Range is { } dates)
                badge.Content = $"共 {Enumerable.Range(0, dates.End.DayNumber - dates.Start.DayNumber + 1).Count(offset => !Weekend(dates.Start.AddDays(offset)))} 个工作日";
        }
        OnValue(calendar, Sync);
        Sync();
        return Stack(Space.Md, calendar, Row(Space.Sm, text, badge));
    }

    static FrameworkElement Report()
    {
        DateRangePreset[] presets = [DateRangePreset.Today, DateRangePreset.Last7Days, DateRangePreset.Last30Days, DateRangePreset.ThisMonth, DateRangePreset.LastMonth];
        var today = Today;
        var calendar = new RangeCalendar { VisibleMonths = 2, Max = Iso(today) };
        AutomationProperties.SetName(calendar, "统计区间");
        // 快捷区间按今天与这个日历的一周第一天算；名字取语言包（进了界面才知道语言）
        DateRange Preset(DateRangePreset preset) => CalendarState.PresetRange(preset, today, CalendarText.FirstDayOf(Provider.GetLang(calendar), calendar.FirstDayOfWeek));
        calendar.Range = CalendarState.PresetRange(DateRangePreset.Last7Days, today, DayOfWeek.Monday);
        var footer = new Flex { Gap = Space.Xs, Wrap = true };
        var buttons = presets.Select(preset =>
        {
            var button = new Button { Size = TextSize.Sm, Tone = StatusTone.Neutral };
            button.Click += (_, _) => calendar.Range = Preset(preset);
            footer.Children.Add(button);
            return (button, preset);
        }).ToList();
        calendar.Footer = footer;
        var text = new Text { Tone = TextTone.Muted };
        void Sync()
        {
            var lang = Provider.GetLang(calendar);
            foreach (var (button, preset) in buttons)
            {
                button.Content = CalendarText.PresetName(preset, lang);
                button.Variant = calendar.Range == Preset(preset) ? ButtonVariant.Soft : ButtonVariant.Ghost;
            }
            text.Text = calendar.Range is { } range ? $"统计 {range.Start.ToString("D", Chinese)} – {range.End.ToString("D", Chinese)}" : "选一个区间";
        }
        OnValue(calendar, Sync);
        calendar.Loaded += (_, _) => Sync();
        Sync();
        return Stack(Space.Md, calendar, text);
    }
}
