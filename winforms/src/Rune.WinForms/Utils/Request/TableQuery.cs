using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Rune.WinForms;

/// <summary>一页结果（vue-tsx-pro 的 TablePage）。</summary>
/// <typeparam name="T">行的类型。</typeparam>
/// <param name="Rows">这一页的行。</param>
/// <param name="Total">符合条件的总条数。</param>
public sealed record TablePage<T>(IReadOnlyList<T> Rows, int Total);

/// <summary>交给 fetcher 的一次查询（vue-tsx-pro 的 TableQuery）。</summary>
/// <typeparam name="TFilters">筛选条件的类型。</typeparam>
/// <param name="Page">第几页，从 1 开始。</param>
/// <param name="PageSize">每页条数。</param>
/// <param name="Sorts">排序，前面的优先；空是默认顺序。发给服务端时写成 <see cref="TableColumns.FormatSorts"/>（<c>-amount,name</c>）。</param>
/// <param name="ColumnFilters">表头筛选：列 key → 选中的值（关键字筛选时是 [关键字]），同 DataTable 的 Filters。</param>
/// <param name="Total">已知的总条数：只翻页、换排序或每页条数时是上一次结果的总数，服务端可以不再数行数；筛选条件、表头筛选、Params 变了，或查询、Refresh 时为 null。</param>
/// <param name="Filters">已应用的筛选条件。</param>
/// <param name="Params">筛选条件之外也决定结果的值（当前页签、数据源）。</param>
public sealed record TableQueryRequest<TFilters>(
    int Page, int PageSize, IReadOnlyList<SortState> Sorts, IReadOnlyDictionary<string, IReadOnlyList<string>> ColumnFilters, int? Total, TFilters Filters, object? Params)
{
    /// <summary>
    /// 写成接口的地址栏参数（vue-tsx-pro 的 tableParams，与 query 库的 parseTableRequest、Java 的 TableRequest.parse 一一对应）：
    /// page、pageSize；有排序时 sort=-amount,name；已知总数时 total；表头筛选里多选的列写成 filter.列=值（可重复），关键字的列（列的 Filter 是 Keyword，
    /// 要给 columns 才分得清）写成 search.列=关键字；筛选条件按字段名平铺（属性名，写了 JsonPropertyName 时用它；字典按键），值按线上的文字写
    /// （decimal 不用科学计数法、日期是 ISO），集合写成重复的参数，DateRange 写成两个同名参数，空值不写，条件树（Condition）写成 JSON 文字。
    /// 结果是有序的键值对，同名的键可以重复；拼成地址用 <see cref="TableQuery.QueryString"/>。
    /// </summary>
    /// <param name="columns">列定义：分得清哪些表头筛选是关键字；不给时都按多选。</param>
    public IReadOnlyList<KeyValuePair<string, string>> ToParams(IEnumerable<DataTableColumn>? columns = null)
    {
        var output = new List<KeyValuePair<string, string>>();
        void Add(string key, string value) => output.Add(new(key, value));
        Add("page", Wire(Page));
        Add("pageSize", Wire(PageSize));
        if (TableColumns.FormatSorts(Sorts) is { Length: > 0 } sort) Add("sort", sort);
        if (Total is { } total) Add("total", Wire(total));
        var keyword = (columns ?? []).Where(column => column.Filter?.IsKeyword == true).Select(column => column.Key).ToHashSet();
        foreach (var (key, values) in ColumnFilters)
        {
            var kept = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
            if (kept.Count == 0) continue;
            if (keyword.Contains(key)) Add($"search.{key}", kept[0]);
            else foreach (var value in kept) Add($"filter.{key}", value);
        }
        foreach (var (key, value) in TableQuery.FieldsOf(Filters))
            if (FilterParam(value) is { } items)
                foreach (var item in items) Add(key, item);
        return output;
    }

    /// <summary>筛选条件里的一个字段写成参数（一个或几个同名的）；空值为 null。</summary>
    static IReadOnlyList<string>? FilterParam(object? value)
    {
        switch (value)
        {
            case null or "":
                return null;
            case Condition condition:
                return [condition.ToJson()];
            case DateRange range:
                return [Wire(range.Start), Wire(range.End)];
            case string text:
                return [text];
            case IEnumerable items:
                List<string> texts = [.. items.Cast<object?>().Select(Wire)];
                return texts.All(text => text == "") ? null : texts;
            default:
                return [Wire(value)];
        }
    }

    /// <summary>线上的文字（上游的 wireText）：数字、日期按固定格式，与界面的语言无关。</summary>
    static string Wire(object? value) => value switch
    {
        null => "",
        string text => text,
        bool flag => flag ? "true" : "false",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime time => time.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset time => time.ToString("O", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}

/// <summary>服务端分页的列表状态，不管行与筛选条件的类型（QueryTable 读它）。</summary>
public interface ITableQuery : INotifyPropertyChanged
{
    /// <summary>第几页（从 1 开始）：改了立即请求。</summary>
    int Page { get; set; }

    /// <summary>每页条数：改了回到第一页。</summary>
    int PageSize { get; set; }

    /// <summary>排序的第一列（单列排序的表格绑它）：改了回到第一页；设置时换掉全部排序，null 是默认顺序。</summary>
    SortState? Sort { get; set; }

    /// <summary>全部排序，前面的优先（多列排序的表格绑它）：改了回到第一页。</summary>
    IReadOnlyList<SortState> Sorts { get; set; }

    /// <summary>表头筛选（DataTable 的 Filters，ManualFilter）：改了回到第一页。</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> ColumnFilters { get; set; }

    /// <summary>已应用的筛选条件。</summary>
    object? AppliedFilters { get; }

    /// <summary>应用新的筛选条件（查询栏的"查询"）：回到第一页，条件没变也重新请求（并重新数总数）。</summary>
    void Search(object? filters);

    /// <summary>当前页的行。</summary>
    IReadOnlyList<object> RowItems { get; }

    /// <summary>总条数；第一次结果回来之前为 0。</summary>
    int Total { get; }

    /// <summary>正在请求。</summary>
    bool IsLoading { get; }

    /// <summary>最后一次失败的原因；成功后清空。</summary>
    Exception? Error { get; }

    /// <summary>按当前条件重新请求（新建、删除之后），重新数总数。</summary>
    Task Refresh();
}

/// <summary>
/// 服务端分页的列表（vue-tsx-pro 的 useTableQuery，参考 ProTable 的 request）：页码、每页条数、排序、表头筛选、已应用的筛选条件、总数、加载与出错一起管。
/// <code>
/// var tasks = new TableQuery&lt;TaskRow, TaskFilters&gt;((query, cancellation) =&gt; api.Tasks(query.ToParams(columns), cancellation), new TaskFilters(), pageSize: 20);
/// _ = tasks.Refresh();   // 新建后取第一页
/// table.ItemsSource = tasks.Rows;  pagination.Page = tasks.Page;  pagination.Total = tasks.Total;   // 或交给 QueryTable
/// </code>
/// 换排序、每页条数、表头筛选或 <see cref="Params"/> 时回到第一页；删掉数据后当前页空了，退到最后一页。
/// 只翻页、换排序或条数时，交给 fetcher 的 Total 是上一次的总数，服务端可以不再数行数。
/// 只采用最后一次请求的结果，新的请求取消上一次；翻页期间保留当前这页的行。继续执行回到调用时的线程（界面的同步上下文），状态变了发 PropertyChanged。
/// 地址栏同步（上游的 syncToUrl）是浏览器的概念，这里没有。
/// </summary>
/// <typeparam name="T">行的类型。</typeparam>
/// <typeparam name="TFilters">筛选条件的类型（一个类或记录，属性是各个字段；也可以是字典）。</typeparam>
public sealed class TableQuery<T, TFilters> : ITableQuery
{
    readonly Func<TableQueryRequest<TFilters>, CancellationToken, Task<TablePage<T>>> fetcher;
    CancellationTokenSource? request;
    // 每次请求一个编号：结果回来时不是最后一次的就丢掉
    int latest;
    // 上一次结果的总数与它所属的条件（筛选条件、表头筛选、Params）：同一组条件里翻页、排序时交给服务端
    (string Scope, int Total)? known;
    // 构造函数里设初值时不请求
    readonly bool ready;

    /// <summary>新建：给出 fetcher 与一开始的条件；之后调一次 <see cref="Refresh"/> 取第一页。</summary>
    /// <param name="fetcher">取一页：查询条件、取消。</param>
    /// <param name="filters">一开始已应用的筛选条件。</param>
    /// <param name="pageSize">每页条数，默认 10（同 Pagination）。</param>
    /// <param name="sorts">一开始的排序。</param>
    /// <param name="columnFilters">一开始的表头筛选。</param>
    public TableQuery(Func<TableQueryRequest<TFilters>, CancellationToken, Task<TablePage<T>>> fetcher, TFilters filters,
        int pageSize = 10, IReadOnlyList<SortState>? sorts = null, IReadOnlyDictionary<string, IReadOnlyList<string>>? columnFilters = null)
    {
        this.fetcher = fetcher;
        Filters = filters;
        PageSize = pageSize;
        Sorts = sorts ?? [];
        ColumnFilters = columnFilters ?? new Dictionary<string, IReadOnlyList<string>>();
        ready = true;
    }

    /// <inheritdoc />
    public int Page
    {
        get;
        set
        {
            if (field == value) return;
            Set(ref field, value, nameof(Page));
            if (ready) _ = Load();
        }
    } = 1;

    /// <inheritdoc />
    public int PageSize
    {
        get;
        set
        {
            if (field == value) return;
            Set(ref field, value, nameof(PageSize));
            FirstPage();
        }
    }

    /// <inheritdoc />
    public SortState? Sort
    {
        get => Sorts.Count > 0 ? Sorts[0] : null;
        set => Sorts = value is { } one ? [one] : [];
    }

    /// <inheritdoc />
    public IReadOnlyList<SortState> Sorts
    {
        get;
        set
        {
            if (field.SequenceEqual(value)) return;
            Set(ref field, value, nameof(Sorts), nameof(Sort));
            FirstPage();
        }
    } = [];

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ColumnFilters
    {
        get;
        set
        {
            if (ReferenceEquals(field, value)) return;
            Set(ref field, value, nameof(ColumnFilters));
            FirstPage();
        }
    }

    /// <summary>已应用的筛选条件。</summary>
    public TFilters Filters { get; private set => Set(ref field, value, nameof(Filters)); }

    /// <summary>筛选条件之外也决定结果的值（当前页签、数据源）：变了回到第一页重新请求。</summary>
    public object? Params
    {
        get;
        set
        {
            if (Equals(field, value)) return;
            Set(ref field, value, nameof(Params));
            FirstPage();
        }
    }

    /// <summary>当前页的行；没有结果时为空。</summary>
    public IReadOnlyList<T> Rows => Data?.Rows ?? [];

    /// <inheritdoc />
    public int Total => Data?.Total ?? 0;

    /// <summary>最后一次成功的完整结果。</summary>
    public TablePage<T>? Data { get; private set => Set(ref field, value, nameof(Data), nameof(Rows), nameof(Total)); }

    /// <inheritdoc />
    public bool IsLoading { get; private set => Set(ref field, value, nameof(IsLoading)); }

    /// <inheritdoc />
    public Exception? Error { get; private set => Set(ref field, value, nameof(Error)); }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    object? ITableQuery.AppliedFilters => Filters;

    IReadOnlyList<object> ITableQuery.RowItems => Rows as IReadOnlyList<object> ?? [.. Rows.Cast<object>()];

    void ITableQuery.Search(object? filters) => Search((TFilters)filters!);

    /// <summary>应用新的筛选条件（查询栏的"查询"）：回到第一页，条件没变也重新请求（并重新数总数）。</summary>
    public void Search(TFilters filters)
    {
        known = null;
        Filters = filters;
        if (Page == 1) _ = Load();
        else Page = 1;
    }

    /// <inheritdoc />
    public Task Refresh()
    {
        known = null;
        return Load();
    }

    /// <summary>条件变了：回到第一页并请求（已在第一页时直接请求）。</summary>
    void FirstPage()
    {
        if (!ready) return;
        if (Page == 1) _ = Load();
        else Page = 1;
    }

    string Scope() => string.Join("\u001f", [.. TableQuery.FieldsOf(Filters).Select(pair => $"{pair.Key}={TableQuery.Describe(pair.Value)}"),
        .. ColumnFilters.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}:{string.Join(',', pair.Value)}"), TableQuery.Describe(Params)]);

    async Task Load()
    {
        latest++;
        request?.Cancel();
        var id = latest;
        var own = request = new CancellationTokenSource();
        var scope = Scope();
        var query = new TableQueryRequest<TFilters>(Page, PageSize, Sorts, ColumnFilters, known is { } last && last.Scope == scope ? last.Total : null, Filters, Params);
        IsLoading = true;
        try
        {
            var result = await fetcher(query, own.Token);
            if (id != latest) return;
            known = (scope, result.Total);
            Error = null;
            Data = result;
            // 删掉数据后当前页空了：退到最后一页（随即按新页码请求）
            var lastPage = Math.Max(1, (int)Math.Ceiling(result.Total / (double)query.PageSize));
            if (result.Rows.Count == 0 && query.Page > lastPage) Page = lastPage;
        }
        catch (OperationCanceledException) when (own.IsCancellationRequested || id != latest)
        {
        }
        catch (Exception error) when (id == latest)
        {
            Error = error;
        }
        finally
        {
            if (id == latest)
            {
                IsLoading = false;
                request = null;
            }
        }
    }

    void Set<TValue>(ref TValue field, TValue value, params string[] names)
    {
        if (EqualityComparer<TValue>.Default.Equals(field, value)) return;
        field = value;
        foreach (var name in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>TableQuery 的共用方法：读筛选条件的字段、拼地址栏参数。</summary>
public static class TableQuery
{
    /// <summary>把 <see cref="TableQueryRequest{TFilters}.ToParams"/> 的结果拼成地址栏的查询串（不带 ?）。</summary>
    public static string QueryString(IEnumerable<KeyValuePair<string, string>> parameters) =>
        string.Join('&', parameters.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

    /// <summary>筛选条件的各个字段：字典按键，对象按可读的公开属性（写了 JsonPropertyName 时用它作名字）；null 没有字段。</summary>
    public static IEnumerable<KeyValuePair<string, object?>> FieldsOf(object? filters)
    {
        switch (filters)
        {
            case null:
                yield break;
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary) yield return new(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? "", entry.Value);
                yield break;
        }
        foreach (var property in filters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0 || property.Name == "EqualityContract") continue;
            yield return new(property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name, property.GetValue(filters));
        }
    }

    /// <summary>一个值的文字（比较条件变没变用）：集合逐项写。</summary>
    internal static string Describe(object? value) => value switch
    {
        null => "",
        string text => text,
        Condition condition => condition.ToJson(),
        IEnumerable items => $"[{string.Join(',', items.Cast<object?>().Select(Describe))}]",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
