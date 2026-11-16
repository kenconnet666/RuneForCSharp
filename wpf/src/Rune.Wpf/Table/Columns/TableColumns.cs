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

/// <summary>列固定在哪一侧（横向滚动时不动）。</summary>
public enum ColumnSticky
{
    /// <summary>起始边：名称、编号。</summary>
    Start,

    /// <summary>结束边：操作。</summary>
    End,
}

/// <summary>排序方向。</summary>
public enum SortDirection
{
    /// <summary>升序：小到大、A 到 Z。</summary>
    Ascending,

    /// <summary>降序。</summary>
    Descending,
}

/// <summary>按哪一列、哪个方向排序（vue-tsx-pro 的 SortState）。</summary>
/// <param name="Key">列的 Key。</param>
/// <param name="Direction">方向。</param>
public sealed record SortState(string Key, SortDirection Direction);

/// <summary>表头筛选里的一个选项。</summary>
/// <param name="Value">值：筛选状态里存它，默认与这一列的值（写成文字）比较。</param>
/// <param name="Label">显示的内容；不写时用 Value。</param>
public sealed record ColumnFilterOption(string Value, object? Label = null);

/// <summary>列的表头筛选（vue-tsx-pro 的 ColumnFilter）：<see cref="Values"/> 列出这一列出现过的值，<see cref="Keyword"/> 输入关键字，也可以自己给选项与判断。</summary>
public sealed record ColumnFilter
{
    /// <summary>列出这一列出现过的值（有 Statuses 时显示状态的名称；上游的 filter: true）。</summary>
    public static ColumnFilter Values { get; } = new();

    /// <summary>输入关键字，按语言忽略大小写与重音的"包含"（上游的 options: 'text'）。</summary>
    public static ColumnFilter Keyword { get; } = new() { IsKeyword = true };

    /// <summary>选项（可以多选）；不写时列出这一列出现过的值。</summary>
    public IReadOnlyList<ColumnFilterOption>? Options { get; init; }

    /// <summary>输入关键字，不列选项。</summary>
    public bool IsKeyword { get; init; }

    /// <summary>这一行留不留：参数是行与选中的值（关键字筛选时是 [关键字]）；默认这一列的值在其中、文字包含关键字。</summary>
    public Func<object, IReadOnlyList<string>, bool>? Match { get; init; }
}

/// <summary>固定列的偏移（像素），按列的 Key 取。</summary>
/// <param name="Start">固定在起始边的列：从控制列（勾选列）之后累加。</param>
/// <param name="End">固定在结束边的列：从末尾往回累加。</param>
/// <param name="StartEdge">起始边固定列里最靠里的一列（挨着滚动的列，画分隔线）。</param>
/// <param name="EndEdge">结束边固定列里最靠里的一列。</param>
public sealed record StickyOffsets(IReadOnlyDictionary<string, double> Start, IReadOnlyDictionary<string, double> End, string? StartEdge, string? EndEdge);

/// <summary>列虚拟化时不渲染的一段列 [From, To)。</summary>
/// <param name="From">第一列的下标。</param>
/// <param name="To">最后一列的下一列的下标。</param>
public readonly record struct ColumnSkip(int From, int To);

/// <summary>
/// 表格的一列（vue-tsx-pro 的 DataTableColumn，参考 TanStack 的列模型，只取最常用的部分）：ColumnTable、DataTable 用它（排序、筛选、固定、调整宽度只在 DataTable 里起作用）。
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

    /// <summary>可以点表头排序（DataTable）。</summary>
    public bool Sortable { get; init; }

    /// <summary>表头筛选（DataTable）。</summary>
    public ColumnFilter? Filter { get; init; }

    /// <summary>固定在哪一侧（DataTable）：固定列一定要写宽度（偏移按它算）。</summary>
    public ColumnSticky? Sticky { get; init; }

    /// <summary>可以拖动调整宽度；不写时跟随表格的 Resizable（DataTable）。</summary>
    public bool? Resizable { get; init; }

    /// <summary>可以就地编辑（DataTable 的 CellChange 写回）；表格随之变成可以用方向键走格的网格。按行判断写 <see cref="IsEditable"/>。</summary>
    public bool Editable { get; init; }

    /// <summary>按行判断能不能编辑（上游 editable 的函数写法）；写了时以它为准。</summary>
    public Func<object, bool>? IsEditable { get; init; }

    /// <summary>编辑器：参数是编辑的上下文，返回平台的控件；不写时按列选（状态列是下拉，数字类是数字框，其余是文字框）。</summary>
    public Func<CellEditorContext, object>? Editor { get; init; }

    /// <summary>提交前校验这一格：参数是新值与行，返回原因就不提交，编辑器标红并说明。</summary>
    public Func<object?, object, string?>? Validate { get; init; }
}

