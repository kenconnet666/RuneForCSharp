using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Rune.WinForms;

/// <summary>分段输入里的一段（vue-tsx-pro 的 DateSegmentType）。</summary>
public enum DateSegmentType
{
    /// <summary>年。</summary>
    Year,

    /// <summary>月。</summary>
    Month,

    /// <summary>日。</summary>
    Day,

    /// <summary>季度（1–4）。</summary>
    Quarter,

    /// <summary>一年里的第几周（ISO 8601：周一开始，含 1 月 4 日的那周是第 1 周）。</summary>
    Week,

    /// <summary>时（值总是 0–23，12 小时制时显示 1–12）。</summary>
    Hour,

    /// <summary>分。</summary>
    Minute,

    /// <summary>秒。</summary>
    Second,

    /// <summary>上午 / 下午（12 小时制时自动加上；值 0 上午、1 下午）。</summary>
    DayPeriod,
}

/// <summary>分段输入精确到哪一段（vue-tsx-pro 的 DateFieldGranularity）。</summary>
public enum DateFieldGranularity
{
    /// <summary>只有年：2026。</summary>
    Year,

    /// <summary>年月：2026-10。</summary>
    Month,

    /// <summary>年月日（默认）：2026-10-20。</summary>
    Day,

    /// <summary>年与季度：2026-Q4。</summary>
    Quarter,

    /// <summary>年与周（ISO 8601）：2026-W43。</summary>
    Week,

    /// <summary>日期加时：2026-10-20T14:00。</summary>
    Hour,

    /// <summary>日期加时分：2026-10-20T14:30。</summary>
    Minute,

    /// <summary>日期加时分秒：2026-10-20T14:30:15。</summary>
    Second,
}

/// <summary>分段输入的各个字段（vue-tsx-pro 的 DateFieldValues）：没填的是 null；时是 0–23。</summary>
public sealed record DateFieldValues
{
    /// <summary>年。</summary>
    public int? Year { get; init; }

    /// <summary>月。</summary>
    public int? Month { get; init; }

    /// <summary>日。</summary>
    public int? Day { get; init; }

    /// <summary>季度。</summary>
    public int? Quarter { get; init; }

    /// <summary>周。</summary>
    public int? Week { get; init; }

    /// <summary>时（0–23）。</summary>
    public int? Hour { get; init; }

    /// <summary>分。</summary>
    public int? Minute { get; init; }

    /// <summary>秒。</summary>
    public int? Second { get; init; }

    /// <summary>取一段的值（DayPeriod 没有字段，是 null）。</summary>
    /// <param name="type">段。</param>
    public int? this[DateSegmentType type] => type switch
    {
        DateSegmentType.Year => Year,
        DateSegmentType.Month => Month,
        DateSegmentType.Day => Day,
        DateSegmentType.Quarter => Quarter,
        DateSegmentType.Week => Week,
        DateSegmentType.Hour => Hour,
        DateSegmentType.Minute => Minute,
        DateSegmentType.Second => Second,
        _ => null,
    };

    /// <summary>换一段的值。</summary>
    /// <param name="type">段（不是 DayPeriod）。</param>
    /// <param name="value">新的值；null 是清空。</param>
    public DateFieldValues With(DateSegmentType type, int? value) => type switch
    {
        DateSegmentType.Year => this with { Year = value },
        DateSegmentType.Month => this with { Month = value },
        DateSegmentType.Day => this with { Day = value },
        DateSegmentType.Quarter => this with { Quarter = value },
        DateSegmentType.Week => this with { Week = value },
        DateSegmentType.Hour => this with { Hour = value },
        DateSegmentType.Minute => this with { Minute = value },
        DateSegmentType.Second => this with { Second = value },
        _ => this,
    };
}

/// <summary>渲染用的一段（vue-tsx-pro 的 DateSegment）。</summary>
/// <param name="Type">段的类型；分隔符（/、:、年）是 null。</param>
/// <param name="Text">显示的文字：值、正在输入的数字，或占位文字。</param>
/// <param name="IsPlaceholder">还没填：显示的是占位文字。</param>
/// <param name="Value">当前值（DayPeriod 是 0 上午 / 1 下午）；没填时 null。</param>
/// <param name="Min">最小值。</param>
/// <param name="Max">最大值。</param>
/// <param name="Label">段的名称（年、月、日 / year、month、day），给读屏。</param>
public sealed record DateSegment(DateSegmentType? Type, string Text, bool IsPlaceholder, int? Value, int Min, int Max, string Label)
{
    /// <summary>是分隔符。</summary>
    public bool IsLiteral => Type is null;
}

