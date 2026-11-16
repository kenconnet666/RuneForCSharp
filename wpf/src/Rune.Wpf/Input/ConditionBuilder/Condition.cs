using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Rune.Wpf;

/// <summary>比较的方式（vue-tsx-pro 的 ConditionOp；JSON 里写成 eq、notIn 这样的小驼峰）。</summary>
public enum ConditionOp
{
    /// <summary>等于。</summary>
    Eq,

    /// <summary>不等于。</summary>
    Ne,

    /// <summary>大于（日期：晚于）。</summary>
    Gt,

    /// <summary>大于等于。</summary>
    Gte,

    /// <summary>小于（日期：早于）。</summary>
    Lt,

    /// <summary>小于等于。</summary>
    Lte,

    /// <summary>在两个值之间（含两端）：值是 [下限, 上限]，任一端为空时不限。</summary>
    Between,

    /// <summary>是其中之一：值是一组。</summary>
    In,

    /// <summary>不是其中任何一个：值是一组。</summary>
    NotIn,

    /// <summary>文字包含（不分大小写）。</summary>
    Contains,

    /// <summary>文字不包含。</summary>
    NotContains,

    /// <summary>文字以它开头。</summary>
    StartsWith,

    /// <summary>有值（不是 null、空字符串、空的一组）。</summary>
    Exists,

    /// <summary>没有值。</summary>
    Empty,
}

/// <summary>字段的类型：决定能用哪些比较方式、值用什么控件填。</summary>
public enum ConditionFieldType
{
    /// <summary>文字：包含、等于、开头是。</summary>
    Text,

    /// <summary>数字：大小、介于。</summary>
    Number,

    /// <summary>日期（ISO 文字）：早于、晚于、介于。</summary>
    Date,

    /// <summary>从选项里选：是、不是、是其中之一。</summary>
    Select,

    /// <summary>是或否。</summary>
    Boolean,
}

/// <summary>比较方式要的值的形状。</summary>
public enum ConditionValueShape
{
    /// <summary>没有值（有值、没有值）。</summary>
    None,

    /// <summary>一个值。</summary>
    Single,

    /// <summary>两个值：介于的下限与上限。</summary>
    Range,

    /// <summary>一组值：是其中之一、不在其中。</summary>
    List,
}

/// <summary>选项型字段的一个选项。</summary>
/// <param name="Value">值（文字或数字）。</param>
/// <param name="Label">显示的文字。</param>
public sealed record ConditionOption(object Value, string Label);

/// <summary>自己画值的输入时拿到的参数。</summary>
/// <param name="Field">字段。</param>
/// <param name="Op">比较的方式。</param>
/// <param name="Value">现在的值。</param>
/// <param name="Change">改值。</param>
/// <param name="Label">无障碍名称（"金额的值"）。</param>
public sealed record ConditionInputContext(ConditionField Field, ConditionOp Op, object? Value, Action<object?> Change, string Label);

/// <summary>能参与比较的字段。</summary>
/// <param name="Name">字段名：写进条件的 Field。</param>
/// <param name="Label">显示名。</param>
/// <param name="Type">类型。</param>
public sealed record ConditionField(string Name, string Label, ConditionFieldType Type)
{
    /// <summary>分组名：字段多时字段列表按它分组（如"申请单""流程变量"）。</summary>
    public string? Group { get; init; }

    /// <summary>选项（Select 类型）。</summary>
    public IReadOnlyList<ConditionOption>? Options { get; init; }

    /// <summary>能用的比较方式，默认按类型（<see cref="Condition.Operators"/>）。</summary>
    public IReadOnlyList<ConditionOp>? Operators { get; init; }

    /// <summary>自己画值的输入（人员选择器、带单位的数字）：返回平台的控件；类型仍决定比较方式与摘要里值的写法。</summary>
    public Func<ConditionInputContext, object?>? Input { get; init; }

    /// <summary>摘要里值的写法，默认选项的文字、日期原样、是否写成"是 / 否"。</summary>
    public Func<object?, string>? Format { get; init; }
}

