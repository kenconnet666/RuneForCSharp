namespace Rune.WinForms.Tests;

/// <summary>VirtualListControl 与 InfiniteListStatusControl：只建看得见的项、带第几项共几项；数据变了重排；没有数据时是组、显示空状态；随外层滚动；滚动方法与可见范围；量出的实际大小；加载中的骨架；失败时重试、取完了“没有更多了”。</summary>
public class VirtualListTests
{
    /// <summary>每项高 size 逻辑像素的控件，读屏名称是文字。</summary>
    static VirtualListControl Create(int count, int height = 200, Func<int, int>? size = null)
    {
        var list = new VirtualListControl
        {
            ItemsSource = Enumerable.Range(0, count).ToList(),
            Width = 300,
        };
        list.Height = list.LogicalToDeviceUnits(height);
        list.ItemFactory = item => new Control { AccessibleName = $"第 {item} 项", Height = list.LogicalToDeviceUnits(size?.Invoke((int)item!) ?? 40) };
        return list;
    }

    [Fact]
    [Upstream("VirtualList", "只渲染可见范围加 overscan 的项；每项带 aria-setsize 与 aria-posinset")]
    public Task Only_visible_items_are_realized_with_set_positions() => WinFormsUi.Run(() =>
    {
        var list = Create(10000);
        list.AccessibleName = "联系人";
        using var form = WinFormsUi.Show(list);

        // 5 项看得见，后面多建 4 项
        Assert.Equal(Enumerable.Range(0, 9), list.RealizedItems.Keys.Order());
        Assert.Equal(AccessibleRole.List, list.AccessibilityObject.Role);
        var first = list.RealizedItems[0];
        Assert.Equal((AccessibleRole.ListItem, "第 0 项", "第 1 项，共 10000 项"), (first.AccessibleRole, first.AccessibleName, first.AccessibleDescription));
        Assert.Equal(list.LogicalToDeviceUnits(40) * 10000, list.TotalSize);
        Assert.Equal(list.Height, list.ItemsPart.Height);
    });

    [Fact]
    [Upstream("VirtualList", "数据变了重新排：总大小跟着变")]
    public Task Changing_the_data_relayouts() => WinFormsUi.Run(() =>
    {
        var list = Create(100);
        using var form = WinFormsUi.Show(list);
        var before = list.TotalSize;

        list.ItemsSource = Enumerable.Range(0, 200).ToList();
        Assert.Equal(2 * before, list.TotalSize);
        Assert.Equal("第 1 项，共 200 项", list.RealizedItems[0].AccessibleDescription);
    });

    [Fact]
    [Upstream("VirtualList", "没有数据时显示 empty，不是列表")]
    public Task Without_data_it_shows_empty_as_a_group() => WinFormsUi.Run(() =>
    {
        var list = Create(0);
        list.Empty = new TextControl { Text = "还没有联系人" };
        using var form = WinFormsUi.Show(list);

        Assert.True(list.Empty.Visible);
        Assert.Equal(AccessibleRole.Grouping, list.AccessibilityObject.Role);
    });

    [Fact]
    [Upstream("VirtualList", "window 模式不是滚动容器")]
    public Task Window_mode_follows_the_outer_scroll() => WinFormsUi.Run(() =>
    {
        var list = Create(500);
        list.Scroll = VirtualListScroll.Window;
        var page = new ScrollAreaControl { Width = 300, Height = 300, Content = list };
        using var form = WinFormsUi.Show(page);

        Assert.Same(page, list.Viewer);
        Assert.Equal(list.LogicalToDeviceUnits(40) * 500, list.Height);
        Assert.InRange(list.RealizedItems.Count, 8, 16);
        list.ScrollToIndex(400, ScrollAlign.Start);
        Assert.Equal(list.LogicalToDeviceUnits(40) * 400, page.ScrollOffset.Y);
        Assert.Contains(400, list.RealizedItems.Keys);
    });

    [Fact]
    [Upstream("VirtualList", "handle 拿到滚动方法；onRangeChange 报告可见范围")]
    public Task Scroll_methods_and_range_changes() => WinFormsUi.Run(() =>
    {
        var list = Create(1000);
        var ranges = new List<VisibleRange>();
        list.RangeChanged += (_, range) => ranges.Add(range);
        using var form = WinFormsUi.Show(list);
        Assert.Equal(new VisibleRange(0, 4), ranges[^1]);

        list.ScrollToIndex(500, ScrollAlign.Start);
        Assert.Equal(list.LogicalToDeviceUnits(40) * 500, list.Viewer!.ScrollOffset.Y);
        Assert.Equal(500, ranges[^1].StartIndex);
        Assert.DoesNotContain(0, list.RealizedItems.Keys);
        list.ScrollToOffset(list.LogicalToDeviceUnits(80));
        Assert.Equal(new VisibleRange(2, 6), ranges[^1]);
    });

    [Fact]
    public Task Measured_sizes_replace_the_estimate() => WinFormsUi.Run(() =>
    {
        var list = Create(100, size: index => index % 2 == 0 ? 40 : 80);
        using var form = WinFormsUi.Show(list);

        var item = list.RealizedItems[1];
        Assert.Equal((list.LogicalToDeviceUnits(40), list.LogicalToDeviceUnits(80)), (item.Top, item.Height));
        Assert.Equal(list.LogicalToDeviceUnits(120), list.RealizedItems[2].Top);
    });

    [Fact]
    [Upstream("VirtualList", "只用骨架：没有数据时铺几行")]
    public Task Loading_uses_skeleton_rows() => WinFormsUi.Run(() =>
    {
        var empty = Create(0);
        empty.IsLoading = true;
        var more = Create(3);
        more.IsLoading = true;
        more.Left = 320;
        using var form = WinFormsUi.Show(empty, more);

        WinFormsUi.Until(() => empty.Placeholders.Skeleton.Visible && more.Placeholders.More.Visible);
        Assert.Equal(5, empty.Placeholders.Skeleton.Controls.Count);
        Assert.Equal(2, more.Placeholders.More.Controls.Count);
        Assert.False(more.Placeholders.Skeleton.Visible);
    });

    [Fact]
    public Task The_status_retries_failures_and_says_when_done() => WinFormsUi.Run(() =>
    {
        var fail = true;
        var list = new InfiniteList<int>((_, _) => fail
            ? Task.FromException<InfinitePage<int>>(new InvalidOperationException("网关超时"))
            : Task.FromResult(new InfinitePage<int>([1, 2])));
        var status = new InfiniteListStatusControl { List = list, Width = 300 };
        using var form = WinFormsUi.Show(status);
        _ = list.Refresh();
        WinFormsUi.Pump();

        Assert.True(status.Parts.Error.Visible && status.Parts.Retry.Visible);
        Assert.Equal("加载失败，请重试", status.Parts.Error.Text);
        fail = false;
        status.Parts.Retry.PerformClick();
        WinFormsUi.Pump();
        Assert.False(status.Parts.Retry.Visible);
        Assert.True(status.Parts.NoMore.Visible);
        Assert.Equal("没有更多了", status.Parts.NoMore.Text);
    });
}
