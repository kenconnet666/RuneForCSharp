using System.Globalization;

namespace Rune.Avalonia;

/// <summary>聚合的方式。</summary>
public enum PivotAggregate
{
    /// <summary>求和（默认）。</summary>
    Sum,

    /// <summary>明细的条数。</summary>
    Count,

    /// <summary>平均。</summary>
    Avg,

    /// <summary>最小。</summary>
    Min,

    /// <summary>最大。</summary>
    Max,
}

/// <summary>透视的选项（vue-tsx-pro 的 PivotOptions）。</summary>
/// <param name="Rows">行维度：每个取出一级的分组值（地区、供应商）。</param>
public sealed record PivotOptions(IReadOnlyList<Func<object, object?>> Rows)
{
    /// <summary>列维度；不写时只有一列（只按行分组）。</summary>
    public Func<object, object?>? Columns { get; init; }

    /// <summary>聚合的数值；Count 时可以不写。</summary>
    public Func<object, object?>? Value { get; init; }

    /// <summary>聚合的方式，默认 Sum。</summary>
    public PivotAggregate Aggregate { get; init; }

    /// <summary>各级行维度的顺序（按位置对应 Rows）：按它排，没列出的排在后面。</summary>
    public IReadOnlyList<IReadOnlyList<string>?>? RowOrder { get; init; }

    /// <summary>列的顺序。</summary>
    public IReadOnlyList<string>? ColumnOrder { get; init; }

    /// <summary>排序用的比较；默认按 <see cref="Pivot.Comparer"/>（当前语言、数字按大小）。</summary>
    public Comparison<string>? Compare { get; init; }
}

/// <summary>透视的结果。</summary>
public sealed class PivotResult
{
    internal PivotResult(IReadOnlyList<IReadOnlyList<string>> rowKeys, IReadOnlyList<string> columnKeys, Func<IReadOnlyList<string>, string, decimal?> cell,
        Func<IReadOnlyList<string>, decimal?> rowTotal, Func<string, decimal?> columnTotal, decimal? grandTotal, (decimal Low, decimal High)? extent)
    {
        RowKeys = rowKeys;
        ColumnKeys = columnKeys;
        cellOf = cell;
        rowTotalOf = rowTotal;
        columnTotalOf = columnTotal;
        GrandTotal = grandTotal;
        Extent = extent;
    }

    readonly Func<IReadOnlyList<string>, string, decimal?> cellOf;
    readonly Func<IReadOnlyList<string>, decimal?> rowTotalOf;
    readonly Func<string, decimal?> columnTotalOf;

    /// <summary>各行的维度值（每级一个），按顺序。</summary>
    public IReadOnlyList<IReadOnlyList<string>> RowKeys { get; }

    /// <summary>各列的值，按顺序；没有列维度时是一个空字符串。</summary>
    public IReadOnlyList<string> ColumnKeys { get; }

    /// <summary>总计。</summary>
    public decimal? GrandTotal { get; }

    /// <summary>各格（不含合计）里最小、最大的值：热力图按它定颜色深浅；没有值时 null。</summary>
    public (decimal Low, decimal High)? Extent { get; }

    /// <summary>一格的值：没有明细（或明细里都没有数值）时 null。</summary>
    public decimal? Cell(IReadOnlyList<string> row, string column) => cellOf(row, column);

    /// <summary>一行的合计（这一行全部明细的聚合）。</summary>
    public decimal? RowTotal(IReadOnlyList<string> row) => rowTotalOf(row);

    /// <summary>一列的合计。</summary>
    public decimal? ColumnTotal(string column) => columnTotalOf(column);
}

/// <summary>
/// 透视（vue-tsx-pro 的 pivot）：把明细按行维度（可以多级）与列维度分组，每格聚合一个数值（求和、计数、平均、最小、最大），带行合计、列合计与总计。
/// PivotTable 用它；要自己画（热力图、导出）时直接用。
/// <code>
/// var result = Pivot.Of(records, new PivotOptions([r => ((Sale)r).Region, r => ((Sale)r).Supplier]) { Columns = r => ((Sale)r).Quarter, Value = r => ((Sale)r).Amount });
/// result.Cell(["华东", "华辰"], "Q1")   // 这一格的值；没有明细时 null
/// </code>
/// 数值是 decimal（十进制精确，接口给的十进制字符串也算数，同 <see cref="TableColumns.Numeric"/>）。三个库各有一份（tools/twins）。
/// </summary>
public static class Pivot
{
    const char Separator = '\0';

