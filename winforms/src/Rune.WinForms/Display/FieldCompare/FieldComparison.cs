using System.Collections;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>字段对比的排法（TS 的 layout）。</summary>
public enum FieldCompareLayout
{
    /// <summary>表格：字段、原值、新值、变化四列（默认），列出全部字段。</summary>
    Table,

    /// <summary>列表：一行一个字段“原值 → 新值 变化”，只列改了的（时间线、通知、审批意见）。</summary>
    List,
}

/// <summary>数值变化的好坏（TS 的 better）。</summary>
public enum CompareBetter
{
    /// <summary>越高越好：涨了是绿、降了是红（利润率、数量）。</summary>
    Higher,

    /// <summary>越低越好：成本、耗时。</summary>
    Lower,
}

/// <summary>
/// 一个要对比的字段（vue-tsx-pro 的 CompareField）：TS 用表格的列定义，这里只取对比用得上的几项。
/// </summary>
/// <param name="Key">字段名：不写 <see cref="Value"/> 时按它取记录的属性（或字典的键）。</param>
/// <param name="Header">字段的显示名。</param>
public sealed record CompareField(string Key, string Header)
{
    /// <summary>从记录里取值；不写时按 Key 取属性（字典按键）。</summary>
    public Func<object, object?>? Value { get; init; }

    /// <summary>数值的格式（金额、百分比、缩放）；不写时是普通数字。</summary>
    public NumericFormat? Format { get; init; }

    /// <summary>状态字段：值是状态的键，显示成状态的名字（界面上是 StatusBadge）。</summary>
    public StatusMap? Statuses { get; init; }

    /// <summary>值是空时显示的文字，默认“—”。</summary>
    public string? Empty { get; init; }

    /// <summary>自定义显示（TS 的 cell）：参数是记录与哪一份（0 原值、1 新值），返回显示的内容；不写时按格式显示文字。</summary>
    public Func<object, int, object?>? Cell { get; init; }

    /// <summary>数值变化的好坏；不写时变化量不着色。</summary>
    public CompareBetter? Better { get; init; }
}

/// <summary>对比的一行：字段与它的比较结果（vue-tsx-pro 的 FieldCompareRow，建在 diffFields 上）。</summary>
/// <param name="Field">字段。</param>
/// <param name="Before">原值；没有原记录时是 null。</param>
/// <param name="After">新值。</param>
/// <param name="Changed">变了没有。</param>
/// <param name="Delta">两边都是数值时的差（新 − 原，十进制精确）。</param>
/// <param name="Ratio">两边都是数值且原值不为 0 时的变化率（-0.2 是降了 20%）。</param>
public sealed record FieldCompareRow(CompareField Field, object? Before, object? After, bool Changed, decimal? Delta, double? Ratio);

/// <summary>一行和之前比怎么了（vue-tsx-pro 的 RowChanges）。</summary>
public enum RowChange
{
    /// <summary>之前没有。</summary>
    Inserted,

    /// <summary>有字段变了。</summary>
    Updated,

    /// <summary>现在没有了。</summary>
    Deleted,
}

/// <summary>一行的比较结果（vue-tsx-pro 的 RowDiff）。</summary>
/// <param name="Key">行的 key。</param>
/// <param name="Row">现在的行；删除了的行是之前的样子。</param>
/// <param name="Change">没变时 null。</param>
/// <param name="Before">修改了的行之前的样子。</param>
/// <param name="ChangedFields">修改了的行里变了的字段（属性名）。</param>
public sealed record RowDiff<T>(string Key, T Row, RowChange? Change = null, T? Before = default, IReadOnlyList<string>? ChangedFields = null);

/// <summary>
/// 字段对比的计算（vue-tsx-pro 的 diffFields 与 FieldCompare 里的格式化）：取值、比较、数值的变化量与变化率、显示的文字与色调。三个库共用一份。
/// </summary>
public static class FieldComparison
{
    /// <summary>变化率的格式：百分比、最多一位小数、正负都带符号。</summary>
    static readonly NumericFormat RatioFormat = new() { Style = NumericStyle.Percent, MaximumFractionDigits = 1, SignDisplay = NumericSign.ExceptZero };

    /// <summary>按字段比较两份记录；某一份是 null 时它的每个字段都当作空。</summary>
    public static IReadOnlyList<FieldCompareRow> Diff(IEnumerable<CompareField> fields, object? before, object? after) =>
        [.. fields.Select(field =>
        {
            var (from, to) = (ValueOf(field, before), ValueOf(field, after));
            decimal? delta = null;
            double? ratio = null;
            if (Numeric(from) is { } a && Numeric(to) is { } b)
            {
                delta = b - a;
                if (a != 0) ratio = (double)(delta.Value / a);
            }
            return new FieldCompareRow(field, from, to, delta is { } d ? d != 0 : !Same(from, to), delta, ratio);
        })];