/// <summary>分段输入里与平台无关的按键（DateSegments.HandleKey）。</summary>
public enum DateSegmentKey
{
    /// <summary>↑：加一。</summary>
    Up,

    /// <summary>↓：减一。</summary>
    Down,

    /// <summary>Page Up：大步加。</summary>
    PageUp,

    /// <summary>Page Down：大步减。</summary>
    PageDown,

    /// <summary>Home：最小值。</summary>
    Home,

    /// <summary>End：最大值。</summary>
    End,

    /// <summary>Backspace / Delete：删一位。</summary>
    Backspace,

    /// <summary>→：下一段（从右到左排版时上一段）。</summary>
    Right,

    /// <summary>←：上一段（从右到左排版时下一段）。</summary>
    Left,
}

/// <summary>
/// 分段输入的状态（vue-tsx-pro 的 useDateSegments，参考 React Aria 的 useDateFieldState）：DateField、TimeField 共用，不碰界面。
/// 控件把每段画成可聚焦的元素，按键、输入交给这里，焦点该去哪一段由 <see cref="FocusedSegment"/> 告诉控件。
/// <list type="bullet">
/// <item>段的顺序、分隔符取自区域格式（zh-CN 年/月/日，en-US 月/日/年，de-DE 日.月.年），12 / 24 小时制按语言或 HourCycle；</item>
/// <item>逐段输入：打一个数字后，再打一个就超出范围时直接跳到下一段（月份打 3 → 03 跳走，打 1 → 等第二位）；</item>
/// <item>↑↓ 加减（到头时回绕），Page Up / Down 大步，Home / End 到最小 / 最大，Backspace 删一位、删空后回到上一段；</item>
/// <item>所有段都填了才写回值；填好的值删掉一段时写回 null。认全角数字与阿拉伯-印度数字。</item>
/// </list>
/// 上游的段名取自 Intl.DisplayNames，.NET 没有对应：中文、英文按内置的名称（其余语言用英文）。
/// </summary>
public sealed class DateSegments
{
    /// <summary>Page Up / Down 的步长（同 React Aria）。</summary>
    static readonly Dictionary<DateSegmentType, int> BigSteps = new()
    {
        [DateSegmentType.Year] = 5,
        [DateSegmentType.Month] = 2,
        [DateSegmentType.Day] = 7,
        [DateSegmentType.Quarter] = 2,
        [DateSegmentType.Week] = 4,
        [DateSegmentType.Hour] = 2,
        [DateSegmentType.Minute] = 15,
        [DateSegmentType.Second] = 15,
        [DateSegmentType.DayPeriod] = 1,
    };

    // 正在编辑的各字段：可以只填了一部分
    DateFieldValues draft;

    // 当前段已经打了的数字（还可能接着打第二位）
    string buffer = "";

    // 年份没打满四位：草稿里有了，先不写回（逐位打 2、0、2、6 时不写出 0002、0020、0202）
    bool pendingYear;

    /// <summary>新建：草稿取当前的值。</summary>
    /// <param name="fields">有哪些段（不含 DayPeriod，12 小时制时自动加）。</param>
    public DateSegments(IReadOnlyList<DateSegmentType> fields)
    {
        Fields = fields;
        draft = new DateFieldValues();
    }

    /// <summary>有哪些段（不含 DayPeriod）。</summary>
    public IReadOnlyList<DateSegmentType> Fields { get; set; }

    /// <summary>语言：段的顺序、分隔符、上午下午的写法；默认当前界面文化。</summary>
    public CultureInfo Culture { get; set; } = CultureInfo.CurrentUICulture;

    /// <summary>"h12" 或 "h23"；不写时按语言。</summary>
    public string? HourCycle { get; set; }

    /// <summary>完整的值（外部的）；没有时 null。改了以后调 <see cref="Sync"/>。</summary>
    public Func<DateFieldValues?> Value { get; set; } = () => null;

    /// <summary>所有段都填了时写回完整的值，填好的值删掉一段时写回 null。</summary>
    public Action<DateFieldValues?> Changed { get; set; } = _ => { };