/// <summary>
/// 条件（vue-tsx-pro 的 Condition，input/condition.ts）：比较一个字段（<see cref="ConditionRule"/>），或用全部满足（<see cref="ConditionAll"/>）、
/// 任一满足（<see cref="ConditionAny"/>）、不满足（<see cref="ConditionNot"/>）组合的表达式树。三个库各一份（孪生文件）。
/// <code>
/// var condition = Condition.All(new ConditionRule("amount", ConditionOp.Gte, 10000),
///     Condition.Any(new ConditionRule("category", ConditionOp.In, new[] { "钢材", "铝材" }), Condition.Not(new ConditionRule("urgent", ConditionOp.Eq, false))));
/// rows.Where(row => condition.Matches(row));
/// condition.Describe(fields, lang);   // "金额 大于等于 10,000，且 （品类 是其中之一 钢材、铝材，或 不满足：加急 为 否）"
/// </code>
/// 存库、传给后端用 JSON（<see cref="ToJson"/>、<see cref="Parse"/>），形状与上游一致：{ "field", "op", "value" }、{ "all": [] }、{ "any": [] }、{ "not": {} }。
/// 值的形式：介于是 [下限, 上限]，是其中之一是一组，有值、没有值没有值；日期是 ISO 文字，按文字比就是先后。
/// 求值的规则与上游（query 的 conditionBy、流程引擎的模型包）一致，同一份条件在各处筛出同样的行。
/// </summary>
[JsonConverter(typeof(ConditionJsonConverter))]
public abstract record Condition
{
    /// <summary>全部满足。</summary>
    public static ConditionAll All(params IEnumerable<Condition> items) => new([.. items]);

    /// <summary>任一满足。</summary>
    public static ConditionAny Any(params IEnumerable<Condition> items) => new([.. items]);

    /// <summary>不满足。</summary>
    public static ConditionNot Not(Condition inner) => new(inner);

    /// <summary>各类型默认能用的比较方式，第一个是新条件默认的。</summary>
    public static IReadOnlyDictionary<ConditionFieldType, IReadOnlyList<ConditionOp>> Operators { get; } = new Dictionary<ConditionFieldType, IReadOnlyList<ConditionOp>>
    {
        [ConditionFieldType.Text] = [ConditionOp.Contains, ConditionOp.NotContains, ConditionOp.Eq, ConditionOp.Ne, ConditionOp.StartsWith, ConditionOp.Empty, ConditionOp.Exists],
        [ConditionFieldType.Number] = [ConditionOp.Eq, ConditionOp.Ne, ConditionOp.Gt, ConditionOp.Gte, ConditionOp.Lt, ConditionOp.Lte, ConditionOp.Between, ConditionOp.Empty, ConditionOp.Exists],
        [ConditionFieldType.Date] = [ConditionOp.Eq, ConditionOp.Lt, ConditionOp.Gt, ConditionOp.Lte, ConditionOp.Gte, ConditionOp.Between, ConditionOp.Empty, ConditionOp.Exists],
        [ConditionFieldType.Select] = [ConditionOp.Eq, ConditionOp.Ne, ConditionOp.In, ConditionOp.NotIn, ConditionOp.Empty, ConditionOp.Exists],
        [ConditionFieldType.Boolean] = [ConditionOp.Eq],
    };

    /// <summary>字段能用的比较方式。</summary>
    public static IReadOnlyList<ConditionOp> OperatorsOf(ConditionField field) => field.Operators ?? Operators[field.Type];

    /// <summary>这种比较要不要值。</summary>
    public static bool NeedsValue(ConditionOp op) => op is not (ConditionOp.Exists or ConditionOp.Empty);

    /// <summary>比较方式要的值的形状。</summary>
    public static ConditionValueShape ShapeOf(ConditionOp op) => op switch
    {
        ConditionOp.Exists or ConditionOp.Empty => ConditionValueShape.None,
        ConditionOp.Between => ConditionValueShape.Range,
        ConditionOp.In or ConditionOp.NotIn => ConditionValueShape.List,
        _ => ConditionValueShape.Single,
    };

    /// <summary>JSON 与语言包里的键：eq、notIn、startsWith。</summary>
    public static string KeyOf(ConditionOp op) => JsonNamingPolicy.CamelCase.ConvertName(op.ToString());

    /// <summary>语言包里类型的键：text、number、date、select、boolean。</summary>
    public static string KeyOf(ConditionFieldType type) => JsonNamingPolicy.CamelCase.ConvertName(type.ToString());

