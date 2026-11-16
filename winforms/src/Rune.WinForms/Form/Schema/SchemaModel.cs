using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace Rune.WinForms;

/// <summary>SchemaForm 能生成的控件（vue-tsx-pro 的 SchemaControls）。</summary>
public enum SchemaControl
{
    /// <summary>单行文字：TextField。</summary>
    Text,

    /// <summary>多行文字：Textarea。</summary>
    Textarea,

    /// <summary>数字：NumberField。</summary>
    Number,

    /// <summary>开关：Switch。</summary>
    Switch,

    /// <summary>单个复选框：Checkbox。</summary>
    Checkbox,

    /// <summary>下拉单选：Select。</summary>
    Select,

    /// <summary>单选组：RadioGroup（选项少时）。</summary>
    Radio,

    /// <summary>下拉多选：Select 的多选。</summary>
    Multiselect,

    /// <summary>复选组：CheckboxGroup。</summary>
    Checkboxes,

    /// <summary>日期：DatePicker。</summary>
    Date,

    /// <summary>日期加时分：DatePicker（分钟）。</summary>
    Datetime,

    /// <summary>嵌套的对象：一组字段。</summary>
    Object,

    /// <summary>对象的列表：可增删的行。</summary>
    List,

    /// <summary>人员、部门：PeoplePicker（数据来自表单的数据来源）。</summary>
    People,

    /// <summary>附件：Upload。</summary>
    File,

    /// <summary>对象的列表排成表格（明细表）：一行一条、一列一个字段，能增删行；列写了合计时表底有合计行。</summary>
    Table,
}

/// <summary>
/// 字段在 SchemaForm、SchemaView 里的写法（上游写在 zod 的 .meta() 里）：控件、栏宽、页签、单位、条件、合计、选项来源。
/// 标签、说明、占位、分组用平台的 <see cref="DisplayAttribute"/>（Name、Description、Prompt、GroupName），这里写了的优先。
/// 条件是 <see cref="Condition"/> 的 JSON（同 ConditionBuilder 的值），字段名是同一层或顶层的属性名。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SchemaFieldAttribute : Attribute
{
    /// <summary>占满一行的栏宽（上游的 span: 'full'）。</summary>
    public const int Full = int.MaxValue;

    /// <summary>用哪个控件；不写时按类型选。</summary>
    public SchemaControl Control { get => control ?? SchemaControl.Text; set => control = value; }

    internal SchemaControl? control;

    /// <summary>占几栏（分了多栏时）；<see cref="Full"/> 占满一行，0 是不写。</summary>
    public int Span { get; set; }

    /// <summary>占位文字（Display.Prompt 之外的写法）。</summary>
    public string? Placeholder { get; set; }

    /// <summary>分组（Display.GroupName 之外的写法）：同组的字段放进一个 FormSection。</summary>
    public string? Group { get; set; }

    /// <summary>页签：同一页签的字段放在一个标签页里（页签里照旧分组），没写的放在页签前面。</summary>
    public string? Tab { get; set; }

    /// <summary>数字的单位，写在输入框的结束边：元、天、kg。</summary>
    public string? Unit { get; set; }

    /// <summary>显示条件（Condition 的 JSON）：不成立时字段不显示、不校验。</summary>
    public string? VisibleWhen { get; set; }

    /// <summary>必填条件（Condition 的 JSON）：成立时空着报"必填"。</summary>
    public string? RequiredWhen { get; set; }

    /// <summary>只读条件（Condition 的 JSON）：成立时控件只读。</summary>
    public string? ReadonlyWhen { get; set; }

    /// <summary>明细表的这一列在表底求和（精确，不经过浮点数）。</summary>
    public bool Sum { get; set; }

    /// <summary>选项来自登记的来源（表单数据来源里的名字：字典名、远程搜索的名字）。</summary>
    public string? Source { get; set; }
}

/// <summary>一个可选值的显示文字（上游 meta 的 options）：写在属性上，可以写几个；没写的值显示它自己。字符串属性写了它就是从这些值里选。</summary>
/// <param name="value">值。</param>
/// <param name="label">显示的文字。</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class SchemaOptionAttribute(string value, string? label = null) : Attribute
{
    /// <summary>值。</summary>
    public string Value { get; } = value;

    /// <summary>显示的文字；null 时显示值。</summary>
    public string? Label { get; } = label;
}

