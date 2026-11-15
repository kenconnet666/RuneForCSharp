using System.Globalization;

namespace Rune.Wpf.Tests;

/// <summary>DateSegments：段的顺序按语言、逐段输入与自动跳段、↑↓ 回绕、Backspace、12 / 24 小时制、填全才写回。</summary>
public class DateSegmentsTests
{
    static readonly DateSegmentType[] Ymd = [DateSegmentType.Year, DateSegmentType.Month, DateSegmentType.Day];
    static readonly DateSegmentType[] Hm = [DateSegmentType.Hour, DateSegmentType.Minute];

    sealed class Setup
    {
        public readonly DateSegments State;
        public DateFieldValues? Value;
        public readonly List<DateFieldValues?> Changes = [];
        public readonly List<int> Exits = [];

        public Setup(DateSegmentType[]? fields = null, string culture = "zh-CN", string? hourCycle = null, DateFieldValues? value = null)
        {
            Value = value;
            State = new DateSegments(fields ?? Ymd)
            {
                Culture = CultureInfo.GetCultureInfo(culture),
                HourCycle = hourCycle,
                Value = () => Value,
                Changed = next =>
                {
                    Changes.Add(next);
                    Value = next;
                },
                PlaceholderValue = () => new DateFieldValues { Year = 2026, Month = 10, Day = 20, Hour = 14, Minute = 30, Second = 0 },
                Placeholders = new Dictionary<string, string> { ["year"] = "年", ["month"] = "月", ["day"] = "日" },
                Exit = Exits.Add,
            };
            State.Sync();
        }

        public bool Press(DateSegmentKey key, bool rtl = false) => State.HandleKey(key, rtl);

        public string Texts => string.Concat(State.Segments.Select(segment => segment.Text));
    }

    [Theory]
    [Upstream("date-segments", "%s")]
    [InlineData("zh-CN", new[] { DateSegmentType.Year, DateSegmentType.Month, DateSegmentType.Day }, "年/月/日")]
    [InlineData("en-US", new[] { DateSegmentType.Month, DateSegmentType.Day, DateSegmentType.Year }, "月/日/年")]
    [InlineData("de-DE", new[] { DateSegmentType.Day, DateSegmentType.Month, DateSegmentType.Year }, "日.月.年")]
    public void Segments_follow_the_culture_order(string culture, DateSegmentType[] order, string text)
    {
        var setup = new Setup(culture: culture);
        Assert.Equal(order, setup.State.Editable.Select(segment => segment.Type!.Value));
        Assert.Equal(text, setup.Texts);
        Assert.All(setup.State.Editable, segment => Assert.True(segment.IsPlaceholder));
    }

    [Fact]
    [Upstream("date-segments", "段的名称取自 Intl.DisplayNames")]
    public void Segment_names_follow_the_language()
    {
        Assert.Equal(["年", "月", "日"], new Setup().State.Editable.Select(segment => segment.Label));
        Assert.Equal(["month", "day", "year"], new Setup(culture: "en-US").State.Editable.Select(segment => segment.Label));
    }

    [Fact]
    [Upstream("date-segments", "已有的值补零显示")]
    public void Values_are_zero_padded() => Assert.Equal("2026/05/01", new Setup(value: new() { Year = 2026, Month = 5, Day = 1 }).Texts);

    [Fact]
    [Upstream("date-segments", "时间：zh-CN 默认 24 小时制，en-US 默认 12 小时制并加上上午 / 下午")]
    public void Times_use_the_culture_hour_cycle()
    {
        Assert.Equal("14:05", new Setup(Hm, value: new() { Hour = 14, Minute = 5 }).Texts);
        var en = new Setup(Hm, "en-US", value: new() { Hour = 14, Minute = 5 });
        Assert.Equal([DateSegmentType.Hour, DateSegmentType.Minute, DateSegmentType.DayPeriod], en.State.Editable.Select(segment => segment.Type!.Value));
        Assert.Equal("2:05 PM", en.Texts);
        Assert.Equal("上午9:00", new Setup(Hm, hourCycle: "h12", value: new() { Hour = 9, Minute = 0 }).Texts);
    }

    [Fact]
    [Upstream("date-segments", "焦点要越过两端时调用 onExit")]
    public void Crossing_either_end_calls_exit()
    {
        var setup = new Setup();
        setup.State.SetFocused(DateSegmentType.Year);
        setup.State.Type("2026111");
        Assert.Empty(setup.Exits);
        setup.State.Type("0");
        Assert.Equal([1], setup.Exits);
        setup.State.SetFocused(DateSegmentType.Year);
        for (var i = 0; i < 4; i++) setup.Press(DateSegmentKey.Backspace);
        Assert.Equal([1], setup.Exits);
        setup.Press(DateSegmentKey.Backspace);
        Assert.Equal([1, -1], setup.Exits);
    }

