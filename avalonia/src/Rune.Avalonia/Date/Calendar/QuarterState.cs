using System.Globalization;

namespace Rune.Avalonia;

/// <summary>季度面板的一格（vue-tsx-pro QuarterCalendar 渲染时算的那几项）。</summary>
/// <param name="Key">季度（2026-Q4）。</param>
/// <param name="Disabled">不能选：整个季度都在 Min–Max 之外，或面板不可用。</param>
/// <param name="Selected">已选（区间里是两端与中间的每一季）。</param>
/// <param name="InRange">在区间里（含两端；选到一半时是预览的区间）。</param>
/// <param name="RangeStart">区间的开始。</param>
/// <param name="RangeEnd">区间的结束。</param>
/// <param name="Today">本季度。</param>
/// <param name="Focused">焦点所在的格（在 Tab 顺序里）。</param>
public sealed record QuarterCellState(string Key, bool Disabled, bool Selected, bool InRange, bool RangeStart, bool RangeEnd, bool Today, bool Focused);

/// <summary>
/// 季度面板的状态（vue-tsx-pro 的 QuarterCalendar）：一年一页的四个季度，选一个季度或一段区间。三个库共用，界面各自画。
/// 选项都是委托（同 <see cref="CalendarState"/>：每次读最新的值）。焦点在一个季度上（roving tabindex）：←→ 移动（到头换年，从右到左时反过来），
/// Home / End 到第一、第四季度，Page Up / Down 换年；区间选到一半时悬停预览，Esc 取消。
/// </summary>
public sealed class QuarterState
{
    string? focused;
    int year;
    string? hovered;

    /// <summary>选区间（先开始、再结束）；默认选一个季度。</summary>
    public Func<bool> IsRange { get; set; } = () => false;

    /// <summary>单选时已选的季度（2026-Q4）。</summary>
    public Func<string?> Selected { get; set; } = () => null;

    /// <summary>选区间时已选的区间。</summary>
    public Func<IsoDateRange?> SelectedRange { get; set; } = () => null;

    /// <summary>今天（本季度带描边、一开始的焦点）。</summary>
    public Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>这个季度能不能选（整段在 Min–Max 之外时不能）。</summary>
    public Func<string, bool> IsDisabled { get; set; } = _ => false;

    /// <summary>不能改选择（只读、不可用）：仍能翻页、移动焦点。</summary>
    public Func<bool> ReadOnly { get; set; } = () => false;

    /// <summary>焦点所在的季度。</summary>
    public string Focused => Started().focused!;

    /// <summary>显示的年份。</summary>
    public int Year => Started().year;

    /// <summary>选区间选到一半时的开始。</summary>
    public string? Anchor { get; private set; }

    /// <summary>季度的写法：2026-Q4。</summary>
    public static string Key(int year, int quarter) => $"{year.ToString("D4", CultureInfo.InvariantCulture)}-Q{quarter}";

    /// <summary>季度所在的年。</summary>
    public static int YearOf(string key) => int.Parse(key[..4], CultureInfo.InvariantCulture);

    /// <summary>第几季度（1–4）。</summary>
    public static int QuarterOf(string key) => key[6] - '0';

    /// <summary>这一天所在的季度。</summary>
    public static string Of(DateOnly date) => Key(date.Year, (date.Month - 1) / 3 + 1);