    /// <summary>按语言比较维度值（同 localeCompare 的 numeric：中文按拼音，"第2章"在"第10章"前）。</summary>
    /// <param name="culture">语言（Lang.Code）。</param>
    public static Comparison<string> Comparer(string culture)
    {
        var compare = CultureInfo.GetCultureInfo(culture).CompareInfo;
        return (a, b) => compare.Compare(a, b, CompareOptions.NumericOrdering);
    }

    /// <summary>按行维度、列维度分组聚合。</summary>
    public static PivotResult Of(IEnumerable<object> data, PivotOptions options)
    {
        var compare = options.Compare ?? Comparer(CultureInfo.CurrentCulture.Name);
        var all = data.ToList();
        var groups = new Dictionary<string, List<object>>();
        var byRow = new Dictionary<string, List<object>>();
        var byColumn = new Dictionary<string, List<object>>();
        foreach (var row in all)
        {
            var rowKey = string.Join(Separator, options.Rows.Select(dimension => Text(dimension(row))));
            var columnKey = options.Columns is { } columns ? Text(columns(row)) : "";
            Add(groups, $"{rowKey}{Separator}{Separator}{columnKey}", row);
            Add(byRow, rowKey, row);
            Add(byColumn, columnKey, row);
        }

        // 行：逐级排序（同一上级下的下级按 order 或 compare）
        var rowKeys = byRow.Keys.Select(key => (IReadOnlyList<string>)key.Split(Separator)).ToList();
        rowKeys.Sort((a, b) =>
        {
            for (var level = 0; level < options.Rows.Count; level++)
            {
                if (a[level] == b[level]) continue;
                return Ordered([a[level], b[level]], options.RowOrder?.ElementAtOrDefault(level), compare)[0] == a[level] ? -1 : 1;
            }
            return 0;
        });
        var columnKeys = Ordered(byColumn.Keys, options.ColumnOrder, compare);

        var cache = new Dictionary<string, decimal?>();
        decimal? Cell(IReadOnlyList<string> row, string column)
        {
            var key = $"{string.Join(Separator, row)}{Separator}{Separator}{column}";
            if (!cache.TryGetValue(key, out var result)) cache[key] = result = Aggregate(groups.GetValueOrDefault(key) ?? [], options.Value, options.Aggregate);
            return result;
        }
        var values = rowKeys.SelectMany(row => columnKeys.Select(column => Cell(row, column))).OfType<decimal>().ToList();
        return new PivotResult(
            rowKeys,
            columnKeys,
            Cell,
            row => Aggregate(byRow.GetValueOrDefault(string.Join(Separator, row)) ?? [], options.Value, options.Aggregate),
            column => Aggregate(byColumn.GetValueOrDefault(column) ?? [], options.Value, options.Aggregate),
            Aggregate(all, options.Value, options.Aggregate),
            values.Count > 0 ? (values.Min(), values.Max()) : null);

        static void Add(Dictionary<string, List<object>> map, string key, object row)
        {
            if (map.TryGetValue(key, out var list)) list.Add(row);
            else map[key] = [row];
        }
    }

    /// <summary>一组明细的聚合：求和、平均只算数值（跳过空值与文字）；没有任何数值时 null。</summary>
    static decimal? Aggregate(IReadOnlyList<object> rows, Func<object, object?>? value, PivotAggregate how)
    {
        if (how == PivotAggregate.Count) return rows.Count;
        if (value is null) return null;
        var numbers = rows.Select(row => TableColumns.Numeric(value(row))).OfType<decimal>().ToList();
        if (numbers.Count == 0) return null;
        return how switch
        {
            PivotAggregate.Avg => numbers.Sum() / numbers.Count,
            PivotAggregate.Min => numbers.Min(),
            PivotAggregate.Max => numbers.Max(),
            _ => numbers.Sum(),
        };
    }

    /// <summary>按 order 排，没列出的按 compare 排在后面。</summary>
    static List<string> Ordered(IEnumerable<string> values, IReadOnlyList<string>? order, Comparison<string> compare)
    {
        var rank = new Dictionary<string, int>();
        foreach (var value in order ?? []) rank.TryAdd(value, rank.Count);
        var list = values.Distinct().ToList();
        list.Sort((a, b) =>
        {
            var (ra, rb) = (rank.TryGetValue(a, out var x) ? x : int.MaxValue, rank.TryGetValue(b, out var y) ? y : int.MaxValue);
            return ra != int.MaxValue || rb != int.MaxValue ? ra.CompareTo(rb) : compare(a, b);
        });
        return list;
    }

    /// <summary>文字形式的维度值：空值是空字符串。</summary>
    static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
