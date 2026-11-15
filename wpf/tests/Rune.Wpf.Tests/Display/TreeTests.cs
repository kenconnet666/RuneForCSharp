using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Tree：层级与同级位置、可展开的带展开状态；方向键展开 / 收起 / 进出子节点与按字母跳转；选中；勾选联动与 CheckStrictly；骨架与空状态；懒加载与失败重试；箭头、勾选框各管各的；MaxHeight；Alt + 方向键移动。</summary>
public class TreeTests
{
    sealed class Node(string id, string name, List<Node>? children = null, bool lazy = false)
    {
        public string Id { get; } = id;

        public string Name { get; } = name;

        public List<Node>? Children { get; set; } = children;

        public bool Lazy { get; } = lazy;
    }

    static List<Node> Data() => [new("src", "src", [new("main", "main.ts"), new("app", "App.tsx")]), new("readme", "README.md")];

    static Tree Create(IEnumerable<Node>? items = null) => new()
    {
        ItemsSource = items ?? Data(),
        GetKey = node => ((Node)node).Id,
        GetLabel = node => ((Node)node).Name,
        GetChildren = node => ((Node)node).Children,
        Width = 300,
    };

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static void Press(Tree tree, Key key)
    {
        var source = PresentationSource.FromVisual(tree)!;
        tree.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        WpfUi.Flush();
    }