    /// <summary>两个季度排好序（2026-Q1 这种写法按字符串比较就是先后）。</summary>
    public static IsoDateRange Ordered(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? new(a, b) : new(b, a);

    string? Chosen => IsRange() ? SelectedRange()?.Start : Selected();

    /// <summary>第一次用到时开始：一开始显示已选的（没有时是本季度）那一年，焦点也在它上面。</summary>
    QuarterState Started()
    {
        if (focused is not null) return this;
        focused = Chosen ?? Of(Today());
        year = YearOf(focused);
        return this;
    }

    /// <summary>外面改了值（快捷按钮、重置）：翻到它那一年，焦点也过去。</summary>
    public void SelectionChanged()
    {
        Started();
        if (Chosen is not { } key || key == focused) return;
        focused = key;
        year = YearOf(key);
    }

    /// <summary>翻页（上一年、下一年）：焦点不动，这一页没有焦点时第一季度进 Tab 顺序。</summary>
    /// <param name="direction">1 往后，-1 往前。</param>
    public void TurnYear(int direction)
    {
        Started();
        year += direction;
    }

    /// <summary>一格的状态。</summary>
    /// <param name="quarter">第几季度（1–4）。</param>
    public QuarterCellState Cell(int quarter)
    {
        Started();
        var key = Key(year, quarter);
        var preview = Preview();
        var inRange = preview is { } range && string.CompareOrdinal(key, range.Start) >= 0 && string.CompareOrdinal(key, range.End) <= 0;
        var selected = IsRange() ? inRange : Selected() == key;
        return new QuarterCellState(key, IsDisabled(key), selected, inRange, preview?.Start == key, preview?.End == key, key == Of(Today()),
            key == focused || (YearOf(focused!) != year && quarter == 1));
    }

    /// <summary>画出来的区间：选到一半时是开始到悬停的季度，否则是已选的区间。</summary>
    IsoDateRange? Preview() => !IsRange() ? null : Anchor is { } anchor ? Ordered(anchor, hovered ?? anchor) : SelectedRange();

    /// <summary>点了一格：单选时返回选中的季度（两端相同）；区间时第一下记下开始、返回 null，第二下返回排好序的区间。不能选、只读时返回 null。</summary>
    /// <param name="key">季度。</param>
    public IsoDateRange? Activate(string key)
    {
        Started();
        if (ReadOnly() || IsDisabled(key)) return null;
        focused = key;
        if (!IsRange()) return new IsoDateRange(key, key);
        if (Anchor is not { } anchor)
        {
            Anchor = hovered = key;
            return null;
        }
        Anchor = hovered = null;
        return Ordered(anchor, key);
    }

    /// <summary>格子得到了焦点（点击、Tab）：键盘从这一格接着走。</summary>
    public void Focus(string key)
    {
        Started();
        focused = key;
    }

    /// <summary>指针悬停的季度：区间选到一半时预览。</summary>
    public void Hover(string? key)
    {
        if (Anchor is not null) hovered = key;
    }

    /// <summary>按键（←→、Home / End、Page Up / Down、Esc）：返回是否处理了（处理了就要把焦点交给新的焦点格）。Esc 没有选到一半的区间时不处理（交给弹层关闭）。</summary>
    /// <param name="key">按键（↑↓ 不用）。</param>
    /// <param name="rtl">从右到左排版：←→ 反过来。</param>
    public bool HandleKey(CalendarKey key, bool rtl = false)
    {
        Started();
        var quarter = QuarterOf(focused!);
        var at = YearOf(focused!);
        void MoveTo(string next)
        {
            focused = next;
            year = YearOf(next);
            if (Anchor is not null) hovered = next;
        }
        void Step(int delta)
        {
            var index = at * 4 + quarter - 1 + delta;
            MoveTo(Key(index / 4, index % 4 + 1));
        }
        switch (key)
        {
            case CalendarKey.Right:
                Step(rtl ? -1 : 1);
                return true;
            case CalendarKey.Left:
                Step(rtl ? 1 : -1);
                return true;
            case CalendarKey.Home:
                MoveTo(Key(at, 1));
                return true;
            case CalendarKey.End:
                MoveTo(Key(at, 4));
                return true;
            case CalendarKey.PageUp:
                MoveTo(Key(at - 1, quarter));
                return true;
            case CalendarKey.PageDown:
                MoveTo(Key(at + 1, quarter));
                return true;
            case CalendarKey.Escape when Anchor is not null:
                Anchor = hovered = null;
                return true;
            default:
                return false;
        }
    }
}
