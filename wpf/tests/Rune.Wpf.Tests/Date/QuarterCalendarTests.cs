using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>QuarterCalendar：外面改值翻到那一年、读屏名称、键盘移动与换年、Enter 选中、区间与范围外的季度。今天固定为 2026-10-20（第四季度）。</summary>
public class QuarterCalendarTests
{
    static QuarterCalendar Show(QuarterCalendar calendar)
    {
        calendar.State.Today = () => new DateOnly(2026, 10, 20);
        Provider.SetLang(calendar, Lang.ZhCn);
        calendar.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(calendar);
        WpfUi.Flush();
        return calendar;
    }

    static CalendarCellButton Cell(QuarterCalendar calendar, int quarter) => calendar.Cells.ElementAt(quarter - 1);

    static void Click(UIElement button)
    {
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        WpfUi.Flush();
    }

    static void Press(Key key)
    {
        var target = (UIElement)Keyboard.FocusedElement;
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("QuarterCalendar", "QuarterCalendar：父级从外面改了值就翻到那一年")]
    public Task Outside_values_move_to_their_year() => WpfUi.Run(() =>
    {
        var calendar = Show(new QuarterCalendar { Value = "2026-Q4" });
        Assert.Equal("2026年", calendar.Heading.Text);
        Assert.Equal("2026年第4季度", AutomationProperties.GetName(Cell(calendar, 4)));
        Assert.Equal("四季度", Cell(calendar, 4).Content);
        Assert.True(Cell(calendar, 4).IsTabStop);
        Assert.True(Cell(calendar, 4).IsMarked && Cell(calendar, 4).IsToday);
        calendar.Value = "2024-Q2";
        WpfUi.Flush();
        Assert.Equal("2024年", calendar.Heading.Text);
        Assert.True(Cell(calendar, 2).IsMarked);
    });

    [Fact]
    [Upstream("QuarterCalendar", "QuarterCalendar 的键盘")]
    public Task Keys_move_across_years_and_enter_picks() => WpfUi.Run(() =>
    {
        var picks = 0;
        var calendar = Show(new QuarterCalendar { Value = "2026-Q4" });
        calendar.Picked += (_, _) => picks++;
        Cell(calendar, 4).Focus();
        Press(Key.Right);
        Assert.Equal("2027年", calendar.Heading.Text);
        Assert.True(Cell(calendar, 1).IsKeyboardFocused);
        Press(Key.PageUp);
        Assert.Equal("2026年", calendar.Heading.Text);
        Assert.True(Cell(calendar, 1).IsKeyboardFocused);
        Press(Key.Enter);
        Assert.Equal("2026-Q1", calendar.Value);
        Assert.Equal(1, picks);
    });

    [Fact]
    public Task Range_and_limits() => WpfUi.Run(() =>
    {
        var calendar = Show(new QuarterCalendar { SelectionMode = CalendarSelectionMode.Range, Min = "2026-04-01" });
        Assert.True(Cell(calendar, 1).IsOff);
        Click(Cell(calendar, 1));
        Assert.Null(calendar.State.Anchor);
        Click(Cell(calendar, 3));
        Assert.Equal("请选择结束日期", AutomationProperties.GetHelpText(Cell(calendar, 2)));
        Click(calendar.PageButtons.Next);
        Assert.Equal("2027年", calendar.Heading.Text);
        Click(Cell(calendar, 2));
        Assert.Equal(new IsoDateRange("2026-Q3", "2027-Q2"), calendar.RangeValue);
        Assert.True(((CalendarCell)Cell(calendar, 1).Parent).IsInRange);
        Assert.True(Cell(calendar, 2).IsMarked);
        Assert.Throws<ArgumentException>(() => calendar.SelectionMode = CalendarSelectionMode.Multiple);
    });
}
