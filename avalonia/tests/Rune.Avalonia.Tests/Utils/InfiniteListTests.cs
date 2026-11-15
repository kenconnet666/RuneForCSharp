namespace Rune.Avalonia.Tests;

/// <summary>InfiniteList 与 VirtualScroll：一页页取、按 key 去重、没有下一页时停；失败后 LoadMore 重试；Refresh 从第一页重来、丢掉过期的结果；接近末尾时取下一页；滚到某一项的对齐与可见范围。</summary>
public class InfiniteListTests
{
    /// <summary>假的接口：每页 3 项（第二页开头与第一页结尾重复），共 3 页；fail 时这一次失败。</summary>
    static Func<object?, CancellationToken, Task<InfinitePage<int>>> Pages(Func<int, bool>? fail = null, List<int>? calls = null) => (cursor, _) =>
    {
        var page = (int?)cursor ?? 1;
        calls?.Add(page);
        if (fail?.Invoke(page) == true) return Task.FromException<InfinitePage<int>>(new InvalidOperationException("网关超时"));
        var start = (page - 1) * 3 - (page > 1 ? 1 : 0);
        return Task.FromResult(new InfinitePage<int>([.. Enumerable.Range(start, 3)], page < 3 ? page + 1 : null));
    };

    [Fact]
    public async Task Pages_append_dedupe_and_stop_at_the_end()
    {
        var list = new InfiniteList<int>(Pages()) { KeyOf = item => item };
        await list.Refresh();
        Assert.Equal([0, 1, 2], list.Items);
        await list.LoadMore();
        await list.LoadMore();
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7], list.Items);
        Assert.False(list.HasMore);
        await list.LoadMore();
        Assert.Equal(8, list.Count);
    }

    [Fact]
    public async Task A_failed_page_stops_and_load_more_retries()
    {
        var failed = false;
        Exception? reported = null;
        var list = new InfiniteList<int>(Pages(page => page == 2 && !failed && (failed = true))) { KeyOf = item => item };
        list.Failed += (_, error) => reported = error;
        await list.Refresh();
        await list.LoadMore();
        Assert.Equal(("网关超时", false), (list.Error?.Message, list.IsLoading));
        Assert.Same(list.Error, reported);
        await list.LoadMore();
        Assert.Null(list.Error);
        Assert.Equal(5, list.Count);
    }

    [Fact]
    public async Task Refresh_drops_stale_results_and_range_changes_load_near_the_end()
    {
        var slow = new TaskCompletionSource<InfinitePage<int>>();
        var calls = 0;
        var list = new InfiniteList<int>((_, _) => ++calls == 1 ? slow.Task : Task.FromResult(new InfinitePage<int>([9], 2)));
        var first = list.Refresh();
        await list.Refresh();
        slow.SetResult(new InfinitePage<int>([1, 2, 3]));
        await first;
        Assert.Equal([9], list.Items);

        list.OnRangeChanged(new VisibleRange(0, 0));
        Assert.Equal(3, calls);
        list.IsEnabled = false;
        list.OnRangeChanged(new VisibleRange(0, 0));
        Assert.Equal(3, calls);
    }

    [Fact]
    public void Scroll_offsets_follow_the_alignment_and_ranges_use_binary_search()
    {
        Assert.Equal(100, VirtualScroll.OffsetFor(150, 40, 100, 200, 1000, ScrollAlign.Auto));
        Assert.Equal(150, VirtualScroll.OffsetFor(150, 40, 200, 200, 1000, ScrollAlign.Auto));
        Assert.Equal(90, VirtualScroll.OffsetFor(250, 40, 0, 200, 1000, ScrollAlign.Auto));
        Assert.Equal(70, VirtualScroll.OffsetFor(150, 40, 0, 200, 1000, ScrollAlign.Center));
        Assert.Equal(800, VirtualScroll.OffsetFor(950, 40, 0, 200, 1000, ScrollAlign.Start));

        double[] starts = [0, 40, 80, 120, 160];
        double[] ends = [40, 80, 120, 160, 200];
        Assert.Equal(new VisibleRange(1, 3), VirtualScroll.RangeOf(starts, ends, 50, 80));
        Assert.Equal(new VisibleRange(4, 4), VirtualScroll.RangeOf(starts, ends, 190, 80));
        Assert.Null(VirtualScroll.RangeOf([], [], 0, 80));
    }
}
