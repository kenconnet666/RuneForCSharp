namespace Rune.WinForms.Tests;

/// <summary>
/// NavListControl：当前页按 CurrentHref 判断（读屏里是选中的链接），点击先发 Navigate 再切换；分组默认收起、含当前页时自动展开、标题切换；
/// NavSectionControl 一直展开，标题是列表的读屏名称；紧凑时只剩图标，名称在提示里。
/// </summary>
public class NavListTests
{
    static (NavListControl List, NavItemControl[] Items, NavGroupControl Group) Build(string path)
    {
        var items = new[]
        {
            new NavItemControl { Href = "/", Icon = Lucide.House, Text = "概览" },
            new NavItemControl { Href = "/orders", Text = "全部订单" },
            new NavItemControl { Href = "/orders/refunds", Text = "退款" },
        };
        var group = new NavGroupControl { Label = "订单", Icon = Lucide.Package };
        group.Items.AddRange([items[1], items[2]]);
        var list = new NavListControl { CurrentHref = path, AccessibleName = "后台", Width = 240 };
        list.Controls.AddRange([items[0], group]);
        return (list, items, group);
    }

    [Fact]
    [Upstream("NavList", "当前页按路由适配判断")]
    public Task The_current_page_follows_the_current_href_and_clicks_navigate() => WinFormsUi.Run(() =>
    {
        var (list, items, _) = Build("/");
        using var form = WinFormsUi.Show(list);
        string? navigated = null;
        list.Navigate += (_, e) => navigated = e.Href;

        Assert.Equal([true, false, false], items.Select(item => item.IsCurrent));
        Assert.True(items[0].AccessibilityObject.State.HasFlag(AccessibleStates.Selected));
        Assert.Equal(AccessibleRole.Link, items[0].AccessibilityObject.Role);

        items[1].AccessibilityObject.DoDefaultAction();
        Assert.Equal("/orders", navigated);
        Assert.Equal("/orders", list.CurrentHref);
        Assert.Equal([false, true, false], items.Select(item => item.IsCurrent));

        // 处理了 Navigate：路由接管，NavListControl 不改当前地址
        list.Navigate += (_, e) => e.Handled = true;
        items[2].Activate();
        Assert.Equal("/orders/refunds", navigated);
        Assert.Equal("/orders", list.CurrentHref);
    });

    [Fact]
    [Upstream("NavList", "分组默认收起；含当前页时自动展开；触发器切换")]
    public Task Groups_start_closed_open_for_the_current_page_and_toggle() => WinFormsUi.Run(() =>
    {
        var (list, _, group) = Build("/");
        using var form = WinFormsUi.Show(list);

        Assert.True(group.Header.AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed));
        group.Header.AccessibilityObject.DoDefaultAction();
        Assert.True(group.IsOpen);
        Assert.True(group.Header.AccessibilityObject.State.HasFlag(AccessibleStates.Expanded));

        var (opened, _, openedGroup) = Build("/orders/refunds");
        using var other = WinFormsUi.Show(opened);
        Assert.True(openedGroup.IsOpen);
    });

    [Fact]
    [Upstream("NavList", "NavSection：带小标题的一组，一直展开；标题是列表的名称")]
    public Task Sections_are_always_open_and_name_their_list() => WinFormsUi.Run(() =>
    {
        var section = new NavSectionControl { Label = "指南" };
        section.Items.Add(new NavItemControl { Href = "/start", Text = "快速开始" });
        var list = new NavListControl { Width = 240 };
        list.Controls.Add(section);
        using var form = WinFormsUi.Show(list);

        Assert.Equal("指南", section.List.AccessibilityObject.Name);
        Assert.Equal("快速开始", section.List.Controls[0].Text);
        Assert.Empty(list.Controls.OfType<NavGroupControl>());
    });

    [Fact]
    public Task Compact_rows_keep_only_the_icon_and_show_the_name_in_a_tooltip() => WinFormsUi.Run(() =>
    {
        var item = new NavItemControl { Href = "/", Icon = Lucide.House, Text = "概览", Badge = 3 };
        var list = new NavListControl { Compact = true, Width = 56 };
        list.Controls.Add(item);
        using var form = WinFormsUi.Show(list);

        Assert.True(item.IsIconOnly);
        Assert.Equal("概览", list.Tips.GetTooltip(item));
        Assert.True(item.Controls.OfType<IndicatorControl>().Single().Dot);
        Assert.False(item.Controls.OfType<BadgeControl>().Single().Visible);

        list.Compact = false;
        Assert.False(item.IsIconOnly);
        Assert.Null(list.Tips.GetTooltip(item));
        Assert.Equal("3", item.Controls.OfType<BadgeControl>().Single().Text);
    });
}
