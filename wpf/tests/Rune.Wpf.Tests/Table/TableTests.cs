using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>
/// Table：表头、表体、表尾、标题的结构，标题是读屏名称；表头的格 textMuted、sm、中等字重，表体与表尾里的行标题中等字重，表尾加粗；
/// 斑马纹隔行；列宽按内容比例分满表格宽，对齐按格。
/// </summary>
public class TableTests
{
    static (Table Table, StackPanel Host) Build(bool striped = false)
    {
        var table = new Table
        {
            Striped = striped, Width = 400,
            Sections =
            {
                new TableCaption { Content = "属性" },
                new TableHead { Rows = { new TableRow { Cells = { new TableHeaderCell { Content = "名称" }, new TableHeaderCell { Content = "默认值", Align = TableAlign.End } } } } },
                new TableBody
                {
                    Rows =
                    {
                        new TableRow { Cells = { new TableHeaderCell { Scope = "row", Content = "size" }, new TableCell { Content = "md", Align = TableAlign.End } } },
                        new TableRow { Cells = { new TableCell { Content = "tone" }, new TableCell { Content = "accent", Align = TableAlign.End } } },
                    },
                },
                new TableFoot { Rows = { new TableRow { Cells = { new TableHeaderCell { Scope = "row", Content = "合计" }, new TableCell { Content = "2" } } } } },
            },
        };
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(table);
        return (table, host);
    }

    static TableCell Cell(Table table, int row, int column) => (TableCell)table.Rows[row].Row.Cells[column];

    [Fact]
    [Upstream("Table", "渲染原生 table 结构")]
    public Task Sections_rows_and_the_caption_make_the_structure() => WpfUi.Run(() =>
    {
        var (table, host) = Build();
        WpfUi.Show(host);

        Assert.Equal(["head", "body", "body", "foot"], table.Rows.Select(row => row.Section.GetType().Name[5..].ToLowerInvariant()));
        Assert.Equal(AutomationControlType.Table, table.Peer().GetAutomationControlType());
        Assert.Equal("属性", table.Peer().GetName());
        Assert.Equal(AutomationControlType.HeaderItem, Cell(table, 0, 0).Peer().GetAutomationControlType());
        Assert.Equal(AutomationControlType.DataItem, Cell(table, 1, 1).Peer().GetAutomationControlType());
    });

    [Fact]
    [Upstream("Table", "表体、表尾里的行标题用单元格的样式")]
    public Task Header_cells_look_like_headers_only_in_the_head() => WpfUi.Run(() =>
    {
        var (table, host) = Build();
        WpfUi.Show(host);

        var head = Cell(table, 0, 0);
        Assert.Equal("head", head.SectionKind);
        Assert.Equal((double)table.FindResource("Rune.FontSize.Sm"), head.FontSize);
        Assert.Equal(table.ThemeColor("Rune.Brush.TextMuted"), Lookup.ColorOf(head.Foreground));
        var rowHeader = Cell(table, 1, 0);
        Assert.Equal("body", rowHeader.SectionKind);
        Assert.Equal(FontWeights.Medium, rowHeader.FontWeight);
        Assert.Equal(FontWeights.SemiBold, Cell(table, 3, 1).FontWeight); // 表尾加粗
        Assert.Equal(FontWeights.SemiBold, Cell(table, 3, 0).FontWeight); // 表尾里的行标题也照表尾
    });

    [Fact]
    public Task Columns_share_the_table_width_and_cells_align() => WpfUi.Run(() =>
    {
        var (table, host) = Build(striped: true);
        WpfUi.Show(host);

        // 两列按内容比例分满 400 减去外框；每格左右各 space.md（按格自己的字号）
        var name = Cell(table, 1, 0);
        var value = Cell(table, 1, 1);
        Assert.Equal(398, name.ActualWidth + value.ActualWidth + 1.5 * name.FontSize + 1.5 * value.FontSize, 1.0);
        Assert.Equal(HorizontalAlignment.Right, value.HorizontalContentAlignment);
        Assert.Equal([false, true], table.Rows.Where(row => row.Section is TableBody).Select(row => row.Row.IsStriped));
    });
}
