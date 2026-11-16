using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Rune.Avalonia;

/// <summary>什么时候开始校验（vue-tsx-pro 的 FormValidateTimings）；提交过一次之后总是随输入更新。</summary>
public enum FormValidateOn
{
    /// <summary>失焦后（默认）。</summary>
    Blur,

    /// <summary>输入时。</summary>
    Change,

    /// <summary>只在提交时。</summary>
    Submit,
}

/// <summary>字段从模型的校验特性推出的约束（vue-tsx-pro 的 FieldConstraints）：标签的必填标记、输入框的最大长度、数字的范围交给控件。</summary>
/// <param name="Required">必填（Required）。</param>
/// <param name="MinLength">最短（MinLength、StringLength 的 MinimumLength）。</param>
/// <param name="MaxLength">最长（MaxLength、StringLength）。</param>
/// <param name="Min">最小值（Range）。</param>
/// <param name="Max">最大值（Range）。</param>
public sealed record FieldConstraints(bool Required = false, int? MinLength = null, int? MaxLength = null, double? Min = null, double? Max = null);

/// <summary>登记过的字段（Field 写了 Name 时登记）：错误汇总据此显示字段名、跳到控件。</summary>
/// <param name="Label">标签的文字。</param>
/// <param name="Focus">把焦点移到这个字段的控件上。</param>
public sealed record FormFieldInfo(string Label, Action Focus);

/// <summary>单个字段的异步校验（vue-tsx-pro 的 FieldValidator）：只在这个字段（或它依赖的字段）变化时防抖运行，适合要请求接口的“用户名已占用”。</summary>
/// <typeparam name="T">表单的值的类型。</typeparam>
/// <param name="Validate">返回错误信息，通过时返回 null；值又变了、表单重置时取消。</param>
public sealed record FieldValidator<T>(Func<object?, T, CancellationToken, Task<string?>> Validate)
{
    /// <summary>值停止变化多久后运行，默认 300 毫秒。</summary>
    public TimeSpan Debounce { get; init; } = TimeSpan.FromMilliseconds(300);
}

/// <summary>
/// 提交时（OnSubmit 里）抛出：服务端回的校验失败。按字段的错误标到字段上，其余是整份表单的错误；和 SetError 一样，值改了就不再显示，下一次提交开始时清掉。
/// </summary>
/// <param name="fields">字段路径 → 错误。</param>
/// <param name="form">整份表单的错误。</param>
public sealed class FormSubmitException(IReadOnlyDictionary<string, string>? fields = null, string? form = null) : Exception(form ?? "表单提交没有通过")
{
    /// <summary>字段路径 → 错误。</summary>
    public IReadOnlyDictionary<string, string> Fields { get; } = fields ?? new Dictionary<string, string>();

    /// <summary>整份表单的错误。</summary>
    public string? Form { get; } = form;
}

/// <summary>数组字段的操作（vue-tsx-pro 的 FieldArray）：<see cref="FormController{T}.Array"/> 的返回值。错误、失焦记录跟着下标移动。</summary>
public sealed class FieldArray
{
    readonly Func<IList> list;
    readonly Func<List<string>> keys;
    readonly Action<Func<int, int?>> remap;
    readonly Action changed;

    internal FieldArray(Func<IList> list, Func<List<string>> keys, Action<Func<int, int?>> remap, Action changed)
    {
        this.list = list;
        this.keys = keys;
        this.remap = remap;
        this.changed = changed;
    }

    /// <summary>当前的各项（就是值里的列表）。</summary>
    public IList Items => list();

    /// <summary>每项一个稳定的 key：渲染列表时用，增删、移动后不变。</summary>
    public IReadOnlyList<string> Keys => keys();

    /// <summary>加到末尾。</summary>
    /// <param name="item">新的一项。</param>
    public void Append(object? item) => Insert(list().Count, item);

    /// <summary>插到 index 处。</summary>
    /// <param name="index">位置。</param>
    /// <param name="item">新的一项。</param>
    public void Insert(int index, object? item)
    {
        remap(old => old >= index ? old + 1 : old);
        keys().Insert(index, FormController.NextKey());
        list().Insert(index, item);
        changed();
    }

    /// <summary>删掉 index 处的一项。</summary>
    /// <param name="index">位置。</param>
    public void Remove(int index)
    {
        remap(old => old == index ? null : old > index ? old - 1 : old);
        keys().RemoveAt(index);
        list().RemoveAt(index);
        changed();
    }

    /// <summary>把 from 处的一项移到 to 处。</summary>
    /// <param name="from">原来的位置。</param>
    /// <param name="to">新的位置。</param>
    public void Move(int from, int to)
    {
        if (from == to) return;
        remap(old => old == from ? to : from < to && old > from && old <= to ? old - 1 : from > to && old >= to && old < from ? old + 1 : old);
        var key = keys()[from];
        var item = list()[from];
        keys().RemoveAt(from);
        list().RemoveAt(from);
        keys().Insert(to, key);
        list().Insert(to, item);
        changed();
    }
}

