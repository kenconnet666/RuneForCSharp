using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Rune.Avalonia.Tests;

/// <summary>VirtualList 与 InfiniteListStatus：只建看得见的项、带同级位置与总数；数据变了重排；没有数据时是组、显示空状态；随外层滚动时视口不可聚焦；滚动方法与可见范围；加载中的骨架；失败时重试、取完了“没有更多了”。</summary>
public class VirtualListTests
{
    static VirtualList Create(int count, double height = 200) => new()
    {
        ItemsSource = Enumerable.Range(0, count).Select(index => $"第 {index} 项").ToList(),
        ItemTemplate = new FuncDataTemplate<string>((text, _) => new TextBlock { Text = text, Height = 40 }),
        Height = height,
        Width = 300,
    };

    [Fact]
    [Upstream("VirtualList", "只渲染可见范围加 overscan 的项；每项带 aria-setsize 与 aria-posinset")]
    public Task Only_visible_items_are_realized_with_set_positions() => HeadlessUi.Run(() =>
    {
        var list = Create(10000);
        AutomationProperties.SetName(list, "联系人");
        HeadlessUi.Flush(HeadlessUi.Show(list));

        var realized = list.RealizedItems.ToList();
        Assert.InRange(realized.Count, 5, 30);
        Assert.Equal((1, 10000), (AutomationProperties.GetPositionInSet(realized[0]), AutomationProperties.GetSizeOfSet(realized[0])));
        Assert.Equal((AutomationControlType.List, "联系人"), (list.Peer().GetAutomationControlType(), list.Peer().GetName()));
        Assert.Equal(AutomationControlType.ListItem, realized[0].Peer().GetAutomationControlType());
    });

    [Fact]
    [Upstream("VirtualList", "数据变了重新排：总大小跟着变")]
    public Task Changing_the_data_relayouts() => HeadlessUi.Run(() =>
    {
        var list = Create(100);
        var window = HeadlessUi.Show(list);
        HeadlessUi.Flush(window);
        var before = list.ScrollPart.Extent.Height;

        list.ItemsSource = Enumerable.Range(0, 200).Select(index => $"第 {index} 项").ToList();
        HeadlessUi.Flush(window);
        Assert.Equal(2 * before, list.ScrollPart.Extent.Height, 1);
        Assert.Equal(200, AutomationProperties.GetSizeOfSet(list.RealizedItems.First()));
    });

    [Fact]
    [Upstream("VirtualList", "没有数据时显示 empty，不是列表")]
    public Task Without_data_it_shows_empty_as_a_group() => HeadlessUi.Run(() =>
    {
        var list = Create(0);
        list.Empty = new Empty { Description = "还没有联系人" };
        AutomationProperties.SetName(list, "联系人");
        HeadlessUi.Flush(HeadlessUi.Show(list));

        Assert.True(list.Placeholders.Empty.IsVisible);
        Assert.Equal((AutomationControlType.Group, "联系人"), (list.Peer().GetAutomationControlType(), list.Peer().GetName()));
    });

    [Fact]
    [Upstream("VirtualList", "window 模式不是滚动容器")]
    public Task Window_mode_follows_the_outer_scroll() => HeadlessUi.Run(() =>
    {
        var list = Create(500);
        list.Height = double.NaN;
        list.Scroll = VirtualListScroll.Window;
        var page = new ScrollArea { Height = 300, Content = new StackPanel { Children = { new TextBlock { Text = "页面开头", Height = 100 }, list } } };
        var window = HeadlessUi.Show(page);
        HeadlessUi.Flush(window);

        Assert.Null(list.ScrollPart.Parent);
        Assert.InRange(list.RealizedItems.Count(), 3, 30);
        Assert.True(page.Extent.Height > 500 * 40 * 0.9, $"{page.Extent.Height}");
    });

    [Fact]
    [Upstream("VirtualList", "handle 拿到滚动方法；onRangeChange 报告可见范围")]
    public Task Scroll_methods_and_range_changes() => HeadlessUi.Run(() =>
    {
        var list = Create(1000);
        var ranges = new List<VisibleRange>();
        list.RangeChanged += (_, range) => ranges.Add(range);
        var window = HeadlessUi.Show(list);
        HeadlessUi.Flush(window);
        Assert.Equal(new VisibleRange(0, 4), ranges[^1]);

        list.ScrollToIndex(500, ScrollAlign.Start);
        HeadlessUi.Flush(window);
        Assert.Equal(500 * 40, list.ScrollPart.Offset.Y, 1);
        Assert.Equal(500, ranges[^1].StartIndex);
        list.ScrollToOffset(80);
        HeadlessUi.Flush(window);
        Assert.Equal(new VisibleRange(2, 6), ranges[^1]);
    });

    [Fact]
    [Upstream("VirtualList", "只用骨架：没有数据时铺几行")]
    public Task Loading_uses_skeleton_rows() => HeadlessUi.Run(() =>
    {
        var empty = Create(0);
        empty.IsLoading = true;
        var more = Create(3);
        more.IsLoading = true;
        var window = HeadlessUi.Show(new StackPanel { Children = { empty, more } });

        HeadlessUi.Until(() => empty.Placeholders.Skeleton.IsVisible && more.Placeholders.More.IsVisible);
        Assert.Equal(5, empty.Placeholders.Skeleton.Children.Count);
        Assert.Equal(2, more.Placeholders.More.Children.Count);
        Assert.False(empty.Placeholders.Empty.IsVisible);
        Assert.False(more.Placeholders.Skeleton.IsVisible);
    });

    [Fact]
    public Task The_status_retries_failures_and_says_when_done() => HeadlessUi.Run(() =>
    {
        var fail = true;
        var list = new InfiniteList<int>((cursor, _) => fail
            ? Task.FromException<InfinitePage<int>>(new InvalidOperationException("网关超时"))
            : Task.FromResult(new InfinitePage<int>([1, 2])));
        var status = new InfiniteListStatus { List = list };
        var window = HeadlessUi.Show(status);
        _ = list.Refresh();
        HeadlessUi.Flush(window);

        Assert.True(status.Parts.Error.IsVisible && status.Parts.Retry.IsVisible);
        Assert.Equal("加载失败，请重试", status.Parts.Error.Text);
        fail = false;
        status.Parts.Retry.RaiseEvent(new RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
        HeadlessUi.Flush(window);
        Assert.False(status.Parts.Retry.IsVisible);
        Assert.True(status.Parts.NoMore.IsVisible);
        Assert.Equal("没有更多了", status.Parts.NoMore.Text);
    });
}