/// <summary>分组表头（vue-tsx-pro 的 DataTableColumnGroup）：相邻的几列上方共用一个标题（如"测算"下的 A、B、D 值）；只分一层。</summary>
/// <param name="Key">组的标识。</param>
/// <param name="Header">组标题：文字或控件。</param>
/// <param name="Columns">组里各列的 Key：要相邻，显示顺序随列定义。</param>
public sealed record DataTableColumnGroup(string Key, object Header, IReadOnlyList<string> Columns);

/// <summary>表头第一行的一格（vue-tsx-pro 的 HeaderLayoutCell）：一个分组（跨组里的几列），或没有分组的一列（跨两行）。</summary>
/// <param name="Group">分组；没有分组的一列时为 null。</param>
/// <param name="Columns">这一格下面的列：分组时是组里（这一段）的列，否则是那一列。</param>
public sealed record HeaderLayoutCell(DataTableColumnGroup? Group, IReadOnlyList<DataTableColumn> Columns);

/// <summary>自定义单元格编辑器拿到的（vue-tsx-pro 的 CellEditorContext）。</summary>
/// <param name="Row">这一行。</param>
/// <param name="Value">正在编辑的值（草稿）。</param>
/// <param name="Change">改草稿。</param>
/// <param name="Commit">提交（校验通过后交给 CellChange）。</param>
/// <param name="Cancel">放弃，回到原来的值。</param>
/// <param name="Error">校验没过的原因：编辑器用它标出错误。</param>
public sealed record CellEditorContext(object Row, object? Value, Action<object?> Change, Action Commit, Action Cancel, string? Error);