    /// <summary>↑↓ 从空段开始加减时的起点（一般是现在）。</summary>
    public Func<DateFieldValues> PlaceholderValue { get; set; } = () => FromDateTime(DateTime.Now);

    /// <summary>各段的占位文字（年、月、日 / yyyy、mm、dd）。</summary>
    public IReadOnlyDictionary<string, string> Placeholders { get; set; } = new Dictionary<string, string>();

    /// <summary>季度、周这两种段的排法（如 <c>{year}年第{quarter}季度</c>）；不写时是 <c>{year}-{quarter}</c>。</summary>
    public (string Quarter, string Week)? PeriodPatterns { get; set; }

    /// <summary>只读或不可用：不响应输入。</summary>
    public Func<bool> ReadOnly { get; set; } = () => false;

    /// <summary>焦点要越过这组的两端时（最后一段输入满了；第一段删空后再删）：日期区间把焦点交给另一组。</summary>
    public Action<int>? Exit { get; set; }

    /// <summary>焦点应该在的段：输入满了跳到下一段、Backspace 删空回到上一段时变化。</summary>
    public DateSegmentType? FocusedSegment { get; private set; }

    /// <summary>有段填了、但还没填全。</summary>
    public bool Partial => !Complete && Fields.Any(type => draft[type] is not null);

    bool Complete => Fields.All(type => draft[type] is not null);

    bool Hour12 => (HourCycle ?? DefaultHourCycle(Culture)) == "h12";

    /// <summary>段的列表（含 12 小时制自动加的 DayPeriod）。</summary>
    IReadOnlyList<DateSegmentType> AllFields => Hour12 && Fields.Contains(DateSegmentType.Hour) ? [.. Fields, DateSegmentType.DayPeriod] : Fields;

    /// <summary>外部的值变了（父级改了、清空了）：以它为准；自己写回的值也会回到这里，结果相同。</summary>
    public void Sync()
    {
        var value = Value();
        if (Same(value, Complete ? draft : null)) return;
        draft = value ?? new DateFieldValues();
        buffer = "";
    }

    /// <summary>各段，含分隔符，按语言的顺序。</summary>
    public IReadOnlyList<DateSegment> Segments
    {
        get
        {
            var layout = Layout();
            return [.. layout.Select((part, index) =>
            {
                if (part.Type is not { } type) return new DateSegment(null, part.Literal, false, null, 0, 0, "");
                var value = Shown(type);
                var (min, max) = Range(type);
                var filled = Text(type, value);
                var following = index + 1 < layout.Count ? layout[index + 1].Literal : null;
                return new DateSegment(type, filled.Length > 0 ? filled : PlaceholderOf(type, following), filled.Length == 0, value, min, max, LabelOf(type));
            })];
        }
    }

    /// <summary>可以输入的段（不含分隔符），Tab 与 ←→ 的顺序。</summary>
    public IReadOnlyList<DateSegment> Editable => [.. Segments.Where(segment => !segment.IsLiteral)];

    /// <summary>焦点进了某一段（控件的 GotFocus）；离开时传 null。</summary>
    /// <param name="type">段。</param>
    public void SetFocused(DateSegmentType? type)
    {
        if (type != FocusedSegment)
        {
            Commit();
            buffer = "";
        }
        FocusedSegment = type;
    }

    /// <summary>在焦点段输入文字（数字、a / p）：逐个字符处理。</summary>
    /// <param name="input">输入的文字。</param>
    public void Type(string input)
    {
        if (FocusedSegment is null || ReadOnly()) return;
        foreach (var c in NormalizeDigits(input))
        {
            if (FocusedSegment is not { } segment) return;
            if (segment == DateSegmentType.DayPeriod)
            {
                var lower = char.ToLower(c, Culture).ToString();
                var (am, pm) = Periods();
                if (lower == "a" || am.ToLower(Culture).StartsWith(lower, StringComparison.Ordinal)) Write(DateSegmentType.DayPeriod, 0);
                else if (lower == "p" || pm.ToLower(Culture).StartsWith(lower, StringComparison.Ordinal)) Write(DateSegmentType.DayPeriod, 1);
                else continue;
                MoveFocus(1);
                continue;
            }
            if (c is < '0' or > '9') continue;
            var (min, max) = Range(segment);
            var next = buffer + c;
            // 打了这一位就超出范围：从这一位重新开始
            if (int.Parse(next, CultureInfo.InvariantCulture) > max) next = c.ToString();
            buffer = next;
            var number = int.Parse(next, CultureInfo.InvariantCulture);
            // 0 还不是合法值（月、日、12 小时制的时）：先记着，等下一位；年份打满四位才写回
            if (number >= min) Write(segment, number, segment != DateSegmentType.Year || next.Length >= 4);
            if (next.Length >= max.ToString(CultureInfo.InvariantCulture).Length || number * 10 > max)
            {
                buffer = "";
                if (number < min) Write(segment, min);
                MoveFocus(1);
            }
        }
    }

