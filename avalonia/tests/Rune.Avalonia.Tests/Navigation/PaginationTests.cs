using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Rune.Avalonia.Tests;

/// <summary>Pagination：页码序列长度固定；当前页 soft、读屏"当前页"；上一页、下一页到头不可用；点页码写回 Page；页数按总条数算。Pager：两张链接卡片。</summary>
public class PaginationTests
{
    [Theory]
    [InlineData(6, 20, 1, 1, "1,…,5,6,7,…,20")]
    [InlineData(1, 20, 1, 1, "1,2,3,4,5,…,20")]
    [InlineData(20, 20, 1, 1, "1,…,16,17,18,19,20")]
    [InlineData(6, 20, 2, 2, "1,2,3,4,5,6,7,8,…,19,20")]
    [InlineData(2, 5, 1, 1, "1,2,3,4,5")]
    [InlineData(1, 1, 1, 1, "1")]
    public void Items_keep_a_fixed_length(int page, int count, int siblings, int boundaries, string expected) =>
        Assert.Equal(expected, string.Join(",", PaginationItems.Of(page, count, siblings, boundaries).Select(item => item?.ToString() ?? "…")));

    [Fact]
    public Task Pages_are_buttons_and_the_ends_disable_the_steps() => HeadlessUi.Run(() =>
    {
        var pagination = new Pagination { Total = 95, PageSize = 10, ShowTotal = true };
        HeadlessUi.Show(pagination);

        Assert.Equal(10, pagination.Count);
        Assert.Equal(Provider.GetLang(pagination).Pagination, pagination.Peer().GetName());
        var children = pagination.List.Children;
        Assert.False(children[0].IsEnabled); // 第 1 页：上一页不可用
        var first = Assert.IsType<Button>(children[1]);
        Assert.Equal(ButtonVariant.Soft, first.Variant);
        Assert.Equal("当前页", AutomationProperties.GetItemStatus(first));
        Assert.True(pagination.TotalPart.IsVisible);

        var third = children.OfType<Button>().First(button => button.Content as string == "3");
        third.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(3, pagination.Page);
        Assert.Equal(ButtonVariant.Soft, pagination.List.Children.OfType<Button>().First(button => button.Content as string == "3").Variant);

        // 总数变少：Page 超出时显示夹回最后一页
        pagination.Total = 15;
        Assert.Equal(2, pagination.Count);
        Assert.False(pagination.List.Children[^1].IsEnabled);
    });

    [Fact]
    public Task Href_makes_pages_links() => HeadlessUi.Run(() =>
    {
        var pagination = new Pagination { Page = 3, PageCount = 12, Href = page => $"#page-{page}" };
        HeadlessUi.Show(pagination);

        Assert.All(pagination.List.Children.OfType<Button>(), button => Assert.IsType<LinkButton>(button));
        Assert.Equal(new Uri("#page-2", UriKind.Relative), Assert.IsType<IconLinkButton>(pagination.List.Children[0]).Href);
    });

    [Fact]
    public Task Pager_shows_previous_and_next_cards() => HeadlessUi.Run(() =>
    {
        var pager = new Pager { Previous = new PagerLink("/tabs", "Tabs 标签页"), Next = new PagerLink("/breadcrumbs", "Breadcrumbs 面包屑") };
        HeadlessUi.Show(pager);

        Assert.Equal(Provider.GetLang(pager).PagerLabel, pager.Peer().GetName());
        Assert.Equal(["Tabs 标签页", "Breadcrumbs 面包屑"], pager.Cards.Select(card => card.Link.Label));
        Assert.Equal(global::Avalonia.Automation.Peers.AutomationControlType.Hyperlink, pager.Cards.First().Peer().GetAutomationControlType());
    });
}
