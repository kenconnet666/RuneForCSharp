using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Transfer：勾上之后移到另一边（Values 按移过去的先后）、全选只管看得见的能勾的项、双击直接移、不可用的选项移不动；在 Field 里以标签为名。TreeTransfer：只移叶子，移过去的在树上不可用。</summary>
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

    static IconButton Mover(TransferBase transfer, string label) =>
        transfer.GetVisualDescendants().OfType<IconButton>().Single(button => AutomationProperties.GetName(button) == label);

    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    [Upstream("Transfer", "勾上之后点按钮移到右边，value 按移过去的先后；再移回来")]
    public Task Checked_items_move_across_in_order() => HeadlessUi.Run(() =>
    {
        var transfer = new Transfer { ItemsSource = Fruits, Values = ["grape"] };
        var field = new Field { Label = "水果", Content = transfer };
        var window = HeadlessUi.Show(new StackPanel { Width = 800, Children = { field } });

        var (left, right) = ((Listbox)transfer.SideParts(0).Body, (Listbox)transfer.SideParts(1).Body);
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

        var right = (Listbox)transfer.SideParts(1).Body;
        var apple = right.GetVisualDescendants().OfType<ListboxItem>().Single(item => item.Option?.Value == "apple");
        apple.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));
        HeadlessUi.Flush(window);
        Assert.Equal(["grape", "banana"], transfer.Values);
    });

    sealed record Org(string Id, string Name, Org[]? Children = null);

    [Fact]
    [Upstream("Transfer.browser", "勾上部门、移到右边：只移还没移过去的叶子，它们在树上变成不可用")]
    public Task Tree_moves_only_leaves_not_yet_moved() => HeadlessUi.Run(() =>
    {
        Org[] orgs =
        [
            new("rd", "研发中心", [new("rd-fe", "前端组"), new("rd-be", "后端组"), new("rd-qa", "测试组")]),
            new("mfg", "制造中心", [new("mfg-plant", "组装厂", [new("mfg-plant-1", "一车间"), new("mfg-plant-2", "二车间")]), new("mfg-quality", "质量部")]),
            new("purchase", "采购部"),
        ];
        var transfer = new TreeTransfer
        {
            ItemsSource = orgs, GetKey = org => ((Org)org).Id, GetLabel = org => ((Org)org).Name, GetChildren = org => ((Org)org).Children,
            Values = ["rd-fe", "mfg-quality"], Titles = ["组织机构", "通知的团队"],
        };
        var window = HeadlessUi.Show(new StackPanel { Width = 800, Children = { transfer } });
        var tree = transfer.Tree;
        TreeItemState State(string key) => tree.StateOf(tree.Rows.Single(row => row.Key == key));

        // 研发中心已经有前端组移过去了：半选
        Assert.Equal(CheckState.Indeterminate, State("rd").Checked);
        Assert.Equal("0 / 5", transfer.SideParts(0).Count.Text);
        tree.ClickCheck("rd");
        HeadlessUi.Flush(window);
        Assert.Equal(CheckState.Checked, State("rd").Checked);
        Assert.Equal("2 / 5", transfer.SideParts(0).Count.Text);
        Click(Mover(transfer, "移到通知的团队"));
        HeadlessUi.Flush(window);
        Assert.Equal(["rd-fe", "mfg-quality", "rd-be", "rd-qa"], transfer.Values);
        Assert.Equal(["前端组", "质量部", "后端组", "测试组"], Texts((Listbox)transfer.SideParts(1).Body));

        tree.Expanded = ["rd"];
        HeadlessUi.Flush(window);
        Assert.True(State("rd-be").Disabled);
        Assert.Equal(CheckState.Checked, State("rd-be").Checked);
        Assert.Equal("0 / 3", transfer.SideParts(0).Count.Text);
    });
}
