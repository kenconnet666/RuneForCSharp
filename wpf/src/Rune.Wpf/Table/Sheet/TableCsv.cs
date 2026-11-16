using System.Globalization;
using System.Text.RegularExpressions;

namespace Rune.Wpf;

/// <summary><see cref="TableCsv.ToCsv"/> 的选项（vue-tsx-pro 的 CsvOptions）。</summary>
public sealed record CsvOptions
{
    /// <summary>开头加 BOM（U+FEFF），默认 true：Excel 按 UTF-8 读，中文不乱码。</summary>
    public bool Bom { get; init; } = true;

    /// <summary>导出原值，不按列的格式格式化（数值不带千分位、日期是 ISO），默认 false。</summary>
    public bool Raw { get; init; }

    /// <summary>分组表头（同 DataTable 的 ColumnGroups）：组里的列的表头写成"组标题 / 列标题"。</summary>
    public IReadOnlyList<DataTableColumnGroup> Groups { get; init; } = [];

    /// <summary>语言（Lang.Code）：数字、日期按它格式化，默认 zh-CN。</summary>
    public string Culture { get; init; } = "zh-CN";

    /// <summary>币种（Lang.Currency），默认 CNY。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>时区：日期时间按它换算；null 时用本机时区。</summary>
    public TimeZoneInfo? Zone { get; init; }
}

/// <summary>
/// 按列定义把行导出成 CSV（vue-tsx-pro 的 tableToCsv，RFC 4180）：表头是列的标题文字，单元格是表格里看到的文字
/// （状态显示名字，数字、金额、日期按列的类型与格式）；开头带 BOM，CRLF 分行。只要导出部分列时先筛 columns（去掉操作列）；Raw 时数值、日期导出原值，便于表格软件再计算。
/// 文字以 = + - @ 开头的内容前面加 '，打开时不会被当成公式执行（CSV 注入；同 PapaParse 的 escapeFormulae）；真正的数值不受影响，数字、日期列里混进来的字符串照样要防。
/// </summary>
public static partial class TableCsv
{
    /// <summary>行 → CSV 文字。</summary>
    public static string ToCsv(IEnumerable<object> rows, IReadOnlyList<DataTableColumn> columns, CsvOptions? options = null)
    {
        options ??= new CsvOptions();
        var list = rows.ToList();
        var zone = options.Zone ?? TimeZoneInfo.Local;
        var types = columns.Select(column => TableColumns.TypeOf(column, list)).ToList();
        var header = columns.Select(column =>
        {
            var title = TableColumns.Label(column);
            var group = options.Groups.FirstOrDefault(item => item.Columns.Contains(column.Key));
            return GuardFormula(group?.Header is string groupTitle ? $"{groupTitle} / {title}" : title);
        });
        var lines = list.Select(row => columns.Select((column, index) =>
        {
            var value = TableColumns.ValueOf(column, row);
            if (value is null) return "";
            // 真正的数值不会是公式；其余的（状态名、文字、数字列里混进来的字符串）都过一遍 GuardFormula
            if (value is not string && TableColumns.Numeric(value) is { } number)
                return options.Raw ? number.ToString(CultureInfo.InvariantCulture) : TableColumns.FormatCell(value, types[index], column.Format, options.Culture, options.Currency, zone);
            if (column.Statuses is { } statuses && value is string key) return GuardFormula(statuses.Label(key));
            if (options.Raw || types[index] == ColumnType.Text) return GuardFormula(RawText(value));
            return GuardFormula(TableColumns.FormatCell(value, types[index], column.Format, options.Culture, options.Currency, zone));
        }));
        var text = string.Join("\r\n", new[] { header }.Concat(lines).Select(line => string.Join(',', line.Select(Field))));
        return options.Bom ? $"﻿{text}" : text;
    }

    /// <summary>原值的文字：日期是 ISO。</summary>
    static string RawText(object value) => value switch
    {
        DateTime time => time.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset time => time.ToString("O", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    /// <summary>文字以 = + - @ 开头（且不是数字）时前面加 '：表格软件不会当成公式执行。</summary>
    static string GuardFormula(string text) => FormulaStart().IsMatch(text) && TableColumns.Numeric(text) is null ? $"'{text}" : text;

    /// <summary>一格：含逗号、引号、换行时加引号（引号写两遍）。</summary>
    static string Field(string text) => text.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;

    [GeneratedRegex("^[=+\\-@\\t\\r]")]
    private static partial Regex FormulaStart();
}
