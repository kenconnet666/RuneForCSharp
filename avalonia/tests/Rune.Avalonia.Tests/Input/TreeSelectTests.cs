using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// TreeSelect：触发框里的文字、多选只列最上层的整枝（标签可移除、MaxTagCount）、CheckedStrategy、清空；
/// 打开时展开已选的祖先、单选选中后关闭、搜索按节点文字过滤。
/// </summary>
public class TreeSelectTests
{
    sealed record Org(string Id, string Name, Org[]? Children = null);

    static readonly Org[] Orgs =
    [
        new("assembly", "生产制造中心", [new("system", "技术集成团队"), new("testing", "测试团队")]),
        new("purchase", "采购部"),
    ];

    static TreeSelect Create() => new()
    {
        ItemsSource = Orgs,
        GetKey = org => ((Org)org).Id,
        GetLabel = org => ((Org)org).Name,
        GetChildren = org => ((Org)org).Children,
    };

    static T Part<T>(TreeSelect select, string name) where T : Control =>
        select.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    static List<string> Tags(TreeSelect select) => [.. select.TagRow!.ShownTags.Select(tag => (string)tag.Content!)];

    static void Press(Control control, Key key) =>
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = control });

    [Fact]
    [Upstream("TreeSelect", "单选：显示节点的文字，触发按钮是 aria-haspopup=\"tree\" 的 combobox")]
    public Task Single_shows_the_node_label() => HeadlessUi.Run(() =>
    {
        var select = Create();
        select.Value = "testing";
        HeadlessUi.Show(select);
        Assert.Equal("测试团队", Part<TextBlock>(select, SelectParts.Value).Text);
        Assert.Equal(AutomationControlType.ComboBox, select.Peer().GetAutomationControlType());
        Assert.Equal("TreeSelect", select.Peer().GetClassName());
    });

    [Fact]
    [Upstream("TreeSelect", "多选：父节点勾上时只列父节点；清空后显示占位；表单提交勾上的全部")]
    public Task Multiple_lists_top_branches_and_clears() => HeadlessUi.Run(() =>
    {
        var select = Create();
        select.SelectionMode = SelectionMode.Multiple;
        select.Values = ["assembly", "system", "testing", "purchase"];
        select.Clearable = true;
        select.Placeholder = "全部";
        var window = HeadlessUi.Show(select);

        Assert.Equal(["生产制造中心", "采购部"], Tags(select));
        Assert.Equal("生产制造中心、采购部", ((IValueProvider)select.Peer()).Value);
        ((IInvokeProvider)Part<ClearButton>(select, SelectParts.Clear).Peer()).Invoke();
        HeadlessUi.Flush(window);
        Assert.Empty(select.Values!);
        Assert.Equal("全部", Part<TextBlock>(select, SelectParts.Value).Text);
    });

    [Fact]
    [Upstream("TreeSelect", "checkedStrategy：移除标签取消整枝，value 按取法写回（parent 只放父节点、child 只放叶子）")]
    public Task Removing_a_tag_unchecks_the_branch_by_strategy() => HeadlessUi.Run(() =>
    {
        var parents = Create();
        parents.SelectionMode = SelectionMode.Multiple;
        parents.CheckedStrategy = CheckStrategy.Parent;
        parents.Values = ["assembly", "purchase"];
        var leaves = Create();
        leaves.SelectionMode = SelectionMode.Multiple;
        leaves.CheckedStrategy = CheckStrategy.Child;
        leaves.Values = ["system", "testing", "purchase"];
        var window = HeadlessUi.Show(new StackPanel { Children = { parents, leaves } });

        Assert.Equal(["技术集成团队", "测试团队", "采购部"], Tags(leaves));
        Press(parents, Key.Back);
        HeadlessUi.Flush(window);
        Assert.Equal(["assembly"], parents.Values);

        var remove = leaves.TagRow!.ShownTags.ElementAt(1).GetVisualChildren().OfType<CloseButton>().Single();
        ((IInvokeProvider)remove.Peer()).Invoke();
        HeadlessUi.Until(() => leaves.Values!.Count == 2);
        Assert.Equal(["system", "purchase"], leaves.Values);
    });

    [Fact]
    [Upstream("TreeSelect", "maxTagCount：其余收进“+N”")]
    public Task Max_tag_count_folds_the_rest() => HeadlessUi.Run(() =>
    {
        var select = Create();
        select.SelectionMode = SelectionMode.Multiple;
        select.CheckedStrategy = CheckStrategy.Child;
        select.Values = ["system", "testing", "purchase"];
        select.MaxTagCount = 2;
        HeadlessUi.Show(select);
        Assert.Equal(["技术集成团队", "测试团队"], Tags(select));
        Assert.Equal(1, select.TagRow!.HiddenCount);
    });

    [Fact]
    public Task Opening_expands_ancestors_and_picking_closes() => HeadlessUi.Run(() =>
    {
        var select = Create();
        select.Value = "testing";
        var window = HeadlessUi.Show(select);

        ((IExpandCollapseProvider)select.Peer()).Expand();
        HeadlessUi.Flush(window);
        Assert.True(select.IsOpen);
        Assert.Contains("assembly", select.Tree.Expanded);
        Assert.Equal(["testing"], select.Tree.Selected);

        select.Tree.Click("purchase");
        HeadlessUi.Flush(window);
        Assert.Equal("purchase", select.Value);
        Assert.False(select.IsOpen);
    });

    [Fact]
    public Task Multiple_checks_whole_branches_in_the_tree() => HeadlessUi.Run(() =>
    {
        var select = Create();
        select.SelectionMode = SelectionMode.Multiple;
        var window = HeadlessUi.Show(select);
        ((IExpandCollapseProvider)select.Peer()).Expand();
        HeadlessUi.Flush(window);

        select.Tree.ClickCheck("assembly");
        HeadlessUi.Flush(window);
        Assert.Equal(["assembly", "system", "testing"], select.Values);
        Assert.Equal(["生产制造中心"], Tags(select));
        Assert.True(select.IsOpen);
    });

    [Fact]
    public Task Searching_filters_the_tree_by_label() => HeadlessUi.Run(() =>
    {
        var select = Create();
        select.Searchable = true;
        var window = HeadlessUi.Show(select);
        ((IExpandCollapseProvider)select.Peer()).Expand();
        HeadlessUi.Flush(window);

        select.SearchBox.Text = "测试";
        HeadlessUi.Flush(window);
        Assert.Equal(["assembly", "testing"], select.Tree.Rows.Select(row => row.Key));
    });

    [Fact]
    [Upstream("TreeSelect", "服务端搜索：打开时与搜索词停下后按搜索词请求")]
    public Task Remote_search_loads_on_open_and_after_a_pause() => HeadlessUi.Run(() =>
    {
        Org[] tree = [new("hq", "总部", [new("finance", "财务部")]), new("buy", "采购部")];
        var calls = new List<string>();
        var select = Create();
        select.ItemsSource = new[] { new Org("hq", "总部", [new("finance", "财务部")]) };
        select.Value = "finance";
        select.Searchable = true;
        select.Load = (query, _, _) =>
        {
            calls.Add(query);
            return Task.FromResult(OptionsPage.All(query.Length > 0 ? tree[..1] : tree));
        };
        HeadlessUi.Show(select);
        HeadlessUi.Pump(TimeSpan.FromMilliseconds(50));
        Assert.Empty(calls);

        select.IsOpen = true;
        HeadlessUi.Until(() => select.Tree.Rows.Count == 2);
        select.SearchBox.Text = "财务";
        HeadlessUi.Until(() => calls.Count == 2);
        HeadlessUi.Pump(TimeSpan.FromMilliseconds(50));
        Assert.Equal(["", "财务"], calls);
        // 服务端的结果全部展开
        Assert.Equal(["hq", "finance"], select.Tree.Rows.Select(row => row.Key));
        Assert.Equal("财务部", Part<TextBlock>(select, SelectParts.Value).Text);
    });
}
