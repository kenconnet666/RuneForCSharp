using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>TreeControl：层级与同级位置、可展开的带展开状态；方向键展开 / 收起 / 进出子节点与按字母跳转；选中；勾选联动与 CheckStrictly；骨架与空状态；懒加载与失败重试；箭头、勾选框各管各的；最高 20em；Alt + 方向键移动。</summary>
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

    static TreeControl Create(IEnumerable<Node>? items = null) => new()
    {
        ItemsSource = items ?? Data(),
        GetKey = node => ((Node)node).Id,
        GetLabel = node => ((Node)node).Name,
        GetChildren = node => ((Node)node).Children,
        Width = 300,
        Height = 300,
    };

    static void Raise(Control control, string method, EventArgs args)
    {
        typeof(Control).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [args]);
        WinFormsUi.Pump();
    }

    static void Press(TreeControl tree, Keys key) => Raise(tree, "OnKeyDown", new KeyEventArgs(key));

    static void Type(TreeControl tree, char character) => Raise(tree, "OnKeyPress", new KeyPressEventArgs(character));

    static void ClickAt(Control view, Point point)
    {
        Raise(view, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
        Raise(view, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
    }

    static AccessibleObject Item(TreeControl tree, string key) => tree.AccessibilityObject.GetChild(tree.Rows.Select(row => row.Key).ToList().IndexOf(key))!;

    [Fact]
    [Upstream("Tree", "treeitem 带层级、同级位置；可展开的带 aria-expanded")]
    public Task Items_have_level_position_and_expansion() => WinFormsUi.Run(() =>
    {
        var tree = Create();
        tree.Expanded = ["src"];
        using var form = WinFormsUi.Show(tree);

        Assert.Equal(AccessibleRole.Outline, tree.AccessibilityObject.Role);
        Assert.Equal(["src", "main", "app", "readme"], tree.Rows.Select(row => row.Key));
        var main = tree.Rows[1];
        Assert.Equal((1, 1, 2), (main.Depth, main.Position, main.SetSize));
        Assert.True(Item(tree, "src").State.HasFlag(AccessibleStates.Expanded));
        Assert.False(Item(tree, "main").State.HasFlag(AccessibleStates.Expanded) || Item(tree, "main").State.HasFlag(AccessibleStates.Collapsed));
        Assert.Equal((AccessibleRole.OutlineItem, "main.ts"), (Item(tree, "main").Role, Item(tree, "main").Name));
    });

    [Fact]
    [Upstream("Tree", "→ 展开、再按进入第一个子节点；← 回到父节点、再按收起")]
    public Task Arrow_keys_open_enter_leave_and_close() => WinFormsUi.Run(() =>
    {
        var tree = Create();
        using var form = WinFormsUi.Show(tree);
        tree.Focus();
        WinFormsUi.Pump();
        Assert.Equal("src", tree.Active);

        Press(tree, Keys.Right);
        Assert.Equal(["src"], tree.Expanded);
        Press(tree, Keys.Right);
        Assert.Equal("main", tree.Active);
        Press(tree, Keys.Left);
        Assert.Equal("src", tree.Active);
        Press(tree, Keys.Left);
        Assert.Empty(tree.Expanded);
        Type(tree, 'r');
        Assert.Equal("readme", tree.Active);
    });

    [Fact]
    [Upstream("Tree", "单选：Enter 选中当前项，aria-selected 跟着变")]
    public Task Enter_selects_the_active_item() => WinFormsUi.Run(() =>
    {
        var tree = Create();
        tree.SelectionMode = SelectionMode.Single;
        using var form = WinFormsUi.Show(tree);
        tree.Focus();
        Press(tree, Keys.Down);
        Press(tree, Keys.Enter);

        Assert.Equal(["readme"], tree.Selected);
        Assert.True(Item(tree, "readme").State.HasFlag(AccessibleStates.Selected));
    });

    [Fact]
    [Upstream("Tree", "勾选：空格勾上父节点时子节点一起勾上；只勾一个子节点时父节点半选")]
    public Task Checking_cascades_and_parents_go_mixed() => WinFormsUi.Run(() =>
    {
        var tree = Create();
        tree.Expanded = ["src"];
        tree.Checkable = true;
        using var form = WinFormsUi.Show(tree);
        tree.Focus();
        Press(tree, Keys.Space);
        Assert.Equal(["src", "main", "app"], tree.Checked);
        Press(tree, Keys.Down);
        Press(tree, Keys.Space);

        Assert.Equal(["app"], tree.Checked);
        Assert.True(Item(tree, "src").State.HasFlag(AccessibleStates.Mixed));
    });

    [Fact]
    [Upstream("Tree", "checkStrictly：父子不联动，每个节点各自勾选、没有半选")]
    public Task Check_strictly_keeps_nodes_independent() => WinFormsUi.Run(() =>
    {
        var tree = Create();
        tree.Expanded = ["src"];
        tree.Checkable = true;
        tree.CheckStrictly = true;
        using var form = WinFormsUi.Show(tree);
        tree.Focus();
        Press(tree, Keys.Space);
        Assert.Equal(["src"], tree.Checked);
        Press(tree, Keys.Down);
        Press(tree, Keys.Space);

        Assert.Equal(["src", "main"], tree.Checked);
        Assert.Equal([true, true, false, false], tree.Rows.Select(row => Item(tree, row.Key).State.HasFlag(AccessibleStates.Checked)));
        Assert.DoesNotContain(tree.Rows, row => Item(tree, row.Key).State.HasFlag(AccessibleStates.Mixed));
    });

    [Fact]
    [Upstream("Tree", "没有节点时：loading 显示骨架、带 aria-busy；否则显示空状态（可以换成 empty）")]
    public Task Empty_shows_skeleton_or_empty_state() => WinFormsUi.Run(() =>
    {
        var loading = Create([]);
        loading.IsLoading = true;
        var empty = Create([]);
        empty.Left = 320;
        var custom = Create([]);
        custom.EmptyText = "没有文件";
        custom.Top = 320;
        using var form = WinFormsUi.Show(loading, empty, custom);

        WinFormsUi.Until(() => loading.Placeholders.Skeleton.Visible);
        Assert.False(loading.Placeholders.Empty.Visible);
        Assert.Equal(ProviderControl.LangOf(empty).TreeEmpty, empty.Placeholders.Empty.Description);
        Assert.Equal("没有文件", custom.Placeholders.Empty.Description);
    });

    [Fact]
    [Upstream("Tree", "懒加载：展开还没有子节点的节点时调用 loadChildren，加载中带 aria-busy")]
    public Task Lazy_loading_shows_busy_until_done() => WinFormsUi.Run(() =>
    {
        var done = new TaskCompletionSource();
        var remote = new Node("remote", "remote", lazy: true);
        var tree = Create([remote]);
        tree.HasChildNodes = node => ((Node)node).Lazy;
        tree.LoadChildren = _ => done.Task;
        using var form = WinFormsUi.Show(tree);

        tree.ClickItem("remote");
        Assert.True(Item(tree, "remote").State.HasFlag(AccessibleStates.Busy));
        remote.Children = [new("r1", "r1")];
        done.SetResult();
        WinFormsUi.Until(() => !Item(tree, "remote").State.HasFlag(AccessibleStates.Busy));
        tree.Refresh();
        Assert.Equal(["remote", "r1"], tree.Rows.Select(row => row.Key));
    });

    [Fact]
    [Upstream("Tree", "懒加载失败：节点收回去，onLoadError 拿到原因；再展开会重试")]
    public Task A_failed_load_collapses_and_retries() => WinFormsUi.Run(() =>
    {
        var failed = new InvalidOperationException("offline");
        var calls = 0;
        Exception? reported = null;
        var tree = Create([new Node("remote", "remote", lazy: true)]);
        tree.HasChildNodes = node => ((Node)node).Lazy;
        tree.LoadChildren = _ =>
        {
            calls++;
            return Task.FromException(failed);
        };
        tree.LoadError += (_, e) => reported = e.Error;
        using var form = WinFormsUi.Show(tree);

        tree.ClickItem("remote");
        WinFormsUi.Until(() => reported is not null);
        Assert.Same(failed, reported);
        Assert.Empty(tree.Expanded);
        tree.ClickItem("remote");
        WinFormsUi.Until(() => calls == 2);
    });

    [Fact]
    [Upstream("Tree", "propsRow、propsToggle、propsCheckbox 落到各自的节点上；点击仍按组件的处理")]
    public Task Toggle_and_checkbox_do_their_own_thing() => WinFormsUi.Run(() =>
    {
        var tree = Create();
        tree.Checkable = true;
        using var form = WinFormsUi.Show(tree);

        ClickAt(tree.View, tree.View.Points(0).Toggle);
        Assert.Equal(4, tree.Rows.Count);
        Assert.Empty(tree.Checked);
        ClickAt(tree.View, tree.View.Points(0).Check);
        Assert.Equal(["src", "main", "app"], tree.Checked);
        Assert.Equal(["src"], tree.Expanded);
        // 点文字：能勾时勾选（没有选择方式）
        ClickAt(tree.View, tree.View.Points(3).Label);
        Assert.Contains("readme", tree.Checked);
    });

    [Fact]
    [Upstream("Tree", "maxHeight：3 个节点时按内容高，200 个节点时正好 16rem 并能滚动")]
    public Task Max_height_fits_few_and_scrolls_many() => WinFormsUi.Run(() =>
    {
        TreeControl Nodes(int count)
        {
            var tree = Create(Enumerable.Range(0, count).Select(i => new Node($"n{i}", $"n{i}")));
            tree.MaximumSize = new Size(0, tree.LogicalToDeviceUnits(16 * 14));
            return tree;
        }
        var few = Nodes(3);
        var many = Nodes(200);
        using var form = WinFormsUi.Show(few, many);
        many.Size = many.GetPreferredSize(new Size(300, 0));
        WinFormsUi.Pump();

        Assert.True(few.GetPreferredSize(new Size(300, 0)).Height < few.LogicalToDeviceUnits(8 * 14));
        Assert.Equal(many.LogicalToDeviceUnits(16 * 14), many.Height);
        Assert.True(many.View.Height > many.ScrollPart.ClientSize.Height);
        Assert.Empty(many.View.Controls);
    });

    [Fact]
    public Task Alt_arrows_move_nodes() => WinFormsUi.Run(() =>
    {
        var tree = Create();
        tree.Expanded = ["src"];
        var moves = new List<TreeMovedEventArgs>();
        tree.Moved += (_, e) => moves.Add(e);
        using var form = WinFormsUi.Show(tree);
        tree.Focus();
        Press(tree, Keys.Down);
        Press(tree, Keys.Alt | Keys.Down);
        Assert.Equal(("main", new TreeMoveTarget("src", 1)), (moves[^1].Key, moves[^1].Target));
        Assert.Equal("main.ts移到src下第 2 位", tree.Announced);
        Press(tree, Keys.Alt | Keys.Left);
        Assert.Equal(new TreeMoveTarget(null, 1), moves[^1].Target);
    });

    [Fact]
    public Task Dragging_a_row_onto_another_moves_it_inside() => WinFormsUi.Run(() =>
    {
        var tree = Create();
        var moves = new List<TreeMovedEventArgs>();
        tree.Moved += (_, e) => moves.Add(e);
        using var form = WinFormsUi.Show(tree);

        var from = tree.View.Points(1).Label;
        var to = tree.View.Points(0).Label;
        Raise(tree.View, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, from.X, from.Y, 0));
        Raise(tree.View, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, to.X, to.Y, 0));
        Assert.Equal(("src", TreeDropZone.Inside), tree.View.Drop);
        Raise(tree.View, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, to.X, to.Y, 0));

        Assert.Equal(("readme", new TreeMoveTarget("src", int.MaxValue)), (moves.Single().Key, moves.Single().Target));
    });
}
