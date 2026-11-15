using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// CalendarBoard：数据按天放进格子（跨天的每天都有）、超出的收成"还有 N 项"（点了选中那天）、日期数字的读屏名称带日程数、
/// 键盘移动焦点、翻页写回 Month、日期旁边的小字与 DayContent、部件名。今天固定为 2026-10-20。
/// </summary>
public class CalendarBoardTests
{
    sealed record Item(string Title, DateOnly Date, DateOnly? End = null);

    static readonly Item[] Items =
    [
        new("周会", new DateOnly(2026, 10, 19)),
        new("评审", new DateOnly(2026, 10, 19)),
        new("复盘", new DateOnly(2026, 10, 19)),
        new("预算", new DateOnly(2026, 10, 19)),
        new("出差", new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16)),
    ];

    static (CalendarBoard Board, Window Window) Show(CalendarBoard board)
    {
        board.State.Today = () => new DateOnly(2026, 10, 20);
        Provider.SetLang(board, Lang.ZhCn);
        var window = HeadlessUi.Show(board);
        HeadlessUi.Flush(window);
        return (board, window);
    }

    static CalendarBoard WithItems() => new()
    {
        ItemsSource = Items,
        GetEventDate = item => ((Item)item!).Date,
        GetEventEnd = item => ((Item)item!).End ?? ((Item)item!).Date,
        GetEventLabel = item => ((Item)item!).Title,
        Month = "2026-10",
    };

    static Border Cell(CalendarBoard board, int day) => board.Days.First(entry => entry.Date == new DateOnly(2026, 10, day)).Cell;

    static CalendarBoardDayButton Button(CalendarBoard board, int day) => board.Days.First(entry => entry.Date == new DateOnly(2026, 10, day)).Button;

    /// <summary>格子里日程那一列的文字（日程与"还有 N 项"）。</summary>
    static IEnumerable<string> Texts(Border cell) =>
        cell.GetLogicalDescendants().OfType<StackPanel>().Where(panel => panel.Name == CalendarBoardParts.Events)
            .SelectMany(panel => panel.Children).Select(child => child switch
            {
                Border { Child: Text text } => text.Text ?? "",
                ButtonBase button => button.Content as string ?? "",
                _ => "",
            });

    [Fact]
    [Upstream("CalendarBoard", "数据按天放进格子，跨天的每天都有；超出 maxEvents 的收成“还有 N 项”，点了选中那天")]
    public Task Events_fill_their_days_and_more_selects_the_day() => HeadlessUi.Run(() =>
    {
        var (board, window) = Show(WithItems());
        Assert.Equal(["周会", "评审", "复盘", "还有 1 项"], Texts(Cell(board, 19)));
        Assert.Equal(["出差"], Texts(Cell(board, 15)));
        Assert.Equal(["出差"], Texts(Cell(board, 16)));
        Assert.Empty(Texts(Cell(board, 17)));
        Assert.Contains("4 项日程", AutomationProperties.GetName(Button(board, 19)));
        var more = Cell(board, 19).GetLogicalDescendants().OfType<ButtonBase>().Single(button => button.Name == CalendarBoardParts.More);
        more.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        HeadlessUi.Flush(window);
        Assert.Equal("2026-10-19", board.Value);
        Assert.Contains("selected", Cell(board, 19).Classes);
    });

    [Fact]
    [Upstream("CalendarBoard", "键盘：→ 移到下一天，Page Down 翻到下个月并写回 month")]
    public Task Keys_move_focus_and_turning_the_page_writes_the_month() => HeadlessUi.Run(() =>
    {
        var (board, window) = Show(new CalendarBoard { Value = "2026-10-30", Month = "2026-10" });
        CalendarBoardDayButton Focused() => board.Days.Single(entry => entry.Button.IsTabStop).Button;
        Focused().Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.Equal(new DateOnly(2026, 10, 31), Focused().Date);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        Assert.Equal("2026-11", board.Month);
        Assert.Equal(new DateOnly(2026, 11, 30), Focused().Date);
        Assert.True(Focused().IsFocused);
    });

    [Fact]
    [Upstream("CalendarBoard", "方向键、Page Down 移动焦点，Enter 选中")]
    public Task Arrow_keys_and_page_down_move_and_enter_selects() => HeadlessUi.Run(() =>
    {
        var schedule = WithItems();
        schedule.Value = "2026-10-19";
        var (board, window) = Show(schedule);
        CalendarBoardDayButton Focused() => board.Days.Single(entry => entry.Button.IsTabStop).Button;
        Focused().Focus();
        Assert.Equal(new DateOnly(2026, 10, 19), Focused().Date);
        void Press(PhysicalKey key, DateOnly expected)
        {
            window.KeyPressQwerty(key, RawInputModifiers.None);
            Assert.Equal(expected, Focused().Date);
            Assert.True(Focused().IsFocused);
        }
        Press(PhysicalKey.ArrowRight, new DateOnly(2026, 10, 20));
        Press(PhysicalKey.ArrowDown, new DateOnly(2026, 10, 27));
        Press(PhysicalKey.PageDown, new DateOnly(2026, 11, 27));
        Press(PhysicalKey.ArrowUp, new DateOnly(2026, 11, 20));
        Press(PhysicalKey.ArrowLeft, new DateOnly(2026, 11, 19));
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        HeadlessUi.Flush(window);
        Assert.Equal("2026-11-19", board.Value);
        Assert.Contains("selected", board.Days.Single(entry => entry.Date == new DateOnly(2026, 11, 19)).Cell.Classes);
    });

    [Fact]
    [Upstream("CalendarBoard", "getDayNote 是日期旁边的小字；renderDay 拿到这一天的状态")]
    public Task Day_notes_and_day_content() => HeadlessUi.Run(() =>
    {
        var (board, _) = Show(new CalendarBoard
        {
            Month = "2026-10",
            GetDayNote = date => date == new DateOnly(2026, 10, 1) ? "国庆节" : null,
            DayContent = (date, state) => state.Weekend && !state.Outside ? $"周末 {date:yyyy-MM-dd}" : null,
        });
        Assert.Contains(Cell(board, 1).GetLogicalDescendants().OfType<Text>(), text => text.Name == CalendarBoardParts.Note && text.Text == "国庆节");
        // 2026-10-03 是周六
        Assert.Contains(Cell(board, 3).GetLogicalDescendants().OfType<Text>(), text => text.Text == "周末 2026-10-03");
        Assert.DoesNotContain(Cell(board, 5).GetLogicalDescendants().OfType<Text>(), text => text.Text?.StartsWith("周末") == true);
    });

    [Fact]
    [Upstream("CalendarBoard", "部件：日程可以按条给部件属性，页头、表格、格子、日期数字、更多都带 data-part")]
    public Task Parts_are_named_and_events_carry_their_data() => HeadlessUi.Run(() =>
    {
        var (board, _) = Show(WithItems());
        var names = board.GetLogicalDescendants().OfType<Control>().Select(control => control.Name).ToHashSet();
        foreach (var part in new[]
                 {
                     CalendarBoardParts.Header, CalendarBoardParts.Title, CalendarBoardParts.Today, CalendarBoardParts.Previous, CalendarBoardParts.Next,
                     CalendarBoardParts.Grid, CalendarBoardParts.Weekday, CalendarBoardParts.Cell, CalendarBoardParts.DayHead, CalendarBoardParts.DayButton,
                     CalendarBoardParts.Events, CalendarBoardParts.Event, CalendarBoardParts.More,
                 })
            Assert.Contains(part, names);
        // 按条的外观：每条日程的 DataContext 是那条数据（样式、模板按它区分）
        var events = board.GetLogicalDescendants().OfType<Border>().Where(border => border.Name == CalendarBoardParts.Event).Select(border => ((Item)border.DataContext!).Title);
        Assert.Contains("周会", events);
    });

    [Fact]
    public Task Today_and_page_buttons_move_the_page() => HeadlessUi.Run(() =>
    {
        var (board, window) = Show(new CalendarBoard { Month = "2026-08" });
        var next = board.GetLogicalDescendants().OfType<IconButton>().Single(button => button.Name == CalendarBoardParts.Next);
        next.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal("2026-09", board.Month);
        var today = board.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == CalendarBoardParts.Today);
        Assert.Equal("今天", today.Content);
        today.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        HeadlessUi.Flush(window);
        Assert.Equal("2026-10", board.Month);
        Assert.Contains("今天", AutomationProperties.GetName(board.Days.Single(entry => entry.Button.IsTabStop).Button));
    });
}
