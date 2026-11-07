using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Grid：等宽的列、按宽度自动换列（auto-fit）、跨列跨行、dense 回填、格子里的对齐。各项是方块，结果按像素对。</summary>
public class GridTests
{
    static Border Box(double height = 20) => new() { Height = height };

    static Grid Show(Grid grid, double width)
    {
        var host = new Border { Width = width, Child = grid, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
        return grid;
    }

    static Grid With(Grid grid, params UIElement[] children)
    {
        foreach (var child in children) grid.Children.Add(child);
        return grid;
    }

    static Rect At(FrameworkElement child) => new(child.TranslatePoint(default, (UIElement)VisualTreeHelper.GetParent(child)), child.RenderSize);

    [Upstream("Grid", "列数与最小宽度是动态样式")]
    [Fact]
    public Task Fixed_columns_are_equal_with_gaps() => WpfUi.Run(() =>
    {
        var boxes = Enumerable.Range(0, 4).Select(_ => Box()).ToArray();
        var grid = Show(With(new Grid { Columns = 3, Gap = Space.None, ColumnGap = Space.Lg, RowGap = Space.Sm }, boxes), 300);
        var em = TextElement.GetFontSize(grid);
        var width = (300 - 2 * em) / 3;

        Assert.Equal(new Rect(0, 0, width, 20), At(boxes[0]));
        Assert.Equal(new Rect(2 * (width + em), 0, width, 20), At(boxes[2]));
        Assert.Equal(new Rect(0, 20 + 0.5 * em, width, 20), At(boxes[3])); // 换到第二行
    });

    [Upstream("Grid", "同时给 columns 与 minItemWidth 时以 minItemWidth 为准")]
    [Fact]
    public Task Min_item_width_picks_the_column_count_and_collapses_empty_columns() => WpfUi.Run(() =>
    {
        var five = Enumerable.Range(0, 5).Select(_ => Box()).ToArray();
        var grid = Show(With(new Grid { Columns = 1, MinItemWidth = 100 }, five), 350);
        Assert.Equal(3, grid.ColumnCount); // 350 放得下 3 个 100
        Assert.Equal(350 / 3d, At(five[0]).Width, 3);

        var two = Enumerable.Range(0, 2).Select(_ => Box()).ToArray();
        var fit = Show(With(new Grid { MinItemWidth = "5em" }, two), 600);
        Assert.Equal(2, fit.ColumnCount); // 只有两项：空列收起，各项拉宽（auto-fit）
        Assert.Equal(300, At(two[0]).Width, 3);

        var only = Box();
        Show(With(new Grid { MinItemWidth = 200 }, only), 150);
        Assert.Equal(150, At(only).Width, 3); // 比最小宽度还窄：一列，不溢出
    });

    [Upstream("Grid", "GridItem 跨列、跨行")]
    [Fact]
    public Task Items_span_columns_and_rows() => WpfUi.Run(() =>
    {
        var (wide, tall, a, full, b) = (new GridItem { Span = 2, Child = Box() }, new GridItem { RowSpan = 2, Child = Box() }, Box(), new GridItem { Span = GridSpan.Full, Child = Box() }, Box());
        Show(With(new Grid { Columns = 3 }, wide, tall, a, full, b), 300);

        Assert.Equal(new Rect(0, 0, 200, 20), At(wide));
        Assert.Equal(new Rect(200, 0, 100, 40), At(tall)); // 跨两行
        Assert.Equal(new Rect(0, 20, 100, 20), At(a));
        Assert.Equal(new Rect(0, 40, 300, 20), At(full)); // 占满一整行
        Assert.Equal(new Rect(0, 60, 100, 20), At(b));

        // 跨列数不超过当前的列数
        var clamped = new GridItem { Span = 3, Child = Box() };
        Show(With(new Grid { MinItemWidth = 100 }, clamped, Box()), 150);
        Assert.Equal(150, At(clamped).Width, 3);
    });

    [Fact]
    public Task Dense_flow_backfills_holes() => WpfUi.Run(() =>
    {
        var (a, wide, c) = (Box(), new GridItem { Span = 2, Child = Box() }, Box());
        Show(With(new Grid { Columns = 2, Flow = GridFlow.RowDense }, a, wide, c), 200);
        Assert.Equal(new Point(100, 0), At(c).Location); // 回填 a 后面的空位

        var (d, wider, f) = (Box(), new GridItem { Span = 2, Child = Box() }, Box());
        Show(With(new Grid { Columns = 2 }, d, wider, f), 200);
        Assert.Equal(new Point(0, 40), At(f).Location); // 不回填：排在后面
    });

    [Fact]
    public Task Align_and_justify_place_content_inside_cells() => WpfUi.Run(() =>
    {
        var small = new Border { Width = 20, Height = 10 };
        Show(With(new Grid { Columns = 2, Align = GridAlign.Center, Justify = GridAlign.End }, small, Box(40)), 200);

        Assert.Equal(new Rect(80, 15, 20, 10), At(small));
    });

    [Fact]
    public Task Grid_lists_are_lists_of_list_items() => WpfUi.Run(() =>
    {
        var item = new GridListItem { Child = Box() };
        var list = With(new GridList { Columns = 2 }, item);

        Assert.Equal(AutomationControlType.List, UIElementAutomationPeer.CreatePeerForElement(list).GetAutomationControlType());
        Assert.Equal(AutomationControlType.ListItem, UIElementAutomationPeer.CreatePeerForElement(item).GetAutomationControlType());
    });

    [Upstream("Grid", "balancedColumns：")]
    [Fact]
    public Task Balanced_never_leaves_one_item_alone_on_the_last_row() => WpfUi.Run(() =>
    {
        Assert.Equal([2, 4, 1, 3, 1], new[] { (4, 3), (4, 4), (4, 1), (6, 4), (1, 3) }.Select(pair => GridLayout.BalancedColumns(pair.Item1, pair.Item2)));
        Grid Four(double width) => Show(With(new Grid { MinItemWidth = 100, Balanced = true }, [.. Enumerable.Range(0, 4).Select(_ => Box())]), width);
        Assert.Equal(2, Four(350).ColumnCount); // 放得下 3 列：2 + 2，不排成 3 + 1
        Assert.Equal(4, Four(450).ColumnCount);
        Assert.Equal(1, Four(120).ColumnCount);
    });
}
