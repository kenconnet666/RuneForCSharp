using System.Collections;

namespace Rune.Avalonia;

/// <summary>
/// 远程选项的加载函数（vue-tsx-pro 的 OptionsLoader）：搜索词、第几页（从 1 开始）→ 一页选项。
/// <paramref name="cancellation"/> 在被后来的请求顶掉、弹出层关上时取消。
/// </summary>
/// <param name="query">搜索词（没输入时是空字符串）。</param>
/// <param name="page">第几页，从 1 开始；不分页时只会是 1。</param>
/// <param name="cancellation">取消。</param>
public delegate Task<OptionsPage> OptionsLoader(string query, int page, CancellationToken cancellation);

/// <summary>一页远程选项：数据（同 ItemsSource 的项）与下一页的页码（没有更多时为 null）。</summary>
/// <param name="Items">这一页的数据。</param>
/// <param name="Next">下一页的页码；null 时没有更多。</param>
public sealed record OptionsPage(IReadOnlyList<object?> Items, int? Next = null)
{
    /// <summary>不分页：全部选项就是这些。</summary>
    /// <param name="items">数据。</param>
    public static OptionsPage All(IEnumerable items) => new([.. items.Cast<object?>()]);
}

/// <summary>
/// 远程选项的状态（vue-tsx-pro 的 remote-items）：弹出层打开时按当前搜索词立即请求，之后搜索词变了停下 300 毫秒再请求；
/// 只采用最后一次的结果，结果代替本地的数据。弹出层关着不请求，关上时取消还在进行的。第一次结果回来之前用本地的数据
/// （编辑页把已选的那一项放进 ItemsSource，触发框一开始就有文字）。分页时列表滚到离末尾不到 5 项取下一页，接在后面（按值去掉重复的）。
/// 状态变了调 <c>changed</c>；继续执行回到调用时的线程（界面的同步上下文）。
/// </summary>
/// <param name="key">数据的值（去重用）。</param>
/// <param name="changed">选项、加载中、失败变了。</param>
sealed class RemoteOptions(Func<object?, string> key, Action changed)
{
    /// <summary>搜索词停下多久再请求（毫秒）。</summary>
    public const int Debounce = 300;

    /// <summary>离末尾不到几项时取下一页。</summary>
    public const int Threshold = 5;

    // 每次换一个新的列表：使用方按引用判断选项变没变
    IReadOnlyList<object?> items = [];
    readonly HashSet<string> keys = [];
    CancellationTokenSource? pending;
    int? next;
    bool loaded;
    bool open;
    string query = "";

    /// <summary>加载函数；null 时原样用本地的数据。</summary>
    public OptionsLoader? Load { get; set; }

    /// <summary>正在取第一页（列表显示骨架）。</summary>
    public bool Loading { get; private set; }

    /// <summary>正在取下一页（骨架接在已有的选项后面）。</summary>
    public bool LoadingMore { get; private set; }

    /// <summary>最后一次请求失败了。</summary>
    public bool Failed { get; private set; }

    /// <summary>当前的选项：请求过就是请求到的（取第一页时是空的），之前是本地的数据。</summary>
    /// <param name="local">本地的数据。</param>
    public IReadOnlyList<object?> ItemsOr(IReadOnlyList<object?> local) => Load is not null && (loaded || Loading) ? items : local;

    /// <summary>弹出层开关：打开时立即按当前搜索词从第一页请求（不等防抖），关上时取消。</summary>
    /// <param name="value">打开着。</param>
    public void SetOpen(bool value)
    {
        if (open == value) return;
        open = value;
        if (Load is null) return;
        if (open) _ = Fetch(1, 0);
        else Cancel();
    }

    /// <summary>搜索词变了：打开着时停下 300 毫秒再请求。</summary>
    /// <param name="value">搜索词。</param>
    public void SetQuery(string value)
    {
        if (query == value) return;
        query = value;
        if (Load is not null && open) _ = Fetch(1, Debounce);
    }

    /// <summary>列表显示到了第几项：离末尾不到 <see cref="Threshold"/> 项、还有下一页时取下一页。</summary>
    /// <param name="last">看得见的最后一项的序号。</param>
    public void Reached(int last)
    {
        if (Load is null || !open || Loading || LoadingMore || next is not { } page || last < items.Count - Threshold) return;
        _ = Fetch(page, 0);
    }

    async Task Fetch(int page, int delay)
    {
        Cancel();
        var source = pending = new CancellationTokenSource();
        var token = source.Token;
        try
        {
            if (delay > 0) await Task.Delay(delay, token);
            if (page == 1)
            {
                items = [];
                keys.Clear();
                next = null;
                Loading = true;
            }
            else LoadingMore = true;
            Failed = false;
            changed();
            var result = await Load!(query, page, token);
            if (token.IsCancellationRequested) return;
            items = [.. items, .. result.Items.Where(item => keys.Add(key(item)))];
            next = result.Next;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            // 失败的原因交给使用方的加载函数记录；列表里只说"加载失败，请重试"
            if (token.IsCancellationRequested) return;
            Failed = true;
        }
        finally
        {
            if (pending == source)
            {
                loaded = true;
                Loading = LoadingMore = false;
                changed();
            }
        }
    }

    void Cancel()
    {
        if (pending is null) return;
        pending.Cancel();
        pending = null;
        if (!Loading && !LoadingMore) return;
        Loading = LoadingMore = false;
        changed();
    }
}
