using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>KanbanBoard：列与计数、空列；键盘拿起、换列、调整落点、放下，报告 Moved 或 Rejected 并播报；列内排序；Esc 取消；指针拖到别的列。</summary>
public class KanbanBoardTests
{
    sealed record Card(string Id, string Title, string Stage);

    static readonly KanbanColumn[] Columns = [new("todo", "待处理"), new("doing", "处理中"), new("done", "已完成")];

    static int IndexOf(string key) => Array.FindIndex(Columns, column => column.Key == key);

    static KanbanBoard Create(IEnumerable<Card>? cards = null)
    {
        var board = new KanbanBoard
        {
            Columns = Columns,
            ItemsSource = cards ?? [new Card("a", "写文档", "todo"), new Card("b", "修 bug", "doing")],
            GetKey = card => ((Card)card).Id,
            GetColumn = card => ((Card)card).Stage,
            GetLabel = card => ((Card)card).Title,
            CanMove = (card, to) => IndexOf(to) == IndexOf(((Card)card).Stage) + 1 ? null : "只能到下一列",
            ItemTemplate = Title(),
            Width = 900,
        };
        AutomationProperties.SetName(board, "看板");
        return board;
    }

    /// <summary>卡片的内容：标题（WPF 的绑定看不到嵌套的私有类型，按属性名绑定照样能取到）。</summary>
    static DataTemplate Title()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Title"));
        return new DataTemplate { VisualTree = text };
    }

    static StackPanel Host(UIElement child)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(child);
        return host;
    }

    static AutomationPeer Peer(UIElement element) => UIElementAutomationPeer.CreatePeerForElement(element);

    static void Press(KanbanBoard board, string key, Key pressed)
    {
        var handle = board.Cards[key].Handle;
        handle.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(handle)!, 0, pressed) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("KanbanBoard", "一列一个 section（列名是它的名称），列名旁是卡片数")]
    public Task Columns_have_names_counts_and_an_empty_note() => WpfUi.Run(() =>
    {
        var board = Create();
        WpfUi.Show(Host(board));
        WpfUi.Flush();

        Assert.Equal(3, board.ColumnViews.Count);
        var todo = board.ColumnViews["todo"];
        Assert.Equal((AutomationControlType.Group, "待处理"), (Peer(todo).GetAutomationControlType(), Peer(todo).GetName()));
        Assert.Equal("1", todo.CountPart.Content);
        Assert.Contains(board.ColumnViews["done"].Empty, board.ColumnViews["done"].List.Children.Cast<UIElement>());
        Assert.Equal("移动 写文档", AutomationProperties.GetName(board.Cards["a"].Handle));
        Assert.Equal((AutomationControlType.Group, "看板"), (Peer(board).GetAutomationControlType(), Peer(board).GetName()));
    });

    [Fact]
    [Upstream("KanbanBoard", "键盘：拿起、→ 换到下一列、放下，报告 onMove 并播报")]
    public Task Keyboard_moves_to_the_next_column() => WpfUi.Run(() =>
    {
        var board = Create();
        var moves = new List<(object Item, string To, int Index)>();
        board.Moved += (_, e) => moves.Add((e.Item, e.To, e.Index));
        WpfUi.Show(Host(board));
        WpfUi.Flush();

        Press(board, "a", Key.Space);
        Assert.Equal("a", board.Active);
        Assert.Contains("写文档", board.Announced);
        Press(board, "a", Key.Right);
        Assert.Contains("处理中", board.Announced);
        Press(board, "a", Key.Enter);
        Assert.Equal(("a", "doing", 0), (((Card)moves.Single().Item).Id, moves.Single().To, moves.Single().Index));
    });

    [Fact]
    [Upstream("KanbanBoard", "不能放的列：经过时那一列标出原因，放下时弹回、报告 onReject 而不是 onMove")]
    public Task A_denied_column_shows_the_reason_and_rejects() => WpfUi.Run(() =>
    {
        var board = Create();
        var moved = false;
        KanbanRejectedEventArgs? rejected = null;
        board.Moved += (_, _) => moved = true;
        board.Rejected += (_, e) => rejected = e;
        WpfUi.Show(Host(board));
        WpfUi.Flush();

        Press(board, "a", Key.Space);
        Press(board, "a", Key.Right);
        Press(board, "a", Key.Right);
        var done = board.ColumnViews["done"];
        Assert.Equal(("denied", "只能到下一列"), (done.DropState, done.ReasonPart.Text));
        Press(board, "a", Key.Space);
        Assert.False(moved);
        Assert.Equal(("done", "只能到下一列"), (rejected!.To, rejected.Reason));
        Assert.Contains("只能到下一列", board.Announced);
    });

    [Fact]
    [Upstream("KanbanBoard", "换到别的列后 ↑ / ↓ 调整落点：插入线跟着走，onMove 带上位置")]
    public Task Arrows_adjust_the_drop_position() => WpfUi.Run(() =>
    {
        var board = Create();
        var moves = new List<(string, int)>();
        board.Moved += (_, e) => moves.Add((e.To, e.Index));
        WpfUi.Show(Host(board));
        WpfUi.Flush();

        Press(board, "a", Key.Space);
        Press(board, "a", Key.Right);
        var doing = board.ColumnViews["doing"];
        Assert.Equal("allowed", doing.DropState);
        Assert.Equal(0, doing.List.Children.IndexOf(doing.Indicator));
        Press(board, "a", Key.Down);
        Assert.Equal(1, doing.List.Children.IndexOf(doing.Indicator));
        Assert.Contains("处理中 第 2 位", board.Announced);
        Press(board, "a", Key.Space);
        Assert.Equal([("doing", 1)], moves);
        Assert.DoesNotContain(doing.Indicator, doing.List.Children.Cast<UIElement>());
    });

    [Fact]
    [Upstream("KanbanBoard", "给了 onReorder 时列内用 ↑ / ↓ 排序，写回整个数组")]
    public Task Subscribing_to_reordered_sorts_within_a_column() => WpfUi.Run(() =>
    {
        var cards = new ObservableCollection<Card> { new("a", "写文档", "todo"), new("b", "修 bug", "doing"), new("c", "发版", "todo") };
        var board = Create(cards);
        board.Reordered += (_, _) => { };
        WpfUi.Show(Host(board));
        WpfUi.Flush();

        Press(board, "a", Key.Space);
        Press(board, "a", Key.Down);
        Assert.Equal(["b", "c", "a"], cards.Select(card => card.Id));
        Assert.Equal(["c", "a"], board.ColumnViews["todo"].List.Children.OfType<KanbanCard>().Select(card => card.Key));
    });

    [Fact]
    [Upstream("KanbanBoard", "Esc 取消：不报告，卡片留在原来的列")]
    public Task Escape_cancels_without_reporting() => WpfUi.Run(() =>
    {
        var board = Create();
        var reported = false;
        board.Moved += (_, _) => reported = true;
        board.Rejected += (_, _) => reported = true;
        WpfUi.Show(Host(board));
        WpfUi.Flush();

        Press(board, "b", Key.Enter);
        Press(board, "b", Key.Right);
        Press(board, "b", Key.Escape);
        Assert.Null(board.Active);
        Assert.False(reported);
        Assert.Contains("处理中", board.Announced);
        Assert.Null(board.ColumnViews["done"].DropState);
    });

    [Fact]
    public Task Dragging_onto_another_column_reports_the_move() => WpfUi.Run(() =>
    {
        var board = Create();
        var moves = new List<(string, int)>();
        board.Moved += (_, e) => moves.Add((e.To, e.Index));
        WpfUi.Show(Host(board));
        WpfUi.Flush();
        var card = board.Cards["a"];
        var start = card.TranslatePoint(new Point(20, 10), board);
        var doing = board.ColumnViews["doing"];
        var below = board.Cards["b"].TranslatePoint(new Point(20, board.Cards["b"].ActualHeight + 4), board);

        board.Press("a", start);
        board.DragTo("a", start + new Vector(1, 1));
        Assert.Null(board.Ghost);
        board.DragTo("a", below);
        WpfUi.Flush();
        Assert.NotNull(board.Ghost);
        Assert.Equal(0.4, card.Opacity);
        Assert.Equal(("allowed", 1), (doing.DropState, doing.List.Children.IndexOf(doing.Indicator)));
        board.Release("a");
        Assert.Null(board.Ghost);
        Assert.Equal([("doing", 1)], moves);
    });
}