/// <summary>表单控制器里与值的类型无关的部分（vue-tsx-pro 的 FormControllerBase）：Form、Field、错误汇总只用到它。</summary>
public interface IFormController : INotifyPropertyChanged
{
    /// <summary>所有字段当前的校验错误（含还没显示的）；整份表单的错误在空字符串键上。</summary>
    IReadOnlyDictionary<string, string> Errors { get; }

    /// <summary>整份表单的错误（不属于哪个字段），提交过之后才有。</summary>
    string? FormError { get; }

    /// <summary>正在执行 OnSubmit。</summary>
    bool IsSubmitting { get; }

    /// <summary>提交过几次（不论成功与否）。</summary>
    int SubmitCount { get; }

    /// <summary>重置过几次：控件据此丢掉自己还没写回的输入。</summary>
    int ResetCount { get; }

    /// <summary>值与初始值（或上次 Reset 的值）不同。</summary>
    bool IsDirty { get; }

    /// <summary>登记过的字段，按登记的顺序。</summary>
    IReadOnlyList<KeyValuePair<string, FormFieldInfo>> Fields { get; }

    /// <summary>状态变了（值、错误、提交、重置）：Field、错误汇总据此更新。</summary>
    event EventHandler? Changed;

    /// <summary>字段（不写时为任一字段）的单字段校验正在进行。</summary>
    /// <param name="path">字段路径。</param>
    bool IsValidating(string? path = null);

    /// <summary>字段的错误是否该显示：失焦过、或提交过之后才显示。</summary>
    /// <param name="path">字段路径。</param>
    string? VisibleError(string path);

    /// <summary>字段失焦：Field 在焦点离开字段时调用。</summary>
    /// <param name="path">字段路径。</param>
    void Blur(string path);

    /// <summary>校验整份表单（给了 paths 时只看这些字段与它们的子路径，并立即显示它们的错误），返回是否通过。</summary>
    /// <param name="paths">只校验这些字段。</param>
    Task<bool> ValidateAsync(IReadOnlyList<string>? paths = null);

    /// <summary>提交：先校验，通过后调用 OnSubmit；返回是否提交成功（OnSubmit 抛 FormSubmitException 时为 false，别的异常原样抛出）。</summary>
    Task<bool> SubmitAsync();

    /// <summary>回到初始值，清空错误与失焦记录。</summary>
    void Reset();

    /// <summary>字段从模型的校验特性推出的约束。</summary>
    /// <param name="path">字段路径。</param>
    FieldConstraints Constraints(string path);

    /// <summary>字段的当前值。</summary>
    /// <param name="path">字段路径。</param>
    object? GetValue(string path);

    /// <summary>写字段的值（中途是 null 时什么也不做），随后按校验时机校验。</summary>
    /// <param name="path">字段路径。</param>
    /// <param name="value">新的值。</param>
    void SetValue(string path, object? value);

    /// <summary>登记字段（Field 写了 Name 时）；返回的对象 Dispose 时注销。</summary>
    /// <param name="path">字段路径。</param>
    /// <param name="field">标签与聚焦。</param>
    IDisposable Register(string path, FormFieldInfo field);

    /// <summary>模型的类型（SchemaForm 按它推出字段）。</summary>
    Type ModelType { get; }

    /// <summary>
    /// 字段此刻的样子（按 Access 与模型上 SchemaField 特性的条件）：不显示的不校验，SchemaForm 不渲染；路径里列表的行号照写（<c>Lines.0.Price</c>）。
    /// </summary>
    /// <param name="path">字段路径。</param>
    SchemaFieldState FieldState(string path);

    /// <summary>列表字段的增删、移动（SchemaForm 的可增删的行、明细表用）。</summary>
    /// <param name="path">字段路径。</param>
    FieldArray Array(string path);
}

/// <summary>FormController 的共用部分（数组项的 key、路径的读写、错误的文案）。</summary>
public static class FormController
{
    static int nextKey;

    internal static string NextKey() => $"item-{Interlocked.Increment(ref nextKey)}";

    /// <summary>按点连接的路径取值（属性名、列表下标）；中途是 null 时为 null。</summary>
    /// <param name="source">值。</param>
    /// <param name="path">路径（Address.City、Items.0.Name）。</param>
    public static object? Read(object? source, string path)
    {
        if (path.Length == 0) return source;
        var current = source;
        foreach (var key in path.Split('.'))
        {
            if (current is null) return null;
            current = current is IList list && int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                ? index < list.Count ? list[index] : null
                : current is IDictionary dictionary ? dictionary.Contains(key) ? dictionary[key] : null
                : current.GetType().GetProperty(key)?.GetValue(current);
        }
        return current;
    }