/// <summary>一个可选值（vue-tsx-pro 的 SchemaOption）。</summary>
/// <param name="Value">值（枚举是成员名）。</param>
/// <param name="Label">显示的文字。</param>
public sealed record SchemaOption(string Value, string Label);

/// <summary>一个字段的描述（vue-tsx-pro 的 SchemaField）。</summary>
/// <param name="Key">键：属性名；完整路径由所在的位置拼出（同 FormController 的路径）。</param>
/// <param name="Label">标签：Display.Name，没写时是键。</param>
/// <param name="Control">控件。</param>
/// <param name="Type">属性的类型。</param>
public sealed record SchemaField(string Key, string Label, SchemaControl Control, Type Type)
{
    /// <summary>说明：Display.Description。</summary>
    public string? Description { get; init; }

    /// <summary>占位文字。</summary>
    public string? Placeholder { get; init; }

    /// <summary>可选值（Select、Radio、Multiselect、Checkboxes）。</summary>
    public IReadOnlyList<SchemaOption>? Options { get; init; }

    /// <summary>数字的单位。</summary>
    public string? Unit { get; init; }

    /// <summary>数字的值类型（decimal、long、int……；可空时是里面的类型）。</summary>
    public Type? ValueType { get; init; }

    /// <summary>占几栏；<see cref="SchemaFieldAttribute.Full"/> 占满一行；null 是不写。</summary>
    public int? Span { get; init; }

    /// <summary>分组。</summary>
    public string? Group { get; init; }

    /// <summary>页签。</summary>
    public string? Tab { get; init; }

    /// <summary>嵌套对象的字段、列表每一行的字段。</summary>
    public IReadOnlyList<SchemaField>? Fields { get; init; }

    /// <summary>列表每一行的类型（Object、List、Table 的元素）：新的一行按它新建。</summary>
    public Type? ItemType { get; init; }

    /// <summary>必填（Required 特性，或不可空的选择、数字、日期）。</summary>
    public bool Required { get; init; }

    /// <summary>显示条件。</summary>
    public Condition? VisibleWhen { get; init; }

    /// <summary>必填条件。</summary>
    public Condition? RequiredWhen { get; init; }

    /// <summary>只读条件。</summary>
    public Condition? ReadonlyWhen { get; init; }

    /// <summary>明细表的这一列在表底求和。</summary>
    public bool Sum { get; init; }

    /// <summary>选项来源的名字。</summary>
    public string? Source { get; init; }
}

/// <summary>字段的权限（按字段路径；上游 useForm 的 access）：流程的环节权限（字段 × 环节）直接映射过来；明细表的列没写时跟着明细表。</summary>
public enum SchemaFieldAccess
{
    /// <summary>照 schema 与条件。</summary>
    Editable,

    /// <summary>不显示、不校验。</summary>
    Hidden,

    /// <summary>只读、不算必填。</summary>
    Readonly,

    /// <summary>必填。</summary>
    Required,
}

/// <summary>字段此刻的样子（vue-tsx-pro 的 SchemaFieldState）。</summary>
/// <param name="Visible">显示（不显示的不渲染、不校验）。</param>
/// <param name="Readonly">只读。</param>
/// <param name="Required">条件或权限要求必填（字段本身的必填另算）。</param>
public readonly record struct SchemaFieldState(bool Visible, bool Readonly, bool Required);

/// <summary>
/// 从模型的类型推出表单的字段（vue-tsx-pro 的 schema-fields；上游从 zod schema 推，这里从属性与 DataAnnotations 推，同 FormController 的校验）：
/// 键、标签、控件、可选值、嵌套的字段。SchemaForm 按它生成表单，SchemaView 按它显示。
/// <list type="bullet">
/// <item>控件按类型选：字符串 → Text（DataType.MultilineText 或最长超过 100 字时 Textarea，DataType.Date 时 Date），写了 SchemaOption 的字符串、枚举 → Select，
/// 数字 → Number，布尔 → Switch，DateOnly → Date，DateTime / DateTimeOffset → Datetime（DataType.Date 时 Date），枚举或选项的集合 → Multiselect，
/// 对象 → Object（一组字段），对象的集合 → List（可增删的行）。SchemaField 特性的 Control 换成同类的别的控件（Radio、Checkboxes、Checkbox、Textarea、Table……）。</item>
/// <item>按属性的声明顺序；Display.Order 写了时按它排在前面（同 DataAnnotations 的约定）。</item>
/// </list>
/// </summary>
public static class SchemaModel
{
    static readonly ConcurrentDictionary<Type, IReadOnlyList<SchemaField>> Cache = new();
    static readonly NullabilityInfoContext Nullability = new();

