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

    /// <summary>两个方向都能滚的明细表放进限高的滚动区域（同文档站 ScrollArea 页的 both 示例）：表头吸顶，行号那一列固定。</summary>
    static (ScrollAreaControl Area, TableControl Table) Sheet()
    {
        static TableRowControl Row(TableCellControl first, IEnumerable<TableCellControl> rest)
        {
            var row = new TableRowControl();
            row.Controls.Add(first);
            foreach (var cell in rest) row.Controls.Add(cell);
            return row;
        }
        var head = new TableHeadControl();
        head.Controls.Add(Row(new TableHeaderCellControl { Sticky = true, ColumnWidth = 48, Text = "行" }, Enumerable.Range(0, 14).Select(c => new TableHeaderCellControl { ColumnWidth = 88, Text = ((char)('A' + c)).ToString() })));
        var body = new TableBodyControl();
        for (var r = 1; r <= 30; r++)
            body.Controls.Add(Row(new TableHeaderCellControl { Scope = "row", Sticky = true, Text = r.ToString(System.Globalization.CultureInfo.InvariantCulture) }, Enumerable.Range(0, 14).Select(_ => new TableCellControl { Nowrap = true, Text = "12.34" })));
        var table = new TableControl { StickyHeader = true, Controls = { head, body } };
        return (new ScrollAreaControl { Axis = ScrollAreaAxis.Both, Size = new Size(400, 288), Content = table }, table);
    }

    [Fact]
    [Upstream("Table", "stickyHeader 与 sticky：滚动后表头贴着顶边、第一列贴着起始边")]
    public Task The_sticky_header_and_column_stay_put_while_scrolling() => WinFormsUi.Run(() =>
    {
        var (area, table) = Sheet();
        using var form = WinFormsUi.Show(area);
        Point At(Control cell) => area.PointToClient(cell.PointToScreen(Point.Empty));
        var (corner, header, rowLabel, body) = (Cell(table, 0, 0), Cell(table, 0, 5), Cell(table, 10, 0), Cell(table, 10, 5));
        var before = (corner: At(corner), header: At(header), rowLabel: At(rowLabel), body: At(body));
        Assert.True(table.Width > area.Width); // 各列最小宽度放不下：表格比滚动区域宽

        area.ScrollOffset = new Point(200, 150);
        WinFormsUi.Pump();

        var border = table.LogicalToDeviceUnits(1);
        Assert.Equal(before.corner - new Size(border, border), At(corner)); // 角上的格贴着左上（外框滚走了）
        Assert.Equal(before.header.Y - border, At(header).Y); // 表头贴着顶边
        Assert.Equal(before.header.X - 200, At(header).X); // 横向照常滚
        Assert.Equal(before.rowLabel.X - border, At(rowLabel).X); // 第一列贴着起始边
        Assert.Equal(before.rowLabel.Y - 150, At(rowLabel).Y); // 竖向照常滚
        Assert.Equal(before.body - new Size(200, 150), At(body)); // 别的格两个方向都滚
        Assert.True(rowLabel.IsStickyColumn);
        // 叠放：表头的部分在表体上面，固定的格在同一行的别的格上面（子控件序号小的在上）
        Assert.Equal(0, table.Controls.IndexOf(corner.Parent!.Parent));
        Assert.Equal(0, rowLabel.Parent!.Controls.IndexOf(rowLabel));
    });
}
