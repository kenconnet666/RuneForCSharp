using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace Rune.Wpf.Examples;

/// <summary>
/// DateRangePicker 的示例（vue-tsx-pro 文档站 date/DateRangePicker）。上游的 valueType="dateValue" 在 C# 里是 Range（两端 DateOnly）；
/// startName / endName 是原生表单提交用的，C# 里走 Form 的模型。
/// </summary>
static class DateRangePickerExamples
{
    public static Example[] All =>
    [
        new("DateRangePicker", "periods", "按月、按年、按季度、按周与日期时间", "预算期间按月、对比区间按年、滚动四个季度、排班周、精确到分钟的维护窗口：Granularity 决定两端的形式与日历选什么。", Periods),
        new("DateRangePicker", "report", "报表区间与入住离店", "统计区间带快捷区间（内置的与自己的结算周期），今天之后不能选；酒店入住与离店，订满的日子不能夹在中间。", Report),
        new("DateRangePicker", "states", "跨不可选的日期、日期运算与外观状态", "请假的起止只能是工作日，但可以跨周末（AllowsUnavailableInRange）；出差读 Range（DateOnly）算天数；外观与只读、不可用。", States),
    ];

    static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");

    static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static void OnValue(DateRangePicker picker, Action changed) =>
        DependencyPropertyDescriptor.FromProperty(DateRangePicker.ValueProperty, typeof(DateRangePicker)).AddValueChanged(picker, (_, _) => changed());

    static string Show(IsoDateRange? range) => range is null ? "null" : $"{range.Start} – {range.End}";

    static Grid Columns(params FrameworkElement[] children)
    {
        var grid = new Grid { MinItemWidth = new Length("20em"), Gap = Space.Lg };
        foreach (var child in children) grid.Children.Add(child);
        return grid;
    }

    /// <summary>说明里显示当前的值（没有时 fallback）。</summary>
    static Field Shown(string label, DateRangePicker picker, string fallback = "null")
    {
        var field = new Field { Label = label, Content = picker };
        void Sync() => field.Description = picker.Value is null ? fallback : Show(picker.Value);
        OnValue(picker, Sync);
        Sync();
        return field;
    }

    static FrameworkElement Periods() => Columns(
        Shown("预算期间（按月）", new DateRangePicker
        {
            Granularity = DateFieldGranularity.Month,
            Value = new("2026-01", "2026-06"),
            Presets =
            [
                new RangePreset("上半年", new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30))),
                new RangePreset("下半年", new DateRange(new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31))),
                DateRangePreset.ThisYear,
            ],
        }),
        Shown("对比区间（按年）", new DateRangePicker { Granularity = DateFieldGranularity.Year, Value = new("2023", "2026") }),
        Shown("滚动四个季度", new DateRangePicker { Granularity = DateFieldGranularity.Quarter, Value = new("2025-Q4", "2026-Q3") }),
        Shown("排班周（按 ISO 周）", new DateRangePicker { Granularity = DateFieldGranularity.Week, VisibleMonths = 1 }),
        Shown("维护窗口", new DateRangePicker { Granularity = DateFieldGranularity.Minute, VisibleMonths = 1, Value = new("2026-10-24T22:00", "2026-10-25T06:00") }));

    /// <summary>本结算周期：上月 26 日到本月 25 日（今天过了 25 日就是本月 26 日到下月 25 日）。</summary>
    static DateRange SettlementPeriod()
    {
        var today = Today;
        var anchor = today.Day > 25 ? today : today.AddMonths(-1);
        var start = new DateOnly(anchor.Year, anchor.Month, 26);
        return new DateRange(start, start.AddMonths(1).AddDays(-1));
    }

    static FrameworkElement Report()
    {
        var today = Today;
        // 演示数据：从今天往后数的第 5、6 天已订满
        var booked = new HashSet<DateOnly> { today.AddDays(5), today.AddDays(6) };
        var report = new DateRangePicker
        {
            Value = new(Iso(today.AddDays(-6)), Iso(today)),
            Max = Iso(today),
            Presets =
            [
                DateRangePreset.Today,
                DateRangePreset.Last7Days,
                DateRangePreset.Last30Days,
                DateRangePreset.ThisMonth,
                DateRangePreset.LastMonth,
                // 自己的快捷区间：每次打开时按当天算
                new RangePreset("本结算周期", SettlementPeriod),
                DateRangePreset.ThisYear,
            ],
        };
        var stay = new DateRangePicker { Min = Iso(today), IsDateUnavailable = booked.Contains, Clearable = true };
        var stayField = new Field { Label = "入住与离店", Content = stay };
        void Sync() => stayField.Description = stay.Range is { } range
            ? $"{range.Start.ToString("D", Chinese)} 入住，共 {range.End.DayNumber - range.Start.DayNumber} 晚"
            : "订满的日子不能夹在中间";
        OnValue(stay, Sync);
        Sync();
        return Columns(Shown("统计区间", report, "选一个区间"), stayField);
    }

    static FrameworkElement States()
    {
        var leave = new DateRangePicker
        {
            Min = Iso(Today),
            VisibleMonths = 1,
            IsDateUnavailable = date => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            AllowsUnavailableInRange = true,
            Clearable = true,
        };
        var trip = new DateRangePicker { Icon = Lucide.Plane, Radius = Radius.Full };
        var tripField = new Field { Label = "出差", IsRequired = true, Content = trip };
        void Sync()
        {
            tripField.Error = trip.Value is null ? "请选择出差日期" : null;
            tripField.Description = trip.Range is { } range ? $"共 {range.End.DayNumber - range.Start.DayNumber + 1} 天" : "Range 是 DateOnly，选好后算出天数";
        }
        OnValue(trip, Sync);
        Sync();
        IsoDateRange holiday = new("2026-10-01", "2026-10-07");
        return Columns(
            Shown("请假", leave, "起止只能是工作日，可以跨周末"),
            tripField,
            new Field { Label = "Variant=soft · Size=sm", Content = new DateRangePicker { Value = holiday, Variant = ControlVariant.Soft, Size = TextSize.Sm } },
            new Field { Label = "只读", IsReadOnly = true, Content = new DateRangePicker { Value = holiday } },
            new Field { Label = "不可用", IsEnabled = false, Content = new DateRangePicker { Value = holiday } });
    }
}
