namespace Rune.WinForms.Tests;

/// <summary>
/// TableControl：表头、表体、表尾、标题的结构，标题是读屏名称；表头的格 textMuted、sm，表尾加粗；
/// 斑马纹隔行；列宽按内容比例分满表格宽，对齐按格。
/// </summary>
public class TableTests
{
    static TableControl Build(bool striped = false) => new()
    {
        Striped = striped, Width = 400,
        Controls =
        {
            new TableCaptionControl { Text = "属性" },
            new TableHeadControl { Controls = { new TableRowControl { Controls = { new TableHeaderCellControl { Text = "名称" }, new TableHeaderCellControl { Text = "默认值", Align = TableAlign.End } } } } },
            new TableBodyControl
            {
                Controls =
                {
                    new TableRowControl { Controls = { new TableHeaderCellControl { Scope = "row", Text = "size" }, new TableCellControl { Text = "md", Align = TableAlign.End } } },
                    new TableRowControl { Controls = { new TableCellControl { Text = "tone" }, new TableCellControl { Text = "accent", Align = TableAlign.End } } },
                },
            },
            new TableFootControl { Controls = { new TableRowControl { Controls = { new TableHeaderCellControl { Scope = "row", Text = "合计" }, new TableCellControl { Text = "2" } } } } },
        },
    };

    static TableCellControl Cell(TableControl table, int row, int column) => (TableCellControl)table.Rows[row].Row.Controls[column];

    [Fact]
    [Upstream("Table", "渲染原生 table 结构")]
    public Task Sections_rows_and_the_caption_make_the_structure() => WinFormsUi.Run(() =>
    {
        var table = Build();
        using var form = WinFormsUi.Show(table);

        Assert.Equal(["head", "body", "body", "foot"], table.Rows.Select(row => Cell(table, table.Rows.ToList().IndexOf(row), 0).SectionKind));
        Assert.Equal(AccessibleRole.Table, table.AccessibilityObject.Role);
        Assert.Equal("属性", table.AccessibilityObject.Name);
        Assert.Equal(AccessibleRole.ColumnHeader, Cell(table, 0, 0).AccessibilityObject.Role);
        Assert.Equal(AccessibleRole.RowHeader, Cell(table, 1, 0).AccessibilityObject.Role);
        Assert.Equal(AccessibleRole.Cell, Cell(table, 1, 1).AccessibilityObject.Role);
        // 各部分从上往下：标题、表头、表体、表尾
        var tops = table.Rows.Select(row => row.Row.PointToScreen(Point.Empty).Y).ToArray();
        Assert.Equal(tops.Order(), tops);
    });

    [Fact]
    [Upstream("Table", "表体、表尾里的行标题用单元格的样式")]
    public Task Header_cells_look_like_headers_only_in_the_head() => WinFormsUi.Run(() =>
    {
        var table = Build();
        using var form = WinFormsUi.Show(table);

        var head = Cell(table, 0, 0);
        Assert.Equal(Tokens.FontSize.Sm * 72 / 96, head.Font.SizeInPoints, 0.01);
        var rowHeader = Cell(table, 1, 0);
        Assert.Equal(table.Font.SizeInPoints, rowHeader.Font.SizeInPoints, 0.01);
        Assert.False(rowHeader.Font.Bold);
        Assert.True(Cell(table, 3, 0).Font.Bold); // 表尾加粗，行标题也照表尾
        Assert.True(Cell(table, 3, 1).Font.Bold);
    });

    [Fact]
    public Task Columns_share_the_table_width_and_cells_align() => WinFormsUi.Run(() =>
    {
        var table = Build(striped: true);
        using var form = WinFormsUi.Show(table);

        // 两列分满 400 减去外框；每格左右各 space.md 的内边距（按格自己的字号）
        var name = Cell(table, 1, 0);
        var value = Cell(table, 1, 1);
        var line = table.LogicalToDeviceUnits(1);
        var padding = (int)MathF.Round(0.75f * name.Font.SizeInPoints * name.DeviceDpi / 72f);
        Assert.Equal(400 - 2 * line, name.Width + value.Width); // 格占满整列，内边距是 Padding
        Assert.Equal(padding, name.Padding.Left);
        Assert.Equal(TableAlign.End, value.Align);
        Assert.Equal([false, true], table.Rows.Where(row => row.Section is TableBodyControl).Select(row => row.Row.IsStriped));
    });
}
