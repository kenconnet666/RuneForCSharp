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

/// <summary>PageSizeSelectControl、PageJumpControl、PaginationBarControl：条数与 Select 的值同步；跳页夹到范围、输入框清空；分页栏的总数、换条数回第 1 页、窄时收起跳页、总数单独一行。</summary>
public class PaginationBarTests
{
    [Fact]
    public Task Page_size_select_keeps_the_number_and_the_value_in_sync() => WinFormsUi.Run(() =>
    {
        var select = new PageSizeSelectControl { PageSize = 20 };
        using var form = WinFormsUi.Show(select);
        int? changed = null;
        select.PageSizeChanged += (_, size) => changed = size;

        Assert.Equal("20", select.Value);
        select.Value = "50"; // 用户选了 50 条/页
        Assert.Equal(50, select.PageSize);
        Assert.Equal(50, changed);
        Assert.Equal(ProviderControl.LangOf(select).PageSizeLabel, select.AccessibleName);
    });

    [Fact]
    public Task Page_jump_clamps_and_clears() => WinFormsUi.Run(() =>
    {
        var jump = new PageJumpControl { PageCount = 12 };
        using var form = WinFormsUi.Show(jump);

        jump.Input.Value = 40;
        Assert.Equal(12, jump.Page);
        Assert.Null(jump.Input.Value);
    });

    [Fact]
    public Task Bar_shows_the_range_and_resets_the_page_on_size_change() => WinFormsUi.Run(() =>
    {
        var bar = new PaginationBarControl { Page = 3, PageSize = 10, Total = 95, AutoSize = false, Width = 760 };
        using var form = WinFormsUi.Show(bar);
        var changed = 0;
        bar.PageChanged += (_, _) => changed++;

        Assert.Equal(new PageRange(21, 30, 95), bar.Range);
        Assert.Equal(ProviderControl.LangOf(bar).PaginationRange(21, 30, 95), bar.TotalPart.Text);
        Assert.True(bar.JumpPart.Visible);

        bar.SizeSelectPart.Value = "20";
        Assert.Equal(20, bar.PageSize);
        Assert.Equal(1, bar.Page);
        Assert.Equal(1, changed);

        bar.PaginationPart.Page = 2; // 点了页码：写回分页栏
        Assert.Equal(2, bar.Page);

        bar.Width = 300; // 窄于 36em：不显示跳页
        WinFormsUi.Pump();
        Assert.False(bar.JumpPart.Visible);
    });

    [Fact]
    public Task Narrow_bar_puts_the_total_on_its_own_line_and_the_card_grows() => WinFormsUi.Run(() =>
    {
        var bar = new PaginationBarControl { Page = 4, Total = 86, ShowSizeSelect = false };
        var card = new CardControl { Controls = { bar } };
        card.MinimumSize = card.MaximumSize = new Size(320, 0);
        using var form = WinFormsUi.Show(card);
        WinFormsUi.Pump();

        Assert.False(bar.JumpPart.Visible);
        var row = bar.PaginationPart.Parent!;
        Assert.True(row.Top >= bar.TotalPart.Bottom, $"total {bar.TotalPart.Bounds} row {row.Bounds} bar {bar.Bounds}"); // 页码换到总数下面一行
        Assert.True(bar.Bottom <= card.ClientSize.Height - card.Padding.Bottom, $"bar {bar.Bounds} card {card.ClientSize}"); // 卡片随之长高，没有裁掉
    });
}