    /// <summary>
    /// 记录满不满足条件。字段的值默认按名字取（字典的键，或同名属性，不分大小写：JSON 里的 amount 对上 C# 的 Amount），<paramref name="read"/> 可以改（嵌套字段、计算字段）。
    /// 没填完整的比较与空的组不加条件，组里只看加了条件的那几项；不等于、不在其中、不包含把空值算作满足。
    /// </summary>
    public bool Matches(object? record, Func<object?, string, object?>? read = null) => Evaluate(record, read ?? ReadField) ?? true;

    /// <summary>求值；不加条件（没填完整的比较、空的组）时是 null，同 SQL 端丢掉空条件。</summary>
    internal abstract bool? Evaluate(object? record, Func<object?, string, object?> read);

    /// <summary>条件写成一句话（收起的高级筛选、流程连线上的摘要）：不认识的字段原样写字段名；嵌套的组加括号，取反写成"不满足：…"。</summary>
    public string Describe(IReadOnlyList<ConditionField> fields, Lang lang) => Describe(this, fields, lang, nested: false);

    static string Describe(Condition condition, IReadOnlyList<ConditionField> fields, Lang lang, bool nested)
    {
        switch (condition)
        {
            case ConditionNot not:
                return lang.ConditionNotPrefix + Describe(not.Inner, fields, lang, nested: true);
            case ConditionAll or ConditionAny:
                var list = condition is ConditionAll all ? all.Items : ((ConditionAny)condition).Items;
                var text = string.Join(condition is ConditionAll ? lang.ConditionAndJoiner : lang.ConditionOrJoiner, list.Select(child => Describe(child, fields, lang, nested: true)));
                return nested && list.Count > 1 ? $"（{text}）" : text;
        }
        var rule = (ConditionRule)condition;
        var field = fields.FirstOrDefault(candidate => candidate.Name == rule.Field);
        List<string> parts = [field?.Label ?? rule.Field, OperatorLabel(lang, field?.Type ?? ConditionFieldType.Text, rule.Op)];
        switch (ShapeOf(rule.Op))
        {
            case ConditionValueShape.Range:
                var ends = ListOf(rule.Value) ?? [];
                parts.Add(lang.ConditionRange(FormatValue(lang, field, ends.ElementAtOrDefault(0)), FormatValue(lang, field, ends.ElementAtOrDefault(1))));
                break;
            case not ConditionValueShape.None:
                parts.Add(FormatValue(lang, field, rule.Value));
                break;
        }
        return string.Join(' ', parts);
    }

    /// <summary>比较方式的文字：日期、选项、是否这几种类型有自己的说法（早于、是其中之一）。</summary>
    public static string OperatorLabel(Lang lang, ConditionFieldType type, ConditionOp op) =>
        lang.ConditionTypeOperators.TryGetValue(KeyOf(type), out var labels) && labels.TryGetValue(KeyOf(op), out var label) ? label : lang.ConditionOperators[KeyOf(op)];

    /// <summary>值在摘要里的写法：一组值用分隔符连起来；Format、是否、选项的文字、数字按语言格式化，其余写成文字。</summary>
    public static string FormatValue(Lang lang, ConditionField? field, object? value)
    {
        string One(object? item)
        {
            if (field?.Format is { } format) return format(item);
            if (field?.Type == ConditionFieldType.Boolean) return Truthy(item) ? lang.ConditionTrue : lang.ConditionFalse;
            if (field?.Options?.FirstOrDefault(option => Equals(option.Value, item)) is { } option) return option.Label;
            return IsNumber(item) ? string.Format(CultureOf(lang), "{0:#,##0.###}", item) : ToText(item);
        }
        return ListOf(value) is { } list ? string.Join(lang.ConditionValueSeparator, list.Select(One)) : One(value);
    }

