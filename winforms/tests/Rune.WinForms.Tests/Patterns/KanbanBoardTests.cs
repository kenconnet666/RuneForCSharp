using System.Collections.ObjectModel;
using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>KanbanBoardControl：列与计数、空列；键盘拿起、换列、调整落点、放下，报告 Moved 或 Rejected 并播报；列内排序；Esc 取消；指针拖到别的列。</summary>
public class KanbanBoardTests
{
    sealed record Card(string Id, string Title, string Stage);

    static readonly KanbanColumn[] Columns = [new("todo", "待处理"), new("doing", "处理中"), new("done", "已完成")];

    static int IndexOf(string key) => Array.FindIndex(Columns, column => column.Key == key);

    static KanbanBoardControl Create(IEnumerable<Card>? cards = null) => new()
    {
        Columns = Columns,
        ItemsSource = cards ?? [new Card("a", "写文档", "todo"), new Card("b", "修 bug", "doing")],
        GetKey = card => ((Card)card).Id,
        GetColumn = card => ((Card)card).Stage,
        GetLabel = card => ((Card)card).Title,
        CanMove = (card, to) => IndexOf(to) == IndexOf(((Card)card).Stage) + 1 ? null : "只能到下一列",
        AccessibleName = "看板",
        Width = 900,
    };

    static void Press(KanbanBoardControl board, string key, Keys pressed)
    {
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(board.Cards[key].Handle, [new KeyEventArgs(pressed)]);
        WinFormsUi.Pump();
    }

    [Fact]
    [Upstream("KanbanBoard", "一列一个 section（列名是它的名称），列名旁是卡片数")]
    public Task Columns_have_names_counts_and_an_empty_note() => WinFormsUi.Run(() =>
    {
        var board = Create();
        using var form = WinFormsUi.Show(board);

        Assert.Equal(3, board.ColumnViews.Count);
        var todo = board.ColumnViews["todo"];
        Assert.Equal((AccessibleRole.Grouping, "待处理"), (todo.AccessibleRole, todo.AccessibleName));
        Assert.Equal("1", todo.CountPart.Text);
        Assert.Equal((true, "没有"), (board.ColumnViews["done"].EmptyPart.Visible, board.ColumnViews["done"].EmptyPart.Text));
        Assert.Equal("移动 写文档", board.Cards["a"].Handle.AccessibleName);
        Assert.True(board.ColumnViews["doing"].Left > todo.Right);
    });

    [Fact]
    [Upstream("KanbanBoard", "键盘：拿起、→ 换到下一列、放下，报告 onMove 并播报")]
    public Task Keyboard_moves_to_the_next_column() => WinFormsUi.Run(() =>
    {
        var board = Create();
        var moves = new List<(object Item, string To, int Index)>();
        board.Moved += (_, e) => moves.Add((e.Item, e.To, e.Index));
        using var form = WinFormsUi.Show(board);

        Press(board, "a", Keys.Space);
        Assert.Equal("a", board.Active);
        Assert.Contains("写文档", board.Announced);
        Press(board, "a", Keys.Right);
        Assert.Contains("处理中", board.Announced);
        Press(board, "a", Keys.Enter);
        Assert.Equal(("a", "doing", 0), (((Card)moves.Single().Item).Id, moves.Single().To, moves.Single().Index));
    });

    [Fact]
    [Upstream("KanbanBoard", "不能放的列：经过时那一列标出原因，放下时弹回、报告 onReject 而不是 onMove")]
    public Task A_denied_column_shows_the_reason_and_rejects() => WinFormsUi.Run(() =>
    {
        var board = Create();
        var moved = false;
        KanbanRejectedEventArgs? rejected = null;
        board.Moved += (_, _) => moved = true;
        board.Rejected += (_, e) => rejected = e;
        using var form = WinFormsUi.Show(board);

        Press(board, "a", Keys.Space);
        Press(board, "a", Keys.Right);
        Press(board, "a", Keys.Right);
        var done = board.ColumnViews["done"];
        Assert.Equal(("denied", "只能到下一列", true), (done.DropState, done.ReasonPart.Text, done.ReasonPart.Visible));
        Press(board, "a", Keys.Space);
        Assert.False(moved);
        Assert.Equal(("done", "只能到下一列"), (rejected!.To, rejected.Reason));
        Assert.Contains("只能到下一列", board.Announced);
    });