    /// <summary>按点连接的路径写值；中途是 null 时什么也不做。值的类型与属性不同时按属性的类型转换（文本框写回字符串、数字字段写回 decimal）。</summary>
    /// <param name="target">值。</param>
    /// <param name="path">路径。</param>
    /// <param name="value">新的值。</param>
    public static void Write(object target, string path, object? value)
    {
        var dot = path.LastIndexOf('.');
        var parent = dot < 0 ? target : Read(target, path[..dot]);
        var key = dot < 0 ? path : path[(dot + 1)..];
        switch (parent)
        {
            case null:
                return;
            case IList list when int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index):
                list[index] = value;
                return;
            case IDictionary dictionary:
                dictionary[key] = value;
                return;
        }
        if (parent.GetType().GetProperty(key) is not { CanWrite: true } property) return;
        property.SetValue(parent, Convert(value, property.PropertyType));
    }

    /// <summary>
    /// 把值转成 type：数字之间、文字与数字、枚举按值转；集合转成 List&lt;元素&gt; 或数组（复选组给的是只读列表，模型是 List）；
    /// null 给不可空的值类型时是它的默认值（勾选框的 IsChecked）。
    /// </summary>
    internal static object? Convert(object? value, Type type)
    {
        if (value is null) return type.IsValueType && Nullable.GetUnderlyingType(type) is null ? Activator.CreateInstance(type) : null;
        if (type.IsInstanceOfType(value)) return value;
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (value is string text && text.Length == 0 && target != typeof(string)) return null;
        if (value is IEnumerable items and not string && ElementType(target) is { } element)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
            foreach (var item in items) list.Add(Convert(item, element));
            if (!target.IsArray) return list;
            var array = System.Array.CreateInstance(element, list.Count);
            list.CopyTo(array, 0);
            return array;
        }
        // 日期控件的值是 ISO 文字（2026-10-21、2026-10-21T09:30），模型是 DateOnly、DateTime
        switch (value)
        {
            case string iso when target == typeof(DateOnly):
                return DateOnly.ParseExact(iso.Length > 10 ? iso[..10] : iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            case string iso when target == typeof(DateTime):
                return DateTime.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None);
            case DateOnly date when target == typeof(string):
                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            case DateTime time when target == typeof(string):
                return time.ToString(time.TimeOfDay == TimeSpan.Zero && time.Kind == DateTimeKind.Unspecified ? "yyyy-MM-dd" : "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        }
        return target.IsEnum ? Enum.Parse(target, value.ToString()!) : System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }

    /// <summary>List&lt;T&gt; 能赋给的集合类型（数组、List、IList、IReadOnlyList、IEnumerable…）的元素类型。</summary>
    static Type? ElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        if (!type.IsGenericType || type.GetGenericArguments() is not [var element]) return null;
        return type.IsAssignableFrom(typeof(List<>).MakeGenericType(element)) ? element : null;
    }

    /// <summary>两个值相同：Equals，或者是元素逐个相同的集合（控件与模型的列表常是不同的实例）。</summary>
    internal static bool SameValue(object? a, object? b) =>
        Equals(a, b) || a is IEnumerable left and not string && b is IEnumerable right and not string && left.Cast<object?>().SequenceEqual(right.Cast<object?>());

    /// <summary>空值：null、空字符串、空集合（必填的判断）。</summary>
    /// <param name="value">值。</param>
    public static bool IsBlank(object? value) => value switch
    {
        null => true,
        string text => text.Length == 0,
        ICollection collection => collection.Count == 0,
        _ => false,
    };

    /// <summary>
    /// 特性上写了提示：EmailAddress、Phone、Url 这类自带默认的英文提示（ErrorMessage 不为空），和新建的同类特性比，相同就算没写。
    /// </summary>
    static bool Written(ValidationAttribute attribute)
    {
        if (attribute.ErrorMessageResourceType is not null) return true;
        if (string.IsNullOrEmpty(attribute.ErrorMessage)) return false;
        return attribute.GetType().GetConstructor(Type.EmptyTypes) is not { } create || ((ValidationAttribute)create.Invoke(null)).ErrorMessage != attribute.ErrorMessage;
    }

    /// <summary>字段路径 path 是否在 scope 里：就是它，或是它的子路径（Address 包含 Address.City）。</summary>
    internal static bool InScope(string path, IEnumerable<string> scope) => scope.Any(prefix => path == prefix || path.StartsWith(prefix + ".", StringComparison.Ordinal));

    /// <summary>
    /// 校验特性没写 ErrorMessage 时的文案（同上游跟着语言包的 zod 报错）：必填是语言包的“必填”，长度、范围、格式按语言写出限制；写了 ErrorMessage 时用它。
    /// </summary>
    internal static string Message(ValidationAttribute attribute, string name, Lang lang)
    {
        if (Written(attribute)) return attribute.FormatErrorMessage(name);
        var zh = !lang.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return attribute switch
        {
            RequiredAttribute => lang.FormRequired,
            StringLengthAttribute { MinimumLength: > 0 } length => zh ? $"应为 {length.MinimumLength}～{length.MaximumLength} 个字符" : $"Must be {length.MinimumLength}–{length.MaximumLength} characters",
            StringLengthAttribute length => zh ? $"最多 {length.MaximumLength} 个字符" : $"At most {length.MaximumLength} characters",
            MaxLengthAttribute max => zh ? $"最多 {max.Length} 个" : $"At most {max.Length}",
            MinLengthAttribute min => zh ? $"至少 {min.Length} 个" : $"At least {min.Length}",
            RangeAttribute range => zh ? $"应在 {range.Minimum} 到 {range.Maximum} 之间" : $"Must be between {range.Minimum} and {range.Maximum}",
            EmailAddressAttribute => zh ? "邮箱格式不对" : "Invalid email address",
            PhoneAttribute => zh ? "电话号码格式不对" : "Invalid phone number",
            UrlAttribute => zh ? "网址格式不对" : "Invalid URL",
            _ => zh ? "格式不对" : "Invalid value",
        };
    }
}

