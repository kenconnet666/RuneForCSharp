using Avalonia.Controls;
using Avalonia.Media;

namespace Rune.Avalonia.Tests;

/// <summary>Pivot 与 PivotTable：多级行维度、列维度的顺序、各种聚合、合计（decimal 精确）；多级行合并、热力着色、空格子。</summary>
public class PivotTableTests
{
    sealed record Sale(string Region, string Supplier, string Quarter, decimal Amount);

    static readonly Sale[] Records =
    [
        new("华东", "华辰", "Q2", 0.1m),
        new("华东", "华辰", "Q1", 0.2m),
        new("华东", "恒远", "Q2", 1.5m),
        new("华南", "粤海", "Q1", 3),
        new("华南", "粤海", "Q1", 4),
    ];

    static Func<object, object?> Field(Func<Sale, object?> pick) => row => pick((Sale)row);

    [Fact]
    [Upstream("pivot", "行按各级排序、列按值排序；每格聚合、行列合计与总计（Decimal 精确）")]
    public void Groups_sorts_and_totals()
    {
        var result = Pivot.Of(Records, new PivotOptions([Field(r => r.Region), Field(r => r.Supplier)])
        {
            Columns = Field(r => r.Quarter),
            Value = Field(r => r.Amount),
            Compare = Pivot.Comparer("zh-CN"),
        });
        // 文字按拼音：恒（heng）在华（hua）前面
        Assert.Equal([["华东", "恒远"], ["华东", "华辰"], ["华南", "粤海"]], result.RowKeys);
        Assert.Equal(["Q1", "Q2"], result.ColumnKeys);
        Assert.Equal(7m, result.Cell(["华南", "粤海"], "Q1"));
        Assert.Null(result.Cell(["华东", "恒远"], "Q1"));
        Assert.Equal(0.3m, result.RowTotal(["华东", "华辰"]));
        Assert.Equal(1.6m, result.ColumnTotal("Q2"));
        Assert.Equal(8.8m, result.GrandTotal);
        Assert.Equal((0.1m, 7m), result.Extent);
    }

    [Fact]
    [Upstream("pivot", "order 决定顺序，没列出的排在后面；count、avg、min、max")]
    public void Order_and_aggregates()
    {
        PivotOptions Base(PivotAggregate aggregate = default) => new([Field(r => r.Region)]) { Value = Field(r => r.Amount), Aggregate = aggregate };
        Assert.Equal([["华南"], ["华东"]], Pivot.Of(Records, Base() with { RowOrder = [["华南"]] }).RowKeys);
        Assert.Equal(3m, Pivot.Of(Records, Base(PivotAggregate.Count) with { Value = null }).Cell(["华东"], ""));
        Assert.Equal(3.5m, Pivot.Of(Records, Base(PivotAggregate.Avg)).Cell(["华南"], ""));
        Assert.Equal(0.1m, Pivot.Of(Records, Base(PivotAggregate.Min)).Cell(["华东"], ""));
        Assert.Equal(4m, Pivot.Of(Records, Base(PivotAggregate.Max)).GrandTotal);
    }

    [Fact]
    [Upstream("pivot", "接口给的十进制字符串：avg、min、max 和 sum 认同样的值")]
    public void Decimal_strings_count_as_numbers()
    {
        object[] texts = [new Dictionary<string, object?> { ["Region"] = "华东", ["Amount"] = "19.90" }, new Dictionary<string, object?> { ["Region"] = "华东", ["Amount"] = "0.10" }, new Dictionary<string, object?> { ["Region"] = "华东", ["Amount"] = "" }];
        decimal? Of(PivotAggregate aggregate) => Pivot.Of(texts, new PivotOptions([row => ((IDictionary<string, object?>)row)["Region"]]) { Value = row => ((IDictionary<string, object?>)row)["Amount"], Aggregate = aggregate }).Cell(["华东"], "");
        Assert.Equal(20m, Of(PivotAggregate.Sum));
        Assert.Equal(10m, Of(PivotAggregate.Avg));
        Assert.Equal(0.1m, Of(PivotAggregate.Min));
        Assert.Equal(19.9m, Of(PivotAggregate.Max));
    }

    static List<List<TableCell>> Section<T>(Table table) where T : TableSection =>
        [.. table.Sections.OfType<T>().Single().Rows.OfType<TableRow>().Select(row => row.Cells.OfType<TableCell>().ToList())];

    [Fact]
    [Upstream("PivotTable", "表头是行维度、各列与合计；相同的上级合并；合计行与合计列；空格子是 —")]
    public Task Headers_merges_totals_and_empty_cells() => HeadlessUi.Run(() =>
    {
        var table = new PivotTable
        {
            Data = Records,
            Rows = [new("Region", "区域"), new("Supplier", "供应商")],
            Columns = new("Quarter", "季度"),
            Value = new("Amount", "金额") { Format = new NumericFormat { MinimumFractionDigits = 1 } },
        };
        HeadlessUi.Show(new StackPanel { Width = 800, Children = { table } });

        Assert.Equal(["区域", "供应商", "Q1", "Q2", "合计"], Section<TableHead>(table).Single().Select(cell => cell.Content as string));
        var rows = Section<TableBody>(table);
        Assert.Equal(2, rows[0][0].RowSpan);
        Assert.Equal(["华东", "恒远", "—", "1.5", "1.5"], rows[0].Select(cell => cell.Content as string));
        Assert.Equal(["合计", "7.2", "1.6", "8.8"], Section<TableFoot>(table).Single().Where(cell => cell.Content is not null).Select(cell => cell.Content as string));
        var context = Assert.IsType<PivotRow>(((TableRow)rows[0][0].Parent!).DataContext);
        Assert.Equal(["华东", "恒远"], context.Dimensions);
    });

    [Fact]
    [Upstream("PivotTable", "heatmap：有值的格子按比例着色，空格子与合计不着色")]
    public Task Heatmap_tints_cells_with_values() => HeadlessUi.Run(() =>
    {
        var table = new PivotTable
        {
            Data = Records,
            Rows = [new("Region", "区域")],
            Columns = new("Quarter", "季度"),
            Value = new("Amount", "金额"),
            Heatmap = true,
        };
        HeadlessUi.Show(new StackPanel { Width = 800, Children = { table } });

        // 华东：Q1 0.2、Q2 1.6、合计；华南：Q1 7（最深）、Q2 空
        var (east, south) = (Section<TableBody>(table)[0], Section<TableBody>(table)[1]);
        Assert.IsAssignableFrom<ISolidColorBrush>(east[1].Background);
        Assert.IsAssignableFrom<ISolidColorBrush>(east[2].Background);
        Assert.Null(east[3].Background);
        Assert.Null(south[2].Background);
        Assert.Equal(0.45, ((ISolidColorBrush)south[1].Background!).Opacity, 3);
        Assert.True(((ISolidColorBrush)east[1].Background!).Opacity < ((ISolidColorBrush)east[2].Background!).Opacity);
    });
}