    /// <summary>类型的各个字段（按类型缓存）；没有可读写的公开属性时为空。</summary>
    public static IReadOnlyList<SchemaField> Of(Type type) => Cache.GetOrAdd(type, Build);

    /// <summary>类型的各个字段。</summary>
    public static IReadOnlyList<SchemaField> Of<T>() => Of(typeof(T));

    static IReadOnlyList<SchemaField> Build(Type type)
    {
        if (IsScalar(type)) return [];
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0 && property.Name != "EqualityContract")
            .OrderBy(property => property.GetCustomAttribute<DisplayAttribute>()?.GetOrder() ?? int.MaxValue)
            .ThenBy(property => property.MetadataToken)
            .ToList();
        return [.. properties.Select(FieldOf)];
    }

    static SchemaField FieldOf(PropertyInfo property)
    {
        var display = property.GetCustomAttribute<DisplayAttribute>();
        var meta = property.GetCustomAttribute<SchemaFieldAttribute>();
        var type = property.PropertyType;
        var core = Nullable.GetUnderlyingType(type) ?? type;
        var item = ElementType(type);
        var itemCore = item is null ? null : Nullable.GetUnderlyingType(item) ?? item;
        var named = property.GetCustomAttributes<SchemaOptionAttribute>().ToList();
        var dataType = property.GetCustomAttribute<DataTypeAttribute>()?.DataType;
        var control = meta?.control ?? DefaultControl(property, core, itemCore, named.Count > 0, meta?.Source, dataType);
        var fields = control switch
        {
            SchemaControl.Object => Of(core),
            SchemaControl.List or SchemaControl.Table when itemCore is not null && !IsScalar(itemCore) => Of(itemCore),
            _ => null,
        };
        var optionType = control is SchemaControl.Multiselect or SchemaControl.Checkboxes ? itemCore : core;
        return new SchemaField(property.Name, display?.GetName() ?? property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name, control, type)
        {
            Description = display?.GetDescription(),
            Placeholder = meta?.Placeholder ?? display?.GetPrompt(),
            Options = OptionsOf(optionType, named),
            Unit = meta?.Unit,
            ValueType = control == SchemaControl.Number ? core : null,
            Span = meta is { Span: > 0 } ? meta.Span : null,
            Group = meta?.Group ?? display?.GetGroupName(),
            Tab = meta?.Tab,
            Fields = fields,
            ItemType = control == SchemaControl.Object ? core : control is SchemaControl.List or SchemaControl.Table ? itemCore : null,
            Required = IsRequired(property, control),
            VisibleWhen = ConditionOf(meta?.VisibleWhen),
            RequiredWhen = ConditionOf(meta?.RequiredWhen),
            ReadonlyWhen = ConditionOf(meta?.ReadonlyWhen),
            Sum = meta?.Sum ?? false,
            Source = meta?.Source,
        };
    }

    /// <summary>按类型选控件。</summary>
    static SchemaControl DefaultControl(PropertyInfo property, Type core, Type? item, bool hasOptions, string? source, DataType? dataType)
    {
        // 选项来自登记的来源：集合是多选，否则是下拉
        if (source is not null) return item is not null ? SchemaControl.Multiselect : SchemaControl.Select;
        if (item is not null) return IsScalar(item) ? SchemaControl.Multiselect : SchemaControl.List;
        if (core.IsEnum || hasOptions) return SchemaControl.Select;
        if (IsNumeric(core)) return SchemaControl.Number;
        if (core == typeof(bool)) return SchemaControl.Switch;
        if (core == typeof(DateOnly) || dataType == DataType.Date) return SchemaControl.Date;
        if (core == typeof(DateTime) || core == typeof(DateTimeOffset)) return SchemaControl.Datetime;
        if (core != typeof(string) && !IsScalar(core)) return SchemaControl.Object;
        var longest = property.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength ?? property.GetCustomAttribute<MaxLengthAttribute>()?.Length;
        return dataType == DataType.MultilineText || longest > 100 ? SchemaControl.Textarea : SchemaControl.Text;
    }

    /// <summary>可选值：枚举的成员（Display.Name 是显示文字），或属性上的 SchemaOption；SchemaOption 也给枚举成员改显示文字。</summary>
    static IReadOnlyList<SchemaOption>? OptionsOf(Type? type, IReadOnlyList<SchemaOptionAttribute> named)
    {
        string? Named(string value) => named.FirstOrDefault(option => option.Value == value)?.Label;
        if (type is { IsEnum: true })
            return [.. Enum.GetNames(type).Select(name => new SchemaOption(name, Named(name) ?? type.GetField(name)?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? name))];
        return named.Count > 0 ? [.. named.Select(option => new SchemaOption(option.Value, option.Label ?? option.Value))] : null;
    }

    /// <summary>必填：写了 Required；或是不可空的选择、数字、日期（不写就没有值）。文字、开关、集合不算（空串、false、空集合都是值）。</summary>
    static bool IsRequired(PropertyInfo property, SchemaControl control)
    {
        if (property.GetCustomAttribute<RequiredAttribute>() is not null) return true;
        if (control is not (SchemaControl.Select or SchemaControl.Radio or SchemaControl.Number or SchemaControl.Date or SchemaControl.Datetime)) return false;
        if (property.PropertyType.IsValueType) return Nullable.GetUnderlyingType(property.PropertyType) is null;
        return Nullability.Create(property).WriteState == NullabilityState.NotNull;
    }

    static Condition? ConditionOf(string? json) => string.IsNullOrWhiteSpace(json) ? null : Condition.Parse(json);

    /// <summary>集合的元素类型（字符串不算集合）；不是集合时为 null。</summary>
    static Type? ElementType(Type type)
    {
        if (type == typeof(string)) return null;
        if (type.IsArray) return type.GetElementType();
        return type.GetInterfaces().Append(type).FirstOrDefault(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];
    }

    static bool IsNumeric(Type type) => Type.GetTypeCode(type) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
        or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal || type == typeof(System.Numerics.BigInteger);

    /// <summary>当成一个值（不是一组字段）的类型。</summary>
    static bool IsScalar(Type type)
    {
        var core = Nullable.GetUnderlyingType(type) ?? type;
        return core.IsPrimitive || core.IsEnum || core == typeof(string) || core == typeof(decimal) || core == typeof(DateOnly) || core == typeof(DateTime)
               || core == typeof(DateTimeOffset) || core == typeof(TimeOnly) || core == typeof(TimeSpan) || core == typeof(Guid) || core == typeof(System.Numerics.BigInteger);
    }

    /// <summary>新的一行（列表、明细表加一行）或嵌套对象的初始值：按元素的类型新建（模型自己写的默认值照用）。</summary>
    public static object? NewItem(SchemaField field) => field.ItemType is { } type ? Activator.CreateInstance(type) : null;

    /// <summary>值是不是空的（null、空文字、空集合）：必填按它判断。</summary>
    public static bool IsBlank(object? value) => FormController.IsBlank(value);

    /// <summary>字段此刻的样子：权限 → 显示条件 → 只读（权限或条件）→ 必填（权限或条件；字段本身的必填另算）。read 按字段名取值。</summary>
    public static SchemaFieldState State(SchemaField field, SchemaFieldAccess? access, Func<string, object?> read)
    {
        bool Holds(Condition? condition) => condition is not null && condition.Matches(null, (_, name) => read(name));
        if (access == SchemaFieldAccess.Hidden || field.VisibleWhen is not null && !Holds(field.VisibleWhen)) return new SchemaFieldState(false, true, false);
        var readOnly = access == SchemaFieldAccess.Readonly || Holds(field.ReadonlyWhen);
        return new SchemaFieldState(true, readOnly, !readOnly && (access == SchemaFieldAccess.Required || Holds(field.RequiredWhen)));
    }

    /// <summary>
    /// 逐个走值里的字段（嵌套对象、列表逐行逐列），给出路径（同 FormController：<c>Contacts.0.Name</c>）、字段与它此刻的样子：
    /// 表单据此跳过隐藏字段的错误、补条件必填，SchemaFields 控件据此显示与只读。权限按去掉行号的路径找（<c>Contacts.Name</c>），列没写时跟着上一层。
    /// 条件里的字段名先在同一层找，再找顶层。
    /// </summary>
    public static void Walk(IReadOnlyList<SchemaField> fields, object values, IReadOnlyDictionary<string, SchemaFieldAccess> access,
        Action<string, SchemaField, SchemaFieldState> visit) => Walk(fields, values, access, visit, "", values, null);

    static void Walk(IReadOnlyList<SchemaField> fields, object values, IReadOnlyDictionary<string, SchemaFieldAccess> access,
        Action<string, SchemaField, SchemaFieldState> visit, string prefix, object scope, SchemaFieldAccess? inherited)
    {
        object? Read(string name) => HasMember(scope, name) ? FormController.Read(scope, name) : FormController.Read(values, name);
        foreach (var field in fields)
        {
            var path = prefix + field.Key;
            var own = access.TryGetValue(WithoutIndexes(path), out var written) ? written : inherited;
            var state = State(field, own, Read);
            visit(path, field, state);
            if (!state.Visible || field.Fields is not { } children) continue;
            var value = FormController.Read(scope, field.Key);
            if (field.Control is SchemaControl.List or SchemaControl.Table && value is IEnumerable rows and not string)
            {
                var index = 0;
                foreach (var row in rows)
                {
                    if (row is not null) Walk(children, values, access, visit, $"{path}.{index}.", row, own);
                    index++;
                }
            }
            else if (field.Control == SchemaControl.Object && value is not null) Walk(children, values, access, visit, $"{path}.", value, own);
        }
    }

    static bool HasMember(object scope, string name) => scope is IDictionary dictionary ? dictionary.Contains(name) : scope.GetType().GetProperty(name) is not null;

    static string WithoutIndexes(string path) => string.Join('.', path.Split('.').Where(part => !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)));

    /// <summary>只要这些顶层字段（按 pick 的顺序），或者去掉 omit 里的。</summary>
    public static IReadOnlyList<SchemaField> Pick(IReadOnlyList<SchemaField> fields, IReadOnlyList<string>? pick = null, IReadOnlyList<string>? omit = null) =>
        pick is not null ? [.. pick.SelectMany(key => fields.Where(field => field.Key == key))] : [.. fields.Where(field => omit?.Contains(field.Key) != true)];

    /// <summary>明细表一列的合计（写了 Sum 的列）：精确相加、带千分位与单位；没有能加的值时为 null。</summary>
    public static string? ColumnTotal(SchemaField column, IEnumerable rows)
    {
        if (!column.Sum) return null;
        decimal? sum = null;
        foreach (var row in rows)
            if (TableColumns.Numeric(row is null ? null : FormController.Read(row, column.Key)) is { } number) sum = (sum ?? 0) + number;
        if (sum is not { } total) return null;
        var text = total.ToString("#,##0.############################", CultureInfo.InvariantCulture);
        return column.Unit is { } unit ? $"{text} {unit}" : text;
    }

    /// <summary>
    /// 类型 → 条件构造器的字段（ConditionBuilder、FilterBar 的 ConditionFields；上游 schemaConditionFields）：标签、类型、可选值与表单、查询栏同一份描述。嵌套的对象、列表不能当条件。
    /// </summary>
    public static IReadOnlyList<ConditionField> ConditionFields(Type type, IReadOnlyList<string>? pick = null, IReadOnlyList<string>? omit = null) =>
        [.. Pick(Of(type), pick, omit).SelectMany(field => ConditionType(field.Control) is { } kind
            ? [new ConditionField(field.Key, field.Label, kind) { Group = field.Group, Options = field.Options?.Select(option => new ConditionOption(option.Value, option.Label)).ToList() }]
            : Array.Empty<ConditionField>())];

    static ConditionFieldType? ConditionType(SchemaControl control) => control switch
    {
        SchemaControl.Text or SchemaControl.Textarea => ConditionFieldType.Text,
        SchemaControl.Number => ConditionFieldType.Number,
        SchemaControl.Date or SchemaControl.Datetime => ConditionFieldType.Date,
        SchemaControl.Select or SchemaControl.Radio or SchemaControl.Multiselect or SchemaControl.Checkboxes => ConditionFieldType.Select,
        SchemaControl.Switch or SchemaControl.Checkbox => ConditionFieldType.Boolean,
        _ => null,
    };
}