    static void Type(Tree tree, string text)
    {
        tree.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, tree, text)) { RoutedEvent = TextCompositionManager.TextInputEvent });
        WpfUi.Flush();
    }

    static TreeItem Item(Tree tree, string key) => tree.RealizedItems.Single(item => item.Row?.Key == key);

    static T Pattern<T>(UIElement element, PatternInterface pattern) => (T)element.Peer().GetPattern(pattern);

    [Fact]
    [Upstream("Tree", "treeitem 带层级、同级位置；可展开的带 aria-expanded")]
    public Task Items_have_level_position_and_expansion() => WpfUi.Run(() =>
    {
        var tree = Create();
        tree.Expanded = ["src"];
        WpfUi.Show(Host(tree));
        WpfUi.Flush();

        Assert.Equal(AutomationControlType.Tree, tree.Peer().GetAutomationControlType());
        Assert.Equal(["src", "main", "app", "readme"], tree.Rows.Select(row => row.Key));
        var main = tree.Rows[1];
        Assert.Equal((1, 1, 2), (main.Depth, main.Position, main.SetSize));
        Assert.Equal(ExpandCollapseState.Expanded, Pattern<IExpandCollapseProvider>(Item(tree, "src"), PatternInterface.ExpandCollapse).ExpandCollapseState);
        Assert.Equal(ExpandCollapseState.LeafNode, Pattern<IExpandCollapseProvider>(Item(tree, "main"), PatternInterface.ExpandCollapse).ExpandCollapseState);
        Assert.Equal((AutomationControlType.TreeItem, "main.ts"), (Item(tree, "main").Peer().GetAutomationControlType(), Item(tree, "main").Peer().GetName()));
    });

    [Fact]
    [Upstream("Tree", "→ 展开、再按进入第一个子节点；← 回到父节点、再按收起")]
    public Task Arrow_keys_open_enter_leave_and_close() => WpfUi.Run(() =>
    {
        var tree = Create();
        WpfUi.Show(Host(tree));
        Keyboard.Focus(tree);
        Assert.Equal("src", tree.Active);

        Press(tree, Key.Right);
        Assert.Equal(["src"], tree.Expanded);
        Press(tree, Key.Right);
        Assert.Equal("main", tree.Active);
        Press(tree, Key.Left);
        Assert.Equal("src", tree.Active);
        Press(tree, Key.Left);
        Assert.Empty(tree.Expanded);
        Type(tree, "r");
        Assert.Equal("readme", tree.Active);
    });

    [Fact]
    [Upstream("Tree", "单选：Enter 选中当前项，aria-selected 跟着变")]
    public Task Enter_selects_the_active_item() => WpfUi.Run(() =>
    {
        var tree = Create();
        tree.SelectionMode = SelectionMode.Single;
        WpfUi.Show(Host(tree));
        Keyboard.Focus(tree);
        Press(tree, Key.Down);
        Press(tree, Key.Enter);

        Assert.Equal(["readme"], tree.Selected);
        Assert.True(Pattern<ISelectionItemProvider>(Item(tree, "readme"), PatternInterface.SelectionItem).IsSelected);
    });

    [Fact]
    [Upstream("Tree", "勾选：空格勾上父节点时子节点一起勾上；只勾一个子节点时父节点半选")]
    public Task Checking_cascades_and_parents_go_mixed() => WpfUi.Run(() =>
    {
        var tree = Create();
        tree.Expanded = ["src"];
        tree.Checkable = true;
        WpfUi.Show(Host(tree));
        Keyboard.Focus(tree);
        Press(tree, Key.Space);
        Assert.Equal(["src", "main", "app"], tree.Checked);
        Press(tree, Key.Down);
        Press(tree, Key.Space);

        Assert.Equal(["app"], tree.Checked);
        Assert.Equal(ToggleState.Indeterminate, Pattern<IToggleProvider>(Item(tree, "src"), PatternInterface.Toggle).ToggleState);
    });

    [Fact]
    [Upstream("Tree", "checkStrictly：父子不联动，每个节点各自勾选、没有半选")]
    public Task Check_strictly_keeps_nodes_independent() => WpfUi.Run(() =>
    {
        var tree = Create();
        tree.Expanded = ["src"];
        tree.Checkable = true;
        tree.CheckStrictly = true;
        WpfUi.Show(Host(tree));
        Keyboard.Focus(tree);
        Press(tree, Key.Space);
        Assert.Equal(["src"], tree.Checked);
        Press(tree, Key.Down);
        Press(tree, Key.Space);

        Assert.Equal(["src", "main"], tree.Checked);
        Assert.Equal([ToggleState.On, ToggleState.On, ToggleState.Off, ToggleState.Off],
            tree.Rows.Select(row => Pattern<IToggleProvider>(Item(tree, row.Key), PatternInterface.Toggle).ToggleState));
    });

    [Fact]
    [Upstream("Tree", "没有节点时：loading 显示骨架、带 aria-busy；否则显示空状态（可以换成 empty）")]
    public Task Empty_shows_skeleton_or_empty_state() => WpfUi.Run(() =>
    {
        var loading = Create([]);
        loading.IsLoading = true;
        var empty = Create([]);
        var custom = Create([]);
        custom.Empty = "没有文件";
        WpfUi.Show(Host(loading, empty, custom));

        WpfUi.Until(() => loading.Placeholders.Skeleton.Visibility == Visibility.Visible);
        Assert.Equal(Visibility.Collapsed, loading.Placeholders.Empty.Visibility);
        Assert.Equal("暂无数据", ((Empty)empty.Placeholders.Empty.Content!).Description);
        Assert.Equal("没有文件", custom.Placeholders.Empty.Content);
    });

    [Fact]
    [Upstream("Tree", "懒加载：展开还没有子节点的节点时调用 loadChildren，加载中带 aria-busy")]
    public Task Lazy_loading_shows_busy_until_done() => WpfUi.Run(() =>
    {
        var done = new TaskCompletionSource();
        var remote = new Node("remote", "remote", lazy: true);
        var tree = Create([remote]);
        tree.HasChildren = node => ((Node)node).Lazy;
        tree.LoadChildren = _ => done.Task;
        WpfUi.Show(Host(tree));
        WpfUi.Flush();

        tree.Click("remote");
        WpfUi.Flush();
        Assert.True(Item(tree, "remote").State.Loading);
        remote.Children = [new("r1", "r1")];
        done.SetResult();
        WpfUi.Until(() => !Item(tree, "remote").State.Loading);
        tree.Refresh();
        Assert.Equal(["remote", "r1"], tree.Rows.Select(row => row.Key));
    });

    [Fact]
    [Upstream("Tree", "懒加载失败：节点收回去，onLoadError 拿到原因；再展开会重试")]
    public Task A_failed_load_collapses_and_retries() => WpfUi.Run(() =>
    {
        var failed = new InvalidOperationException("offline");
        var calls = 0;
        Exception? reported = null;
        var tree = Create([new Node("remote", "remote", lazy: true)]);
        tree.HasChildren = node => ((Node)node).Lazy;
        tree.LoadChildren = _ =>
        {
            calls++;
            return Task.FromException(failed);
        };
        tree.LoadError += (_, e) => reported = e.Error;
        WpfUi.Show(Host(tree));
        WpfUi.Flush();

        tree.Click("remote");
        WpfUi.Until(() => reported is not null);
        Assert.Same(failed, reported);
        Assert.Empty(tree.Expanded);
        tree.Click("remote");
        WpfUi.Until(() => calls == 2);
    });

    [Fact]
    [Upstream("Tree", "propsRow、propsToggle、propsCheckbox 落到各自的节点上；点击仍按组件的处理")]
    public Task Toggle_and_checkbox_do_their_own_thing() => WpfUi.Run(() =>
    {
        var tree = Create();
        tree.Checkable = true;
        WpfUi.Show(Host(tree));
        WpfUi.Flush();

        Assert.Equal(Visibility.Visible, Item(tree, "src").TogglePart.Visibility);
        tree.ClickToggle("src");
        WpfUi.Flush();
        Assert.Equal(4, tree.Rows.Count);
        Assert.Empty(tree.Checked);
        tree.ClickCheck("src");
        Assert.Equal(["src", "main", "app"], tree.Checked);
        Assert.Equal(["src"], tree.Expanded);
    });

    [Fact]
    [Upstream("Tree", "maxHeight：3 个节点时按内容高，200 个节点时正好 16rem 并能滚动")]
    public Task Max_height_fits_few_and_scrolls_many() => WpfUi.Run(() =>
    {
        Tree Nodes(int count)
        {
            var tree = Create(Enumerable.Range(0, count).Select(i => new Node($"n{i}", $"n{i}")));
            tree.MaxHeight = 16 * 14;
            return tree;
        }
        var few = Nodes(3);
        var many = Nodes(200);
        WpfUi.Show(Host(few, many));
        WpfUi.Flush();

        Assert.True(few.ActualHeight < 8 * 14);
        Assert.Equal(16 * 14, many.ActualHeight, 1);
        var scroll = WpfUi.Descendants(many.RowsPart).OfType<ScrollViewer>().First();
        Assert.True(scroll.ExtentHeight > scroll.ViewportHeight);
        Assert.True(many.RealizedItems.Count() < 60);
    });

    [Fact]
    public Task Alt_arrows_move_nodes() => WpfUi.Run(() =>
    {
        var tree = Create();
        tree.Expanded = ["src"];
        var moves = new List<TreeMovedEventArgs>();
        tree.Moved += (_, e) => moves.Add(e);
        WpfUi.Show(Host(tree));
        Keyboard.Focus(tree);
        Press(tree, Key.Down);
        // WPF 的修饰键状态模拟不了：直接走 Alt + 方向键的处理
        tree.MoveByKey(Key.Down);
        Assert.Equal(("main", new TreeMoveTarget("src", 1)), (moves[^1].Key, moves[^1].Target));
        Assert.Equal("main.ts移到src下第 2 位", tree.Announced);
        tree.MoveByKey(Key.Left);
        Assert.Equal(new TreeMoveTarget(null, 1), moves[^1].Target);
    });
}
