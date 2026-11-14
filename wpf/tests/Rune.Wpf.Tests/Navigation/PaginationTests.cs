using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>Pagination：页码序列长度固定；当前页 soft、读屏"当前页"；上一页、下一页到头不可用；点页码写回 Page；页数按总条数算。Pager：两张链接卡片。</summary>
public class PaginationTests
{
    static T Show<T>(T element) where T : UIElement
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(element);
        WpfUi.Show(host);
        return element;
    }

    [Theory]
    [InlineData(6, 20, 1, 1, "1,…,5,6,7,…,20")]
    [InlineData(1, 20, 1, 1, "1,2,3,4,5,…,20")]
    [InlineData(6, 20, 2, 2, "1,2,3,4,5,6,7,8,…,19,20")]
    [InlineData(2, 5, 1, 1, "1,2,3,4,5")]
    public void Items_keep_a_fixed_length(int page, int count, int siblings, int boundaries, string expected) =>
        Assert.Equal(expected, string.Join(",", PaginationItems.Of(page, count, siblings, boundaries).Select(item => item?.ToString() ?? "…")));

    [Fact]
    public Task Pages_are_buttons_and_the_ends_disable_the_steps() => WpfUi.Run(() =>
    {
        var pagination = Show(new Pagination { Total = 95, PageSize = 10, ShowTotal = true });

        Assert.Equal(10, pagination.Count);
        Assert.Equal(Provider.GetLang(pagination).Pagination, pagination.Peer().GetName());
        var children = pagination.List.Children;
        Assert.False(children[0].IsEnabled); // 第 1 页：上一页不可用
        var first = Assert.IsType<Button>(children[1]);
        Assert.Equal(ButtonVariant.Soft, first.Variant);
        Assert.Equal("当前页", AutomationProperties.GetItemStatus(first));
        Assert.Equal(Visibility.Visible, pagination.TotalPart.Visibility);

        var third = children.OfType<Button>().First(button => button.Content as string == "3");
        third.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.Equal(3, pagination.Page);

        pagination.Total = 15; // 总数变少：Page 超出时显示夹回最后一页
        Assert.Equal(2, pagination.Count);
        Assert.False(pagination.List.Children[^1].IsEnabled);
    });

    [Fact]
    public Task Href_makes_pages_links() => WpfUi.Run(() =>
    {
        var pagination = Show(new Pagination { Page = 3, PageCount = 12, Href = page => $"#page-{page}" });

        Assert.All(pagination.List.Children.OfType<Button>(), button => Assert.IsType<LinkButton>(button));
        Assert.Equal(new Uri("#page-2", UriKind.Relative), Assert.IsType<IconLinkButton>(pagination.List.Children[0]).Href);
    });

    [Fact]
    public Task Pager_shows_previous_and_next_cards() => WpfUi.Run(() =>
    {
        var pager = Show(new Pager { Previous = new PagerLink("/tabs", "Tabs 标签页"), Next = new PagerLink("/breadcrumbs", "Breadcrumbs 面包屑") });

        Assert.Equal(Provider.GetLang(pager).PagerLabel, pager.Peer().GetName());
        Assert.Equal(["Tabs 标签页", "Breadcrumbs 面包屑"], pager.Cards.Select(card => card.Link.Label));
        Assert.Equal(AutomationControlType.Hyperlink, pager.Cards.First().Peer().GetAutomationControlType());
    });
}