    /// <summary>
    /// 两份列表按 key 比较（vue-tsx-pro 的 diffRows：执行前后、编辑前后、两次导入）：哪些行是新增、修改、删除的。
    /// 顺序是现在的行，删除了的排在最后；字段是行的公开属性，变没变按结构相等（内容一样的列表不算变了）。
    /// </summary>
    public static IReadOnlyList<RowDiff<T>> DiffRows<T>(IEnumerable<T> before, IEnumerable<T> after, Func<T, string> getKey) where T : class
    {
        var remaining = new Dictionary<string, T>();
        foreach (var row in before) remaining.TryAdd(getKey(row), row);
        var fields = typeof(T).GetProperties().Where(property => property.GetIndexParameters().Length == 0).Select(property => new CompareField(property.Name, property.Name)).ToList();
        List<RowDiff<T>> rows = [];
        foreach (var row in after)
        {
            var key = getKey(row);
            if (!remaining.Remove(key, out var previous))
            {
                rows.Add(new RowDiff<T>(key, row, RowChange.Inserted));
                continue;
            }
            var changed = Diff(fields, previous, row).Where(field => field.Changed).Select(field => field.Field.Key).ToList();
            rows.Add(changed.Count > 0 ? new RowDiff<T>(key, row, RowChange.Updated, previous, changed) : new RowDiff<T>(key, row));
        }
        rows.AddRange(remaining.Select(pair => new RowDiff<T>(pair.Key, pair.Value, RowChange.Deleted)));
        return rows;
    }

    /// <summary>字段在一份记录里的值：Value 函数、字典的键或同名属性。</summary>
    public static object? ValueOf(CompareField field, object? record) => record switch
    {
        null => null,
        _ when field.Value is { } value => value(record),
        IDictionary<string, object?> map => map.TryGetValue(field.Key, out var found) ? found : null,
        IDictionary map => map.Contains(field.Key) ? map[field.Key] : null,
        _ => record.GetType().GetProperty(field.Key)?.GetValue(record),
    };

    /// <summary>值显示的文字：空是 Empty（默认“—”），状态是状态的名字，数值按格式，日期是短日期，其余 ToString。</summary>
    public static string Text(CompareField field, object? value, string culture, string currency)
    {
        if (value is null || value is string { Length: 0 }) return field.Empty ?? "—";
        if (field.Statuses is { } statuses && value is string key) return statuses.Label(key);
        if (Numeric(value) is { } number) return (field.Format ?? NumericFormat.Default).Format(number, culture, currency);
        var info = CultureInfo.GetCultureInfo(culture);
        return value switch
        {
            DateOnly date => date.ToString("d", info),
            DateTime time => time.TimeOfDay == TimeSpan.Zero ? time.ToString("d", info) : time.ToString("g", info),
            IFormattable formattable => formattable.ToString(null, info),
            _ => value.ToString() ?? "",
        };
    }

    /// <summary>
    /// 数值字段的变化：变化量（按字段的格式，带正负号）与变化率（百分比字段的变化量本身就是百分点，不再给变化率），
    /// 色调按 Better（没写或没变是 muted）；没变或不是数值时 null。
    /// </summary>
    public static (string Text, TextTone Tone)? Change(FieldCompareRow row, string culture, string currency)
    {
        if (!row.Changed || row.Delta is not { } delta) return null;
        var format = (row.Field.Format ?? NumericFormat.Default) with { SignDisplay = NumericSign.ExceptZero };
        var text = format.Format(delta, culture, currency);
        if (row.Ratio is { } ratio && format.Style != NumericStyle.Percent) text += $"（{RatioFormat.Format((decimal)ratio, culture, currency)}）";
        var sign = Math.Sign(delta);
        var tone = row.Field.Better is not { } better || sign == 0 ? TextTone.Muted
            : sign > 0 == (better == CompareBetter.Higher) ? TextTone.Success : TextTone.Danger;
        return (text, tone);
    }

    /// <summary>数值（不含 bool）换成 decimal；不是数值时 null。</summary>
    static decimal? Numeric(object? value) => value switch
    {
        decimal d => d,
        int or long or short or byte or sbyte or ushort or uint or ulong => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        double d when double.IsFinite(d) => (decimal)d,
        float f when float.IsFinite(f) => (decimal)f,
        _ => null,
    };

    /// <summary>结构相等（同 sameValue）：日期按值，其余 Equals。</summary>
    static bool Same(object? a, object? b) => Equals(a, b)
        || (a is IStructuralEquatable structural && structural.Equals(b, StructuralComparisons.StructuralEqualityComparer))
        // 列表（不只数组）也按内容比：编辑后新建的列表与原来的内容一样时不算变了
        || (a is IEnumerable x and not string && b is IEnumerable y and not string && x.Cast<object?>().SequenceEqual(y.Cast<object?>(), EqualityComparer<object?>.Create((m, n) => Same(m, n), item => 0)));
}