    [Fact]
    [Upstream("KanbanBoard", "换到别的列后 ↑ / ↓ 调整落点：插入线跟着走，onMove 带上位置")]
    public Task Arrows_adjust_the_drop_position() => WinFormsUi.Run(() =>
    {
        var board = Create();
        var moves = new List<(string, int)>();
        board.Moved += (_, e) => moves.Add((e.To, e.Index));
        using var form = WinFormsUi.Show(board);

        Press(board, "a", Keys.Space);
        Press(board, "a", Keys.Right);
        var doing = board.ColumnViews["doing"];
        Assert.Equal(("allowed", 0), (doing.DropState, doing.IndicatorIndex));
        Press(board, "a", Keys.Down);
        Assert.Equal(1, doing.IndicatorIndex);
        Assert.Contains("处理中 第 2 位", board.Announced);
        Press(board, "a", Keys.Space);
        Assert.Equal([("doing", 1)], moves);
        Assert.Null(doing.IndicatorIndex);
    });

    [Fact]
    [Upstream("KanbanBoard", "给了 onReorder 时列内用 ↑ / ↓ 排序，写回整个数组")]
    public Task Subscribing_to_reordered_sorts_within_a_column() => WinFormsUi.Run(() =>
    {
        var cards = new ObservableCollection<Card> { new("a", "写文档", "todo"), new("b", "修 bug", "doing"), new("c", "发版", "todo") };
        var board = Create(cards);
        board.Reordered += (_, _) => { };
        using var form = WinFormsUi.Show(board);

        Press(board, "a", Keys.Space);
        Press(board, "a", Keys.Down);
        Assert.Equal(["b", "c", "a"], cards.Select(card => card.Id));
        Assert.Equal(["c", "a"], board.ColumnViews["todo"].Cards.Select(card => card.Key));
        Assert.True(board.Cards["c"].Top < board.Cards["a"].Top);
    });

    [Fact]
    [Upstream("KanbanBoard", "Esc 取消：不报告，卡片留在原来的列")]
    public Task Escape_cancels_without_reporting() => WinFormsUi.Run(() =>
    {
        var board = Create();
        var reported = false;
        board.Moved += (_, _) => reported = true;
        board.Rejected += (_, _) => reported = true;
        using var form = WinFormsUi.Show(board);

        Press(board, "b", Keys.Enter);
        Press(board, "b", Keys.Right);
        Press(board, "b", Keys.Escape);
        Assert.Null(board.Active);
        Assert.False(reported);
        Assert.Contains("处理中", board.Announced);
        Assert.Null(board.ColumnViews["done"].DropState);
    });

    [Fact]
    public Task Dragging_onto_another_column_reports_the_move() => WinFormsUi.Run(() =>
    {
        var board = Create();
        var moves = new List<(string, int)>();
        board.Moved += (_, e) => moves.Add((e.To, e.Index));
        using var form = WinFormsUi.Show(board);
        Point At(Control control, int x, int y) => board.PointToClient(control.PointToScreen(new Point(x, y)));
        var start = At(board.Cards["a"], 20, 10);
        var below = At(board.Cards["b"], 20, board.Cards["b"].Height + 4);

        board.Press("a", start);
        board.DragTo("a", start with { X = start.X + 1 });
        Assert.Null(board.Ghost);
        board.DragTo("a", below);
        Assert.NotNull(board.Ghost);
        Assert.Equal(("allowed", 1), (board.ColumnViews["doing"].DropState, board.ColumnViews["doing"].IndicatorIndex));
        board.Release("a");
        Assert.Null(board.Ghost);
        Assert.Equal([("doing", 1)], moves);
    });
}