    [Fact]
    [Upstream("date-segments", "逐段输入：年满 4 位跳段；月打 1 等第二位、打 3 直接跳；填全才写回")]
    public void Typing_moves_between_segments_and_writes_back_when_complete()
    {
        var setup = new Setup();
        setup.State.SetFocused(DateSegmentType.Year);
        setup.State.Type("2026");
        Assert.Equal(DateSegmentType.Month, setup.State.FocusedSegment);
        setup.State.Type("1");
        Assert.Equal(DateSegmentType.Month, setup.State.FocusedSegment);
        setup.State.Type("1");
        Assert.Equal(DateSegmentType.Day, setup.State.FocusedSegment);
        Assert.Null(setup.Value);
        setup.State.Type("5");
        Assert.Equal(new DateFieldValues { Year = 2026, Month = 11, Day = 5 }, setup.Value);
        Assert.Single(setup.Changes);
    }

    [Fact]
    [Upstream("date-segments", "打了这一位就超出范围：从这一位重新开始；0 先记着等下一位")]
    public void An_out_of_range_digit_starts_over()
    {
        var setup = new Setup(value: new() { Year = 2026, Month = 1, Day = 1 });
        setup.State.SetFocused(DateSegmentType.Month);
        setup.State.Type("0");
        Assert.Equal("2026/0/01", setup.Texts);
        setup.State.Type("9");
        Assert.Equal(DateSegmentType.Day, setup.State.FocusedSegment);
        setup.State.Type("4");
        setup.State.Type("5");
        // 45 超出 9 月的 30 天：5 重新开始，5 * 10 > 30 直接填好
        Assert.Equal("2026/09/05", setup.Texts);
    }

    [Fact]
    [Upstream("date-segments", "全角数字、阿拉伯-印度数字也能输入")]
    public void Full_width_and_arabic_indic_digits_count()
    {
        var setup = new Setup();
        setup.State.SetFocused(DateSegmentType.Year);
        setup.State.Type("２０２６");
        setup.State.Type("١٢");
        setup.State.Type("3");
        Assert.Equal(new DateFieldValues { Year = 2026, Month = 12, Day = 3 }, setup.Value);
    }

    [Fact]
    [Upstream("date-segments", "日超过这个月的天数：写回时取最后一天")]
    public void A_day_past_the_month_end_is_clamped()
    {
        var setup = new Setup(value: new() { Year = 2026, Month = 1, Day = 31 });
        setup.State.SetFocused(DateSegmentType.Month);
        setup.State.Type("2");
        Assert.Equal(new DateFieldValues { Year = 2026, Month = 2, Day = 28 }, setup.Value);
    }

    [Fact]
    [Upstream("date-segments", "12 小时制：打 a / p 切换上午下午；改时保留上午下午")]
    public void Twelve_hour_periods_toggle_with_a_and_p()
    {
        var setup = new Setup(Hm, "en-US", value: new() { Hour = 9, Minute = 15 });
        setup.State.SetFocused(DateSegmentType.DayPeriod);
        setup.State.Type("p");
        Assert.Equal(new DateFieldValues { Hour = 21, Minute = 15 }, setup.Value);
        setup.State.SetFocused(DateSegmentType.Hour);
        setup.State.Type("11");
        Assert.Equal(new DateFieldValues { Hour = 23, Minute = 15 }, setup.Value);
        setup.State.SetFocused(DateSegmentType.Hour);
        setup.State.Type("12");
        Assert.Equal(12, setup.Value?.Hour);
    }

    [Fact]
    [Upstream("date-segments", "↑↓ 加减并回绕；空段从起点（现在）开始；Home / End")]
    public void Arrows_wrap_and_start_from_now()
    {
        var setup = new Setup();
        setup.State.SetFocused(DateSegmentType.Month);
        setup.Press(DateSegmentKey.Up);
        Assert.Equal("年/10/日", setup.Texts);
        setup.Press(DateSegmentKey.Up);
        setup.Press(DateSegmentKey.Up);
        Assert.Equal("年/12/日", setup.Texts);
        setup.Press(DateSegmentKey.Up);
        Assert.Equal("年/01/日", setup.Texts);
        setup.Press(DateSegmentKey.Down);
        setup.Press(DateSegmentKey.PageUp);
        Assert.Equal("年/02/日", setup.Texts);
        setup.Press(DateSegmentKey.End);
        Assert.Equal("年/12/日", setup.Texts);
        setup.Press(DateSegmentKey.Home);
        Assert.Equal("年/01/日", setup.Texts);
    }

