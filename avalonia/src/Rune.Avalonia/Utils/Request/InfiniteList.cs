using System.ComponentModel;

namespace Rune.Avalonia;

/// <summary>一页的结果（vue-tsx-pro 的 InfinitePage）：项接在已有的后面；Next 是取下一页的游标，没有下一页时为 null。</summary>
/// <typeparam name="T">项的类型。</typeparam>
/// <param name="Items">这一页的项。</param>
/// <param name="Next">下一页的游标（页码、偏移、服务端给的 cursor）；null 表示没有更多了。</param>
public sealed record InfinitePage<T>(IReadOnlyList<T> Items, object? Next = null);

/// <summary>滚动加载的列表的状态，不管项的类型（InfiniteListStatus 读它）。</summary>
public interface IInfiniteList : INotifyPropertyChanged
{
    /// <summary>已经取到几项。</summary>
    int Count { get; }

    /// <summary>正在取（第一页或下一页）。</summary>
    bool IsLoading { get; }

    /// <summary>最后一页失败的原因；再取时清空。</summary>
    Exception? Error { get; }

    /// <summary>第一页回来过：之前没有项不代表没有数据。</summary>
    bool IsLoaded { get; }

    /// <summary>还有下一页。</summary>
    bool HasMore { get; }

    /// <summary>取下一页（还没取到第一页时取第一页）；正在取、没有下一页时什么也不做。失败后用它重试。</summary>
    Task LoadMore();
}

/// <summary>
/// 滚动加载的列表（vue-tsx-pro 的 useInfiniteList，参考 TanStack Query 的 useInfiniteQuery）：一页一页取、接在已有的后面，
/// 按 <see cref="KeyOf"/> 去掉重复的项（翻页期间有新数据插进来时，下一页开头会和上一页结尾重复），记住还有没有下一页。
/// <code>
/// var orders = new InfiniteList&lt;Order&gt;(async (cursor, cancellation) =&gt;
/// {
///     var page = (int?)cursor ?? 1;
///     var result = await api.Orders(keyword, page, cancellation);
///     return new InfinitePage&lt;Order&gt;(result.Rows, page * 20 &lt; result.Total ? page + 1 : null);
/// }) { KeyOf = order =&gt; order.Id };
/// list.ItemsSource = orders.Items;  list.RangeChanged += (_, range) =&gt; orders.OnRangeChanged(range);
/// keyword.TextChanged += (_, _) =&gt; orders.Restart(TimeSpan.FromMilliseconds(300));   // 搜索词变了：防抖后从第一页重来
/// </code>
/// 游标是取下一页用的任何东西，第一页时是 null。<see cref="Refresh"/>（或 <see cref="Restart"/> 的防抖之后）从第一页重来：先清空、取消进行中的，只采用最后一次的结果。
/// <see cref="OnRangeChanged"/> 接到 VirtualList 的 RangeChanged：可见范围离末尾不到 <see cref="Threshold"/> 项时取下一页；失败时停在那里，再滚动或调用 <see cref="LoadMore"/> 时接着取。
/// 新建后要调一次 <see cref="Refresh"/> 取第一页（上游的 immediate）。继续执行回到调用时的线程（界面的同步上下文），状态变了发 PropertyChanged。
/// </summary>
/// <typeparam name="T">项的类型。</typeparam>
/// <param name="fetchPage">取一页：游标（第一页是 null）、取消。</param>
public sealed class InfiniteList<T>(Func<object?, CancellationToken, Task<InfinitePage<T>>> fetchPage) : IInfiniteList
{
    HashSet<object> seen = [];
    object? cursor;
    CancellationTokenSource? request;
    CancellationTokenSource? waiting;
    // 每次请求一个编号：结果回来时不是最后一次的就丢掉（fetchPage 没理会取消时也不会接错）
    int latest;

    /// <summary>每项的 key：同一个 key 只留第一次出现的那项；不写时不去重。</summary>
    public Func<T, object>? KeyOf { get; init; }

    /// <summary>可见范围离末尾不到几项时取下一页，默认 5。</summary>
    public int Threshold { get; init; } = 5;

    /// <summary>自动请求的开关（弹出层打开时才请求这类）：关着时滚动不请求，Refresh、LoadMore 照常。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>已经取到的全部项（去重后；每次变了换一个新的列表，按引用就能看出变没变）。</summary>
    public IReadOnlyList<T> Items { get; private set => Set(ref field, value, nameof(Items), nameof(Count)); } = [];

    /// <inheritdoc />
    public int Count => Items.Count;

    /// <inheritdoc />
    public bool IsLoading { get; private set => Set(ref field, value, nameof(IsLoading)); }

    /// <inheritdoc />
    public Exception? Error { get; private set => Set(ref field, value, nameof(Error)); }

    /// <inheritdoc />
    public bool IsLoaded { get; private set => Set(ref field, value, nameof(IsLoaded)); }

    /// <inheritdoc />
    public bool HasMore { get; private set => Set(ref field, value, nameof(HasMore)); } = true;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>一页失败后（取消的不算）。</summary>
    public event EventHandler<Exception>? Failed;

    /// <inheritdoc />
    public async Task LoadMore()
    {
        // 参数刚变、防抖还在等：这时取的是旧参数的下一页，等着的刷新也会被它取消
        if (waiting is not null || IsLoading || (IsLoaded && !HasMore)) return;
        await Run(!IsLoaded);
    }

    /// <summary>从第一页重来：清空、取消进行中的。</summary>
    public Task Refresh() => Run(true);

    /// <summary>参数（搜索词、筛选）变了：等 delay 没有新的变化后从第一页重来（防抖）；期间不取下一页。</summary>
    /// <param name="delay">防抖的时长。</param>
    public async void Restart(TimeSpan delay)
    {
        waiting?.Cancel();
        var own = waiting = new CancellationTokenSource();
        try
        {
            await Task.Delay(delay, own.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        waiting = null;
        await Run(true);
    }

    /// <summary>取消进行中的请求与等着的防抖。</summary>
    public void Cancel()
    {
        waiting?.Cancel();
        waiting = null;
        latest++;
        request?.Cancel();
        request = null;
        IsLoading = false;
    }

    /// <summary>交给 VirtualList 的 RangeChanged：接近末尾时取下一页。</summary>
    /// <param name="range">可见范围。</param>
    public void OnRangeChanged(VisibleRange range)
    {
        if (IsEnabled && range.EndIndex >= Items.Count - Threshold) _ = LoadMore();
    }

    async Task Run(bool first)
    {
        Cancel();
        var id = latest;
        var own = request = new CancellationTokenSource();
        if (first)
        {
            Items = [];
            seen = [];
            cursor = null;
            IsLoaded = false;
            HasMore = true;
        }
        IsLoading = true;
        Error = null;
        try
        {
            var page = await fetchPage(cursor, own.Token);
            if (id != latest) return;
            Items = [.. Items, .. Unseen(page.Items)];
            cursor = page.Next;
            HasMore = page.Next is not null;
            IsLoaded = true;
        }
        catch (OperationCanceledException) when (own.IsCancellationRequested || id != latest)
        {
        }
        catch (Exception error) when (id == latest)
        {
            Error = error;
            Failed?.Invoke(this, error);
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

    /// <summary>第一页之后去掉见过的项。</summary>
    IEnumerable<T> Unseen(IReadOnlyList<T> page) => KeyOf is { } key ? page.Where(item => seen.Add(key(item))) : page;

    void Set<TValue>(ref TValue field, TValue value, params string[] names)
    {
        if (EqualityComparer<TValue>.Default.Equals(field, value)) return;
        field = value;
        foreach (var name in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
