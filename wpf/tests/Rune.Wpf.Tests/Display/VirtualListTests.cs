using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>VirtualList 与 InfiniteListStatus：只建看得见的项、带同级位置与总数；数据变了重排；没有数据时是组、显示空状态；随外层滚动时视口不可聚焦；滚动方法与可见范围；加载中的骨架；失败时重试、取完了“没有更多了”。</summary>
public class VirtualListTests
{
    static VirtualList Create(int count, double height = 200) => new()
    {
        ItemsSource = Enumerable.Range(0, count).Select(index => $"第 {index} 项").ToList(),
        ItemTemplate = Row(),
        Height = height,
        Width = 300,
    };

    /// <summary>一项：40 高的文字。</summary>
    static DataTemplate Row()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
        text.SetValue(FrameworkElement.HeightProperty, 40.0);
        return new DataTemplate { VisualTree = text };
    }

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    [Fact]
    [Upstream("VirtualList", "只渲染可见范围加 overscan 的项；每项带 aria-setsize 与 aria-posinset")]
    public Task Only_visible_items_are_realized_with_set_positions() => WpfUi.Run(() =>
    {
        var list = Create(10000);
        AutomationProperties.SetName(list, "联系人");
        WpfUi.Show(Host(list));
        WpfUi.Flush();

        var realized = list.RealizedItems.ToList();
        Assert.InRange(realized.Count, 5, 30);
        Assert.Equal((1, 10000), (AutomationProperties.GetPositionInSet(realized[0]), AutomationProperties.GetSizeOfSet(realized[0])));
        Assert.Equal((AutomationControlType.List, "联系人"), (list.Peer().GetAutomationControlType(), list.Peer().GetName()));
        Assert.Equal(AutomationControlType.ListItem, UIElementAutomationPeer.CreatePeerForElement(realized[0]).GetAutomationControlType());
    });

    [Fact]
    [Upstream("VirtualList", "数据变了重新排：总大小跟着变")]
    public Task Changing_the_data_relayouts() => WpfUi.Run(() =>
    {
        var list = Create(100);
        WpfUi.Show(Host(list));
        WpfUi.Flush();
        var before = list.Viewer!.ExtentHeight;

        list.ItemsSource = Enumerable.Range(0, 200).Select(index => $"第 {index} 项").ToList();
        WpfUi.Flush();
        Assert.Equal(2 * before, list.Viewer!.ExtentHeight, 1);
        Assert.Equal(200, AutomationProperties.GetSizeOfSet(list.RealizedItems.First()));
    });

    [Fact]
    [Upstream("VirtualList", "没有数据时显示 empty，不是列表")]
    public Task Without_data_it_shows_empty_as_a_group() => WpfUi.Run(() =>
    {
        var list = Create(0);
        list.Empty = new Empty { Description = "还没有联系人" };
        AutomationProperties.SetName(list, "联系人");
        WpfUi.Show(Host(list));
        WpfUi.Flush();

        Assert.True(list.Placeholders.Empty.IsVisible);
        Assert.Equal((AutomationControlType.Group, "联系人"), (list.Peer().GetAutomationControlType(), list.Peer().GetName()));
    });

    [Fact]
    [Upstream("VirtualList", "window 模式不是滚动容器")]
    public Task Window_mode_follows_the_outer_scroll() => WpfUi.Run(() =>
    {
        var list = Create(50);
        list.Height = double.NaN;
        list.Scroll = VirtualListScroll.Window;
        var page = new ScrollViewer { Height = 300, Content = new StackPanel { Children = { new TextBlock { Text = "页面开头", Height = 100 }, list } } };
        WpfUi.Show(Host(page));
        WpfUi.Flush();

        // 随外层滚动：列表里没有自己的滚动区域，滚动的是外层
        Assert.Same(page, list.Viewer);
        Assert.Null(VirtualList.FindDescendant<ScrollArea>(list));
        list.ScrollToOffset(400);
        Assert.Equal(500, page.VerticalOffset, 1);
    });

    [Fact]
    [Upstream("VirtualList", "handle 拿到滚动方法；onRangeChange 报告可见范围")]
    public Task Scroll_methods_and_range_changes() => WpfUi.Run(() =>
    {
        var list = Create(1000);
        var ranges = new List<VisibleRange>();
        list.RangeChanged += (_, range) => ranges.Add(range);
        WpfUi.Show(Host(list));
        WpfUi.Flush();
        Assert.Equal(new VisibleRange(0, 4), ranges[^1]);

        list.ScrollToIndex(500, ScrollAlign.Start);
        WpfUi.Flush();
        Assert.Equal(500 * 40, list.Viewer!.VerticalOffset, 1);
        Assert.Equal(500, ranges[^1].StartIndex);
        list.ScrollToOffset(80);
        WpfUi.Flush();
        Assert.Equal(new VisibleRange(2, 6), ranges[^1]);
    });

    [Fact]
    [Upstream("VirtualList", "只用骨架：没有数据时铺几行")]
    public Task Loading_uses_skeleton_rows() => WpfUi.Run(() =>
    {
        var empty = Create(0);
        empty.IsLoading = true;
        var more = Create(3);
        more.IsLoading = true;
        WpfUi.Show(Host(empty, more));

        WpfUi.Until(() => empty.Placeholders.Skeleton.IsVisible && more.Placeholders.More.IsVisible);
        Assert.Equal(5, empty.Placeholders.Skeleton.Children.Count);
        Assert.Equal(2, more.Placeholders.More.Children.Count);
        Assert.Equal(Visibility.Collapsed, empty.Placeholders.Empty.Visibility);
        Assert.Equal(Visibility.Collapsed, more.Placeholders.Skeleton.Visibility);
    });

    [Fact]
    public Task The_status_retries_failures_and_says_when_done() => WpfUi.Run(() =>
    {
        var fail = true;
        var list = new InfiniteList<int>((cursor, _) => fail
            ? Task.FromException<InfinitePage<int>>(new InvalidOperationException("网关超时"))
            : Task.FromResult(new InfinitePage<int>([1, 2])));
        var status = new InfiniteListStatus { List = list };
        WpfUi.Show(Host(status));
        _ = list.Refresh();
        WpfUi.Flush();

        Assert.True(status.Parts.Error.IsVisible && status.Parts.Retry.IsVisible);
        Assert.Equal("加载失败，请重试", status.Parts.Error.Text);
        fail = false;
        ((IInvokeProvider)status.Parts.Retry.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
        Assert.False(status.Parts.Retry.IsVisible);
        Assert.True(status.Parts.NoMore.IsVisible);
        Assert.Equal("没有更多了", status.Parts.NoMore.Text);
    });
}
