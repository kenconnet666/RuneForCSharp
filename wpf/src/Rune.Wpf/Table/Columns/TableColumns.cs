using System.Collections;
using System.Globalization;

namespace Rune.Wpf;

/// <summary>列的值类型：不写 Cell 时按它格式化（跟随语言），数字类默认靠结束边对齐。</summary>
public enum ColumnType
{
    /// <summary>文字：原样显示。</summary>
    Text,

    /// <summary>数字：按语言分组，1234.5 → 1,234.5。</summary>
    Number,

    /// <summary>金额，语言的币种：19.9 → ¥19.90。</summary>
    Currency,

    /// <summary>百分比：0.125 → 12.5%。</summary>
    Percent,

    /// <summary>文件大小（字节）：1234567 → 1.2 MB。</summary>
    FileSize,

    /// <summary>日期：DateOnly、DateTime、DateTimeOffset 或 ISO 字符串。</summary>
    Date,

    /// <summary>日期与时间。</summary>
    DateTime,
}

/// <summary>一格合并几行、几列；0 是被别的格合并掉（不显示）。</summary>
/// <param name="RowSpan">往下合并几行。</param>
/// <param name="ColumnSpan">往后合并几列。</param>
public readonly record struct CellSpan(int RowSpan = 1, int ColumnSpan = 1);

/// <summary>合计行里一列的内容（TS 的 summary）：<see cref="Sum"/> 把这一列的数值精确相加，<see cref="Of"/> 拿到全部行自己算（平均值、"3 项待定价"）。</summary>
public sealed class ColumnSummary
{
    ColumnSummary(Func<IReadOnlyList<object>, object?>? compute) => Compute = compute;

    /// <summary>把这一列的数值相加（decimal，十进制精确），按列的类型与 Format 显示。</summary>
    public static ColumnSummary Sum { get; } = new(null);

    /// <summary>自己算：拿到全部行，返回显示的内容（文字或控件）。</summary>
    /// <param name="compute">计算。</param>
    public static ColumnSummary Of(Func<IReadOnlyList<object>, object?> compute) => new(compute);

    /// <summary>自己算的函数；相加时为 null。</summary>
    public Func<IReadOnlyList<object>, object?>? Compute { get; }
}

/// <summary>
/// 表格的一列（vue-tsx-pro 的 DataTableColumn，参考 TanStack 的列模型，只取最常用的部分）：ColumnTable 用它，以后的 DataTable 也用它。
/// <code>
/// new DataTableColumn("Amount", "金额") { Type = ColumnType.Currency, Summary = ColumnSummary.Sum }
/// </code>
/// 取值：<see cref="Value"/> 函数，不写时按 Key 取行的同名属性（字典按键）。
/// </summary>
/// <param name="Key">列的标识（同一表格里不能重复）；不写 Value 时也是取值的属性名。</param>
/// <param name="Header">表头内容：文字或控件。</param>
public sealed record DataTableColumn(string Key, object Header)
{
    /// <summary>取值（排序、格式化、合并用）；不写时取行的同名属性。</summary>
    public Func<object, object?>? Value { get; init; }

    /// <summary>单元格内容：参数是行与下标，返回文字或控件；不写或返回 null 时按 <see cref="Type"/> 显示值（只有几行要换内容时不用自己格式化其余的行）。</summary>
    public Func<object, int, object?>? Cell { get; init; }

    /// <summary>值的类型：决定默认的格式与对齐；不写时按第一个非空值推断（数字 → Number，日期 → Date，其余 Text）。</summary>
    public ColumnType? Type { get; init; }

    /// <summary>数字类（Number、Currency、Percent）的格式，与类型的默认格式合并：金额以万元显示写 <c>new() { Scale = 10_000, MaximumFractionDigits = 2 }</c>。</summary>
    public NumericFormat? Format { get; init; }

    /// <summary>宽度（像素）；不写时按内容。</summary>
    public double? Width { get; init; }

    /// <summary>最小宽度（像素）。</summary>
    public double? MinWidth { get; init; }

    /// <summary>对齐；默认数字类靠结束边，其余起始边。</summary>
    public TableAlign? Align { get; init; }

    /// <summary>内容可以换行；写了宽度的列默认一行显示不下时省略。</summary>
    public bool Wrap { get; init; }

    /// <summary>没有值（null、空字符串）时显示的内容，如"待定价"；默认语言包的 EmptyValue（—）。</summary>
    public object? Empty { get; init; }

