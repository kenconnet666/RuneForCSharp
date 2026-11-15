using System.Buffers;
using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Rune.WinForms;

/// <summary>JsonViewer 里一项的类型：决定颜色与能不能展开（Other 是自带 ToString 的对象，Circular 是循环引用）。</summary>
public enum JsonViewerKind
{
    /// <summary>对象：JSON 对象、键是字符串的字典、普通对象（公开属性）、记录、匿名类型。</summary>
    Object,

    /// <summary>数组：JSON 数组与其他可枚举的集合。</summary>
    Array,

    /// <summary>键不是字符串的字典（上游的 Map）。</summary>
    Map,

    /// <summary>集合（ISet、IReadOnlySet）。</summary>
    Set,

    /// <summary>字符串、字符。</summary>
    String,

    /// <summary>数字（含 decimal、BigInteger：照实显示，不丢精度）。</summary>
    Number,

    /// <summary>布尔。</summary>
    Boolean,

    /// <summary>null。</summary>
    Null,

    /// <summary>日期时间（显示 ISO 8601）。</summary>
    Date,

    /// <summary>自带 ToString 的对象（Uri、Guid、TimeSpan、枚举）：当成一个值照实显示。</summary>
    Other,

    /// <summary>委托。</summary>
    Function,

    /// <summary>循环引用。</summary>
    Circular,
}

/// <summary>JsonViewer 树上的一项（vue-tsx-pro 的 JsonViewerNode）：路径也是 Tree 的 key；子节点展开到哪一层才建哪一层，每个节点只建一次。</summary>
public sealed class JsonViewerNode
{
    internal JsonViewerNode(string path, string name, object? value, JsonViewerKind kind, object[] ancestors)
    {
        Path = path;
        Name = name;
        Value = value;
        Kind = kind;
        Ancestors = ancestors;
    }

    /// <summary>路径（JavaScript 写法：$.items[0]["unit-price"]），也是 Tree 的 key。</summary>
    public string Path { get; }

    /// <summary>键名或序号。</summary>
    public string Name { get; }

    /// <summary>这一项的值。</summary>
    public object? Value { get; }

    /// <summary>值的类型。</summary>
    public JsonViewerKind Kind { get; }

    /// <summary>能展开（对象、数组、Map、Set）。</summary>
    public bool IsContainer => Kind is JsonViewerKind.Object or JsonViewerKind.Array or JsonViewerKind.Map or JsonViewerKind.Set;

    /// <summary>名称是序号（数组、Set 的项）：颜色同注释。</summary>
    public bool IsIndex => Name.Length > 0 && Name.All(char.IsAsciiDigit);

    /// <summary>从根到这里经过的对象：认出循环引用。</summary>
    internal object[] Ancestors { get; }

    internal IReadOnlyList<JsonViewerNode>? Children { get; set; }
}

/// <summary>
/// JsonViewer 的纯计算（vue-tsx-pro JsonViewer 里的 kindOf、childrenOf、sizeOf、display、copyText）：任意 .NET 值按类型分成可展开的容器与单个值。
/// <list type="bullet">
/// <item>System.Text.Json 的 JsonElement、JsonNode 按 JSON 的类型；键是字符串的字典、普通对象（公开属性）、记录、匿名类型是对象，键不是字符串的字典是 Map，
/// ISet 是 Set，其他可枚举的集合是数组。</item>
/// <item>数字（含 decimal、BigInteger）按固定区域照实显示；日期时间显示 ISO 8601（DateTime、DateTimeOffset 换成 UTC，带毫秒与 Z，同 toISOString）；
/// 自己写了 ToString 的类型（Uri、Guid、TimeSpan、枚举）当成一个值。</item>
/// <item>循环引用（经过的对象里又出现自己）显示 [Circular]，不再展开。</item>
/// </list>
/// </summary>
public static partial class JsonViewerNodes
{
    [GeneratedRegex(@"^[A-Za-z_$][\w$]*$")]
    private static partial Regex Identifier();

    /// <summary>第一层：根是容器时是它的各项，不然是根本身。</summary>
    public static IReadOnlyList<JsonViewerNode> Roots(object? value)
    {
        var root = NodeOf("$", "$", value, []);
        return ChildrenOf(root) ?? [root];
    }