/// <summary>
/// 表单控制器（vue-tsx-pro 的 useForm）：值、校验、错误、提交状态放在一个对象里，交给 Form；Field 写了 Name 时按它读错误、报告失焦。
/// <code>
/// var form = new FormController&lt;Signup&gt;(new Signup()) { OnSubmit = signup =&gt; api.SignupAsync(signup) };
/// &lt;rune:Form Controller="{Binding Form}"&gt; &lt;rune:Field Name="Email" Label="邮箱"&gt;&lt;rune:TextField /&gt;&lt;/rune:Field&gt; … &lt;/rune:Form&gt;
/// </code>
/// <list type="bullet">
/// <item>校验：模型上的 DataAnnotations 特性（Required、StringLength、Range、EmailAddress……，嵌套的对象与列表逐层校验，IValidatableObject 没写成员名的结果是整份表单的错误），
/// 加上 <see cref="Validate"/> 与按字段的异步校验 <see cref="FieldValidators"/>（防抖、只采用最后一次的结果、提交时等它完成）。特性没写 ErrorMessage 时文案跟着语言包。</item>
/// <item>何时显示错误（“迟一点批评、早一点肯定”）：字段失焦后才显示它的错误，之后随输入实时更新；提交过一次之后所有字段随输入实时更新（<see cref="ValidateOn"/>）。</item>
/// <item>值：<see cref="Values"/> 是初始值的一份深拷贝（JSON 往返，模型要能序列化），经 <see cref="SetValue"/> 写入（Field 自动绑定时就是这样）；直接改了 Values 时调 <see cref="Touch"/>。
/// 字段路径用点连接属性名与列表下标（Address.City、Items.0.Name）。</item>
/// <item>数组字段 <see cref="Array"/>、联动 <see cref="Dependencies"/>、局部校验 <see cref="ValidateAsync"/>（分步表单）、<see cref="SetError"/>（服务端的“邮箱已注册”）。</item>
/// </list>
/// 状态变了发 PropertyChanged 与 <see cref="Changed"/>；异步的继续执行回到调用时的线程（界面的同步上下文）。
/// </summary>
/// <typeparam name="T">值的类型（可序列化的类或记录）。</typeparam>
public sealed class FormController<T> : IFormController where T : class
{
    readonly Func<Lang> lang;
    Dictionary<string, string> errors = [];
    // 单字段校验的结果，与 errors 分开存：整份校验不会冲掉还在路上的单字段结果
    Dictionary<string, string?> fieldErrors = [];
    // SetError 给的错误与当时的值（JSON）：值一改就不再算
    Dictionary<string, (string Message, string Value)> externalErrors = [];
    HashSet<string> touched = [];
    readonly HashSet<string> pending = [];
    readonly Dictionary<string, (CancellationTokenSource? Timer, CancellationTokenSource? Running, Task? Done, string? Checked)> runs = [];
    readonly Dictionary<string, List<string>> arrayKeys = [];
    readonly List<KeyValuePair<string, FormFieldInfo>> fields = [];
    readonly HashSet<string> submitErrorPaths = [];
    string baseline;
    int run;
    Task<bool>? submission;

    /// <summary>新建：值是 initial 的一份深拷贝。</summary>
    /// <param name="initial">初始值；Reset 回到它。</param>
    /// <param name="lang">取语言包（错误文案）；不写时按当前界面语言。</param>
    public FormController(T initial, Func<Lang>? lang = null)
    {
        Initial = initial;
        this.lang = lang ?? (() => Lang.ForCulture(CultureInfo.CurrentUICulture));
        baseline = Snapshot(initial);
        Values = Clone(initial);
    }

    /// <summary>初始值。</summary>
    public T Initial { get; private set; }

    /// <summary>当前的值。</summary>
    public T Values { get; private set; }

    /// <summary>按模型上的 DataAnnotations 特性校验，默认是。</summary>
    public bool UseDataAnnotations { get; init; } = true;