    /// <summary>状态映射：值是状态的键，显示成 StatusBadge；写了 Cell 时以 Cell 为准。</summary>
    public StatusMap? Statuses { get; init; }

    /// <summary>合计行里这一列的内容；有一列写了，表格底部就多一行合计。</summary>
    public ColumnSummary? Summary { get; init; }
}

/// <summary>列定义的计算（vue-tsx-pro 的 columns.ts）：取值、类型、对齐、格式化、合计、相同值合并的跨行数。三个库各有一份（tools/twins）。</summary>
public static class TableColumns
{
    static readonly string[] FileSizeUnits = ["byte", "kilobyte", "megabyte", "gigabyte", "terabyte"];

    /// <summary>列在这一行的值：Value 函数、字典的键或同名属性。</summary>
    public static object? ValueOf(DataTableColumn column, object? row) => row switch
    {
        null => null,
        _ when column.Value is { } value => value(row),
        IDictionary<string, object?> map => map.TryGetValue(column.Key, out var found) ? found : null,
        IDictionary map => map.Contains(column.Key) ? map[column.Key] : null,
        _ => row.GetType().GetProperty(column.Key)?.GetValue(row),
    };

    /// <summary>列的名称（列设置、读屏用）：表头是文字时就是它，否则是 Key。</summary>
    public static string Label(DataTableColumn column) => column.Header as string ?? column.Key;

    /// <summary>
    /// 列的顺序调整（列设置）：order 是列的 Key 的顺序，没列到的按定义的顺序排在后面；记下的顺序里已经删掉的列不算。
    /// 上游还把固定的列排到两头，列定义有了固定（DataTable）时再加。
    /// </summary>
    public static IReadOnlyList<DataTableColumn> Arrange(IReadOnlyList<DataTableColumn> columns, IReadOnlyList<string>? order)
    {
        var rank = new Dictionary<string, int>();
        foreach (var key in order ?? []) rank.TryAdd(key, rank.Count);
        return [.. columns.Select((column, index) => (column, position: rank.TryGetValue(column.Key, out var at) ? at : rank.Count + index)).OrderBy(item => item.position).Select(item => item.column)];
    }

    /// <summary>空值：null 或空字符串。</summary>
    public static bool IsEmpty(object? value) => value is null or string { Length: 0 };

    /// <summary>
    /// 列的值类型：写了 Type 用它；写了数字格式、或合计是相加的是数字列（全是空值时也靠结束边对齐）；
    /// 否则看第一个非空值（字符串不猜，算文字）。
    /// </summary>
    public static ColumnType TypeOf(DataTableColumn column, IEnumerable<object> rows)
    {
        if (column.Type is { } type) return type;
        if (column.Format is not null || column.Summary == ColumnSummary.Sum) return ColumnType.Number;
        foreach (var row in rows)
        {
            var value = ValueOf(column, row);
            if (value is null) continue;
            if (IsNumber(value)) return ColumnType.Number;
            return value is DateTime or DateTimeOffset or DateOnly ? ColumnType.Date : ColumnType.Text;
        }
        return ColumnType.Text;
    }

    /// <summary>列的默认对齐：数字类靠结束边方便对比位数，其余起始边。</summary>
    public static TableAlign AlignOf(ColumnType type) => type is ColumnType.Number or ColumnType.Currency or ColumnType.Percent or ColumnType.FileSize ? TableAlign.End : TableAlign.Start;

    /// <summary>数值（不含 bool）与接口常给的十进制字符串（"19.90"）换成 decimal，不经过浮点；不是数值时 null。</summary>
    public static decimal? Numeric(object? value) => value switch
    {
        decimal d => d,
        int or long or short or byte or sbyte or ushort or uint or ulong => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        double d when double.IsFinite(d) => (decimal)d,
        float f when float.IsFinite(f) => (decimal)f,
        string text when decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };

    static bool IsNumber(object value) => value is not string && Numeric(value) is not null;