    /// <summary>子节点（容器才有，第一次要时建）。</summary>
    public static IReadOnlyList<JsonViewerNode>? ChildrenOf(JsonViewerNode node)
    {
        if (!node.IsContainer) return null;
        if (node.Children is { } cached) return cached;
        var ancestors = node.Value is null ? node.Ancestors : [.. node.Ancestors, node.Value];
        var children = new List<JsonViewerNode>();
        switch (node.Kind)
        {
            case JsonViewerKind.Array:
                foreach (var (item, index) in Items(node.Value!).Select((item, index) => (item, index)))
                    children.Add(NodeOf($"{node.Path}[{index}]", Invariant(index), item, ancestors));
                break;
            case JsonViewerKind.Set:
                foreach (var (item, index) in Items(node.Value!).Select((item, index) => (item, index)))
                    children.Add(NodeOf($"[...{node.Path}][{index}]", Invariant(index), item, ancestors));
                break;
            case JsonViewerKind.Map:
                foreach (var entry in Entries((IDictionary)node.Value!))
                    children.Add(NodeOf($"{node.Path}.get({KeyLiteral(entry.Key)})", Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? "", entry.Value, ancestors));
                break;
            default:
                foreach (var (key, item) in Members(node.Value!))
                    children.Add(NodeOf(MemberPath(node.Path, key), key, item, ancestors));
                break;
        }
        node.Children = children;
        return children;
    }

    /// <summary>容器收起时的项数。</summary>
    public static int SizeOf(JsonViewerNode node) => node.Value switch
    {
        JsonElement { ValueKind: JsonValueKind.Array } element => element.GetArrayLength(),
        JsonElement element => element.EnumerateObject().Count(),
        JsonObject json => json.Count,
        JsonArray json => json.Count,
        ICollection collection => collection.Count,
        _ => ChildrenOf(node)?.Count ?? 0,
    };

    /// <summary>一行里值的写法：字符串带引号（同 JSON.stringify），容器是 {…}、[…]、Map {…}、Set […]。</summary>
    public static string Display(JsonViewerNode node) => node.Kind switch
    {
        JsonViewerKind.Object => "{…}",
        JsonViewerKind.Array => "[…]",
        JsonViewerKind.Map => "Map {…}",
        JsonViewerKind.Set => "Set […]",
        JsonViewerKind.String => Quote(StringOf(node.Value)),
        JsonViewerKind.Number => NumberOf(node.Value),
        JsonViewerKind.Boolean => BooleanOf(node.Value) ? "true" : "false",
        JsonViewerKind.Null => "null",
        JsonViewerKind.Date => DateOf(node.Value!),
        JsonViewerKind.Function => $"ƒ {((Delegate)node.Value!).Method.Name}()",
        JsonViewerKind.Circular => "[Circular]",
        _ => Convert.ToString(node.Value, CultureInfo.InvariantCulture) ?? "",
    };

