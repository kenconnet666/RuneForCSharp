using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>ColumnTable：按列定义渲染静态表格（格式化、状态、空值同 DataTable），MergeRepeated 层层合并、GetCellSpan 合并行列，合计行；Table 的合并单元格。</summary>
public class ColumnTableTests
{
    sealed record Quote(string Supplier, string Category, string Part, decimal? Price, string Status);

    static readonly StatusMap Statuses = new()
    {
        ["valid"] = new("有效", StatusTone.Success),
        ["expired"] = new("已过期", StatusTone.Neutral),
    };

    static readonly Quote[] Quotes =
    [
        new("华辰", "机械件", "支架", 120, "valid"),
        new("华辰", "机械件", "底座", 80, "valid"),
        new("华辰", "钣金件", "罩壳", null, "expired"),
        new("启航", "钣金件", "盖板", 45.5m, "valid"),
    ];

    static readonly DataTableColumn[] Columns =
    [
        new("Supplier", "供应商") { Summary = ColumnSummary.Of(_ => "共 4 项") },
        new("Category", "品类"),
        new("Part", "零件"),
        new("Price", "单价") { Type = ColumnType.Currency, Summary = ColumnSummary.Sum, Empty = "待报价" },
        new("Status", "状态") { Statuses = Statuses },
    ];

    static List<List<TableCell>> BodyRows(Table table) =>
        [.. table.Sections.OfType<TableBody>().Single().Rows.OfType<TableRow>().Select(row => row.Cells.OfType<TableCell>().ToList())];

    static string Text(TableCell cell) => cell.Content switch
    {
        StatusBadge badge => badge.Statuses!.Label(badge.Value!),
        var content => content?.ToString() ?? "",
    };

    [Fact]
    [Upstream("ColumnTable", "repeatedRowSpans：相邻相同的合并，后一列不跨前一列的分组")]
    public void Repeated_spans_do_not_cross_the_previous_groups()
    {
        var spans = TableColumns.RepeatedRowSpans(Quotes, Columns, ["Supplier", "Category"]);
        Assert.Equal([3, 0, 0, 1], spans["Supplier"]);
        // 第 3、4 行都是钣金件，但供应商不同：不合并
        Assert.Equal([2, 0, 1, 1], spans["Category"]);
    }

    [Fact]
    [Upstream("ColumnTable", "格式化同 DataTable：金额按语言、空值显示 empty、状态显示名称；mergeRepeated 写 rowspan、跳过被合并的格；合计行")]
    public Task Formats_merges_and_sums() => HeadlessUi.Run(() =>
    {
        var table = new ColumnTable { ItemsSource = Quotes, Columns = Columns, MergeRepeated = ["Supplier", "Category"] };
        HeadlessUi.Show(new StackPanel { Width = 800, Children = { table } });

        var rows = BodyRows(table);
        Assert.Equal([5, 3, 4, 5], rows.Select(row => row.Count));
        Assert.Equal((3, 2), (rows[0][0].RowSpan, rows[0][1].RowSpan));
        Assert.Equal("¥120.00", Text(rows[0][3]));
        Assert.Equal(TableAlign.End, rows[0][3].Align);
        Assert.Contains("待报价", rows[2].Select(Text));
        Assert.Contains("已过期", rows[2].Select(Text));
        var foot = table.Sections.OfType<TableFoot>().Single().Rows.OfType<TableRow>().Single().Cells.OfType<TableCell>().Select(Text).ToList();
        Assert.Contains("共 4 项", foot);
        Assert.Contains("¥245.50", foot);
        Assert.Same(Quotes[0], ((TableRow)rows[0][0].Parent!).DataContext);

        // 合并的格占三行高：和第 1 至 3 行的格上下对齐（同 HTML 的 rowspan）
        var (merged, third) = (rows[0][0].Bounds, rows[2][0].Bounds);
        Assert.True(merged.Top < rows[0][2].Bounds.Bottom && merged.Bottom > third.Top);
        // 第 2 行的第一格排在第三列（前两列被上面合并的格占住）
        Assert.Equal(rows[0][2].Bounds.Left, rows[1][0].Bounds.Left, 1);
    });

    [Fact]
    [Upstream("ColumnTable", "没有行：表体里一行占满的“暂无数据”，可以用 empty 换")]
    public Task Empty_rows_show_one_spanning_cell() => HeadlessUi.Run(() =>
    {
        var (plain, custom) = (new ColumnTable { Columns = Columns }, new ColumnTable { Columns = Columns, ItemsSource = Array.Empty<Quote>(), Empty = "还没有报价" });
        HeadlessUi.Show(new StackPanel { Width = 800, Children = { plain, custom } });

        Assert.Equal(["暂无数据", "还没有报价"], new[] { plain, custom }.Select(table => Text(BodyRows(table).Single().Single())));
        Assert.Equal(Columns.Length, BodyRows(plain)[0][0].ColumnSpan);
        // 跨满所有列：和表头一样宽
        var head = plain.GetVisualDescendants().OfType<TableHeaderCell>().ToList();
        Assert.True(BodyRows(plain)[0][0].Bounds.Right >= head[^1].Bounds.Right - 1);
    });

    [Fact]
    [Upstream("ColumnTable", "cellSpan 合并列：被合并的格返回 0 不渲染")]
    public Task Cell_spans_merge_columns() => HeadlessUi.Run(() =>
    {
        var table = new ColumnTable
        {
            ItemsSource = Quotes.Take(1).ToArray(),
            Columns = Columns,
            GetCellSpan = (_, column, _) => column.Key == "Category" ? new CellSpan(ColumnSpan: 2) : column.Key == "Part" ? new CellSpan(ColumnSpan: 0) : null,
        };
        HeadlessUi.Show(new StackPanel { Width = 800, Children = { table } });

        var cells = BodyRows(table).Single();
        Assert.Equal(4, cells.Count);
        Assert.Equal(2, cells[1].ColumnSpan);
        // 合并两列的格之后，单价格在单价的表头那一列（表头是 sm 字号，内边距小一点，不逐像素比）
        var head = table.GetVisualDescendants().OfType<TableHeaderCell>().ToList();
        Assert.InRange(cells[2].Bounds.Left, head[2].Bounds.Right, head[3].Bounds.Right);
        Assert.InRange(cells[1].Bounds.Right, head[2].Bounds.Left, head[3].Bounds.Left);
    });

    [Fact]
    public Task Collections_that_change_rebuild_and_types_format() => HeadlessUi.Run(() =>
    {
        var items = new System.Collections.ObjectModel.ObservableCollection<Dictionary<string, object?>> { new() { ["Size"] = 1_234_567L, ["Rate"] = 0.125, ["Day"] = new DateOnly(2026, 10, 20) } };
        var table = new ColumnTable
        {
            ItemsSource = items,
            Columns = [new("Size", "大小") { Type = ColumnType.FileSize }, new("Rate", "比例") { Type = ColumnType.Percent }, new("Day", "日期")],
        };
        HeadlessUi.Show(new StackPanel { Width = 800, Children = { table } });

        Assert.Equal(["1.2 MB", "12.5%", "2026年10月20日"], BodyRows(table)[0].Select(Text));
        items.Add(new() { ["Size"] = 999_999L });
        Assert.Equal(2, BodyRows(table).Count);
        Assert.Equal(["1 MB", "—", "—"], BodyRows(table)[1].Select(Text));
    });
}
