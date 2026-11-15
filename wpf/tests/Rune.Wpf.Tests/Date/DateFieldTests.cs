using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>DateField、TimeField：段的顺序与读屏、逐段输入（焦点跟着跳）、填全才写回、↑↓ 与 Backspace、外部改值、超出范围出错、清空、语言与粒度、不可用、表单重置与 Enter 提交。</summary>
public class DateFieldTests
{
    static (T Control, object Window) Show<T>(T control, string label = "日期", Lang? lang = null) where T : SegmentedField
    {
        var field = new Field { Label = label, Content = control };
        Provider.SetLang(field, lang ?? Lang.ZhCn);
        field.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(field);
        WpfUi.Flush();
        return (control, field);
    }

    static void Type(string text)
    {
        var target = (UIElement)Keyboard.FocusedElement;
        target.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, target, text)) { RoutedEvent = TextCompositionManager.TextInputEvent });
        WpfUi.Flush();
    }

    static void Press(Key key)
    {
        var target = (UIElement)Keyboard.FocusedElement;
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        WpfUi.Flush();
    }

    static string Text(SegmentedField field) => field.Group!.Text;

    [Fact]
    [Upstream("DateField", "zh-CN 年/月/日")]
    public Task Segments_are_named_spin_buttons_in_culture_order() => WpfUi.Run(() =>
    {
        var (date, _) = Show(new DateField());
        Assert.Equal("年/月/日", Text(date));
        var peers = date.Group!.Boxes.Select(box => box.Peer()).ToList();
        Assert.Equal(["日期 年", "日期 月", "日期 日"], peers.Select(peer => peer.GetName()));
        Assert.All(peers, peer => Assert.Equal(AutomationControlType.Spinner, peer.GetAutomationControlType()));
        Assert.Equal("未填写", ((IValueProvider)peers[0].GetPattern(PatternInterface.Value)).Value);
    });

    [Fact]
    [Upstream("DateField", "逐段输入：满了自动跳段、焦点跟过去；填全才写回 ISO")]
    public Task Typing_moves_focus_and_writes_iso_when_complete() => WpfUi.Run(() =>
    {
        var (date, window) = Show(new DateField());
        date.Group!.Boxes[0].Focus();
        Type("2026");
        Assert.True(date.Group.Boxes[1].IsKeyboardFocused);
        Type("10");
        Assert.Null(date.Value);
        Type("20");
        Assert.Equal(("2026-10-20", new DateOnly(2026, 10, 20)), (date.Value, date.Date));
    });

    [Fact]
    [Upstream("DateField", "↑↓ 加减并回绕；Backspace 删空后写回 null")]
    public Task Arrows_wrap_and_backspace_clears_the_value() => WpfUi.Run(() =>
    {
        var (date, window) = Show(new DateField { Value = "2026-12-20" });
        date.Group!.Boxes[1].Focus();
        Press(Key.Up);
        Assert.Equal("2026-01-20", date.Value);
        date.Group.Boxes[2].Focus();
        Press(Key.Back);
        Press(Key.Back);
        Assert.Null(date.Value);
    });

    [Fact]
    [Upstream("DateField", "外部改值时各段跟着变")]
    public Task Outside_values_show_in_the_segments() => WpfUi.Run(() =>
    {
        var (date, window) = Show(new DateField { Value = "2026-10-20" });
        date.Date = new DateOnly(2025, 1, 2);
        WpfUi.Flush();
        Assert.Equal("2025/01/02", Text(date));
    });

    [Fact]
    [Upstream("DateField", "超出 min / max、不可选的日期：外壳显示出错，各段 aria-invalid")]
    public Task Out_of_range_and_unavailable_dates_are_invalid() => WpfUi.Run(() =>
    {
        var (date, _) = Show(new DateField { Value = "2026-10-20", Max = "2026-10-01" });
        Assert.True(date.Group!.IsInvalid);
        Assert.True(date.ShowsInvalid);
        date.Max = null;
        date.IsDateUnavailable = day => day.DayOfWeek == DayOfWeek.Tuesday;
        Assert.True(date.Group.IsInvalid);
        date.IsDateUnavailable = null;
        Assert.False(date.Group.IsInvalid);
    });

    [Fact]
    [Upstream("DateField", "min / max 带时间时同一天里比时分；只给日期时那一整天都可选")]
    public Task Timed_limits_compare_within_the_day() => WpfUi.Run(() =>
    {
        var (date, _) = Show(new DateField { Granularity = DateFieldGranularity.Minute, Value = "2026-10-20T08:30", Min = "2026-10-20T09:00" });
        Assert.True(date.Group!.IsInvalid);
        date.Min = "2026-10-20";
        Assert.False(date.Group.IsInvalid);
    });

    [Fact]
    [Upstream("DateField", "清空按钮、隐藏的表单值")]
    public Task The_clear_button_empties_the_value() => WpfUi.Run(() =>
    {
        var (date, window) = Show(new DateField { Value = "2026-10-20", Clearable = true });
        var clear = date.ClearButton!;
        Assert.True(clear.IsVisible);
        clear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        WpfUi.Flush();
        Assert.Null(date.Value);
        Assert.Equal("年/月/日", Text(date));
        Assert.False(clear.IsVisible);
    });

    [Fact]
    [Upstream("DateField", "en-US 月/日/年；granularity=")]
    public Task English_order_and_granularities() => WpfUi.Run(() =>
    {
        var day = new DateField { Value = "2026-10-20" };
        var month = new DateField { Granularity = DateFieldGranularity.Month, Value = "2026-10" };
        var minute = new DateField { Granularity = DateFieldGranularity.Minute, Value = "2026-10-20T14:30" };
        var stack = new StackPanel { Children = { day, month, minute } };
        Provider.SetLang(stack, Lang.EnUs);
        stack.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(stack);
        WpfUi.Flush();
        Assert.Equal(["10/20/2026", "10/2026", "10/20/2026, 2:30 PM"], new[] { day, month, minute }.Select(Text));
    });

    [Fact]
    [Upstream("DateField", "不可用：段不能聚焦")]
    public Task Disabled_segments_cannot_take_focus() => WpfUi.Run(() =>
    {
        var (date, window) = Show(new DateField { IsEnabled = false });
        date.Group!.Boxes[0].Focus();
        Assert.False(date.Group.Boxes[0].IsKeyboardFocused);
        Assert.False(date.IsEnabled);
    });

    [Fact]
    [Upstream("TimeField", "zh-CN 24 小时制：打 1430 得到 14:30")]
    public Task Twenty_four_hour_time() => WpfUi.Run(() =>
    {
        var (time, window) = Show(new TimeField(), "提醒时间");
        time.Group!.Boxes[0].Focus();
        Type("1430");
        Assert.Equal(("14:30", new TimeOnly(14, 30)), (time.Value, time.Time));
    });

    [Fact]
    [Upstream("TimeField", "en-US 12 小时制：打 a / p 切换上午下午，值仍是 24 小时制")]
    public Task Twelve_hour_time_keeps_a_24_hour_value() => WpfUi.Run(() =>
    {
        var (time, window) = Show(new TimeField { Value = "09:15" }, "At", Lang.EnUs);
        Assert.Equal("9:15 AM", Text(time));
        time.Group!.Boxes[2].Focus();
        Type("p");
        Assert.Equal("21:15", time.Value);
    });

    [Fact]
    [Upstream("TimeField", "秒、min / max")]
    public Task Seconds_and_limits() => WpfUi.Run(() =>
    {
        var (time, _) = Show(new TimeField { Granularity = TimeFieldGranularity.Second, Value = "08:30:15", Min = "09:00", Max = "18:00" });
        Assert.Equal("08:30:15", Text(time));
        Assert.True(time.Group!.IsInvalid);
        time.Time = new TimeOnly(12, 0);
        Assert.Equal("12:00:00", time.Value);
        Assert.False(time.Group.IsInvalid);
    });

    public sealed class Meeting
    {
        [Required]
        public string? Day { get; set; }
    }

    [Fact]
    [Upstream("TimeField", "在表单里：打了一半时表单重置，各段清空；Enter 写回并提交表单")]
    public Task Form_reset_drops_partial_input_and_enter_submits() => WpfUi.Run(() =>
    {
        var submitted = 0;
        var controller = new FormController<Meeting>(new Meeting(), () => Lang.ZhCn)
        {
            OnSubmit = _ =>
            {
                submitted++;
                return Task.CompletedTask;
            },
        };
        var date = new DateField();
        var form = new Form { Controller = controller, Child = new Field { Path = "Day", Label = "日期", Content = date } };
        Provider.SetLang(form, Lang.ZhCn);
        form.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(form);
        WpfUi.Flush();

        date.Group!.Boxes[0].Focus();
        Type("2026");
        Assert.True(date.Segments.Partial);
        controller.Reset();
        WpfUi.Flush();
        Assert.Equal("年/月/日", Text(date));
        date.Group.Boxes[0].Focus();
        Type("20261020");
        Assert.Equal("2026-10-20", controller.Values.Day);
        Press(Key.Enter);
        WpfUi.Until(() => submitted == 1);
    });

    [Fact]
    [Upstream("TimeField", "季度、周：按语言包的排法显示")]
    public Task Quarters_and_weeks_use_the_language_patterns() => WpfUi.Run(() =>
    {
        var (quarter, window) = Show(new DateField { Granularity = DateFieldGranularity.Quarter });
        Assert.Equal("––年第––季度", Text(quarter));
        quarter.Group!.Boxes[0].Focus();
        Type("20264");
        Assert.Equal("2026-Q4", quarter.Value);
        Assert.Equal("2026年第4季度", Text(quarter));
    });
}
