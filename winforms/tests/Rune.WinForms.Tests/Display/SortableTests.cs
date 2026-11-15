using System.Collections.ObjectModel;
using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>SortableControl：把手的名称与说明；空格拿起、方向键移动、放下，顺序写回、每一步播报、焦点留在把手上；Esc 取消；指针拖过中线重排。分组见 Avalonia 的 SortableTests（孪生文件）。</summary>
public class SortableTests
{
    sealed record Todo(string Id, string Title);

    static ObservableCollection<Todo> Tasks() => [new("a", "写文档"), new("b", "评审"), new("c", "发版")];

    static SortableControl Create(ObservableCollection<Todo> tasks) => new()
    {
        ItemsSource = tasks,
        GetKey = task => ((Todo)task).Id,
        GetLabel = task => ((Todo)task).Title,
        AccessibleName = "待办",
        Width = 300,
    };

    static SortableItemControl Row(SortableControl list, string key) => list.Items.Single(row => row.Key == key);

    static void Press(SortableControl list, string key, Keys pressed)
    {
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Row(list, key).Handle, [new KeyEventArgs(pressed)]);
        WinFormsUi.Pump();
    }

    [Fact]
    [Upstream("Sortable", "把手是带名称的按钮，经 aria-describedby 关联操作说明")]
    public Task Handles_are_named_buttons_with_instructions() => WinFormsUi.Run(() =>
    {
        var list = Create(Tasks());
        using var form = WinFormsUi.Show(list);

        var handle = Row(list, "a").Handle;
        Assert.Equal((AccessibleRole.PushButton, "移动 写文档"), (handle.AccessibilityObject.Role, handle.AccessibleName));
        Assert.Contains("按空格拿起", handle.AccessibleDescription);
        Assert.Equal((AccessibleRole.List, "待办"), (list.AccessibilityObject.Role, list.AccessibleName));
        Assert.Equal(("第 1 项，共 3 项", AccessibleRole.ListItem), (Row(list, "a").AccessibleDescription, Row(list, "a").AccessibleRole));
    });

    [Fact]
    [Upstream("Sortable", "空格拿起、↓ 移动一位、空格放下：顺序写回，每一步都播报")]
    public Task Keyboard_grabs_moves_and_drops() => WinFormsUi.Run(() =>
    {
        var tasks = Tasks();
        var list = Create(tasks);
        var moves = new List<(int From, int To)>();
        list.Reordered += (_, e) => moves.Add((e.From, e.To));
        using var form = WinFormsUi.Show(list);
        var handle = Row(list, "a").Handle;
        handle.Focus();

        Press(list, "a", Keys.Space);
        Assert.Equal(("a", "已拿起 写文档，在第 1 位，共 3 项"), (list.Active, list.Announced));
        Press(list, "a", Keys.Down);
        Assert.Equal(["b", "a", "c"], tasks.Select(task => task.Id));
        Assert.Equal("写文档 移到第 2 位，共 3 项", list.Announced);
        Assert.Equal(["b", "a", "c"], list.Items.Select(row => row.Key));
        Assert.True(Row(list, "b").Top < Row(list, "a").Top);
        Assert.True(handle.Focused);
        Press(list, "a", Keys.End);
        Assert.Equal(["b", "c", "a"], tasks.Select(task => task.Id));
        Press(list, "a", Keys.Space);
        Assert.Null(list.Active);
        Assert.Equal("已放下 写文档，在第 3 位，共 3 项", list.Announced);
        Assert.Equal([(0, 1), (1, 2)], moves);
    });

    [Fact]
    [Upstream("Sortable", "Esc 取消：回到拿起时的位置")]
    public Task Escape_cancels_back_to_the_start() => WinFormsUi.Run(() =>
    {
        var tasks = Tasks();
        var list = Create(tasks);
        using var form = WinFormsUi.Show(list);

        Press(list, "c", Keys.Enter);
        Press(list, "c", Keys.Up);
        Press(list, "c", Keys.Up);
        Assert.Equal(["c", "a", "b"], tasks.Select(task => task.Id));
        Press(list, "c", Keys.Escape);
        Assert.Equal(["a", "b", "c"], tasks.Select(task => task.Id));
        Assert.Equal("已取消，发版 回到第 3 位", list.Announced);
    });

    [Fact]
    public Task Dragging_past_midlines_reorders() => WinFormsUi.Run(() =>
    {
        var tasks = Tasks();
        var list = Create(tasks);
        using var form = WinFormsUi.Show(list);
        var first = Row(list, "a");
        var start = new Point(10, first.Top + first.Height / 2);

        list.Press("a", start);
        list.DragTo("a", start with { Y = start.Y + 1 });
        Assert.Null(list.Active);
        var second = Row(list, "b");
        list.DragTo("a", new Point(10, second.Top + second.Height / 2 + 2));
        Assert.Equal(["b", "a", "c"], tasks.Select(task => task.Id));
        Assert.NotEqual(0, Row(list, "a").Translate);
        list.Release("a");
        Assert.Equal(0, Row(list, "a").Translate);
        Assert.Null(list.Active);
    });
}
