using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Cascader：触发框显示整条路径；一列一列往下点、叶子选中后关闭；ChangeOnSelect 每一级都能选；键盘 ↓ → Enter；懒加载下一级；搜索整条路径；部件名。
/// </summary>
public class CascaderTests
{
    sealed class Region(string code, string name, List<Region>? children = null)
    {
        public string Code { get; } = code;

        public string Name { get; } = name;

        public List<Region>? Children { get; set; } = children;

        public bool? Leaf { get; init; }
    }

    static List<Region> Regions() =>
    [
        new("zj", "浙江", [new("hz", "杭州", [new("xh", "西湖区"), new("bj", "滨江区")]), new("nb", "宁波", [new("yz", "鄞州区")])]),
        new("js", "江苏", [new("sz", "苏州", [])]),
    ];

    static Cascader Create(IEnumerable<Region>? items = null) => new()
    {
        ItemsSource = items ?? Regions(),
        GetKey = (region, _) => ((Region)region).Code,
        GetLabel = region => ((Region)region).Name,
        GetChildren = region => ((Region)region).Children,
    };

    static string ValueText(Cascader cascader) =>
        cascader.GetVisualDescendants().OfType<TextBlock>().First(text => text.Name == SelectParts.Value).Text ?? "";

    static List<string?> Texts(Cascader cascader, int level) =>
        [.. cascader.Columns[level].Options.Select(option => option.GetVisualDescendants().OfType<TextBlock>().First(text => text.Name == CascaderParts.Label).Text)];

    static CascaderOption Option(Cascader cascader, int level, string text) =>
        cascader.Columns[level].Options.First(option => option.GetVisualDescendants().OfType<TextBlock>().First(label => label.Name == CascaderParts.Label).Text == text);

    static void Click(CascaderOption option) => ((ISelectionItemProvider)option.Peer()).Select();