    /// <summary>值写成文字：null 是空字符串，是否写成 true / false，数字与日期按固定的写法（日期是 ISO），其余对象写成 JSON。</summary>
    public static string ToText(object? value) => value switch
    {
        null => "",
        string text => text,
        bool flag => flag ? "true" : "false",
        DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime time => time.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset time => time.ToString("O", CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        IFormattable formattable when IsNumber(value) => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => JsonSerializer.Serialize(value),
    };

    /// <summary>写成 JSON（存库、传给后端），形状同上游。</summary>
    public string ToJson(bool indented = false) => JsonSerializer.Serialize(this, indented ? IndentedJson : JsonSerializerOptions.Default);

    /// <summary>读 JSON；不是条件的形状时抛 JsonException。</summary>
    public static Condition Parse(string json) => JsonSerializer.Deserialize<Condition>(json) ?? throw new JsonException("条件的 JSON 是 null");

    static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>默认的取值：字典的键，或同名属性（不分大小写）。</summary>
    static object? ReadField(object? record, string field) => record switch
    {
        null => null,
        IReadOnlyDictionary<string, object?> map => map.TryGetValue(field, out var found) ? found : null,
        IDictionary<string, object?> map => map.TryGetValue(field, out var found) ? found : null,
        IDictionary map => map.Contains(field) ? map[field] : null,
        _ => record.GetType().GetProperty(field, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)?.GetValue(record),
    };

    static CultureInfo CultureOf(Lang lang)
    {
        try
        {
            return CultureInfo.GetCultureInfo(lang.Code);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    /// <summary>一组值（不是文字的 IEnumerable）；不是一组时为 null。</summary>
    internal static IReadOnlyList<object?>? ListOf(object? value) => value is IEnumerable items and not string ? [.. items.Cast<object?>()] : null;

    internal static bool IsBlank(object? value) => value is null or "" || ListOf(value) is { Count: 0 };

    static bool Truthy(object? value) => value switch
    {
        null => false,
        bool flag => flag,
        string text => text.Length > 0,
        _ when IsNumber(value) => DecimalTextOf(value) is { } text && CompareDecimalText(text, "0") != 0,
        _ => true,
    };

    static bool IsNumber(object? value) => value is int or long or short or byte or sbyte or uint or ulong or ushort or decimal or BigInteger
        || value is double d && double.IsFinite(d) || value is float f && float.IsFinite(f);

    /// <summary>能比大小的单个值（上游 typeof 不是 object 的）：文字、是否、数字、日期。</summary>
    static bool IsScalar(object? value) => value is string or bool or char or DateOnly or DateTime or DateTimeOffset || IsNumber(value);

    /// <summary>
    /// 两个值按大小比：数字与十进制写法的文字按精确的数值（不经过浮点数，长小数、大整数不丢精度），
    /// 其余写成文字后按编码顺序（不按语言环境；ISO 日期按文字比就是先后）。
    /// </summary>
    internal static int Compare(object? a, object? b)
    {
        if (DecimalTextOf(a) is { } x && DecimalTextOf(b) is { } y) return CompareDecimalText(x, y);
        return Math.Sign(string.CompareOrdinal(ToText(a), ToText(b)));
    }

    /// <summary>等于：数字与数字文字视为相等（表单里的 "5" 与数据里的 5）；一组值、对象只认同一个。</summary>
    internal static bool Same(object? a, object? b) => Equals(a, b) || (IsScalar(a) && IsScalar(b) && Compare(a, b) == 0);

    /// <summary>十进制写法（可带指数）："19.90"、"-.5"、"1e3"。</summary>
    static readonly Regex DecimalText = new(@"^([+-]?)(\d+)(?:\.(\d*))?(?:[eE]([+-]?\d+))?$|^([+-]?)\.(\d+)(?:[eE]([+-]?\d+))?$", RegexOptions.CultureInvariant);

    /// <summary>能精确比较的值 → 十进制文本：数字（NaN、无穷不算）、十进制写法的文字；其余是 null。</summary>
    static string? DecimalTextOf(object? value) => value switch
    {
        string text when DecimalText.IsMatch(text.Trim()) => text.Trim(),
        string => null,
        _ when IsNumber(value) => ToText(value),
        _ => null,
    };

    /// <summary>拆开的十进制数：符号、去掉前导零的整数位、去掉末尾零的小数位（小数点按指数移过）。</summary>
    static (bool Negative, string Int, string Frac)? DecimalParts(string text)
    {
        var match = DecimalText.Match(text);
        if (!match.Success) return null;
        var g = match.Groups;
        var intDigits = g[2].Value;
        var digits = intDigits + (g[3].Success ? g[3].Value : g[6].Value);
        var exponent = g[4].Success ? g[4].Value : g[7].Success ? g[7].Value : "0";
        var point = intDigits.Length + int.Parse(exponent, CultureInfo.InvariantCulture);
        var padded = point < 0 ? new string('0', -point) + digits : point > digits.Length ? digits + new string('0', point - digits.Length) : digits;
        var at = Math.Max(point, 0);
        var whole = padded[..at].TrimStart('0');
        var frac = padded[at..].TrimEnd('0');
        var sign = g[1].Success && g[1].Value.Length > 0 ? g[1].Value : g[5].Value;
        return (sign == "-" && (whole.Length > 0 || frac.Length > 0), whole, frac);
    }

    /// <summary>两段十进制文本按数值比，不经过浮点数。</summary>
    static int CompareDecimalText(string a, string b)
    {
        if (DecimalParts(a) is not { } x || DecimalParts(b) is not { } y) return Math.Sign(string.CompareOrdinal(a, b));
        if (x.Negative != y.Negative) return x.Negative ? -1 : 1;
        int magnitude;
        if (x.Int.Length != y.Int.Length) magnitude = x.Int.Length < y.Int.Length ? -1 : 1;
        else if (x.Int != y.Int) magnitude = Math.Sign(string.CompareOrdinal(x.Int, y.Int));
        else
        {
            var width = Math.Max(x.Frac.Length, y.Frac.Length);
            magnitude = Math.Sign(string.CompareOrdinal(x.Frac.PadRight(width, '0'), y.Frac.PadRight(width, '0')));
        }
        return x.Negative ? -magnitude : magnitude;
    }
}

/// <summary>比较一个字段。</summary>
/// <param name="Field">字段名。</param>
/// <param name="Op">比较的方式。</param>
/// <param name="Value">比较的值；有值、没有值不要。介于是两个值的一组，是其中之一是一组值。</param>
public sealed record ConditionRule(string Field, ConditionOp Op, object? Value = null) : Condition
{
    /// <summary>填完整了：要值的有值，介于至少一端有值。</summary>
    public bool IsComplete => ShapeOf(Op) switch
    {
        ConditionValueShape.None => true,
        ConditionValueShape.Range => ListOf(Value) is { } ends && ends.Any(end => !IsBlank(end)),
        _ => !IsBlank(Value),
    };

    /// <summary>"没有一项满足"的比较：字段的值是一组时要每一项都满足。</summary>
    static bool Negative(ConditionOp op) => op is ConditionOp.Ne or ConditionOp.NotIn or ConditionOp.NotContains;

    internal override bool? Evaluate(object? record, Func<object?, string, object?> read)
    {
        if (!IsComplete) return null;
        var actual = read(record, Field);
        // 字段的值是一组时（多选、人员的编号列表）对每一项比：等于、是其中之一、包含、大小是"有一项满足"，不等于、不在其中、不包含是"没有一项满足"
        if (Op is ConditionOp.Exists or ConditionOp.Empty || ListOf(actual) is not { } items) return MatchesOne(actual);
        return Negative(Op) ? items.All(MatchesOne) : items.Any(MatchesOne);
    }

    bool MatchesOne(object? actual)
    {
        static string Lower(object? value) => ToText(value).ToLowerInvariant();
        switch (Op)
        {
            case ConditionOp.Eq: return Same(actual, Value);
            case ConditionOp.Ne: return !Same(actual, Value);
            case ConditionOp.Gt: return actual is not null && Compare(actual, Value) > 0;
            case ConditionOp.Gte: return actual is not null && Compare(actual, Value) >= 0;
            case ConditionOp.Lt: return actual is not null && Compare(actual, Value) < 0;
            case ConditionOp.Lte: return actual is not null && Compare(actual, Value) <= 0;
            case ConditionOp.Between:
                var ends = ListOf(Value) ?? [];
                var (low, high) = (ends.ElementAtOrDefault(0), ends.ElementAtOrDefault(1));
                return actual is not null && (IsBlank(low) || Compare(actual, low) >= 0) && (IsBlank(high) || Compare(actual, high) <= 0);
            case ConditionOp.In: return ListOf(Value) is { } options && options.Any(item => Same(actual, item));
            case ConditionOp.NotIn: return ListOf(Value) is not { } excluded || !excluded.Any(item => Same(actual, item));
            case ConditionOp.Contains: return Lower(actual).Contains(Lower(Value), StringComparison.Ordinal);
            case ConditionOp.NotContains: return !Lower(actual).Contains(Lower(Value), StringComparison.Ordinal);
            case ConditionOp.StartsWith: return Lower(actual).StartsWith(Lower(Value), StringComparison.Ordinal);
            case ConditionOp.Exists: return !IsBlank(actual);
            default: return IsBlank(actual);
        }
    }
}

/// <summary>全部满足（JSON 的 all）。</summary>
/// <param name="Items">各项。</param>
public sealed record ConditionAll(IReadOnlyList<Condition> Items) : Condition
{
    internal override bool? Evaluate(object? record, Func<object?, string, object?> read)
    {
        var results = Items.Select(item => item.Evaluate(record, read)).OfType<bool>().ToList();
        return results.Count == 0 ? null : results.All(result => result);
    }
}

/// <summary>任一满足（JSON 的 any）。</summary>
/// <param name="Items">各项。</param>
public sealed record ConditionAny(IReadOnlyList<Condition> Items) : Condition
{
    internal override bool? Evaluate(object? record, Func<object?, string, object?> read)
    {
        var results = Items.Select(item => item.Evaluate(record, read)).OfType<bool>().ToList();
        return results.Count == 0 ? null : results.Any(result => result);
    }
}

/// <summary>不满足（JSON 的 not）。</summary>
/// <param name="Inner">取反的条件。</param>
public sealed record ConditionNot(Condition Inner) : Condition
{
    internal override bool? Evaluate(object? record, Func<object?, string, object?> read) => !Inner.Evaluate(record, read);
}

/// <summary>条件的 JSON：形状同上游（{ "field", "op", "value" }、{ "all" }、{ "any" }、{ "not" }），值读成文字、数字（整数是 long，其余 decimal）、是否、null 与一组。</summary>
public sealed class ConditionJsonConverter : JsonConverter<Condition>
{
    /// <inheritdoc />
    public override Condition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return Read(document.RootElement);
    }

    static Condition Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new JsonException($"条件应是对象，读到 {element.ValueKind}");
        if (element.TryGetProperty("all", out var all)) return new ConditionAll([.. all.EnumerateArray().Select(Read)]);
        if (element.TryGetProperty("any", out var any)) return new ConditionAny([.. any.EnumerateArray().Select(Read)]);
        if (element.TryGetProperty("not", out var not)) return new ConditionNot(Read(not));
        if (!element.TryGetProperty("field", out var field) || !element.TryGetProperty("op", out var op)) throw new JsonException("条件要有 field 与 op，或 all、any、not");
        if (!Enum.TryParse<ConditionOp>(op.GetString(), ignoreCase: true, out var parsed)) throw new JsonException($"不认识的比较方式 {op.GetString()}");
        return new ConditionRule(field.GetString() ?? "", parsed, element.TryGetProperty("value", out var value) ? ValueOf(value) : null);
    }

    static object? ValueOf(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var whole) ? whole : element.TryGetDecimal(out var exact) ? exact : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => element.EnumerateArray().Select(ValueOf).ToArray(),
        JsonValueKind.Object => element.Clone(),
        _ => null,
    };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Condition value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case ConditionAll all:
                WriteList(writer, "all", all.Items, options);
                break;
            case ConditionAny any:
                WriteList(writer, "any", any.Items, options);
                break;
            case ConditionNot not:
                writer.WritePropertyName("not");
                Write(writer, not.Inner, options);
                break;
            case ConditionRule rule:
                writer.WriteString("field", rule.Field);
                writer.WriteString("op", Condition.KeyOf(rule.Op));
                if (Condition.NeedsValue(rule.Op))
                {
                    writer.WritePropertyName("value");
                    JsonSerializer.Serialize(writer, rule.Value, rule.Value?.GetType() ?? typeof(object), options);
                }
                break;
        }
        writer.WriteEndObject();
    }

    void WriteList(Utf8JsonWriter writer, string name, IReadOnlyList<Condition> items, JsonSerializerOptions options)
    {
        writer.WriteStartArray(name);
        foreach (var item in items) Write(writer, item, options);
        writer.WriteEndArray();
    }
}