    /// <summary>焦点段加减（到头时回绕）；空段从起点（PlaceholderValue）开始。</summary>
    /// <param name="amount">加多少。</param>
    public void Increment(int amount)
    {
        if (FocusedSegment is not { } type || ReadOnly()) return;
        buffer = "";
        var (min, max) = Range(type);
        if (Shown(type) is not { } value)
        {
            var start = PlaceholderValue();
            if (type == DateSegmentType.DayPeriod)
            {
                if (draft.Hour is null) Write(DateSegmentType.Hour, Hour12 ? Twelve(start.Hour ?? 0) : start.Hour ?? 0);
                Write(DateSegmentType.DayPeriod, (start.Hour ?? 0) >= 12 ? 1 : 0);
                return;
            }
            Write(type, type == DateSegmentType.Hour && Hour12 ? Twelve(start.Hour ?? 0) : start[type] ?? min);
            return;
        }
        var size = max - min + 1;
        Write(type, ((value - min + amount) % size + size) % size + min);
    }

    /// <summary>焦点段设成最小或最大值。</summary>
    /// <param name="max">true 是最大值。</param>
    public void ToEdge(bool max)
    {
        if (FocusedSegment is not { } type || ReadOnly()) return;
        buffer = "";
        var range = Range(type);
        Write(type, max ? range.Max : range.Min);
    }

    /// <summary>删掉焦点段的最后一位；已经空了时焦点回到上一段。</summary>
    public void Backspace()
    {
        if (FocusedSegment is not { } type || ReadOnly()) return;
        var value = Shown(type);
        if (value is null && buffer.Length == 0)
        {
            MoveFocus(-1);
            return;
        }
        var left = type == DateSegmentType.DayPeriod ? "" : (buffer.Length > 0 ? buffer : value!.Value.ToString(CultureInfo.InvariantCulture))[..^1];
        buffer = left;
        var (min, _) = Range(type);
        if (type == DateSegmentType.DayPeriod || left.Length == 0)
        {
            if (type is DateSegmentType.DayPeriod or DateSegmentType.Hour)
            {
                // 删掉上午 / 下午或时：时没有了，两段一起清空
                var wasComplete = Complete;
                draft = draft with { Hour = null };
                if (wasComplete) Changed(null);
                return;
            }
            Write(type, null);
            return;
        }
        if (int.Parse(left, CultureInfo.InvariantCulture) >= min) Write(type, int.Parse(left, CultureInfo.InvariantCulture));
    }

    /// <summary>清空所有段。</summary>
    public void Clear()
    {
        buffer = "";
        pendingYear = false;
        var wasComplete = Complete;
        draft = new DateFieldValues();
        if (wasComplete) Changed(null);
    }

    /// <summary>丢掉还没写回的输入（打了一半的段），回到当前的值：表单重置时。</summary>
    public void Reset()
    {
        buffer = "";
        pendingYear = false;
        draft = Value() ?? new DateFieldValues();
    }

    /// <summary>写回还没写回的年份（没打满四位时先不写回）：离开这一段、按 Enter 提交时。</summary>
    public void Commit()
    {
        if (!pendingYear) return;
        pendingYear = false;
        if (Complete) Changed(Constrained(draft));
    }

    /// <summary>焦点移到相邻的段；越过两端时调 Exit。</summary>
    /// <param name="direction">1 下一段，-1 上一段。</param>
    public void MoveFocus(int direction)
    {
        var list = Editable;
        var index = list.ToList().FindIndex(segment => segment.Type == FocusedSegment);
        var target = index + direction;
        if (target >= 0 && target < list.Count) SetFocused(list[target].Type);
        else Exit?.Invoke(direction);
    }

