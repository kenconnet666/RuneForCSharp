using System.IO;
using System.Text;

namespace Rune.Avalonia;

/// <summary>读进来的一张表（vue-tsx-pro 的 SheetTable）：CSV、Excel 的一张工作表。</summary>
/// <param name="Name">工作表名（CSV 是文件名）。</param>
/// <param name="Headers">各列的标题，按列的顺序。</param>
/// <param name="Rows">数据行：列标题 → 值（字符串、数字、布尔、日期或 null）。</param>
public sealed record SheetTable(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);

/// <summary><see cref="Sheet.FromMatrix"/> 的选项（vue-tsx-pro 的 SheetMatrixOptions）。</summary>
public sealed record SheetMatrixOptions
{
    /// <summary>表头占几行，默认 1。</summary>
    public int HeaderRows { get; init; } = 1;

    /// <summary>这些列（标题）的单元格里有换行时拆成几行；null 是不拆。</summary>
    public IReadOnlyCollection<string>? SplitLines { get; init; }

    /// <summary>所有列的单元格里有换行时都拆成几行。</summary>
    public bool SplitAllLines { get; init; }

    /// <summary>没有标题的列叫什么（从 0 起的列号），默认"第 N 列"；跟随语言时传 Lang.SheetColumn。</summary>
    public Func<int, string>? Untitled { get; init; }
}

/// <summary>
/// 读进来的表格统一成 <see cref="SheetTable"/>（vue-tsx-pro 的 table/sheet）：表头是各列的标题文字，每行是"标题 → 值"。DataImport 用它；CSV 由 <see cref="ReadCsv"/> 读，不依赖别的库。
/// 从行列矩阵转成表（<see cref="FromMatrix"/>）时：多行表头（合并单元格已展开）同一列各行的标题用" / "连接，上下相同的只留一个；
/// 全空的行去掉；标题重复时后面的加序号（"备注""备注 2"）；SplitLines 的列里有换行时拆成几行，别的列照抄（一格里写了几个零件号的报价单）。
/// </summary>
public static class Sheet
{
    /// <summary>一格是不是空的：null、空字符串、只有空白。</summary>
    public static bool IsBlank(object? value) => value is null || value is string text && string.IsNullOrWhiteSpace(text);

    /// <summary>行列矩阵（合并单元格已展开）→ 表。</summary>
    public static SheetTable FromMatrix(string name, IReadOnlyList<IReadOnlyList<object?>> matrix, SheetMatrixOptions? options = null)
    {
        options ??= new SheetMatrixOptions();
        var untitled = options.Untitled ?? (column => $"第 {column + 1} 列");
        var width = matrix.Count == 0 ? 0 : matrix.Max(row => row.Count);
        var head = matrix.Take(options.HeaderRows).ToList();
        var seen = new Dictionary<string, int>();
        var headers = new List<string>(width);
        for (var column = 0; column < width; column++)
        {
            var parts = new List<string>();
            foreach (var row in head)
            {
                var cell = column < row.Count ? row[column] : null;
                var text = IsBlank(cell) ? "" : Convert.ToString(cell, System.Globalization.CultureInfo.InvariantCulture)!.Trim();
                // 合并单元格展开后上下两行相同（没分组的列跨两行）：只留一个
                if (text.Length > 0 && (parts.Count == 0 || parts[^1] != text)) parts.Add(text);
            }
            var title = parts.Count > 0 ? string.Join(" / ", parts) : untitled(column);
            var count = seen.GetValueOrDefault(title) + 1;
            seen[title] = count;
            headers.Add(count == 1 ? title : $"{title} {count}");
        }
        bool Split(string header) => options.SplitAllLines || options.SplitLines?.Contains(header) == true;
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var cells in matrix.Skip(options.HeaderRows))
        {
            if (cells.All(IsBlank)) continue;
            // 拆行：要拆的列里最多有几行，就拆成几行
            var lines = headers.Select((header, column) =>
            {
                var value = column < cells.Count ? cells[column] : null;
                return Split(header) && value is string text && text.Contains('\n')
                    ? text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).Cast<object?>().ToList()
                    : [value];
            }).ToList();
            var count = Math.Max(1, lines.Max(values => values.Count));
            for (var line = 0; line < count; line++)
            {
                var row = new Dictionary<string, object?>();
                for (var column = 0; column < headers.Count; column++)
                {
                    var values = lines[column];
                    row[headers[column]] = values.Count > 1 ? line < values.Count ? values[line] : null : values.Count > 0 ? values[0] : null;
                }
                rows.Add(row);
            }
        }
        return new SheetTable(name, headers, rows);
    }

    /// <summary>CSV 文字 → 行列矩阵（RFC 4180：引号里的逗号、换行、两个引号；开头的 BOM 去掉）。</summary>
    public static List<List<string>> ParseCsv(string text)
    {
        var source = text.StartsWith('﻿') ? text[1..] : text;
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < source.Length; index++)
        {
            var c = source[index];
            if (quoted)
            {
                if (c == '"' && index + 1 < source.Length && source[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && index + 1 < source.Length && source[index + 1] == '\n') index++;
                row.Add(field.ToString());
                rows.Add(row);
                row = [];
                field.Clear();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>读 CSV：一张表，名字是文件名（去掉扩展名）。按 UTF-8 读（有 BOM 时按 BOM）。</summary>
    /// <param name="stream">文件内容。</param>
    /// <param name="fileName">文件名。</param>
    /// <param name="options">转成表的选项。</param>
    public static async Task<IReadOnlyList<SheetTable>> ReadCsv(Stream stream, string fileName, SheetMatrixOptions? options = null)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync();
        var matrix = ParseCsv(text).Select(row => (IReadOnlyList<object?>)row.Cast<object?>().ToList()).ToList();
        return [FromMatrix(Path.GetFileNameWithoutExtension(fileName) is { Length: > 0 } name ? name : "CSV", matrix, options)];
    }
}