    [Fact]
    [Upstream("date-segments", "←→ 换段，从右到左排版时反过来")]
    public void Left_and_right_move_between_segments()
    {
        var setup = new Setup();
        setup.State.SetFocused(DateSegmentType.Month);
        setup.Press(DateSegmentKey.Right);
        Assert.Equal(DateSegmentType.Day, setup.State.FocusedSegment);
        setup.Press(DateSegmentKey.Right, rtl: true);
        Assert.Equal(DateSegmentType.Month, setup.State.FocusedSegment);
    }

    [Fact]
    [Upstream("date-segments", "Backspace 删一位；删空后回到上一段；填好的值删掉一段时写回 null")]
    public void Backspace_deletes_a_digit_then_moves_back()
    {
        var setup = new Setup(value: new() { Year = 2026, Month = 10, Day = 20 });
        setup.State.SetFocused(DateSegmentType.Day);
        setup.Press(DateSegmentKey.Backspace);
        Assert.Equal("2026/10/2", setup.Texts);
        setup.Press(DateSegmentKey.Backspace);
        Assert.Equal("2026/10/日", setup.Texts);
        Assert.Null(setup.Value);
        setup.Press(DateSegmentKey.Backspace);
        Assert.Equal(DateSegmentType.Month, setup.State.FocusedSegment);
    }

    [Fact]
    [Upstream("date-segments", "外部改值时以外部为准；clear 清空所有段")]
    public void Outside_values_win_and_clear_empties_everything()
    {
        var setup = new Setup(value: new() { Year = 2026, Month = 10, Day = 20 });
        setup.Value = new DateFieldValues { Year = 2025, Month = 1, Day = 2 };
        setup.State.Sync();
        Assert.Equal("2025/01/02", setup.Texts);
        setup.State.Clear();
        Assert.Equal("年/月/日", setup.Texts);
    }

    [Fact]
    [Upstream("date-segments", "年份打满四位才写回；没打满时离开这一段（或 commit）再写回")]
    public void Years_are_written_back_at_four_digits_or_on_leave()
    {
        var setup = new Setup(value: new() { Year = 2025, Month = 10, Day = 20 });
        setup.State.SetFocused(DateSegmentType.Year);
        setup.State.Type("202");
        Assert.Empty(setup.Changes);
        setup.State.Type("6");
        Assert.Equal([new DateFieldValues { Year = 2026, Month = 10, Day = 20 }], setup.Changes);
        setup.State.SetFocused(DateSegmentType.Year);
        setup.State.Type("19");
        setup.State.SetFocused(null);
        Assert.Equal(new DateFieldValues { Year = 19, Month = 10, Day = 20 }, setup.Changes[^1]);
    }

    [Fact]
    [Upstream("date-segments", "reset 丢掉打了一半的段，回到当前的值")]
    public void Reset_drops_the_partial_input()
    {
        var setup = new Setup();
        setup.State.SetFocused(DateSegmentType.Year);
        setup.State.Type("2026");
        setup.State.Type("1");
        Assert.True(setup.State.Partial);
        setup.State.Reset();
        Assert.Equal((false, "年/月/日", 0), (setup.State.Partial, setup.Texts, setup.Changes.Count));
    }

    [Fact]
    public void Iso_round_trips_by_granularity()
    {
        var day = DateSegments.FieldsOf(DateFieldGranularity.Day);
        Assert.Equal("2026-10-20", DateSegments.ToIso(new() { Year = 2026, Month = 10, Day = 20 }, day));
        Assert.Equal(new DateFieldValues { Year = 2026, Month = 10, Day = 20 }, DateSegments.FromIso("2026-10-20T14:30", day));
        var minute = DateSegments.FieldsOf(DateFieldGranularity.Minute);
        Assert.Equal("2026-10-20T14:30", DateSegments.ToIso(new() { Year = 2026, Month = 10, Day = 20, Hour = 14, Minute = 30 }, minute));
        Assert.Equal("14:30", DateSegments.ToIso(new() { Hour = 14, Minute = 30 }, DateSegments.FieldsOf(DateFieldGranularity.Minute, time: true)));
        Assert.Equal("2026-Q4", DateSegments.ToIso(new() { Year = 2026, Quarter = 4 }, DateSegments.FieldsOf(DateFieldGranularity.Quarter)));
        Assert.Equal(new DateFieldValues { Year = 2026, Week = 43 }, DateSegments.FromIso("2026-W43", DateSegments.FieldsOf(DateFieldGranularity.Week)));
        Assert.Null(DateSegments.FromIso("2026-10", day));
    }

    [Fact]
    public void Quarter_and_week_follow_the_language_pattern()
    {
        var setup = new Setup([DateSegmentType.Year, DateSegmentType.Quarter], value: new() { Year = 2026, Quarter = 4 });
        setup.State.PeriodPatterns = ("{year}年第{quarter}季度", "{year}年第{week}周");
        Assert.Equal("2026年第4季度", setup.Texts);
    }
}
