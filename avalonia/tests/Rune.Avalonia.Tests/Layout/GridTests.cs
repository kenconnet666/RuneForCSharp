using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;

namespace Rune.Avalonia.Tests;

/// <summary>Grid：等宽的列、按宽度自动换列（auto-fit）、跨列跨行、dense 回填、格子里的对齐。各项是方块，结果按像素对。</summary>
public class GridTests
{
    static Border Box(double height = 20) => new() { Height = height };

    static Grid Show(Grid grid, double width)
    {
        HeadlessUi.Show(new Border { Width = width, Child = grid, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left });
        return grid;
    }

    static Grid With(Grid grid, params Control[] children)
    {
        grid.Children.AddRange(children);
        return grid;
    }

    [Upstream("Grid", "列数与最小宽度是动态样式")]
    [Fact]
    public Task Fixed_columns_are_equal_with_gaps() => HeadlessUi.Run(() =>
    {
        var boxes = Enumerable.Range(0, 4).Select(_ => Box()).ToArray();
        var grid = Show(With(new Grid { Columns = 3, Gap = Space.None, ColumnGap = Space.Lg, RowGap = Space.Sm }, boxes), 300);
        var em = grid.GetValue(TextElement.FontSizeProperty);
        var width = (300 - 2 * em) / 3;

        Assert.Equal(new Rect(0, 0, width, 20), boxes[0].Bounds);
        Assert.Equal(new Rect(2 * (width + em), 0, width, 20), boxes[2].Bounds);
        Assert.Equal(new Rect(0, 20 + 0.5 * em, width, 20), boxes[3].Bounds); // 换到第二行
    });

    [Upstream("Grid", "同时给 columns 与 minItemWidth 时以 minItemWidth 为准")]
    [Fact]
    public Task Min_item_width_picks_the_column_count_and_collapses_empty_columns() => HeadlessUi.Run(() =>
    {
        var five = Enumerable.Range(0, 5).Select(_ => Box()).ToArray();
        var grid = Show(With(new Grid { Columns = 1, MinItemWidth = 100 }, five), 350);
        Assert.Equal(3, grid.ColumnCount); // 350 放得下 3 个 100
        Assert.InRange(five[0].Bounds.Width, 350 / 3d - 1, 350 / 3d + 1); // 布局取整

        var two = Enumerable.Range(0, 2).Select(_ => Box()).ToArray();
        var fit = Show(With(new Grid { MinItemWidth = "5em" }, two), 600);
        Assert.Equal(2, fit.ColumnCount); // 只有两项：空列收起，各项拉宽（auto-fit）
        Assert.Equal(300, two[0].Bounds.Width, 3);

        var narrow = Show(With(new Grid { MinItemWidth = 200 }, Box()), 150);
        Assert.Equal(150, narrow.Children[0].Bounds.Width, 3); // 比最小宽度还窄：一列，不溢出
    });

    [Upstream("Grid", "GridItem 跨列、跨行")]
    [Fact]
    public Task Items_span_columns_and_rows() => HeadlessUi.Run(() =>
    {
        var (wide, tall, a, full, b) = (new GridItem { Span = 2, Child = Box() }, new GridItem { RowSpan = 2, Child = Box() }, Box(), new GridItem { Span = GridSpan.Full, Child = Box() }, Box());
        Show(With(new Grid { Columns = 3 }, wide, tall, a, full, b), 300);

        Assert.Equal(new Rect(0, 0, 200, 20), wide.Bounds);
        Assert.Equal(new Rect(200, 0, 100, 40), tall.Bounds); // 跨两行
        Assert.Equal(new Rect(0, 20, 100, 20), a.Bounds);
        Assert.Equal(new Rect(0, 40, 300, 20), full.Bounds); // 占满一整行
        Assert.Equal(new Rect(0, 60, 100, 20), b.Bounds);

        // 跨列数不超过当前的列数
        var clamped = new GridItem { Span = 3, Child = Box() };
        Show(With(new Grid { MinItemWidth = 100 }, clamped, Box()), 150);
        Assert.Equal(150, clamped.Bounds.Width, 3);
    });

    [Fact]
    public Task Dense_flow_backfills_holes() => HeadlessUi.Run(() =>
    {
        var (a, wide, c) = (Box(), new GridItem { Span = 2, Child = Box() }, Box());
        Show(With(new Grid { Columns = 2, Flow = GridFlow.RowDense }, a, wide, c), 200);
        Assert.Equal(new Point(100, 0), c.Bounds.Position); // 回填 a 后面的空位

        var (d, wider, f) = (Box(), new GridItem { Span = 2, Child = Box() }, Box());
        Show(With(new Grid { Columns = 2 }, d, wider, f), 200);
        Assert.Equal(new Point(0, 40), f.Bounds.Position); // 不回填：排在后面
    });

    [Fact]
    public Task Align_and_justify_place_content_inside_cells() => HeadlessUi.Run(() =>
    {
        var small = new Border { Width = 20, Height = 10 };
        var tall = Box(40);
        Show(With(new Grid { Columns = 2, Align = GridAlign.Center, Justify = GridAlign.End }, small, tall), 200);

        Assert.Equal(new Rect(80, 15, 20, 10), small.Bounds);
    });

    [Fact]
    public Task Grid_lists_are_lists_of_list_items() => HeadlessUi.Run(() =>
    {
        var item = new GridListItem { Child = Box() };
        var list = With(new GridList { Columns = 2 }, item);
        HeadlessUi.Show(list);

        Assert.Equal(AutomationControlType.List, ControlAutomationPeer.CreatePeerForElement(list).GetAutomationControlType());
        Assert.Equal(AutomationControlType.ListItem, ControlAutomationPeer.CreatePeerForElement(item).GetAutomationControlType());
    });

    [Upstream("Grid", "balanced：")]
    [Fact]
    public Task Balanced_never_leaves_one_item_alone_on_the_last_row() => HeadlessUi.Run(() =>
    {
        Assert.Equal([2, 4, 1, 3, 1], new[] { (4, 3), (4, 4), (4, 1), (6, 4), (1, 3) }.Select(pair => GridLayout.BalancedColumns(pair.Item1, pair.Item2)));
        Grid Four(double width) => Show(With(new Grid { MinItemWidth = 100, Balanced = true }, [.. Enumerable.Range(0, 4).Select(_ => Box())]), width);
        Assert.Equal(2, Four(350).ColumnCount); // 放得下 3 列：2 + 2，不排成 3 + 1
        Assert.Equal(4, Four(450).ColumnCount);
        Assert.Equal(1, Four(120).ColumnCount);
    });
}
