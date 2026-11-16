using System.Text;

namespace Rune.Avalonia.Tests;

/// <summary>Sheet 与 TableCsv：CSV 的解析（引号、换行、BOM）、矩阵转表（标题去重、没标题的列、全空的行、拆行）；导出 CSV（表头、格式、转义、防公式注入、BOM、分组表头、原值）。</summary>
public class SheetTests
{
    [Fact]
    [Upstream("workbook", "parseCsv：引号里的逗号与换行、两个引号、CRLF、开头的 BOM")]
    public void Parse_csv_handles_quotes_newlines_and_bom()
    {
        var rows = Sheet.ParseCsv("﻿名称,备注\r\n\"支架, 左\",\"第一行\n第二行\"\r\n\"说\"\"明\",x");
        Assert.Equal([["名称", "备注"], ["支架, 左", "第一行\n第二行"], ["说\"明", "x"]], rows);
    }

    [Fact]
    [Upstream("workbook", "sheetFromMatrix：重复的标题加序号、没标题的列有名字、全空的行去掉")]
    public void From_matrix_dedupes_titles_and_drops_blank_rows()
    {
        var table = Sheet.FromMatrix("导入", [["备注", "备注", ""], ["a", "b", "c"], ["", null, " "]]);
        Assert.Equal(["备注", "备注 2", "第 3 列"], table.Headers);
        var row = Assert.Single(table.Rows);
        Assert.Equal(("a", "b", "c"), (row["备注"], row["备注 2"], row["第 3 列"]));
    }

    [Fact]
    public async Task Multi_row_headers_split_lines_and_read_csv()
    {
        var table = Sheet.FromMatrix("报价", [["收益（万元）", "收益（万元）", "零件"], ["目标", "实际", "零件"], [1, 2, "P-1\nP-2"]],
            new SheetMatrixOptions { HeaderRows = 2, SplitLines = ["零件"] });
        Assert.Equal(["收益（万元） / 目标", "收益（万元） / 实际", "零件"], table.Headers);
        Assert.Equal(["P-1", "P-2"], table.Rows.Select(row => row["零件"]));
        Assert.All(table.Rows, row => Assert.Equal(1, row["收益（万元） / 目标"]));
        var sheets = await Sheet.ReadCsv(new MemoryStream(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("料号,数量\r\nP-001,40\r\n")).ToArray()), "零件.csv");
        Assert.Equal(("零件", "P-001"), (sheets[0].Name, sheets[0].Rows[0]["料号"]));
    }

    record CsvTask(string Code, string Name, object Amount, object Due, string Status);

    static readonly StatusMap Statuses = new() { ["doing"] = new("执行中", StatusTone.Info) };

    static readonly DataTableColumn[] Columns =
    [
        new("Code", "编码"),
        new("Name", "名称"),
        new("Amount", "金额") { Format = new NumericFormat { MinimumFractionDigits = 2 } },
        new("Due", "计划完成") { Type = ColumnType.Date },
        new("Status", "状态") { Statuses = Statuses },
    ];

    static readonly CsvTask[] Rows = [new("T-1", "支架, \"A\" 型", -1234.5m, "2026-10-21", "doing"), new("T-2", "=HYPERLINK(\"x\")", 12m, "2026-10-22", "doing")];

    static string[] Lines(string csv) => csv.Split("\r\n");

    [Fact]
    [Upstream("csv", "开头带 BOM，CRLF 分行；表头是列标题，单元格同表格里的文字")]
    public void Csv_has_bom_headers_and_table_text()
    {
        var csv = TableCsv.ToCsv(Rows, Columns);
        Assert.StartsWith("﻿", csv);
        var lines = Lines(csv[1..]);
        Assert.Equal("编码,名称,金额,计划完成,状态", lines[0]);
        Assert.Equal("T-1,\"支架, \"\"A\"\" 型\",\"-1,234.50\",2026年10月21日,执行中", lines[1]);
    }

    [Fact]
    [Upstream("csv", "文字列里以 = 开头的内容前面加 '（防公式注入）；负数金额不受影响")]
    public void Csv_guards_formulas_but_not_negative_amounts()
    {
        var lines = Lines(TableCsv.ToCsv(Rows, Columns, new CsvOptions { Bom = false }));
        Assert.Equal("\"'=HYPERLINK(\"\"x\"\")\"", lines[2].Split(',')[1]);
        Assert.Contains("\"-1,234.50\"", lines[1]);
    }

    [Fact]
    [Upstream("csv", "没登记的状态值、数字与日期列里混进来的字符串同样防公式")]
    public void Csv_guards_odd_strings_in_typed_columns()
    {
        CsvTask[] odd = [Rows[0] with { Amount = "=1+1", Due = "@SUM(A1)", Status = "=HYPERLINK(\"x\")" }];
        var cells = Lines(TableCsv.ToCsv(odd, Columns, new CsvOptions { Bom = false }))[1].Split(',');
        Assert.Equal(["'=1+1", "'@SUM(A1)", "\"'=HYPERLINK(\"\"x\"\")\""], cells[^3..]);
    }

    [Fact]
    [Upstream("csv", "groups：组里的列表头写成“组标题 / 列标题”")]
    public void Csv_writes_group_titles()
    {
        var csv = TableCsv.ToCsv(Rows, Columns, new CsvOptions { Bom = false, Groups = [new("plan", "计划", ["Amount", "Due"])] });
        Assert.Equal("编码,名称,计划 / 金额,计划 / 计划完成,状态", Lines(csv)[0]);
    }

    [Fact]
    [Upstream("csv", "raw：数值导出原值（不带千分位），日期字符串原样")]
    public void Csv_raw_exports_plain_values()
    {
        var lines = Lines(TableCsv.ToCsv(Rows, Columns, new CsvOptions { Bom = false, Raw = true }));
        Assert.Equal("T-1,\"支架, \"\"A\"\" 型\",-1234.5,2026-10-21,执行中", lines[1]);
    }
}