    /// <summary>
    /// 分段输入的键盘操作：↑↓ 加减、Page Up / Down 大步、Home / End 最小 / 最大、Backspace 删除、←→ 换段（从右到左排版时反过来）。
    /// 数字与字母走文字输入（<see cref="Type"/>）。返回是否处理了这个按键。
    /// </summary>
    /// <param name="key">按键。</param>
    /// <param name="rtl">从右到左排版。</param>
    public bool HandleKey(DateSegmentKey key, bool rtl = false)
    {
        if (FocusedSegment is not { } type) return false;
        var forward = rtl ? -1 : 1;
        switch (key)
        {
            case DateSegmentKey.Up: Increment(1); break;
            case DateSegmentKey.Down: Increment(-1); break;
            case DateSegmentKey.PageUp: Increment(BigSteps[type]); break;
            case DateSegmentKey.PageDown: Increment(-BigSteps[type]); break;
            case DateSegmentKey.Home: ToEdge(false); break;
            case DateSegmentKey.End: ToEdge(true); break;
            case DateSegmentKey.Backspace: Backspace(); break;
            case DateSegmentKey.Right: MoveFocus(forward); break;
            case DateSegmentKey.Left: MoveFocus(-forward); break;
            default: return false;
        }
        return true;
    }

    /// <summary>写一段的值（显示值：12 小时制的时是 1–12）；写完检查是否填全，emit 为 false 时只改草稿。</summary>
    void Write(DateSegmentType type, int? value, bool emit = true)
    {
        var next = draft;
        if (type == DateSegmentType.DayPeriod)
        {
            if (next.Hour is not { } hour || value is null) return;
            next = next with { Hour = hour % 12 + value.Value * 12 };
        }
        else if (type == DateSegmentType.Hour && Hour12 && value is not null)
        {
            var afternoon = (draft.Hour ?? PlaceholderValue().Hour ?? 0) >= 12;
            next = next with { Hour = value.Value % 12 + (afternoon ? 12 : 0) };
        }
        else next = next.With(type, value);
        var wasComplete = Complete;
        draft = next;
        pendingYear = !emit;
        if (!emit) return;
        if (Complete) Changed(Constrained(next));
        else if (wasComplete) Changed(null);
    }

    // 日超过了这个月的天数（先填了 31 再把月改成 2 月）：写回时取这个月的最后一天；周同理（这一年只有 52 周时）
    static DateFieldValues Constrained(DateFieldValues values)
    {
        if (values is { Week: { } week, Year: { } year }) return values with { Week = Math.Min(week, WeeksIn(year)) };
        return values.Day is { } day ? values with { Day = Math.Min(day, DaysInMonth(values.Year, values.Month)) } : values;
    }

    (int Min, int Max) Range(DateSegmentType type) => type switch
    {
        DateSegmentType.Year => (1, 9999),
        DateSegmentType.Month => (1, 12),
        DateSegmentType.Day => (1, DaysInMonth(draft.Year, draft.Month)),
        DateSegmentType.Quarter => (1, 4),
        DateSegmentType.Week => (1, draft.Year is { } year ? WeeksIn(year) : 53),
        DateSegmentType.Hour => Hour12 ? (1, 12) : (0, 23),
        DateSegmentType.Minute or DateSegmentType.Second => (0, 59),
        _ => (0, 1),
    };

    /// <summary>段上显示的值：12 小时制的时显示 1–12，上午 / 下午是 0 / 1。</summary>
    int? Shown(DateSegmentType type)
    {
        var hour = draft.Hour;
        if (type == DateSegmentType.DayPeriod) return hour is null ? null : hour >= 12 ? 1 : 0;
        if (type == DateSegmentType.Hour && Hour12 && hour is { } h) return Twelve(h);
        return draft[type];
    }

