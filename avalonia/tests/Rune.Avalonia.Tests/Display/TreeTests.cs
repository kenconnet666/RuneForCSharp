using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>Tree：层级与同级位置、可展开的带展开状态；方向键展开 / 收起 / 进出子节点与按字母跳转；选中；勾选联动与 CheckStrictly；骨架与空状态；懒加载与失败重试；箭头、勾选框各管各的；MaxHeight；拖动与 Alt + 方向键移动。</summary>
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

    static void Press(Tree tree, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = tree });

    static void Type(Tree tree, string text) => tree.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text, Source = tree });

    static TreeItem Item(Tree tree, string key) => tree.RealizedItems.Single(item => item.Row?.Key == key);

    [Fact]
    [Upstream("Tree", "treeitem 带层级、同级位置；可展开的带 aria-expanded")]
    public Task Items_have_level_position_and_expansion() => HeadlessUi.Run(() =>
    {
        var tree = Create();
        tree.Expanded = ["src"];
        var window = HeadlessUi.Show(tree);
        HeadlessUi.Flush(window);

        Assert.Equal(AutomationControlType.Tree, tree.Peer().GetAutomationControlType());
        Assert.Equal(["src", "main", "app", "readme"], tree.Rows.Select(row => row.Key));
        var main = tree.Rows[1];
        Assert.Equal((1, 1, 2), (main.Depth, main.Position, main.SetSize));
        Assert.Equal(ExpandCollapseState.Expanded, ((IExpandCollapseProvider)Item(tree, "src").Peer()).ExpandCollapseState);
        Assert.Equal(ExpandCollapseState.LeafNode, ((IExpandCollapseProvider)Item(tree, "main").Peer()).ExpandCollapseState);
        Assert.Equal((AutomationControlType.TreeItem, "main.ts"), (Item(tree, "main").Peer().GetAutomationControlType(), Item(tree, "main").Peer().GetName()));
    });

    [Fact]
    [Upstream("Tree", "→ 展开、再按进入第一个子节点；← 回到父节点、再按收起")]
    public Task Arrow_keys_open_enter_leave_and_close() => HeadlessUi.Run(() =>
    {
        var tree = Create();
        HeadlessUi.Show(tree);
        tree.Focus();
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
    public Task Enter_selects_the_active_item() => HeadlessUi.Run(() =>
    {
        var tree = Create();
        tree.SelectionMode = SelectionMode.Single;
        var window = HeadlessUi.Show(tree);
        tree.Focus();
        Press(tree, Key.Down);
        Press(tree, Key.Enter);
        HeadlessUi.Flush(window);

        Assert.Equal(["readme"], tree.Selected);
        Assert.True(((ISelectionItemProvider)Item(tree, "readme").Peer()).IsSelected);
    });

    [Fact]
    [Upstream("Tree", "勾选：空格勾上父节点时子节点一起勾上；只勾一个子节点时父节点半选")]
    public Task Checking_cascades_and_parents_go_mixed() => HeadlessUi.Run(() =>
    {
        var tree = Create();
        tree.Expanded = ["src"];
        tree.Checkable = true;
        var window = HeadlessUi.Show(tree);
        tree.Focus();
        Press(tree, Key.Space);
        Assert.Equal(["src", "main", "app"], tree.Checked);
        Press(tree, Key.Down);
        Press(tree, Key.Space);
        HeadlessUi.Flush(window);

        Assert.Equal(["app"], tree.Checked);
        Assert.Equal(ToggleState.Indeterminate, ((IToggleProvider)Item(tree, "src").Peer()).ToggleState);
    });

    [Fact]
    [Upstream("Tree", "checkStrictly：父子不联动，每个节点各自勾选、没有半选")]
    public Task Check_strictly_keeps_nodes_independent() => HeadlessUi.Run(() =>
    {
        var tree = Create();
        tree.Expanded = ["src"];
        tree.Checkable = true;
        tree.CheckStrictly = true;
        var window = HeadlessUi.Show(tree);
        tree.Focus();
        Press(tree, Key.Space);
        Assert.Equal(["src"], tree.Checked);
        Press(tree, Key.Down);
        Press(tree, Key.Space);
        HeadlessUi.Flush(window);

        Assert.Equal(["src", "main"], tree.Checked);
        Assert.Equal([ToggleState.On, ToggleState.On, ToggleState.Off, ToggleState.Off],
            tree.Rows.Select(row => ((IToggleProvider)Item(tree, row.Key).Peer()).ToggleState));
    });

    [Fact]
    [Upstream("Tree", "没有节点时：loading 显示骨架、带 aria-busy；否则显示空状态（可以换成 empty）")]
    public Task Empty_shows_skeleton_or_empty_state() => HeadlessUi.Run(() =>
    {
        var loading = Create([]);
        loading.IsLoading = true;
        var empty = Create([]);
        var custom = Create([]);
        custom.Empty = "没有文件";
        HeadlessUi.Show(new StackPanel { Children = { loading, empty, custom } });

        HeadlessUi.Until(() => loading.Placeholders.Skeleton.IsVisible);
        Assert.False(loading.Placeholders.Empty.IsVisible);
        Assert.Equal("暂无数据", ((Empty)empty.Placeholders.Empty.Content!).Description);
        Assert.Equal("没有文件", custom.Placeholders.Empty.Content);
    });

    [Fact]
    [Upstream("Tree", "懒加载：展开还没有子节点的节点时调用 loadChildren，加载中带 aria-busy")]
    public Task Lazy_loading_shows_busy_until_done() => HeadlessUi.Run(() =>
    {
        var done = new TaskCompletionSource();
        var remote = new Node("remote", "remote", lazy: true);
        var tree = Create([remote]);
        tree.HasChildren = node => ((Node)node).Lazy;
        tree.LoadChildren = _ => done.Task;
        var window = HeadlessUi.Show(tree);
        HeadlessUi.Flush(window);

        tree.Click("remote");
        HeadlessUi.Flush(window);
        Assert.True(Item(tree, "remote").State.Loading);
        remote.Children = [new("r1", "r1")];
        done.SetResult();
        HeadlessUi.Until(() => !Item(tree, "remote").State.Loading);
        tree.Refresh();
        Assert.Equal(["remote", "r1"], tree.Rows.Select(row => row.Key));
    });

    [Fact]
    [Upstream("Tree", "懒加载失败：节点收回去，onLoadError 拿到原因；再展开会重试")]
    public Task A_failed_load_collapses_and_retries() => HeadlessUi.Run(() =>
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
        var window = HeadlessUi.Show(tree);
        HeadlessUi.Flush(window);

        tree.Click("remote");
        HeadlessUi.Until(() => reported is not null);
        Assert.Same(failed, reported);
        Assert.Empty(tree.Expanded);
        tree.Click("remote");
        HeadlessUi.Until(() => calls == 2);
    });

    [Fact]
    [Upstream("Tree", "propsRow、propsToggle、propsCheckbox 落到各自的节点上；点击仍按组件的处理")]
    public Task Toggle_and_checkbox_parts_do_their_own_thing() => HeadlessUi.Run(() =>
    {
        var tree = Create();
        tree.Checkable = true;
        var window = HeadlessUi.Show(tree);
        HeadlessUi.Flush(window);

        Point Center(Control part) => part.TranslatePoint(new Point(part.Bounds.Width / 2, part.Bounds.Height / 2), window)!.Value;
        var src = Item(tree, "src");
        Assert.Equal(TreeParts.Toggle, src.TogglePart.Name);
        window.MouseDown(Center(src.TogglePart), MouseButton.Left);
        window.MouseUp(Center(src.TogglePart), MouseButton.Left);
        HeadlessUi.Flush(window);
        Assert.Equal(4, tree.Rows.Count);
        Assert.Empty(tree.Checked);
        src = Item(tree, "src");
        window.MouseDown(Center(src.CheckboxPart), MouseButton.Left);
        window.MouseUp(Center(src.CheckboxPart), MouseButton.Left);
        HeadlessUi.Flush(window);
        Assert.Equal(["src", "main", "app"], tree.Checked);
        Assert.Equal(["src"], tree.Expanded);
    });

    [Fact]
    [Upstream("Tree", "maxHeight：3 个节点时按内容高，200 个节点时正好 16rem 并能滚动")]
    public Task Max_height_fits_few_and_scrolls_many() => HeadlessUi.Run(() =>
    {
        Tree Nodes(int count)
        {
            var tree = Create(Enumerable.Range(0, count).Select(i => new Node($"n{i}", $"n{i}")));
            tree.MaxHeight = 16 * 14;
            return tree;
        }
        var few = Nodes(3);
        var many = Nodes(200);
        var window = HeadlessUi.Show(new StackPanel { Children = { few, many } });
        HeadlessUi.Flush(window);

        Assert.True(few.Bounds.Height < 8 * 14);
        Assert.Equal(16 * 14, many.Bounds.Height, 1);
        Assert.True(many.ScrollPart.Extent.Height > many.ScrollPart.Viewport.Height);
        Assert.True(many.RealizedItems.Count() < 60);
    });

    [Fact]
    public Task Alt_arrows_move_and_drags_drop_inside() => HeadlessUi.Run(() =>
    {
        var tree = Create();
        tree.Expanded = ["src"];
        var moves = new List<TreeMovedEventArgs>();
        tree.Moved += (_, e) => moves.Add(e);
        var window = HeadlessUi.Show(tree);
        tree.Focus();
        Press(tree, Key.Down);
        Press(tree, Key.Down, KeyModifiers.Alt);
        Assert.Equal(("main", new TreeMoveTarget("src", 1)), (moves[^1].Key, moves[^1].Target));
        Assert.Equal("main.ts移到src下第 2 位", tree.Announced);
        Press(tree, Key.Left, KeyModifiers.Alt);
        Assert.Equal(new TreeMoveTarget(null, 1), moves[^1].Target);

        // 把 README.md 拖到 src 的中间：放进 src 里（最后）
        HeadlessUi.Flush(window);
        var readme = Item(tree, "readme");
        var src = Item(tree, "src");
        Point At(Control item, double ratio) => item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height * ratio), window)!.Value;
        window.MouseDown(At(readme, 0.5), MouseButton.Left);
        window.MouseMove(At(readme, 0.2));
        window.MouseMove(At(src, 0.5));
        Assert.Equal(TreeDropZone.Inside, Item(tree, "src").State.Drop);
        window.MouseUp(At(src, 0.5), MouseButton.Left);
        Assert.Equal(("readme", new TreeMoveTarget("src", int.MaxValue)), (moves[^1].Key, moves[^1].Target));
    });
}
