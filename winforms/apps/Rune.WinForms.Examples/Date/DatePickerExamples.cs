using System.Globalization;

namespace Rune.WinForms.Examples;

/// <summary>
/// DatePicker 的示例（vue-tsx-pro 文档站 date/DatePicker）。WinForms 里是 DatePickerControl。上游 Values 示例讲 DateValue 值类型与原生表单提交：
/// C# 里值总是 ISO 字符串，要做日期运算时读 Date（DateOnly），提交走 FormControl 的模型。
/// </summary>
static class DatePickerExamples
{
    public static Example[] All =>
    [
        new("DatePicker", "booking", "预约门诊", "只能约今天起 30 天内的工作日，已约满的日子不可选；可以直接键入，也可以点按钮翻日历。", Booking),
        new("DatePicker", "granularity", "选年份、季度、月份、周与时间", "Granularity 决定值的形式与弹出的选择：年份、季度面板、月份、按 ISO 周排的日历、日期加时间；会议只能约在 9:00–18:00。", Granularity),
        new("DatePicker", "presets", "快捷选项与\"今天\"", "日历旁边是常用的快捷选项（按今天算），日历下方是\"今天\"（选月份时是\"本月\"）；今天之前不能选，所以\"昨天\"不可用。", Presets),
        new("DatePicker", "states", "外观与状态", "外观（Variant、TextSize、Radius、图标）与状态（只读、不可用、出错、必填）同其他输入框；Placement 让日历弹在上方，VisibleMonths 并排两个月。", States),
        new("DatePicker", "values", "值与日期运算", "值是 ISO 字符串（同表单与接口）；要做日期运算时读 Date（DateOnly）：生日后的第一百天。", Values),
    ];

    static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");

    static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static Control Columns(string minWidth, params Control[] children) => Build.Grid(new GridControl { MinItemWidth = minWidth, Gap = Space.Lg }, children);

    /// <summary>说明里显示当前的值（没有时 fallback）。</summary>
    static FieldControl Shown(string label, DatePickerControl picker, string fallback = "null")
    {
        var field = new FieldControl { Label = label, Content = picker };
        void Sync() => field.Description = picker.Value ?? fallback;
        picker.ValueChanged += (_, _) => Sync();
        Sync();
        return field;
    }

    static Control Booking()
    {
        var today = Today;
        var last = today.AddDays(30);
        // 演示数据：从今天往后数的第 3、4、11 天已约满
        var fullyBooked = new HashSet<DateOnly> { today.AddDays(3), today.AddDays(4), today.AddDays(11) };
        var visit = new DatePickerControl { Min = Iso(today), Max = Iso(last), IsDateUnavailable = date => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || fullyBooked.Contains(date) };
        var birthday = new DatePickerControl { Value = "1992-08-16", Max = Iso(today), Clearable = true };
        var visitField = new FieldControl { Label = "就诊日期", IsRequired = true, Content = visit };
        var result = new TextControl { Tone = TextTone.Muted, TextSize = TextSize.Sm, Wrap = TextWrap.Wrap };
        void Sync()
        {
            visitField.Description = visit.Date is { } date ? CalendarText.FullDate(date, Chinese) : "30 天内的工作日，周末与约满的日子不可选";
            result.Text = $"提交的值：visit={visit.Value ?? "null"}，birthday={birthday.Value ?? "null"}";
        }
        visit.ValueChanged += (_, _) => Sync();
        birthday.ValueChanged += (_, _) => Sync();
        Sync();
        return Columns("16rem", visitField, new FieldControl { Label = "出生日期", Description = "年份久远时直接键入比翻日历快", Content = birthday }, result);
    }