    /// <summary>自定义校验：返回各字段的错误（空字符串键是整份表单的错误），与特性的结果合并（特性的优先）。</summary>
    public Func<T, Task<IReadOnlyDictionary<string, string>?>>? Validate { get; init; }

    /// <summary>按字段的异步校验：字段路径 → 校验。</summary>
    public IReadOnlyDictionary<string, FieldValidator<T>> FieldValidators { get; init; } = new Dictionary<string, FieldValidator<T>>();

    /// <summary>什么时候开始校验，默认失焦后。</summary>
    public FormValidateOn ValidateOn { get; init; } = FormValidateOn.Blur;

    /// <summary>字段联动：字段 → 它依赖的字段；依赖的值改了就重新校验它（它填过或失焦过时显示错误），它的单字段校验也重跑。</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Dependencies { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>校验通过后调用；任务进行中 IsSubmitting 为 true。</summary>
    public Func<T, Task>? OnSubmit { get; init; }

    /// <summary>
    /// 字段的权限（上游 useForm 的 access，按去掉行号的路径：<c>Lines.Price</c>）：Hidden 不显示、不校验，Readonly 只读、不算必填，Required 必填；
    /// 拿到当前的值（随环节、随人、随值变）。与模型上 SchemaField 特性的 VisibleWhen、RequiredWhen、ReadonlyWhen 一起算出字段此刻的样子（<see cref="FieldState"/>）。
    /// 换了一套权限时调 <see cref="Refresh"/>。
    /// </summary>
    public Func<T, IReadOnlyDictionary<string, SchemaFieldAccess>>? Access { get; init; }

    /// <inheritdoc />
    public Type ModelType => typeof(T);

    /// <inheritdoc />
    public SchemaFieldState FieldState(string path) => States().GetValueOrDefault(path, new SchemaFieldState(true, false, false));

    /// <summary>权限的来源（Access 读的外部状态：环节、角色）变了：通知字段与表单重新算。</summary>
    public void Refresh() => Notify(nameof(Values));