/// <summary>列定义的计算（vue-tsx-pro 的 columns.ts）：取值、类型、对齐、格式化、合计、排序、表头筛选、列的位置与宽度、列虚拟化、相同值合并的跨行数。三个库各有一份（tools/twins）。</summary>
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
    /// 列的位置调整（列菜单、列设置）：order 是列的 Key 的顺序，没列到的按定义的顺序排在后面；记下的顺序里已经删掉的列不算。
    /// pins 覆盖列的 Sticky（null 取消固定）。固定在起始边的排到最前、结束边的排到最后：固定列要挨着表格的边。
    /// </summary>
    public static IReadOnlyList<DataTableColumn> Arrange(IReadOnlyList<DataTableColumn> columns, IReadOnlyList<string>? order, IReadOnlyDictionary<string, ColumnSticky?>? pins = null)
    {
        var rank = new Dictionary<string, int>();
        foreach (var key in order ?? []) rank.TryAdd(key, rank.Count);
        var arranged = columns.Select((column, index) => (column, position: rank.TryGetValue(column.Key, out var at) ? at : rank.Count + index)).OrderBy(item => item.position)
            .Select(item => pins is not null && pins.TryGetValue(item.column.Key, out var pin) ? item.column with { Sticky = pin } : item.column).ToList();
        return [.. arranged.Where(column => column.Sticky == ColumnSticky.Start), .. arranged.Where(column => column.Sticky is null), .. arranged.Where(column => column.Sticky == ColumnSticky.End)];
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

    /// <summary>没写宽度的列，算表格最小宽度与固定列偏移时按这么宽算（像素）。</summary>
    public const double ColumnFlexMinWidth = 120;

    /// <summary>拖动调整列宽时默认的最小宽度（像素）。</summary>
    public const double ColumnMinWidth = 64;

    /// <summary>列虚拟化时没写宽度的列按这个宽度（像素）：宽度要确定，才算得出哪些列看得见。</summary>
    public const double VirtualColumnWidth = 150;

    /// <summary>固定列的偏移：起始边的从 leading（勾选列的宽度）之后累加，结束边的从末尾往回累加。widthOf 是列当前的宽度（调整过的、列定义的），没有时按 ColumnFlexMinWidth。</summary>
    public static StickyOffsets StickyOffsetsOf(IReadOnlyList<DataTableColumn> columns, Func<DataTableColumn, double?> widthOf, double leading)
    {
        Dictionary<string, double> Accumulate(ColumnSticky side, IEnumerable<DataTableColumn> ordered, double from, out string? edge)
        {
            var offsets = new Dictionary<string, double>();
            edge = null;
            var offset = from;
            foreach (var column in ordered.Where(column => column.Sticky == side))
            {
                offsets[column.Key] = offset;
                offset += widthOf(column) ?? ColumnFlexMinWidth;
                edge = column.Key;
            }
            return offsets;
        }
        var start = Accumulate(ColumnSticky.Start, columns, leading, out var startEdge);
        var end = Accumulate(ColumnSticky.End, columns.Reverse(), 0, out var endEdge);
        return new StickyOffsets(start, end, startEdge, endEdge);
    }

    /// <summary>表格的最小宽度：写了宽度的列按宽度，没写的按 MinWidth 或 ColumnFlexMinWidth，加上前面的控制列；放不下时横向滚动。</summary>
    public static double TableMinWidth(IReadOnlyList<DataTableColumn> columns, Func<DataTableColumn, double?> widthOf, double leading) =>
        columns.Aggregate(leading, (total, column) => total + (widthOf(column) ?? column.MinWidth ?? ColumnFlexMinWidth));

    /// <summary>
    /// 没写宽度的列的宽度：各列的最小宽度一样时平分剩余宽度就对，返回 null；不一样时是"自己的最小宽度 + 平分多出来的部分"，
    /// 表格放不下时保住最小宽度（上游写成 CSS 的 max(min, calc(min + (100cqi − 最小宽度) / n))，这里 container 是表格外层的宽度）。
    /// </summary>
    public static double? FlexColumnWidth(DataTableColumn column, IReadOnlyList<DataTableColumn> columns, Func<DataTableColumn, double?> widthOf, double leading, double container)
    {
        var flexible = columns.Where(item => widthOf(item) is null).ToList();
        double MinOf(DataTableColumn item) => item.MinWidth ?? ColumnFlexMinWidth;
        if (flexible.Select(MinOf).Distinct().Count() <= 1) return null;
        var min = MinOf(column);
        return Math.Max(min, min + (container - TableMinWidth(columns, widthOf, leading)) / flexible.Count);
    }

    /// <summary>点表头时的下一个排序：没排 → 升序 → 降序 → 不排；换一列从升序开始。</summary>
    public static SortState? NextSort(SortState? current, string key)
    {
        if (current?.Key != key) return new SortState(key, SortDirection.Ascending);
        return current.Direction == SortDirection.Ascending ? current with { Direction = SortDirection.Descending } : null;
    }

    /// <summary>多列排序时点表头：additive（按住 Shift）时在已有的排序后面加上这一列、或切换它的方向（降序时再点去掉）；否则只按这一列（同 NextSort）。</summary>
    public static IReadOnlyList<SortState> NextSorts(IReadOnlyList<SortState> current, string key, bool additive)
    {
        if (!additive) return NextSort(current.Count == 1 ? current[0] : null, key) is { } next ? [next] : [];
        if (current.FirstOrDefault(item => item.Key == key) is not { } existing) return [.. current, new SortState(key, SortDirection.Ascending)];
        return existing.Direction == SortDirection.Ascending
            ? [.. current.Select(item => item.Key == key ? item with { Direction = SortDirection.Descending } : item)]
            : [.. current.Where(item => item.Key != key)];
    }

    /// <summary>排序写成一段文字：逗号分隔的列 Key，倒序的前面加减号（同 JSON:API），如 <c>-amount,name</c>；地址栏、发给服务端的 sort 参数都是它。</summary>
    public static string FormatSorts(IEnumerable<SortState>? sorts) =>
        string.Join(',', (sorts ?? []).Select(item => (item.Direction == SortDirection.Descending ? "-" : "") + item.Key));

    /// <summary>读 FormatSorts 写的文字：<c>-amount,name</c> 是两列排序；空的项跳过。</summary>
    public static IReadOnlyList<SortState> ParseSorts(string text) =>
    [
        .. text.Split(',').Select(part => part.Trim()).Where(part => part.TrimStart('-').Length > 0)
            .Select(part => part.StartsWith('-') ? new SortState(part[1..], SortDirection.Descending) : new SortState(part, SortDirection.Ascending)),
    ];

    /// <summary>
    /// 比较两个值，给排序用（上游 createComparer）：null 排在最后；数值按大小；日期按时间；false 在前；
    /// 文字按语言（中文按拼音、"第2章"在"第10章"前，忽略大小写与重音）。
    /// </summary>
    public static Comparison<object?> Comparer(string culture)
    {
        var compare = CultureInfo.GetCultureInfo(culture).CompareInfo;
        return (a, b) =>
        {
            if (a is null || b is null) return a is null ? b is null ? 0 : 1 : -1;
            if (a is not string && b is not string && Numeric(a) is { } x && Numeric(b) is { } y) return x.CompareTo(y);
            if (Moment(a) is { } p && Moment(b) is { } q) return p.CompareTo(q);
            if (a is bool m && b is bool n) return m.CompareTo(n);
            return compare.Compare(Convert.ToString(a, CultureInfo.InvariantCulture), Convert.ToString(b, CultureInfo.InvariantCulture),
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.NumericOrdering);
        };

        static DateTimeOffset? Moment(object value) => value switch
        {
            DateTimeOffset time => time,
            DateTime time => new DateTimeOffset(time.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : time),
            DateOnly day => new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            _ => null,
        };
    }

    /// <summary>
    /// 按排序排列（返回新列表，原列表不变）：可以是几列（前一列相等时比下一列），相等时保持原来的先后；空值不随方向翻转、总在最后。
    /// 数字类的列里十进制字符串（"2.5"、"2.10"）按数值排。没有排序、排序列都不存在时原样返回。
    /// </summary>
    public static IReadOnlyList<object> SortRows(IReadOnlyList<object> rows, IReadOnlyList<DataTableColumn> columns, IEnumerable<SortState>? sorts, string culture)
    {
        List<(Func<object, object?> Value, int Factor)> keys = [];
        foreach (var state in sorts ?? [])
            if (columns.FirstOrDefault(item => item.Key == state.Key) is { } column)
                keys.Add((SortValue(column, rows), state.Direction == SortDirection.Ascending ? 1 : -1));
        if (keys.Count == 0) return rows;
        var compare = Comparer(culture);
        var entries = rows.Select((row, index) => (Row: row, Index: index, Values: keys.Select(key => key.Value(row)).ToArray())).ToList();
        entries.Sort((a, b) =>
        {
            for (var at = 0; at < keys.Count; at++)
            {
                var (x, y) = (a.Values[at], b.Values[at]);
                var order = x is null || y is null ? compare(x, y) : compare(x, y) * keys[at].Factor;
                if (order != 0) return order;
            }
            return a.Index.CompareTo(b.Index);
        });
        return [.. entries.Select(entry => entry.Row)];

        static Func<object, object?> SortValue(DataTableColumn column, IReadOnlyList<object> rows)
        {
            // 数字类的列里十进制字符串按数值排：按文字比较时 "2.5" 会排在 "2.10" 后面
            var numeric = AlignOf(TypeOf(column, rows)) == TableAlign.End;
            return row => ValueOf(column, row) is var raw && numeric && raw is string && Numeric(raw) is { } number ? number : raw;
        }
    }

    /// <summary>表头筛选的选项：写了的原样，没写的列出这一列出现过的值（有 Statuses 时按状态的顺序、用状态名，其余按语言排序）。关键字筛选没有选项。</summary>
    public static IReadOnlyList<ColumnFilterOption> FilterOptions(DataTableColumn column, IEnumerable<object> rows, string culture)
    {
        if (column.Filter is not { } filter || filter.IsKeyword) return [];
        if (filter.Options is { } options) return options;
        var seen = new Dictionary<string, object>();
        foreach (var row in rows)
            if (ValueOf(column, row) is { } value && !IsEmpty(value)) seen.TryAdd(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", value);
        var compare = Comparer(culture);
        IEnumerable<string> ordered = column.Statuses is { } statuses
            ? seen.Keys.OrderBy(statuses.Order)
            : seen.Keys.Order(Comparer<string>.Create((a, b) => compare(seen[a], seen[b])));
        return [.. ordered.Select(value => new ColumnFilterOption(value, column.Statuses?.Label(value)))];
    }

    /// <summary>按表头筛选留下的行：filters 是各列选中的值（关键字筛选时是 [关键字]），空的不算。</summary>
    public static IReadOnlyList<object> FilterRows(IReadOnlyList<object> rows, IReadOnlyList<DataTableColumn> columns, IReadOnlyDictionary<string, IReadOnlyList<string>>? filters, string culture)
    {
        var compare = CultureInfo.GetCultureInfo(culture).CompareInfo;
        List<Func<object, bool>> tests = [];
        foreach (var column in columns)
        {
            if (column.Filter is not { } filter || filters is null || !filters.TryGetValue(column.Key, out var values) || values.Count == 0) continue;
            if (filter.Match is { } match) tests.Add(row => match(row, values));
            else if (filter.IsKeyword)
                tests.Add(row => compare.IndexOf(Convert.ToString(ValueOf(column, row), CultureInfo.InvariantCulture) ?? "", values[0], CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);
            else
            {
                var allowed = values.ToHashSet();
                tests.Add(row => allowed.Contains(Convert.ToString(ValueOf(column, row), CultureInfo.InvariantCulture) ?? ""));
            }
        }
        return tests.Count == 0 ? rows : [.. rows.Where(row => tests.All(test => test(row)))];
    }

    /// <summary>
    /// 列虚拟化：只渲染看得见的列，前后各多 overscan 列，返回要跳过的几段（最多两段）。两端的固定列（开头连着的 Start、末尾连着的 End）总是渲染；
    /// 固定列夹在中间时不虚拟化（返回空）。offset 是视口起始边在表格里的位置，leading 是数据列前面的宽度（勾选列、详情列）。
    /// </summary>
    public static IReadOnlyList<ColumnSkip> ColumnSkips(IReadOnlyList<DataTableColumn> columns, Func<DataTableColumn, double> widthOf, double offset, double width, double leading = 0, int overscan = 3)
    {
        var lead = 0;
        while (lead < columns.Count && columns[lead].Sticky == ColumnSticky.Start) lead++;
        var trail = columns.Count;
        while (trail > lead && columns[trail - 1].Sticky == ColumnSticky.End) trail--;
        if (columns.Skip(lead).Take(trail - lead).Any(column => column.Sticky is not null)) return [];
        var position = leading + columns.Take(lead).Sum(widthOf);
        int first = -1, last = -1;
        for (var index = lead; index < trail; index++)
        {
            var size = widthOf(columns[index]);
            if (first < 0 && position + size > offset) first = index;
            if (position < offset + width) last = index;
            position += size;
        }
        if (first < 0) first = trail - 1;
        if (last < first) last = first;
        first = Math.Max(lead, first - overscan);
        last = Math.Min(trail - 1, last + overscan);
        List<ColumnSkip> skips = [];
        if (first > lead) skips.Add(new ColumnSkip(lead, first));
        if (last + 1 < trail) skips.Add(new ColumnSkip(last + 1, trail));
        return skips;
    }

    /// <summary>
    /// 按分组排出表头第一行（上游 headerLayout）：columns 是显示的列（隐藏了的不算），组里的列全隐藏时这一组也不显示；
    /// 组里的列被别的列隔开时，后一段另起一格、重复组标题。
    /// </summary>
    public static IReadOnlyList<HeaderLayoutCell> HeaderLayout(IReadOnlyList<DataTableColumn> columns, IReadOnlyList<DataTableColumnGroup> groups)
    {
        var groupOf = new Dictionary<string, DataTableColumnGroup>();
        foreach (var group in groups)
            foreach (var key in group.Columns) groupOf[key] = group;
        List<(DataTableColumnGroup? Group, List<DataTableColumn> Columns)> cells = [];
        foreach (var column in columns)
        {
            var group = groupOf.GetValueOrDefault(column.Key);
            if (group is not null && cells.Count > 0 && cells[^1].Group == group) cells[^1].Columns.Add(column);
            else cells.Add((group, [column]));
        }
        return [.. cells.Select(cell => new HeaderLayoutCell(cell.Group, cell.Columns))];
    }

    /// <summary>这一格能不能编辑：列的 IsEditable 按行判断，否则看 Editable。</summary>
    public static bool IsCellEditable(DataTableColumn column, object row) => column.IsEditable?.Invoke(row) ?? column.Editable;

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