    static Control Granularity()
    {
        var meeting = new DatePickerControl
        {
            Granularity = DateFieldGranularity.Minute,
            Value = "2026-10-23T14:30",
            // 工作时间之外键入时显示出错
            IsTimeUnavailable = (time, _) => time < new TimeOnly(9, 0) || time >= new TimeOnly(18, 0),
        };
        return Columns("15rem",
            Shown("财年", new DatePickerControl { Granularity = DateFieldGranularity.Year, Value = "2026" }),
            Shown("考核季度", new DatePickerControl { Granularity = DateFieldGranularity.Quarter, Value = "2026-Q4" }),
            Shown("账期", new DatePickerControl { Granularity = DateFieldGranularity.Month, Value = "2026-10" }),
            Shown("周报", new DatePickerControl { Granularity = DateFieldGranularity.Week, Value = "2026-W43" }),
            Shown("会议开始", meeting, "只能约在 9:00–18:00"));
    }

    static Control Presets()
    {
        static string Shift(int days) => Iso(DateOnly.FromDateTime(DateTime.Today).AddDays(days));
        // 下个月的 1 日、这个月的最后一天：每次打开时按当天算
        static DateOnly NextMonthStart() => new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
        var delivery = new DatePickerControl
        {
            Min = Iso(Today),
            Presets =
            [
                new DatePreset("昨天", () => Shift(-1)),
                new DatePreset("明天", () => Shift(1)),
                new DatePreset("一周后", () => Shift(7)),
                new DatePreset("月底", () => Iso(NextMonthStart().AddDays(-1))),
                new DatePreset("下月初", () => Iso(NextMonthStart())),
            ],
        };
        return Columns("16rem",
            Shown("交货日期", delivery, "今天之前不能选"),
            Shown("账期", new DatePickerControl { Granularity = DateFieldGranularity.Month }, "日历下方是\"本月\""));
    }

    static Control States()
    {
        var delivery = new DatePickerControl { Clearable = true };
        var deliveryField = new FieldControl { Label = "交付日期", IsRequired = true, Description = "必填；选好后错误消失", Content = delivery };
        void Sync() => deliveryField.Error = delivery.Value is null ? "请选择交付日期" : null;
        delivery.ValueChanged += (_, _) => Sync();
        Sync();
        return Columns("15rem",
            new FieldControl { Label = "Variant=soft · TextSize=Sm", Content = new DatePickerControl { Value = "2026-10-30", Variant = ControlVariant.Soft, TextSize = TextSize.Sm } },
            new FieldControl { Label = "Radius=full · TextSize=Lg 加图标", Content = new DatePickerControl { Value = "2026-10-30", Radius = Radius.Full, TextSize = TextSize.Lg, Icon = Lucide.CalendarClock } },
            new FieldControl { Label = "只读", IsReadOnly = true, Description = "能聚焦、能复制，不能改，也不弹日历", Content = new DatePickerControl { Value = "2026-10-01" } },
            new FieldControl { Label = "不可用", Enabled = false, Content = new DatePickerControl { Value = "2026-10-01" } },
            deliveryField,
            new FieldControl { Label = "日历弹在上方、并排两个月", Content = new DatePickerControl { Placement = FloatingPlacement.TopStart, VisibleMonths = 2 } });
    }

    static Control Values()
    {
        var birthday = new DatePickerControl();
        var joined = new DatePickerControl { Value = "2021-03-15" };
        var birthdayField = new FieldControl { Label = "生日", Content = birthday };
        var joinedField = new FieldControl { Label = "入职日期", Content = joined };
        void Sync()
        {
            birthdayField.Description = birthday.Date is { } day
                ? $"{CalendarText.FullDate(day, Chinese)}；一百天是 {day.AddDays(100).ToString("D", Chinese)}"
                : "Date 是 DateOnly，可以直接 AddDays、比较";
            joinedField.Description = $"Value = {joined.Value ?? "null"}";
        }
        birthday.ValueChanged += (_, _) => Sync();
        joined.ValueChanged += (_, _) => Sync();
        Sync();
        return Columns("15rem", birthdayField, joinedField);
    }
}
