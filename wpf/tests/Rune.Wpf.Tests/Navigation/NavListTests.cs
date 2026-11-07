using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>
/// NavList：当前页按 CurrentHref 判断（读屏的项状态是"当前页"），点击先发 Navigate 再切换；分组默认收起、含当前页时自动展开、标题切换；
/// NavSection 一直展开，标题是列表的读屏名称；紧凑时只剩图标，名称在提示里。
/// </summary>
public class NavListTests
{
    static (NavList List, NavItem[] Items, NavGroup Group, StackPanel Host) Build(string path)
    {
        var items = new[]
        {
            new NavItem { Href = "/", Icon = Lucide.House, Content = "概览" },
            new NavItem { Href = "/orders", Content = "全部订单" },
            new NavItem { Href = "/orders/refunds", Content = "退款" },
        };
        var group = new NavGroup { Label = "订单", Icon = Lucide.Package, Items = { items[1], items[2] } };
        var list = new NavList { CurrentHref = path, Children = { items[0], group } };
        AutomationProperties.SetName(list, "后台");
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(list);
        return (list, items, group, host);
    }

    static void Invoke(UIElement element)
    {
        ((IInvokeProvider)element.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("NavList", "当前页按路由适配判断")]
    public Task The_current_page_follows_the_current_href_and_clicks_navigate() => WpfUi.Run(() =>
    {
        var (list, items, _, host) = Build("/");
        WpfUi.Show(host);
        string? navigated = null;
        list.Navigate += (_, e) => navigated = e.Href;

        Assert.Equal([true, false, false], items.Select(item => item.IsCurrent));
        Assert.Equal("当前页", items[0].Peer().GetItemStatus());
        Assert.Equal(AutomationControlType.Hyperlink, items[0].Peer().GetAutomationControlType());

        Invoke(items[1]);
        Assert.Equal("/orders", navigated);
        Assert.Equal("/orders", list.CurrentHref);
        Assert.Equal([false, true, false], items.Select(item => item.IsCurrent));

        // 处理了 Navigate：路由接管，NavList 不改当前地址
        list.Navigate += (_, e) => e.Handled = true;
        Invoke(items[2]);
        Assert.Equal("/orders/refunds", navigated);
        Assert.Equal("/orders", list.CurrentHref);
    });

    [Fact]
    [Upstream("NavList", "分组默认收起；含当前页时自动展开；触发器切换")]
    public Task Groups_start_closed_open_for_the_current_page_and_toggle() => WpfUi.Run(() =>
    {
        var (_, _, group, host) = Build("/");
        WpfUi.Show(host);
        var header = (IExpandCollapseProvider)group.Header.Peer().GetPattern(PatternInterface.ExpandCollapse);

        Assert.Equal(ExpandCollapseState.Collapsed, header.ExpandCollapseState);
        Invoke(group.Header);
        Assert.True(group.IsOpen);
        Assert.Equal(ExpandCollapseState.Expanded, header.ExpandCollapseState);

        var (_, _, openedGroup, openedHost) = Build("/orders/refunds");
        WpfUi.Show(openedHost);
        Assert.True(openedGroup.IsOpen);
    });

    [Fact]
    [Upstream("NavList", "NavSection：带小标题的一组，一直展开；标题是列表的名称")]
    public Task Sections_are_always_open_and_name_their_list() => WpfUi.Run(() =>
    {
        var section = new NavSection { Label = "指南", Items = { new NavItem { Href = "/start", Content = "快速开始" } } };
        var list = new NavList { Children = { section } };
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(list);
        WpfUi.Show(host);

        Assert.Equal("指南", AutomationProperties.GetName(section.List));
        Assert.Equal("快速开始", ((NavItem)section.List.Children[0]).Content);
        Assert.Empty(WpfUi.Descendants(list).OfType<NavGroupHeader>());
    });

    [Fact]
    public Task Compact_rows_keep_only_the_icon_and_show_the_name_in_a_tooltip() => WpfUi.Run(() =>
    {
        var item = new NavItem { Href = "/", Icon = Lucide.House, Content = "概览", Badge = 3 };
        var list = new NavList { Compact = true, Children = { item } };
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(list);
        WpfUi.Show(host);

        Assert.True(item.IsIconOnly);
        Assert.Equal("概览", item.ToolTip);
        Assert.True(item.Part<Indicator>("PART_Indicator").Dot);
        Assert.Equal(Visibility.Collapsed, item.Part<Badge>("PART_Badge").Visibility);

        list.Compact = false;
        Assert.False(item.IsIconOnly);
        Assert.Null(item.ToolTip);
        Assert.Equal("3", item.Part<Badge>("PART_Badge").Content);
    });
}