    string Text(DateSegmentType type, int? value)
    {
        if (type == DateSegmentType.DayPeriod)
        {
            var (am, pm) = Periods();
            return value is null ? "" : value == 0 ? am : pm;
        }
        if (buffer.Length > 0 && FocusedSegment == type) return int.Parse(buffer, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        if (value is not { } number) return "";
        var width = type == DateSegmentType.Year ? 4 : type == DateSegmentType.Quarter || type == DateSegmentType.Hour && Hour12 ? 1 : 2;
        return number.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
    }

    /// <summary>
    /// 空段的占位：语言包里的单位字（年、月、日）；后面紧跟的文字已经是这个单位时（中文的"2026年"）改用横线，显示成"––年"，否则会是"年年"。
    /// </summary>
    string PlaceholderOf(DateSegmentType type, string? next)
    {
        var unit = Placeholders.GetValueOrDefault(KeyOf(type));
        return string.IsNullOrEmpty(unit) || next?.TrimStart().StartsWith(unit, StringComparison.Ordinal) == true ? "––" : unit;
    }

    (string Am, string Pm) Periods()
    {
        var format = Culture.DateTimeFormat;
        return (format.AMDesignator is { Length: > 0 } am ? am : "AM", format.PMDesignator is { Length: > 0 } pm ? pm : "PM");
    }

    string LabelOf(DateSegmentType type)
    {
        var chinese = Culture.TwoLetterISOLanguageName == "zh";
        return type switch
        {
            DateSegmentType.Year => chinese ? "年" : "year",
            DateSegmentType.Month => chinese ? "月" : "month",
            DateSegmentType.Day => chinese ? "日" : "day",
            DateSegmentType.Quarter => chinese ? "季度" : "quarter",
            DateSegmentType.Week => chinese ? "周" : "week",
            DateSegmentType.Hour => chinese ? "小时" : "hour",
            DateSegmentType.Minute => chinese ? "分钟" : "minute",
            DateSegmentType.Second => chinese ? "秒" : "second",
            _ => chinese ? "上午/下午" : "AM/PM",
        };
    }

    /// <summary>按语言的顺序排好的段（含分隔符）：只取格式的结构，值另外填。</summary>
    List<(DateSegmentType? Type, string Literal)> Layout()
    {
        // 季度、周：区域格式不排这两种段，按语言包的排法
        if (Fields.FirstOrDefault(field => field is DateSegmentType.Quarter or DateSegmentType.Week) is var period && period is DateSegmentType.Quarter or DateSegmentType.Week)
        {
            var pattern = (period == DateSegmentType.Quarter ? PeriodPatterns?.Quarter : PeriodPatterns?.Week) ?? $"{{year}}-{{{KeyOf(period)}}}";
            return [.. Regex.Split(pattern, @"(\{(?:year|quarter|week)\})").Where(part => part.Length > 0).Select(part => part switch
            {
                "{year}" => ((DateSegmentType?)DateSegmentType.Year, ""),
                "{quarter}" => (DateSegmentType.Quarter, ""),
                "{week}" => (DateSegmentType.Week, ""),
                _ => (null, part),
            })];
        }
        var format = Culture.DateTimeFormat;
        var dates = Fields.Where(field => field is DateSegmentType.Year or DateSegmentType.Month or DateSegmentType.Day).ToHashSet();
        var times = AllFields.Where(field => field is DateSegmentType.Hour or DateSegmentType.Minute or DateSegmentType.Second or DateSegmentType.DayPeriod).ToHashSet();
        var parts = Pick(Tokens(format.ShortDatePattern), dates);
        if (times.Count > 0)
        {
            var time = Tokens(format.LongTimePattern);
            var hasPeriod = time.Any(part => part.Type == DateSegmentType.DayPeriod);
            if (Hour12 && !hasPeriod)
                // 区域格式是 24 小时制、使用方要 12 小时制：东亚语言上午下午在前、紧挨着（上午9:00），其余在后面隔一个空格
                time = Culture.TwoLetterISOLanguageName is "zh" or "ja" or "ko" ? [(DateSegmentType.DayPeriod, ""), .. time] : [.. time, (null, " "), (DateSegmentType.DayPeriod, "")];
            var timeParts = Pick(time, times);
            if (parts.Count > 0 && timeParts.Count > 0)
                parts.Add((null, Culture.TwoLetterISOLanguageName is "zh" or "ja" or "ko" ? " " : ", "));
            parts.AddRange(timeParts);
        }
        return parts;
    }

    /// <summary>只留下要的段与它们之间的分隔符：跳过的段连同它前面的分隔符一起去掉；结尾不是空白的分隔符（"2026年10月"的"月"）留着。</summary>
    static List<(DateSegmentType? Type, string Literal)> Pick(List<(DateSegmentType? Type, string Literal)> tokens, HashSet<DateSegmentType> wanted)
    {
        var result = new List<(DateSegmentType? Type, string Literal)>();
        string? pending = null;
        foreach (var token in tokens)
        {
            if (token.Type is not { } type)
            {
                pending = (pending ?? "") + token.Literal;
                continue;
            }
            if (!wanted.Contains(type))
            {
                pending = null;
                continue;
            }
            if (result.Count > 0 && pending is not null) result.Add((null, pending));
            else if (result.Count == 0 && pending is { } leading && leading.Trim().Length > 0) result.Add((null, leading));
            result.Add((type, ""));
            pending = null;
        }
        if (result.Count > 0 && pending is { } trailing && trailing.Trim().Length > 0) result.Add((null, trailing));
        return result;
    }

    /// <summary>.NET 的日期时间格式串 → 段与分隔符（y 年、M 月、d 日、H / h 时、m 分、s 秒、t 上午下午；引号里是原样的文字）。</summary>
    static List<(DateSegmentType? Type, string Literal)> Tokens(string pattern)
    {
        var tokens = new List<(DateSegmentType? Type, string Literal)>();
        var literal = new StringBuilder();
        void Flush()
        {
            // 各种空白（不换行空格、窄不换行空格）统一成普通空格
            if (literal.Length > 0) tokens.Add((null, Regex.Replace(literal.ToString(), "[   ]", " ")));
            literal.Clear();
        }
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c is '\'' or '"')
            {
                var end = pattern.IndexOf(c, i + 1);
                literal.Append(pattern, i + 1, (end < 0 ? pattern.Length : end) - i - 1);
                i = end < 0 ? pattern.Length : end;
                continue;
            }
            if (c == '\\' && i + 1 < pattern.Length)
            {
                literal.Append(pattern[++i]);
                continue;
            }
            DateSegmentType? type = c switch
            {
                'y' => DateSegmentType.Year,
                'M' => DateSegmentType.Month,
                'd' => DateSegmentType.Day,
                'H' or 'h' => DateSegmentType.Hour,
                'm' => DateSegmentType.Minute,
                's' => DateSegmentType.Second,
                't' => DateSegmentType.DayPeriod,
                _ => null,
            };
            if (type is null)
            {
                literal.Append(c);
                continue;
            }
            while (i + 1 < pattern.Length && pattern[i + 1] == c) i++;
            // dddd 是星期几：不是段
            Flush();
            tokens.Add((type, ""));
        }
        Flush();
        return tokens;
    }

    /// <summary>语言默认的小时制：短时间格式里用 h（1–12）的是 12 小时制。</summary>
    static string DefaultHourCycle(CultureInfo culture) => culture.DateTimeFormat.ShortTimePattern.Contains('h') ? "h12" : "h23";

    static string KeyOf(DateSegmentType type) => type == DateSegmentType.DayPeriod ? "dayPeriod" : type.ToString().ToLowerInvariant();

    static int Twelve(int hour) => hour % 12 is 0 ? 12 : hour % 12;

    static int DaysInMonth(int? year, int? month) => month is { } m ? DateTime.DaysInMonth(year is >= 1 and <= 9999 ? year.Value : 2000, m) : 31;

    static int WeeksIn(int year) => ISOWeek.GetWeeksInYear(Math.Clamp(year, 1, 9999));

    /// <summary>全角数字、阿拉伯-印度数字 → 半角。</summary>
    static string NormalizeDigits(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var c in text)
            result.Append(c switch
            {
                >= '０' and <= '９' => (char)('0' + (c - '０')),
                >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
                >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
                _ => c,
            });
        return result.ToString();
    }

    static bool Same(DateFieldValues? a, DateFieldValues? b) => a == b;

    // ───────── 值与 ISO 字符串 ─────────

    static readonly DateSegmentType[] AllValueFields = [DateSegmentType.Year, DateSegmentType.Month, DateSegmentType.Day, DateSegmentType.Hour, DateSegmentType.Minute, DateSegmentType.Second];

    /// <summary>日期分段：从年到 granularity；time 时只取时间的部分（TimeField）。</summary>
    /// <param name="granularity">精确到哪一段。</param>
    /// <param name="time">只要时间的段。</param>
    public static IReadOnlyList<DateSegmentType> FieldsOf(DateFieldGranularity granularity, bool time = false)
    {
        if (granularity is DateFieldGranularity.Quarter or DateFieldGranularity.Week)
            return time ? [] : [DateSegmentType.Year, granularity == DateFieldGranularity.Quarter ? DateSegmentType.Quarter : DateSegmentType.Week];
        var last = granularity switch
        {
            DateFieldGranularity.Year => DateSegmentType.Year,
            DateFieldGranularity.Month => DateSegmentType.Month,
            DateFieldGranularity.Day => DateSegmentType.Day,
            DateFieldGranularity.Hour => DateSegmentType.Hour,
            DateFieldGranularity.Minute => DateSegmentType.Minute,
            _ => DateSegmentType.Second,
        };
        var fields = AllValueFields.Take(Array.IndexOf(AllValueFields, last) + 1);
        return [.. time ? fields.Where(field => field is DateSegmentType.Hour or DateSegmentType.Minute or DateSegmentType.Second) : fields];
    }

    /// <summary>各段的值 → ISO 字符串（按有哪些段：2026、2026-10、2026-10-20、2026-10-20T14:30、14:30、2026-Q4、2026-W43）。</summary>
    /// <param name="values">各段的值。</param>
    /// <param name="fields">有哪些段。</param>
    public static string ToIso(DateFieldValues values, IReadOnlyList<DateSegmentType> fields)
    {
        static string Pad(int? value, int width = 2) => (value ?? 0).ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
        if (fields.Contains(DateSegmentType.Quarter)) return $"{Pad(values.Year, 4)}-Q{values.Quarter ?? 1}";
        if (fields.Contains(DateSegmentType.Week)) return $"{Pad(values.Year, 4)}-W{Pad(values.Week ?? 1)}";
        var date = string.Join("-", new[]
        {
            values.Year is not null ? Pad(values.Year, 4) : null,
            fields.Contains(DateSegmentType.Month) ? Pad(values.Month ?? 1) : null,
            fields.Contains(DateSegmentType.Day) ? Pad(values.Day ?? 1) : null,
        }.OfType<string>());
        if (!fields.Contains(DateSegmentType.Hour)) return date;
        var time = string.Join(":", new[] { Pad(values.Hour), Pad(values.Minute), fields.Contains(DateSegmentType.Second) ? Pad(values.Second) : null }.OfType<string>());
        return fields.Contains(DateSegmentType.Year) ? $"{date}T{time}" : time;
    }

    /// <summary>ISO 字符串 → 各段的值；格式不对或缺了要的段时 null。只取 fields 里有的段（2026-10-20T14:30 给日期字段时只要日期）。</summary>
    /// <param name="value">ISO 字符串。</param>
    /// <param name="fields">有哪些段。</param>
    public static DateFieldValues? FromIso(string? value, IReadOnlyList<DateSegmentType> fields)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (Regex.Match(value, @"^(\d{4})-(?:Q([1-4])|W(\d{2}))$") is { Success: true } period)
        {
            var parsed = new DateFieldValues
            {
                Year = int.Parse(period.Groups[1].Value, CultureInfo.InvariantCulture),
                Quarter = period.Groups[2].Success ? int.Parse(period.Groups[2].Value, CultureInfo.InvariantCulture) : null,
                Week = period.Groups[3].Success ? int.Parse(period.Groups[3].Value, CultureInfo.InvariantCulture) : null,
            };
            return fields.All(field => parsed[field] is not null) ? parsed : null;
        }
        var match = Regex.Match(value, @"^(?:(\d{4})(?:-(\d{2})(?:-(\d{2}))?)?)?(?:T?(\d{2}):(\d{2})(?::(\d{2}))?)?$");
        if (!match.Success) return null;
        var result = new DateFieldValues();
        for (var i = 0; i < AllValueFields.Length; i++)
            if (fields.Contains(AllValueFields[i]) && match.Groups[i + 1].Success)
                result = result.With(AllValueFields[i], int.Parse(match.Groups[i + 1].Value, CultureInfo.InvariantCulture));
        return fields.All(field => result[field] is not null) ? result : null;
    }

    /// <summary>时刻 → 各段的值（年到秒）。</summary>
    /// <param name="time">时刻。</param>
    public static DateFieldValues FromDateTime(DateTime time) =>
        new() { Year = time.Year, Month = time.Month, Day = time.Day, Hour = time.Hour, Minute = time.Minute, Second = time.Second };
}