    /// <summary>
    /// 没写 Cell 时单元格的文字：按值类型与语言格式化；值与类型对不上（数字列里的文字）时原样显示。空值是空字符串（显示 Empty 由调用方决定）。
    /// </summary>
    /// <param name="value">值。</param>
    /// <param name="type">列的类型。</param>
    /// <param name="format">列的数字格式。</param>
    /// <param name="culture">语言（Lang.Code）。</param>
    /// <param name="currency">语言的币种（Lang.Currency）。</param>
    /// <param name="zone">语言的时区（日期按它换算）。</param>
    public static string FormatCell(object? value, ColumnType type, NumericFormat? format, string culture, string currency, TimeZoneInfo zone)
    {
        if (value is null) return "";
        if (Numeric(value) is { } number)
        {
            switch (type)
            {
                case ColumnType.Number:
                    return (format ?? NumericFormat.Default).Format(number, culture, currency);
                case ColumnType.Currency:
                    return WithStyle(format, NumericStyle.Currency).Format(number, culture, currency);
                case ColumnType.Percent:
                    var percent = WithStyle(format, NumericStyle.Percent);
                    return (percent with { MaximumFractionDigits = percent.MaximumFractionDigits ?? 1 }).Format(number, culture, currency);
                case ColumnType.FileSize when value is not string:
                    return FileSize(number, culture, currency);
            }
        }
        if (type is ColumnType.Date or ColumnType.DateTime && TimeText.Resolve(value, zone) is { } resolved)
            return TimeText.Format(resolved.Wall, resolved.Offset, zone, culture, DateStyle.Medium, type == ColumnType.DateTime ? TimeStyle.Short : null);
        return Convert.ToString(value, CultureInfo.GetCultureInfo(culture)) ?? "";
    }

    /// <summary>类型给的样式：列的格式没另写样式（还是普通数字）时换成它，其余选项照列的。</summary>
    static NumericFormat WithStyle(NumericFormat? format, NumericStyle style) =>
        format is null ? new NumericFormat { Style = style } : format.Style == NumericStyle.Decimal ? format with { Style = style } : format;

    /// <summary>文件大小，按 1000 进位取合适的单位：1234567 → 1.2 MB；取整后进到 1000 的换下一档（999,999 B 是 1 MB，不是 1,000 kB）。</summary>
    public static string FileSize(decimal bytes, string culture, string currency)
    {
        var exponent = Math.Clamp((int)Math.Floor(Math.Log10(Math.Max((double)bytes, 1)) / 3), 0, FileSizeUnits.Length - 1);
        if (Math.Round(bytes / Power(exponent) * 10) / 10 >= 1000 && exponent < FileSizeUnits.Length - 1) exponent++;
        var format = new NumericFormat { Style = NumericStyle.Unit, Unit = FileSizeUnits[exponent], MaximumFractionDigits = exponent == 0 ? 0 : 1 };
        return format.Format(bytes / Power(exponent), culture, currency);

        static decimal Power(int exponent)
        {
            var result = 1m;
            for (var i = 0; i < exponent; i++) result *= 1000;
            return result;
        }
    }

    /// <summary>一列数值的合计（decimal 相加，精确）：跳过空值与不是数值的值；没有任何数值时为 null。</summary>
    public static decimal? Sum(DataTableColumn column, IEnumerable<object> rows)
    {
        decimal? total = null;
        foreach (var row in rows)
            if (Numeric(ValueOf(column, row)) is { } number) total = (total ?? 0) + number;
        return total;
    }

    /// <summary>
    /// 相同值合并（TS 的 mergeRepeated）的跨行数：每列一个数组，第 i 行是这一格往下合并几行，被合并掉的是 0。
    /// 后一列在前一列的分组边界处断开（不把不同供应商下同名的品类合在一起）。
    /// </summary>
    public static IReadOnlyDictionary<string, int[]> RepeatedRowSpans(IReadOnlyList<object> rows, IReadOnlyList<DataTableColumn> columns, IEnumerable<string> keys)
    {
        var spans = new Dictionary<string, int[]>();
        // 上一列的分组从哪些行开始：本列的合并不能跨过它们
        var boundaries = new HashSet<int> { 0 };
        foreach (var key in keys)
        {
            if (columns.FirstOrDefault(item => item.Key == key) is not { } column) continue;
            var list = Enumerable.Repeat(1, rows.Count).ToArray();
            var starts = new List<int>();
            var start = 0;
            for (var index = 0; index < rows.Count; index++)
            {
                var same = index > start && !boundaries.Contains(index) && Text(ValueOf(column, rows[index])) == Text(ValueOf(column, rows[start]));
                if (same)
                {
                    list[start]++;
                    list[index] = 0;
                }
                else
                {
                    start = index;
                    starts.Add(index);
                }
            }
            spans[key] = list;
            boundaries.UnionWith(starts);
        }
        return spans;

        static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }
}
