using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>Sortable 与 SortableModel：把手的名称与说明；空格拿起、方向键移动、放下，顺序写回、每一步播报、焦点留在把手上；Esc 取消；指针拖过中线重排；分组时换组、不能放的弹回。</summary>
public class SortableTests
{
    sealed record Task_(string Id, string Title);

    static ObservableCollection<Task_> Tasks() => [new("a", "写文档"), new("b", "评审"), new("c", "发版")];

    static Sortable Create(ObservableCollection<Task_> tasks)
    {
        var list = new Sortable { ItemsSource = tasks, GetKey = task => ((Task_)task).Id, GetLabel = task => ((Task_)task).Title, Width = 300 };
        AutomationProperties.SetName(list, "待办");
        return list;
    }

    static SortableItem Row(Sortable list, string key) => list.Items.Single(row => row.Key == key);

    static void Press(Sortable list, string key, Key pressed) =>
        Row(list, key).Handle.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = pressed, Source = Row(list, key).Handle });

    [Fact]
    [Upstream("Sortable", "把手是带名称的按钮，经 aria-describedby 关联操作说明")]
    public Task Handles_are_named_buttons_with_instructions() => HeadlessUi.Run(() =>
    {
        var list = Create(Tasks());
        HeadlessUi.Flush(HeadlessUi.Show(list));

        var handle = Row(list, "a").Handle;
        Assert.Equal((AutomationControlType.Button, "移动 写文档"), (handle.Peer().GetAutomationControlType(), handle.Peer().GetName()));
        Assert.Contains("按空格拿起", AutomationProperties.GetHelpText(handle));
        Assert.Equal((AutomationControlType.List, "待办"), (list.Peer().GetAutomationControlType(), list.Peer().GetName()));
        Assert.Equal(AutomationControlType.ListItem, Row(list, "a").Peer().GetAutomationControlType());
    });

    [Fact]
    [Upstream("Sortable", "空格拿起、↓ 移动一位、空格放下：顺序写回，每一步都播报")]
    public Task Keyboard_grabs_moves_and_drops() => HeadlessUi.Run(() =>
    {
        var tasks = Tasks();
        var list = Create(tasks);
        var moves = new List<(int From, int To)>();
        list.Reordered += (_, e) => moves.Add((e.From, e.To));
        var window = HeadlessUi.Show(list);
        HeadlessUi.Flush(window);
        var handle = Row(list, "a").Handle;
        handle.Focus();

        Press(list, "a", Key.Space);
        Assert.Equal(("a", "已拿起 写文档，在第 1 位，共 3 项"), (list.Active, list.Announced));
        Press(list, "a", Key.Down);
        Assert.Equal(["b", "a", "c"], tasks.Select(task => task.Id));
        Assert.Equal("写文档 移到第 2 位，共 3 项", list.Announced);
        Assert.Equal(["b", "a", "c"], list.Items.Select(row => row.Key));
        Assert.True(handle.IsFocused);
        Press(list, "a", Key.End);
        Assert.Equal(["b", "c", "a"], tasks.Select(task => task.Id));
        Press(list, "a", Key.Space);
        Assert.Null(list.Active);
        Assert.Equal("已放下 写文档，在第 3 位，共 3 项", list.Announced);
        Assert.Equal([(0, 1), (1, 2)], moves);
    });

    [Fact]
    [Upstream("Sortable", "Esc 取消：回到拿起时的位置")]
    public Task Escape_cancels_back_to_the_start() => HeadlessUi.Run(() =>
    {
        var tasks = Tasks();
        var list = Create(tasks);
        HeadlessUi.Flush(HeadlessUi.Show(list));

        Press(list, "c", Key.Enter);
        Press(list, "c", Key.Up);
        Press(list, "c", Key.Up);
        Assert.Equal(["c", "a", "b"], tasks.Select(task => task.Id));
        Press(list, "c", Key.Escape);
        Assert.Equal(["a", "b", "c"], tasks.Select(task => task.Id));
        Assert.Equal("已取消，发版 回到第 3 位", list.Announced);
    });

    [Fact]
    public Task Dragging_past_midlines_reorders() => HeadlessUi.Run(() =>
    {
        var tasks = Tasks();
        var list = Create(tasks);
        var window = HeadlessUi.Show(list);
        HeadlessUi.Flush(window);
        var first = Row(list, "a");
        var start = new Point(10, first.Bounds.Center.Y);

        list.Press("a", start);
        list.DragTo("a", start + new Point(0, 2));
        Assert.Null(list.Active);
        // 越过评审的中线（第二项的中心）
        list.DragTo("a", new Point(10, Row(list, "b").Bounds.Center.Y + 2));
        Assert.Equal(["b", "a", "c"], tasks.Select(task => task.Id));
        Assert.NotEqual(default, Row(list, "a").Translate);
        list.Release("a");
        Assert.Equal(default, Row(list, "a").Translate);
        Assert.Null(list.Active);
    });

    [Fact]
    public void Groups_move_across_and_reject()
    {
        var cards = new List<(string Id, string Column)> { ("1", "todo"), ("2", "todo"), ("3", "done") };
        var moved = new List<(string, string, int)>();
        string? rejected = null;
        var model = new SortableModel<(string Id, string Column)>(() => cards, card => card.Id, () => Lang.ZhCn)
        {
            Groups = new SortableGroups<(string Id, string Column)>
            {
                Keys = () => ["todo", "doing", "done"],
                Of = card => card.Column,
                Label = column => column.ToUpperInvariant(),
                CanMove = (_, to) => to == "doing" ? "进行中已满" : null,
                Moved = (card, to, index) => moved.Add((card.Id, to, index)),
                Rejected = (_, _, reason) => rejected = reason,
            },
            Reordered = (next, _) => cards = [.. next],
        };

        model.Press("1", SortableKey.Toggle, horizontal: false);
        Assert.Equal("已拿起 1，在 TODO 第 1 位", model.Announcement);
        model.Press("1", SortableKey.Right, horizontal: false);
        Assert.Equal(new SortableTarget("doing", 0, "进行中已满"), model.Target);
        Assert.Equal("DOING：不能放，进行中已满", model.Announcement);
        model.Press("1", SortableKey.Toggle, horizontal: false);
        Assert.Equal("进行中已满", rejected);

        model.Press("2", SortableKey.Toggle, horizontal: false);
        model.Press("2", SortableKey.Right, horizontal: false);
        model.Press("2", SortableKey.Right, horizontal: false);
        model.Press("2", SortableKey.End, horizontal: false);
        model.Press("2", SortableKey.Toggle, horizontal: false);
        Assert.Equal([("2", "done", 1)], moved);
        Assert.Equal("已把 2 放到 DONE 第 2 位", model.Announcement);
        Assert.Equal(2, SortableModel<int>.IndexAt([10, 30, 50], 35));
    }
}