    /// <summary>全部字段此刻的样子（每次按当前的值与权限算；字段几十个，不缓存）。</summary>
    Dictionary<string, SchemaFieldState> States()
    {
        var states = new Dictionary<string, SchemaFieldState>();
        var access = Access?.Invoke(Values) ?? new Dictionary<string, SchemaFieldAccess>();
        SchemaModel.Walk(SchemaModel.Of(typeof(T)), Values, access, (path, _, state) => states[path] = state);
        return states;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Errors => Merged();

    /// <inheritdoc />
    public string? FormError => SubmitCount == 0 ? null : errors.GetValueOrDefault("") ?? External("");

    /// <inheritdoc />
    public bool IsSubmitting { get; private set => Set(ref field, value, nameof(IsSubmitting)); }

    /// <inheritdoc />
    public int SubmitCount { get; private set => Set(ref field, value, nameof(SubmitCount)); }

    /// <inheritdoc />
    public int ResetCount { get; private set => Set(ref field, value, nameof(ResetCount)); }

    /// <inheritdoc />
    public bool IsDirty => Snapshot(Values) != baseline;

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, FormFieldInfo>> Fields => fields;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool IsValidating(string? path = null) => path is null ? pending.Count > 0 : pending.Contains(path);

    /// <inheritdoc />
    public string? VisibleError(string path)
    {
        // 失焦过的字段，或局部校验过的字段（含子路径：ValidateAsync(["Address"]) 之后 Address.City 也显示）
        if (SubmitCount == 0 && !FormController.InScope(path, touched)) return null;
        return errors.GetValueOrDefault(path) ?? External(path) ?? fieldErrors.GetValueOrDefault(path);
    }

    /// <inheritdoc />
    public void Blur(string path)
    {
        if (!touched.Add(path)) return;
        if (ValidateOn != FormValidateOn.Submit) _ = Check(false);
        else Notify();
    }

    /// <inheritdoc />
    public Task<bool> ValidateAsync(IReadOnlyList<string>? paths = null)
    {
        if (paths is null) return Check(true);
        foreach (var path in paths) touched.Add(path);
        return Check(true, paths);
    }

    /// <inheritdoc />
    public Task<bool> SubmitAsync() => submission is { IsCompleted: false } ? submission : submission = Submit();

    // 提交进行中再次提交（连按回车）拿到同一个结果：SubmitAsync 复用还没完成的这一次（没有异步步骤时会同步完成，不能在这里面清掉）
    async Task<bool> Submit()
    {
        SubmitCount++;
        foreach (var path in submitErrorPaths) externalErrors.Remove(path);
        submitErrorPaths.Clear();
        // 等校验的时候值又变了（另一次校验接了手）：按最新的值再校验
        bool valid;
        int mine;
        do
        {
            mine = run + 1;
            valid = await Check(true);
        }
        while (mine != run);
        if (!valid || OnSubmit is null) return valid;
        IsSubmitting = true;
        try
        {
            await OnSubmit(Values);
        }
        catch (FormSubmitException error)
        {
            foreach (var (path, message) in error.Fields) Mark(path, message);
            if (error.Form is not null || error.Fields.Count == 0) Mark("", error.Form ?? error.Message);
            return false;
        }
        finally
        {
            IsSubmitting = false;
            Notify();
        }
        return true;

        void Mark(string path, string message)
        {
            SetError(path, message);
            submitErrorPaths.Add(path);
        }
    }

    /// <inheritdoc />
    public void Reset() => Reset(Initial);

    /// <summary>回到给定的值（之后 IsDirty 以它为准），清空错误与失焦记录；保存成功后写 Reset(form.Values)。</summary>
    /// <param name="next">新的初始值。</param>
    public void Reset(T next)
    {
        run++;
        CancelRuns();
        Initial = Clone(next);
        baseline = Snapshot(next);
        Values = Clone(next);
        arrayKeys.Clear();
        errors = [];
        externalErrors = [];
        touched = [];
        SubmitCount = 0;
        ResetCount++;
        Notify(nameof(Values), nameof(Errors), nameof(FormError), nameof(IsDirty));
    }

    /// <summary>字段与初始值不同。</summary>
    /// <param name="path">字段路径。</param>
    public bool IsFieldDirty(string path) => Json(FormController.Read(Values, path)) != Json(FormController.Read(JsonSerializer.Deserialize<T>(baseline), path));

    /// <inheritdoc />
    public object? GetValue(string path) => FormController.Read(Values, path);

    /// <inheritdoc />
    public void SetValue(string path, object? value)
    {
        FormController.Write(Values, path, value);
        Touch(path);
    }

    /// <summary>
    /// 某个字段的值变了（经 SetValue 写入时自动调；直接改了 Values 时手动调）：按校验时机重新校验，依赖它的字段联动，它的单字段校验防抖运行。
    /// </summary>
    /// <param name="path">字段路径。</param>
    public void Touch(string path)
    {
        var live = ValidateOn == FormValidateOn.Change || SubmitCount > 0 || (ValidateOn == FormValidateOn.Blur && touched.Count > 0);
        foreach (var (dependent, deps) in Dependencies)
        {
            if (!deps.Any(dep => FormController.InScope(path, [dep]) || FormController.InScope(dep, [path]))) continue;
            if (FormController.IsBlank(GetValue(dependent)) && !touched.Contains(dependent)) continue;
            touched.Add(dependent);
            if (ValidateOn != FormValidateOn.Submit || SubmitCount > 0) live = true;
            if (FieldValidators.TryGetValue(dependent, out var dependentValidator)) Debounce(dependent, dependentValidator);
        }
        if (FieldValidators.TryGetValue(path, out var validator)) Debounce(path, validator);
        if (live) _ = Check(false);
        else Notify(nameof(Values), nameof(IsDirty));
    }

    /// <summary>
    /// 设置某个字段的错误（如服务端返回的“邮箱已注册”），null 清除；空字符串是整份表单的错误。校验不会冲掉它，这个字段（整份表单的错误是任一字段）的值改了之后自动消失。
    /// </summary>
    /// <param name="path">字段路径。</param>
    /// <param name="message">错误。</param>
    public void SetError(string path, string? message)
    {
        if (message is null) externalErrors.Remove(path);
        else externalErrors[path] = (message, Json(path.Length == 0 ? Values : GetValue(path)));
        Notify(nameof(Errors), nameof(FormError));
    }

    /// <summary>数组字段的增删、插入、移动。</summary>
    /// <param name="path">值是列表的字段路径。</param>
    public FieldArray Array(string path) => new(
        () => GetValue(path) as IList ?? throw new InvalidOperationException($"字段 {path} 的值不是列表"),
        () =>
        {
            var count = (GetValue(path) as IList)?.Count ?? 0;
            if (!arrayKeys.TryGetValue(path, out var keys) || keys.Count != count)
                arrayKeys[path] = keys = [.. Enumerable.Range(0, count).Select(_ => FormController.NextKey())];
            return keys;
        },
        map => Remap(path, map),
        () => Touch(path));

    /// <inheritdoc />
    public FieldConstraints Constraints(string path)
    {
        if (Property(path) is not { } property) return new FieldConstraints();
        var attributes = property.GetCustomAttributes<ValidationAttribute>().ToList();
        var length = attributes.OfType<StringLengthAttribute>().FirstOrDefault();
        var range = attributes.OfType<RangeAttribute>().FirstOrDefault();
        return new FieldConstraints(
            attributes.OfType<RequiredAttribute>().Any(),
            length?.MinimumLength is > 0 ? length.MinimumLength : attributes.OfType<MinLengthAttribute>().FirstOrDefault()?.Length,
            length?.MaximumLength ?? attributes.OfType<MaxLengthAttribute>().FirstOrDefault()?.Length,
            Number(range?.Minimum),
            Number(range?.Maximum));
    }

    /// <summary>Range 的界限是数字时取它；Range(typeof(bool), "true", "true") 这类非数字的界限不是范围约束。</summary>
    static double? Number(object? bound) =>
        double.TryParse(System.Convert.ToString(bound, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <inheritdoc />
    public IDisposable Register(string path, FormFieldInfo field)
    {
        var entry = KeyValuePair.Create(path, field);
        fields.RemoveAll(item => item.Key == path);
        fields.Add(entry);
        return new Registration(() => fields.Remove(entry));
    }

    /// <summary>字段路径对应的属性（列表的下标跳过：Items.0.Name 是列表项类型的 Name）。</summary>
    PropertyInfo? Property(string path)
    {
        var type = typeof(T);
        PropertyInfo? property = null;
        foreach (var key in path.Split('.'))
        {
            if (int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                type = ItemType(type) ?? typeof(object);
                continue;
            }
            property = type.GetProperty(key);
            if (property is null) return null;
            type = property.PropertyType;
        }
        return property;
    }

    static Type? ItemType(Type type) => type.IsArray ? type.GetElementType()
        : type.GetInterfaces().Append(type).FirstOrDefault(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(IList<>))?.GetGenericArguments()[0];

    /// <summary>校验并记下结果（只采用最后一次）；返回是否通过。给了 scope 时只看 scope 里的字段，别的字段的错误保持原样。</summary>
    async Task<bool> Check(bool full, IReadOnlyList<string>? scope = null)
    {
        var current = ++run;
        var found = await Collect(full, scope);
        if (scope is null)
        {
            if (current == run) errors = found;
            Notify(nameof(Errors), nameof(FormError), nameof(IsDirty), nameof(Values));
            return found.Count == 0 && Merged().Count == 0;
        }
        bool InScope(string path) => FormController.InScope(path, scope);
        if (current == run)
            errors = errors.Where(pair => !InScope(pair.Key)).Concat(found.Where(pair => InScope(pair.Key))).ToDictionary();
        Notify(nameof(Errors), nameof(FormError));
        return !found.Keys.Any(InScope) && !Merged().Keys.Any(InScope);
    }

    /// <summary>算出整份值的错误：特性与 Validate 的；full 时单字段校验也立刻跑完（给了 scope 时只跑 scope 里的）。</summary>
    async Task<Dictionary<string, string>> Collect(bool full, IReadOnlyList<string>? scope)
    {
        var found = new Dictionary<string, string>();
        if (UseDataAnnotations) Annotate(Values, "", found, []);
        if (Validate is not null && await Validate(Values) is { } custom)
            foreach (var (path, message) in custom)
                if (!string.IsNullOrEmpty(message)) found.TryAdd(path, message);
        // 权限与条件：不显示的字段（及它下面的）不报错，条件必填、权限必填的空着报"必填"
        foreach (var (path, state) in States())
        {
            if (!state.Visible)
                foreach (var key in found.Keys.Where(key => key == path || key.StartsWith(path + ".", StringComparison.Ordinal)).ToList()) found.Remove(key);
            else if (state.Required && FormController.IsBlank(GetValue(path))) found.TryAdd(path, lang().FormRequired);
        }
        if (full)
            await Task.WhenAll(FieldValidators.Where(pair => scope is null || FormController.InScope(pair.Key, scope)).Select(pair => RunField(pair.Key, pair.Value)));
        return found;
    }

    /// <summary>按 DataAnnotations 逐层校验：属性上的特性、嵌套的对象与列表项、IValidatableObject（没写成员名的是整份表单的错误）。</summary>
    void Annotate(object target, string prefix, Dictionary<string, string> found, HashSet<object> seen)
    {
        if (!seen.Add(target)) return;
        var language = lang();
        foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            var path = prefix + property.Name;
            var value = property.GetValue(target);
            var name = property.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? property.Name;
            foreach (var attribute in property.GetCustomAttributes<ValidationAttribute>())
            {
                // 必填按空值判断（空字符串、空集合也算没填）；别的特性遇到空值不报（没填时只报“必填”）
                var failed = attribute is RequiredAttribute ? FormController.IsBlank(value) : !FormController.IsBlank(value) && !attribute.IsValid(value);
                if (failed) found.TryAdd(path, FormController.Message(attribute, name, language));
            }
            if (value is null || value is string || property.PropertyType.IsValueType) continue;
            if (value is IList list)
            {
                for (var index = 0; index < list.Count; index++)
                    if (list[index] is { } item && item is not string && !item.GetType().IsValueType) Annotate(item, $"{path}.{index}.", found, seen);
            }
            else if (value is not IEnumerable) Annotate(value, path + ".", found, seen);
        }
        if (target is IValidatableObject validatable)
            foreach (var result in validatable.Validate(new ValidationContext(target)))
            {
                var members = result.MemberNames.ToList();
                foreach (var member in members.Count == 0 ? [""] : members)
                    found.TryAdd(member.Length == 0 ? prefix.TrimEnd('.') : prefix + member, result.ErrorMessage ?? language.FormRequired);
            }
    }

    /// <summary>字段的单字段校验防抖运行（值停止变化 Debounce 之后）。</summary>
    void Debounce(string path, FieldValidator<T> validator)
    {
        var entry = runs.GetValueOrDefault(path);
        entry.Timer?.Cancel();
        var timer = new CancellationTokenSource();
        runs[path] = entry with { Timer = timer };
        _ = Later();

        async Task Later()
        {
            try
            {
                await Task.Delay(validator.Debounce, timer.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            await RunField(path, validator);
        }
    }

    /// <summary>
    /// 运行一个字段的校验：同一个值正在校验时等它；上次校验的就是这个值时用上次的结果；值变了就取消还在路上的那次（只采用最后一次的结果）。
    /// 校验本身失败（接口出错）时当作没有结论，不挡提交，写进跟踪日志。
    /// </summary>
    Task RunField(string path, FieldValidator<T> validator)
    {
        var entry = runs.GetValueOrDefault(path);
        // 依赖的字段也算进“这次校验的输入”：依赖变了，同一个值也要重新校验
        var input = Json(new[] { GetValue(path) }.Concat(Dependencies.GetValueOrDefault(path, []).Select(GetValue)).ToList());
        if (entry.Running is not null && entry.Done is not null && entry.Checked == input) return entry.Done;
        if (entry.Running is null && entry.Checked == input && fieldErrors.ContainsKey(path)) return Task.CompletedTask;
        entry.Running?.Cancel();
        var cancel = new CancellationTokenSource();
        pending.Add(path);
        Notify();
        var done = Go();
        runs[path] = entry with { Running = cancel, Done = done, Checked = input };
        return done;

        async Task Go()
        {
            try
            {
                var message = await validator.Validate(GetValue(path), Values, cancel.Token);
                if (cancel.IsCancellationRequested) return;
                fieldErrors[path] = message;
            }
            catch (Exception error) when (!cancel.IsCancellationRequested)
            {
                fieldErrors.Remove(path);
                System.Diagnostics.Trace.TraceWarning($"FormController：字段 {path} 的校验出错：{error}");
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (runs.TryGetValue(path, out var now) && now.Running == cancel)
                {
                    runs[path] = now with { Running = null };
                    pending.Remove(path);
                }
                Notify(nameof(Errors));
            }
        }
    }

    void CancelRuns()
    {
        foreach (var (_, entry) in runs)
        {
            entry.Timer?.Cancel();
            entry.Running?.Cancel();
        }
        runs.Clear();
        pending.Clear();
        fieldErrors = [];
    }

    /// <summary>数组字段的下标变了：错误、失焦记录、单字段校验的结果跟着移。</summary>
    void Remap(string prefix, Func<int, int?> map)
    {
        string? Shift(string key)
        {
            if (!key.StartsWith(prefix + ".", StringComparison.Ordinal)) return key;
            var rest = key[(prefix.Length + 1)..].Split('.');
            if (!int.TryParse(rest[0], NumberStyles.None, CultureInfo.InvariantCulture, out var index)) return key;
            return map(index) is { } next ? string.Join('.', [prefix, next.ToString(CultureInfo.InvariantCulture), .. rest[1..]]) : null;
        }
        Dictionary<string, TValue> Move<TValue>(Dictionary<string, TValue> source) =>
            source.Select(pair => (Key: Shift(pair.Key), pair.Value)).Where(pair => pair.Key is not null).ToDictionary(pair => pair.Key!, pair => pair.Value);
        errors = Move(errors);
        fieldErrors = Move(fieldErrors);
        externalErrors = Move(externalErrors);
        touched = [.. touched.Select(Shift).OfType<string>()];
    }

    /// <summary>特性与 Validate 的错误优先，再补上 SetError 与单字段校验的。</summary>
    Dictionary<string, string> Merged()
    {
        var merged = new Dictionary<string, string>(errors);
        foreach (var path in externalErrors.Keys)
            if (External(path) is { } message) merged.TryAdd(path, message);
        foreach (var (path, message) in fieldErrors)
            if (message is not null) merged.TryAdd(path, message);
        return merged;
    }

    /// <summary>SetError 给的错误，值改过就不算了；空字符串是整份表单的错误，任一字段改了都不算。</summary>
    string? External(string path) =>
        externalErrors.TryGetValue(path, out var entry) && Json(path.Length == 0 ? Values : GetValue(path)) == entry.Value ? entry.Message : null;

    static string Json(object? value) => JsonSerializer.Serialize(value);

    static string Snapshot(T value) => JsonSerializer.Serialize(value);

    static T Clone(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    void Set<TValue>(ref TValue field, TValue value, string name)
    {
        if (EqualityComparer<TValue>.Default.Equals(field, value)) return;
        field = value;
        Notify(name);
    }

    void Notify(params string[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    sealed class Registration(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
