namespace Rune.Avalonia.Tests;

/// <summary>TreeData：展开成行、勾选的父子联动（半选、懒加载出来的子节点跟随父节点）、勾选结果的取法、路径、搜索过滤、移动节点、平铺的行组装成树。</summary>
public class TreeDataTests
{
    sealed record Node(string Id, IReadOnlyList<Node>? Children = null, bool? Lazy = null);

    static readonly TreeAccessors<Node> Accessors = new((node, _) => node.Id, node => node.Children, node => node.Lazy ?? (node.Children?.Count ?? 0) > 0);

    static readonly Node[] Tree =
    [
        new("a", [new("a1"), new("a2", [new("a2x")])]),
        new("b", Lazy: true),
    ];

    static Node WithChildren(Node node, IReadOnlyList<Node> children) => node with { Children = children };

    /// <summary>树的形状：叶子是 key，有子节点的是“key[子节点…]”。</summary>
    static string Shape(IEnumerable<Node> nodes) => string.Join(",", nodes.Select(node => node.Children is { Count: > 0 } children ? $"{node.Id}[{Shape(children)}]" : node.Id));

    [Fact]
    public void Flatten_only_opens_the_expanded_nodes()
    {
        var rows = TreeData.Flatten(Tree, Accessors, new HashSet<string> { "a" });
        Assert.Equal(["a", "a1", "a2", "b"], rows.Select(row => row.Key));
        Assert.Equal((1, "a", 2, 2), (rows[2].Depth, rows[2].Parent, rows[2].SetSize, rows[2].Position));
        Assert.Equal((true, 0), (rows[3].Expandable, rows[3].Depth));
        Assert.False(rows[1].Expandable);
    }

    [Fact]
    public void Checking_cascades_to_descendants_and_ancestors()
    {
        var index = TreeData.Index(Tree, Accessors);
        Assert.Equal(["a", "a1", "a2", "a2x"], TreeData.ToggleChecked(index, [], "a"));
        Assert.Equal(["a", "a1", "a2", "a2x"], TreeData.ToggleChecked(index, ["a1"], "a2x"));
        // 取消一个子节点时祖先跟着取消，其余兄弟保留
        Assert.Equal(["a1"], TreeData.ToggleChecked(index, ["a"], "a2x"));

        var states = TreeData.CheckStates(index, ["a2x"]);
        Assert.Equal((CheckState.Checked, CheckState.Indeterminate, CheckState.Unchecked, CheckState.Unchecked), (states["a2"], states["a"], states["a1"], states["b"]));
        Assert.Empty(TreeData.ToggleChecked(index, ["a2x"], "a2"));

        // 父节点勾上之后懒加载出来的子节点也算勾上
        var loaded = TreeData.Index([Tree[0], new Node("b", [new("b1"), new("b2")])], Accessors);
        Assert.Equal(CheckState.Checked, TreeData.CheckStates(loaded, ["b"])["b1"]);
        Assert.Equal(["b2"], TreeData.ToggleChecked(loaded, ["b"], "b1"));
    }

    [Fact]
    public void Check_strategies_and_paths()
    {
        var index = TreeData.Index(Tree, Accessors);
        var some = TreeData.ToggleChecked(index, [], "a2");
        Assert.Equal(["a2", "a2x"], TreeData.CheckedByStrategy(index, some, CheckStrategy.All));
        Assert.Equal(["a2"], TreeData.CheckedByStrategy(index, some, CheckStrategy.Parent));
        Assert.Equal(["a2x"], TreeData.CheckedByStrategy(index, some, CheckStrategy.Child));
        var all = TreeData.ToggleChecked(index, [], "a");
        Assert.Equal(["a"], TreeData.CheckedByStrategy(index, all, CheckStrategy.Parent));
        Assert.Equal(["a1", "a2x"], TreeData.CheckedByStrategy(index, all, CheckStrategy.Child));

        Assert.Equal(["a", "a2", "a2x"], TreeData.Path(index, "a2x"));
        Assert.Equal(["b"], TreeData.Path(index, "b"));
        Assert.Empty(TreeData.Path(index, "nope"));
    }

    [Fact]
    public void Filter_keeps_matches_and_their_ancestors()
    {
        var result = TreeData.Filter(Tree, Accessors, node => node.Id.StartsWith("a2", StringComparison.Ordinal));
        Assert.True(result.Include.SetEquals(["a", "a2", "a2x"]));
        Assert.True(result.Expand.SetEquals(["a", "a2"]));
        var rows = TreeData.Flatten(Tree, Accessors, result.Expand, result.Include);
        Assert.Equal(["a", "a2", "a2x"], rows.Select(row => row.Key));
        // 同级的总数与位置按过滤后的算
        Assert.Equal((1, 1), (rows[1].SetSize, rows[1].Position));
        Assert.True(TreeData.Filter(Tree, Accessors, node => node.Id == "a").Include.SetEquals(["a"]));
    }

    [Fact]
    public void Move_rebuilds_only_the_touched_branches()
    {
        var moved = TreeData.Move(Tree, Accessors, WithChildren, "a2x", new TreeMoveTarget("a", 0))!;
        Assert.Equal("a[a2x,a1,a2],b", Shape(moved));
        Assert.Equal("a[a1,a2[a2x]],b", Shape(Tree));
        Assert.Same(Tree[1], moved[1]);
        Assert.Equal("a[a2[a2x]],b,a1", Shape(TreeData.Move(Tree, Accessors, WithChildren, "a1", new TreeMoveTarget(null, 2))!));
        Assert.Equal("b,a[a1,a2[a2x]]", Shape(TreeData.Move(Tree, Accessors, WithChildren, "b", new TreeMoveTarget(null, 0))!));
        // 移到自己或自己的子孙下面、key 不存在时
        Assert.Null(TreeData.Move(Tree, Accessors, WithChildren, "a", new TreeMoveTarget("a2", 0)));
        Assert.Null(TreeData.Move(Tree, Accessors, WithChildren, "a", new TreeMoveTarget("a", 0)));
        Assert.Null(TreeData.Move(Tree, Accessors, WithChildren, "zz", new TreeMoveTarget(null, 0)));
    }

    sealed record Row(string Id, string? ParentId, string Name, IReadOnlyList<Row>? Children = null);

    [Fact]
    public void Build_assembles_flat_rows()
    {
        Row[] rows = [new("b1", "b", "小组"), new("a", null, "研发"), new("b", null, "采购"), new("a1", "a", "前端"), new("x", "gone", "父行不在这批里")];
        var tree = TreeData.Build(rows, row => row.Id, row => row.ParentId, (row, children) => row with { Children = children });
        string Of(IEnumerable<Row> nodes) => string.Join(",", nodes.Select(node => node.Children is { Count: > 0 } children ? $"{node.Id}[{Of(children)}]" : node.Id));
        Assert.Equal("a[a1],b[b1],x", Of(tree));
        Assert.Null(rows[1].Children);
        var accessors = new TreeAccessors<Row>((row, _) => row.Id, row => row.Children);
        var found = TreeData.Filter(tree, accessors, row => row.Name == "前端");
        Assert.Equal(["a", "a1"], [.. found.Expand, .. found.Matched]);

        Assert.Throws<ArgumentException>(() => TreeData.Build([rows[1], rows[1]], row => row.Id, row => row.ParentId, (row, children) => row with { Children = children }));
        Row[] loop = [new("p", "q", ""), new("q", "p", "")];
        Assert.Throws<ArgumentException>(() => TreeData.Build(loop, row => row.Id, row => row.ParentId, (row, children) => row with { Children = children }));
    }
}
