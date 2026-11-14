namespace Rune.WinForms.Tests;

/// <summary>PaginationControl：页码序列长度固定；当前页 soft、读屏说明"当前页"；上一页、下一页到头不可用；点页码写回 Page、发 PageChanged；页数按总条数算。PagerControl：两张链接卡片。</summary>
public class PaginationTests
{
    [Theory]
    [InlineData(6, 20, 1, 1, "1,…,5,6,7,…,20")]
    [InlineData(1, 20, 1, 1, "1,2,3,4,5,…,20")]
    [InlineData(6, 20, 2, 2, "1,2,3,4,5,6,7,8,…,19,20")]
    [InlineData(2, 5, 1, 1, "1,2,3,4,5")]
    public void Items_keep_a_fixed_length(int page, int count, int siblings, int boundaries, string expected) =>
        Assert.Equal(expected, string.Join(",", PaginationItems.Of(page, count, siblings, boundaries).Select(item => item?.ToString() ?? "…")));

    [Fact]
    public Task Pages_are_buttons_and_the_ends_disable_the_steps() => WinFormsUi.Run(() =>
    {
        var pagination = new PaginationControl { Total = 95, PageSize = 10, ShowTotal = true };
        using var form = WinFormsUi.Show(pagination);
        var changed = 0;
        pagination.PageChanged += (_, _) => changed++;

        Assert.Equal(10, pagination.Count);
        Assert.Equal(ProviderControl.LangOf(pagination).Pagination, pagination.AccessibilityObject.Name);
        var children = pagination.List.Controls.Cast<Control>().ToList();
        Assert.False(children[0].Enabled); // 第 1 页：上一页不可用
        var first = Assert.IsType<ButtonControl>(children[1]);
        Assert.Equal(ButtonVariant.Soft, first.Variant);
        Assert.Equal("当前页", first.AccessibleDescription);
        Assert.True(pagination.TotalPart.Visible);

        children.OfType<ButtonControl>().First(button => button.Text == "3").PerformClick();
        Assert.Equal(3, pagination.Page);
        Assert.Equal(1, changed);

        pagination.Total = 15; // 总数变少：Page 超出时显示夹回最后一页
        Assert.Equal(2, pagination.Count);
        Assert.False(pagination.List.Controls[^1].Enabled);
    });

    [Fact]
    public Task Href_makes_pages_links() => WinFormsUi.Run(() =>
    {
        var pagination = new PaginationControl { Page = 3, PageCount = 12, Href = page => $"#page-{page}" };
        using var form = WinFormsUi.Show(pagination);

        Assert.Equal(6, pagination.List.Controls.OfType<LinkButtonControl>().Count()); // 1,2,3,4,5,…,12
        Assert.Equal(new Uri("#page-2", UriKind.Relative), Assert.IsType<IconLinkButtonControl>(pagination.List.Controls[0]).Href);
    });

    [Fact]
    public Task Pager_shows_previous_and_next_cards() => WinFormsUi.Run(() =>
    {
        var pager = new PagerControl { Previous = new PagerLink("/tabs", "Tabs 标签页"), Next = new PagerLink("/breadcrumbs", "Breadcrumbs 面包屑") };
        using var form = WinFormsUi.Show(pager);

        Assert.Equal(ProviderControl.LangOf(pager).PagerLabel, pager.AccessibilityObject.Name);
        Assert.Equal(["Tabs 标签页", "Breadcrumbs 面包屑"], pager.Cards.Select(card => card.Link.Label));
        Assert.Equal(AccessibleRole.Link, pager.Cards.First().AccessibilityObject.Role);
    });
}
