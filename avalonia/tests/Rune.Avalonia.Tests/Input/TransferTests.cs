using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Transfer：勾上之后移到另一边（Values 按移过去的先后）、全选只管看得见的能勾的项、双击直接移、不可用的选项移不动；在 Field 里以标签为名。</summary>
public class TransferTests
{
    static readonly ListboxOption[] Fruits =
    [
        new("apple", "苹果"),
        new("banana", "香蕉"),
        new("cherry", "樱桃") { IsDisabled = true },
        new("grape", "葡萄"),
    ];

    static List<string> Texts(Listbox list) => [.. ((IEnumerable<object?>)list.ItemsSource!).Cast<ListboxOption>().Select(option => option.Label)];

    static IconButton Mover(Transfer transfer, string label) =>
        transfer.GetVisualDescendants().OfType<IconButton>().Single(button => AutomationProperties.GetName(button) == label);

    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    [Upstream("Transfer", "勾上之后点按钮移到右边，value 按移过去的先后；再移回来")]
    public Task Checked_items_move_across_in_order() => HeadlessUi.Run(() =>
    {
        var transfer = new Transfer { ItemsSource = Fruits, Values = ["grape"] };
        var field = new Field { Label = "水果", Content = transfer };
        var window = HeadlessUi.Show(new StackPanel { Width = 800, Children = { field } });

        var (left, right) = (transfer.SideParts(0).List, transfer.SideParts(1).List);
        Assert.Equal(["葡萄"], Texts(right));
        var (toRight, toLeft) = (Mover(transfer, "移到已选"), Mover(transfer, "移到可选"));
        Assert.False(toRight.IsEnabled);
        left.Values = ["banana", "apple"];
        HeadlessUi.Flush(window);
        Assert.True(toRight.IsEnabled);
        Assert.Equal("2 / 3", transfer.SideParts(0).Count.Text);
        Click(toRight);
        HeadlessUi.Flush(window);
        Assert.Equal(["grape", "banana", "apple"], transfer.Values);
        Assert.Equal(["樱桃"], Texts(left));
        Assert.Contains("2", transfer.Announcement);

        right.Values = ["grape"];
        HeadlessUi.Flush(window);
        Click(toLeft);
        HeadlessUi.Flush(window);
        Assert.Equal(["banana", "apple"], transfer.Values);
    });

    [Fact]
    [Upstream("Transfer", "全选只勾看得见、能勾的项；双击一项直接移过去")]
    public Task Select_all_covers_what_is_shown_and_double_click_moves() => HeadlessUi.Run(() =>
    {
        var transfer = new Transfer { ItemsSource = Fruits, Searchable = true };
        var window = HeadlessUi.Show(new StackPanel { Width = 800, Children = { transfer } });
        var (_, checkAll, search, _) = transfer.SideParts(0);
        var toRight = Mover(transfer, "移到已选");

        search.Text = "葡";
        HeadlessUi.Flush(window);
        checkAll.IsChecked = true;
        HeadlessUi.Flush(window);
        Click(toRight);
        HeadlessUi.Flush(window);
        Assert.Equal(["grape"], transfer.Values);

        search.Text = "";
        HeadlessUi.Flush(window);
        // 樱桃不可用：全选也不勾它
        checkAll.IsChecked = true;
        HeadlessUi.Flush(window);
        Click(toRight);
        HeadlessUi.Flush(window);
        Assert.Equal(["grape", "apple", "banana"], transfer.Values);

        var right = transfer.SideParts(1).List;
        var apple = right.GetVisualDescendants().OfType<ListboxItem>().Single(item => item.Option?.Value == "apple");
        apple.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));
        HeadlessUi.Flush(window);
        Assert.Equal(["grape", "banana"], transfer.Values);
    });
}