    /// <summary>复制的值：字符串原样，单个值同显示，容器写成缩进两格的 JSON（数字照实写，Map 写成对象、Set 写成数组，循环引用写成 "[Circular]"）。</summary>
    public static string CopyText(JsonViewerNode node)
    {
        if (node.Kind == JsonViewerKind.String) return StringOf(node.Value);
        if (!node.IsContainer) return Display(node);
        var buffer = new ArrayBufferWriter<byte>();
        // 不转义中文与 HTML 字符：复制出来是给人看、贴进别处的
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            Write(writer, node);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>展开到第 depth 层的容器（depth 为 int.MaxValue 时全部展开），连同经过的节点按路径记进 seen。</summary>
    public static IReadOnlyList<string> ContainersTo(IReadOnlyList<JsonViewerNode> roots, int depth, IDictionary<string, JsonViewerNode>? seen = null)
    {
        var keys = new List<string>();
        void Visit(IReadOnlyList<JsonViewerNode> nodes, int level)
        {
            foreach (var node in nodes)
            {
                if (seen is not null) seen[node.Path] = node;
                if (level >= depth || !node.IsContainer) continue;
                keys.Add(node.Path);
                Visit(ChildrenOf(node)!, level + 1);
            }
        }
        Visit(roots, 0);
        return keys;
    }

    /// <summary>值的类型。</summary>
    public static JsonViewerKind KindOf(object? value) => value switch
    {
        null => JsonViewerKind.Null,
        JsonElement element => element.ValueKind switch
        {
            JsonValueKind.Object => JsonViewerKind.Object,
            JsonValueKind.Array => JsonViewerKind.Array,
            JsonValueKind.String => JsonViewerKind.String,
            JsonValueKind.Number => JsonViewerKind.Number,
            JsonValueKind.True or JsonValueKind.False => JsonViewerKind.Boolean,
            _ => JsonViewerKind.Null,
        },
        JsonObject => JsonViewerKind.Object,
        JsonArray => JsonViewerKind.Array,
        JsonValue json => json.GetValueKind() switch
        {
            JsonValueKind.String => JsonViewerKind.String,
            JsonValueKind.Number => JsonViewerKind.Number,
            JsonValueKind.True or JsonValueKind.False => JsonViewerKind.Boolean,
            _ => JsonViewerKind.Null,
        },
        string or char => JsonViewerKind.String,
        bool => JsonViewerKind.Boolean,
        sbyte or byte or short or ushort or int or uint or long or ulong or nint or nuint or float or double or decimal or Half or Int128 or UInt128 or BigInteger => JsonViewerKind.Number,
        DateTime or DateTimeOffset or DateOnly => JsonViewerKind.Date,
        Delegate => JsonViewerKind.Function,
        Enum => JsonViewerKind.Other,
        IDictionary dictionary => KeyType(dictionary.GetType()) == typeof(string) ? JsonViewerKind.Object : JsonViewerKind.Map,
        _ when IsSet(value.GetType()) => JsonViewerKind.Set,
        IEnumerable => JsonViewerKind.Array,
        _ => IsPlainObject(value.GetType()) ? JsonViewerKind.Object : JsonViewerKind.Other,
    };

    static JsonViewerNode NodeOf(string path, string name, object? value, object[] ancestors)
    {
        var kind = value is not null && !value.GetType().IsValueType && ancestors.Any(ancestor => ReferenceEquals(ancestor, value))
            ? JsonViewerKind.Circular
            : KindOf(value);
        return new JsonViewerNode(path, name, value, kind, ancestors);
    }

    /// <summary>对象的键接在路径后面：标识符用点，别的写成 ["…"]。</summary>
    static string MemberPath(string parent, string key) => Identifier().IsMatch(key) ? $"{parent}.{key}" : $"{parent}[{Quote(key)}]";

    /// <summary>Map 的键在路径里的写法（同 JSON.stringify(key) ?? String(key)）。</summary>
    static string KeyLiteral(object key) => KindOf(key) switch
    {
        JsonViewerKind.Number => NumberOf(key),
        JsonViewerKind.Boolean => (bool)key ? "true" : "false",
        _ => Quote(Convert.ToString(key, CultureInfo.InvariantCulture) ?? ""),
    };

    static IEnumerable<object?> Items(object value) => value switch
    {
        JsonElement element => element.EnumerateArray().Select(item => (object?)item),
        _ => ((IEnumerable)value).Cast<object?>(),
    };

    static IEnumerable<(string Key, object? Value)> Members(object value) => value switch
    {
        JsonElement element => element.EnumerateObject().Select(property => (property.Name, (object?)property.Value)),
        JsonObject json => json.Select(pair => (pair.Key, (object?)pair.Value)),
        IDictionary dictionary => Entries(dictionary).Select(entry => ((string)entry.Key, entry.Value)),
        _ => value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .Select(property => (property.Name, property.GetValue(value))),
    };

    /// <summary>字典的各项（IDictionary 自己的枚举器给 DictionaryEntry；Dictionary 的 IEnumerable 给的是 KeyValuePair）。</summary>
    static IEnumerable<DictionaryEntry> Entries(IDictionary dictionary)
    {
        for (var entries = dictionary.GetEnumerator(); entries.MoveNext();) yield return entries.Entry;
    }

    /// <summary>字典的键类型（IDictionary&lt;K, V&gt; 或 IReadOnlyDictionary&lt;K, V&gt; 的 K；非泛型的 Hashtable 是 object）。</summary>
    static Type KeyType(Type type) =>
        type.GetInterfaces().Prepend(type)
            .FirstOrDefault(face => face.IsGenericType && face.GetGenericTypeDefinition() is var definition
                && (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>)))
            ?.GetGenericArguments()[0] ?? typeof(object);

