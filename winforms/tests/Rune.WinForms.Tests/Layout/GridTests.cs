namespace Rune.WinForms.Tests;

/// <summary>GridControl：等宽的列、按宽度自动换列（auto-fit）、跨列跨行、dense 回填、格子里的对齐、从右到左。</summary>
public class GridTests
{
    /// <summary>自然大小固定的自动大小控件。</summary>
    sealed class Box : Control
    {
        readonly Size natural;

        public Box(int width = 10, int height = 20)
        {
            natural = new Size(width, height);
            AutoSize = true;
            Size = natural;
        }

        public override Size GetPreferredSize(Size proposedSize) => natural;
    }

    static GridControl Grid(GridControl grid, params Control[] children)
    {
        grid.Controls.AddRange(children);
        return grid;
    }

    /// <summary>放进窗体，宽度由父容器给（停靠在顶部）。</summary>
    static Form Show(GridControl grid, int width)
    {
        var host = new Panel { Width = width, Height = 400 };
        grid.Dock = DockStyle.Top;
        host.Controls.Add(grid);
        var form = WinFormsUi.Show(host);
        host.Width = width;
        host.PerformLayout();
        grid.PerformLayout();
        return form;
    }

    static float Em(Control control) => control.Font.SizeInPoints * control.DeviceDpi / 72f;

    [Upstream("Grid", "列数与最小宽度是动态样式")]
    [Fact]
    public Task Fixed_columns_are_equal_with_gaps() => WinFormsUi.Run(() =>
    {
        var boxes = Enumerable.Range(0, 4).Select(_ => new Box()).ToArray();
        var grid = Grid(new GridControl { Columns = 3, Gap = Space.None, ColumnGap = Space.Lg, RowGap = Space.Sm }, boxes);
        using var form = Show(grid, 300);
        var gap = Math.Round(Em(grid));
        var width = (300 - 2 * gap) / 3;

        Assert.Equal(new Rectangle(0, 0, (int)Math.Round(width), 20), boxes[0].Bounds);
        Assert.Equal((int)Math.Round(2 * (width + gap)), boxes[2].Left);
        Assert.Equal(20 + (int)Math.Round(0.5 * Em(grid)), boxes[3].Top); // 换到第二行
        Assert.Equal(boxes[3].Bottom, grid.Height); // 高度跟着内容
    });

    [Upstream("Grid", "同时给 columns 与 minItemWidth 时以 minItemWidth 为准")]
    [Fact]
    public Task Min_item_width_picks_the_column_count_and_collapses_empty_columns() => WinFormsUi.Run(() =>
    {
        var grid = Grid(new GridControl { Columns = 1, MinItemWidth = 100 }, [.. Enumerable.Range(0, 5).Select(_ => new Box())]);
        using var form = Show(grid, 350 * grid.DeviceDpi / 96);
        Assert.Equal(3, grid.ColumnCount); // 350 放得下 3 个 100

        var two = Enumerable.Range(0, 2).Select(_ => new Box()).ToArray();
        var fit = Grid(new GridControl { MinItemWidth = "5em" }, two);
        using var second = Show(fit, 600);
        Assert.Equal(2, fit.ColumnCount); // 只有两项：空列收起，各项拉宽（auto-fit）
        Assert.Equal(300, two[0].Width);
    });

    [Upstream("Grid", "GridItem 跨列、跨行")]
    [Fact]
    public Task Items_span_columns_and_rows() => WinFormsUi.Run(() =>
    {
        var (wide, tall, a, full, b) = (new GridItemControl { Span = 2 }, new Box(), new Box(), new Box(), new Box());
        wide.Controls.Add(new Box());
        var grid = Grid(new GridControl { Columns = 3 }, wide, tall, a, full, b);
        grid.SetRowSpan(tall, 2);
        grid.SetSpan(full, GridSpan.Full);
        using var form = Show(grid, 300);

        Assert.Equal(new Rectangle(0, 0, 200, 20), wide.Bounds);
        Assert.Equal(new Rectangle(200, 0, 100, 40), tall.Bounds); // 跨两行
        Assert.Equal(new Rectangle(0, 20, 100, 20), a.Bounds);
        Assert.Equal(new Rectangle(0, 40, 300, 20), full.Bounds); // 占满一整行
        Assert.Equal(new Rectangle(0, 60, 100, 20), b.Bounds);
    });

    [Fact]
    public Task Dense_flow_backfills_holes_and_right_to_left_mirrors() => WinFormsUi.Run(() =>
    {
        var (a, wide, c) = (new Box(), new Box(), new Box());
        var grid = Grid(new GridControl { Columns = 2, Flow = GridFlow.RowDense }, a, wide, c);
        grid.SetSpan(wide, 2);
        using var form = Show(grid, 200);
        Assert.Equal(new Point(100, 0), c.Location); // 回填 a 后面的空位

        var (d, e) = (new Box(), new Box());
        using var second = Show(Grid(new GridControl { Columns = 2, RightToLeft = RightToLeft.Yes }, d, e), 200);
        Assert.Equal((100, 0), (d.Left, e.Left)); // 从右到左：第一列在右边
    });

    [Fact]
    public Task Align_and_justify_place_content_inside_cells() => WinFormsUi.Run(() =>
    {
        var small = new Box(20, 10);
        using var form = Show(Grid(new GridControl { Columns = 2, Align = GridAlign.Center, Justify = GridAlign.End }, small, new Box(10, 40)), 200);

        Assert.Equal(new Rectangle(80, 15, 20, 10), small.Bounds);
    });

    [Fact]
    public Task Grid_lists_are_lists_of_list_items() => WinFormsUi.Run(() =>
    {
        using var list = new GridListControl();
        using var item = new GridListItemControl();

        Assert.Equal((AccessibleRole.List, AccessibleRole.ListItem), (list.AccessibilityObject.Role, item.AccessibilityObject.Role));
    });

    [Upstream("Grid", "balanced：")]
    [Fact]
    public Task Balanced_never_leaves_one_item_alone_on_the_last_row() => WinFormsUi.Run(() =>
    {
        Assert.Equal([2, 4, 1, 3, 1], new[] { (4, 3), (4, 4), (4, 1), (6, 4), (1, 3) }.Select(pair => GridLayout.BalancedColumns(pair.Item1, pair.Item2)));
        int Columns(int width)
        {
            var grid = Grid(new GridControl { MinItemWidth = 100, Balanced = true }, [.. Enumerable.Range(0, 4).Select(_ => new Box())]);
            using var form = Show(grid, width * grid.DeviceDpi / 96);
            return grid.ColumnCount;
        }
        Assert.Equal(2, Columns(350)); // 放得下 3 列：2 + 2，不排成 3 + 1
        Assert.Equal(4, Columns(450));
        Assert.Equal(1, Columns(120));
    });
}