    static void Press(Control control, Key key) =>
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = control });

    [Fact]
    [Upstream("Cascader", "触发框显示整条路径；showAllLevels={false} 只显示最后一级")]
    public Task Trigger_shows_the_whole_path() => HeadlessUi.Run(() =>
    {
        var all = Create();
        all.Values = ["zj", "hz", "xh"];
        var last = Create();
        last.Values = ["zj", "hz", "xh"];
        last.ShowAllLevels = false;
        HeadlessUi.Show(new StackPanel { Children = { all, last } });
        Assert.Equal("浙江 / 杭州 / 西湖区", ValueText(all));
        Assert.Equal("西湖区", ValueText(last));
        Assert.Equal("浙江 / 杭州 / 西湖区", ((IValueProvider)all.Peer()).Value);
    });

    [Fact]
    [Upstream("Cascader", "点有下一级的项展开下一列，点叶子选中路径并关闭")]
    public Task Clicking_expands_branches_and_picks_leaves() => HeadlessUi.Run(() =>
    {
        var cascader = Create();
        var window = HeadlessUi.Show(cascader);
        ((IExpandCollapseProvider)cascader.Peer()).Expand();
        HeadlessUi.Flush(window);
        Assert.True(cascader.IsOpen);
        Assert.Equal(["浙江", "江苏"], Texts(cascader, 0));

        Click(Option(cascader, 0, "浙江"));
        HeadlessUi.Flush(window);
        Assert.Equal(["杭州", "宁波"], Texts(cascader, 1));
        // 默认只能选叶子：点浙江只是展开
        Assert.Null(cascader.Values);
        Click(Option(cascader, 1, "杭州"));
        HeadlessUi.Flush(window);
        Click(Option(cascader, 2, "滨江区"));
        HeadlessUi.Flush(window);
        Assert.Equal(["zj", "hz", "bj"], cascader.Values);
        Assert.False(cascader.IsOpen);
    });

    [Fact]
    [Upstream("Cascader", "changeOnSelect：点有下一级的项也选中它，弹出层不关")]
    public Task Change_on_select_picks_branches_and_stays_open() => HeadlessUi.Run(() =>
    {
        var cascader = Create();
        cascader.ChangeOnSelect = true;
        var window = HeadlessUi.Show(cascader);
        cascader.IsOpen = true;
        HeadlessUi.Flush(window);

        Click(Option(cascader, 0, "浙江"));
        HeadlessUi.Flush(window);
        Assert.Equal(["zj"], cascader.Values);
        Assert.True(cascader.IsOpen);
        Assert.True(Option(cascader, 0, "浙江").IsSelected);
    });

    [Fact]
    [Upstream("Cascader", "键盘：↓ 移动并显示下一列，→ 进入下一级，Enter 选中叶子")]
    public Task Keyboard_moves_enters_and_picks() => HeadlessUi.Run(() =>
    {
        var cascader = Create();
        var window = HeadlessUi.Show(cascader);
        cascader.IsOpen = true;
        HeadlessUi.Flush(window);
        var first = cascader.Columns[0];
        first.Focus();
        HeadlessUi.Flush(window);
        Assert.True(Option(cascader, 0, "浙江").IsActive);

        Press(first, Key.Down);
        HeadlessUi.Flush(window);
        Assert.True(Option(cascader, 0, "江苏").IsActive);
        Assert.Equal(["苏州"], Texts(cascader, 1));
        Press(first, Key.Up);
        Press(first, Key.Right);
        HeadlessUi.Flush(window);
        Assert.True(Option(cascader, 1, "杭州").IsActive);
        Press(cascader.Columns[1], Key.Right);
        HeadlessUi.Flush(window);
        Press(cascader.Columns[2], Key.Down);
        Press(cascader.Columns[2], Key.Enter);
        HeadlessUi.Flush(window);
        Assert.Equal(["zj", "hz", "bj"], cascader.Values);
    });

    [Fact]
    [Upstream("Cascader", "懒加载：进入还没有下一级的节点时取回来，期间列表忙")]
    public Task Lazy_children_load_when_entered() => HeadlessUi.Run(() =>
    {
        var guangdong = new Region("gd", "广东") { Leaf = false };
        var pending = new TaskCompletionSource();
        var loaded = new List<string>();
        var cascader = Create([guangdong]);
        cascader.HasChildren = region => ((Region)region).Leaf == false || ((Region)region).Children is { Count: > 0 };
        cascader.LoadChildren = region =>
        {
            loaded.Add(((Region)region).Code);
            return pending.Task.ContinueWith(_ => ((Region)region).Children = [new("sz", "深圳")], TaskScheduler.FromCurrentSynchronizationContext());
        };
        var window = HeadlessUi.Show(cascader);
        cascader.IsOpen = true;
        HeadlessUi.Flush(window);

        Click(Option(cascader, 0, "广东"));
        HeadlessUi.Flush(window);
        Assert.Equal(["gd"], loaded);
        Assert.Contains(cascader.Columns[1].GetVisualDescendants().OfType<Spinner>(), spinner => spinner.IsVisible);
        pending.SetResult();
        HeadlessUi.Until(() => cascader.Columns.Count > 1 && Texts(cascader, 1) is ["深圳"]);
    });

    [Fact]
    [Upstream("Cascader", "searchable：按整条路径搜索，只列叶子；Enter 选中高亮的一条")]
    public Task Search_lists_matching_leaf_paths() => HeadlessUi.Run(() =>
    {
        var cascader = Create();
        cascader.Searchable = true;
        var window = HeadlessUi.Show(cascader);
        cascader.IsOpen = true;
        HeadlessUi.Flush(window);

        cascader.SearchBox.Text = "杭州";
        HeadlessUi.Flush(window);
        Assert.Equal(["浙江 / 杭州 / 西湖区", "浙江 / 杭州 / 滨江区"], Texts(cascader, 0));
        var input = cascader.SearchBox;
        Press(input, Key.Down);
        Press(input, Key.Enter);
        HeadlessUi.Flush(window);
        Assert.Equal(["zj", "hz", "bj"], cascader.Values);
        Assert.False(cascader.IsOpen);
    });

    [Fact]
    [Upstream("Cascader", "部件：选项可以按节点给部件属性，列、选项、文字带 data-part")]
    public Task Parts_are_named_and_options_carry_their_node() => HeadlessUi.Run(() =>
    {
        var cascader = Create();
        var window = HeadlessUi.Show(cascader);
        cascader.IsOpen = true;
        HeadlessUi.Flush(window);
        var zhejiang = Option(cascader, 0, "浙江");
        Assert.Equal("zj", ((Region)zhejiang.Node).Code);
        Assert.Equal(CascaderParts.Option, zhejiang.Name);
        Assert.Equal(CascaderParts.Column, cascader.Columns[0].Name);
        Assert.Equal(AutomationControlType.List, cascader.Columns[0].Peer().GetAutomationControlType());
    });

    [Fact]
    [Upstream("Cascader.browser", "已选路径显示名称；打开后展开到已选的区")]
    public Task Selected_area_path_opens_to_its_county() => HeadlessUi.Run(() =>
    {
        var cascader = new Cascader
        {
            ItemsSource = Area.Tree(),
            GetKey = (node, _) => Area.KeyOf(node),
            GetLabel = Area.LabelOf,
            GetChildren = Area.ChildrenOf,
            Values = ["330000", "330100", "330106"],
        };
        var window = HeadlessUi.Show(cascader);
        Assert.Equal("浙江省 / 杭州市 / 西湖区", ValueText(cascader));

        ((IExpandCollapseProvider)cascader.Peer()).Expand();
        HeadlessUi.Flush(window);
        Assert.Equal(3, cascader.Columns.Count);
        Assert.Equal(["浙江省", "杭州市", "西湖区"],
            cascader.Columns.SelectMany(column => column.Options).Where(option => option.IsSelected).Select(option => Area.LabelOf(option.Node)));
    });
}