    static bool IsSet(Type type) =>
        type.GetInterfaces().Any(face => face.IsGenericType && face.GetGenericTypeDefinition() is var definition
            && (definition == typeof(ISet<>) || definition == typeof(IReadOnlySet<>)));

    /// <summary>
    /// 展开成对象的类型（上游的普通对象）：没有自己写 ToString 的类与结构，以及记录、匿名类型（编译器生成的 ToString 列出成员，仍是对象）。
    /// 自己写了 ToString 的（Uri、Guid、TimeSpan）当成一个值。
    /// </summary>
    static bool IsPlainObject(Type type)
    {
        var toString = type.GetMethod(nameof(ToString), Type.EmptyTypes)!;
        if (toString.DeclaringType == typeof(object) || toString.DeclaringType == typeof(ValueType)) return true;
        // 记录有编译器生成的 EqualityContract
        if (type.GetProperty("EqualityContract", BindingFlags.NonPublic | BindingFlags.Instance) is not null) return true;
        return type.IsDefined(typeof(CompilerGeneratedAttribute)) && type.Name.Contains("AnonymousType", StringComparison.Ordinal);
    }

    static string StringOf(object? value) => value switch
    {
        JsonElement element => element.GetString() ?? "",
        JsonValue json => json.GetValue<string>(),
        char character => character.ToString(),
        _ => (string)value!,
    };

    static string NumberOf(object? value) => value switch
    {
        JsonElement element => element.GetRawText(),
        JsonValue json => json.ToJsonString(),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };

    static bool BooleanOf(object? value) => value switch
    {
        JsonElement element => element.GetBoolean(),
        JsonValue json => json.GetValue<bool>(),
        _ => (bool)value!,
    };

    /// <summary>ISO 8601：同 Date.toISOString（UTC，带毫秒与 Z）；不知道时区的 DateTime 不加 Z，DateOnly 只有日期。</summary>
    static string DateOf(object value) => value switch
    {
        DateTimeOffset offset => offset.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
        DateTime { Kind: DateTimeKind.Unspecified } time => time.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture),
        DateTime time => time.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    /// <summary>字符串的 JSON 写法（同 JSON.stringify：只转义引号、反斜杠与控制字符，中文原样）。</summary>
    static string Quote(string text)
    {
        var builder = new StringBuilder(text.Length + 2).Append('"');
        foreach (var character in text)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case < ' ': builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:x4}"); break;
                default: builder.Append(character); break;
            }
        }
        return builder.Append('"').ToString();
    }

    static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);

    static void Write(Utf8JsonWriter writer, JsonViewerNode node)
    {
        switch (node.Kind)
        {
            case JsonViewerKind.Object or JsonViewerKind.Map:
                writer.WriteStartObject();
                foreach (var child in ChildrenOf(node)!)
                {
                    writer.WritePropertyName(child.Name);
                    Write(writer, child);
                }
                writer.WriteEndObject();
                break;
            case JsonViewerKind.Array or JsonViewerKind.Set:
                writer.WriteStartArray();
                foreach (var child in ChildrenOf(node)!) Write(writer, child);
                writer.WriteEndArray();
                break;
            case JsonViewerKind.String:
                writer.WriteStringValue(StringOf(node.Value));
                break;
            case JsonViewerKind.Number:
                // 照实写出数字的文字（BigInteger、decimal 不丢精度）；NaN、无穷不是合法的 JSON 数字，写成字符串
                var number = NumberOf(node.Value);
                if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed)) writer.WriteRawValue(number, skipInputValidation: true);
                else writer.WriteStringValue(number);
                break;
            case JsonViewerKind.Boolean:
                writer.WriteBooleanValue(BooleanOf(node.Value));
                break;
            case JsonViewerKind.Null:
                writer.WriteNullValue();
                break;
            default:
                writer.WriteStringValue(Display(node));
                break;
        }
    }
}
