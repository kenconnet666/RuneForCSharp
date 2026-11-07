using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// NavList：当前页按 CurrentHref 判断（读屏的项状态是"当前页"），点击先发 Navigate 再切换；分组默认收起、含当前页时自动展开、标题切换；
/// NavSection 一直展开，标题是列表的读屏名称；紧凑时只剩图标，名称在提示里。
/// </summary>
public class NavListTests
{
    static (NavList List, NavItem[] Items, NavGroup Group) Build(string path)
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
        return (list, items, group);
    }

    static T Part<T>(Control owner, string name) where T : Control => owner.GetVisualDescendants().OfType<T>().First(part => part.Name == name);

    static void Invoke(Control control) => ((IInvokeProvider)control.Peer()).Invoke();

    static void Click(Window window, Control control)
    {
        var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
    }

    [Fact]
    [Upstream("NavList", "当前页按路由适配判断")]
    public Task The_current_page_follows_the_current_href_and_clicks_navigate() => HeadlessUi.Run(() =>
    {
        var (list, items, _) = Build("/");
        HeadlessUi.Show(list);
        string? navigated = null;
        list.Navigate += (_, e) => navigated = e.Href;

        Assert.Equal([true, false, false], items.Select(item => item.IsCurrent));
        Assert.Equal("当前页", items[0].Peer().GetItemStatus());
        Assert.Equal(AutomationControlType.Hyperlink, items[0].Peer().GetAutomationControlType());
        Assert.Equal(AutomationLandmarkType.Navigation, list.Peer().GetLandmarkType());

        // 分组里的项：展开有高度过渡，用读屏的 Invoke 点
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
    public Task Groups_start_closed_open_for_the_current_page_and_toggle() => HeadlessUi.Run(() =>
    {
        var (list, _, group) = Build("/");
        var window = HeadlessUi.Show(list);
        var header = (IExpandCollapseProvider)group.Header.Peer();

        Assert.Equal(ExpandCollapseState.Collapsed, header.ExpandCollapseState);
        Click(window, group.Header);
        Assert.True(group.IsOpen);
        Assert.Equal(ExpandCollapseState.Expanded, header.ExpandCollapseState);

        var (opened, _, openedGroup) = Build("/orders/refunds");
        HeadlessUi.Show(opened);
        Assert.True(openedGroup.IsOpen);
    });

    [Fact]
    [Upstream("NavList", "NavSection：带小标题的一组，一直展开；标题是列表的名称")]
    public Task Sections_are_always_open_and_name_their_list() => HeadlessUi.Run(() =>
    {
        var section = new NavSection { Label = "指南", Items = { new NavItem { Href = "/start", Content = "快速开始" } } };
        var list = new NavList { Children = { section } };
        HeadlessUi.Show(list);

        Assert.Equal("指南", AutomationProperties.GetName(section.List));
        Assert.Equal("快速开始", ((NavItem)section.List.Children[0]).Content);
        Assert.Empty(list.GetLogicalDescendants().OfType<NavGroupHeader>());
    });

    [Fact]
    public Task Compact_rows_keep_only_the_icon_and_show_the_name_in_a_tooltip() => HeadlessUi.Run(() =>
    {
        var item = new NavItem { Href = "/", Icon = Lucide.House, Content = "概览", Badge = 3 };
        var list = new NavList { Compact = true, Children = { item } };
        HeadlessUi.Show(list);

        Assert.True(item.IsIconOnly);
        Assert.Equal("概览", ToolTip.GetTip(item));
        Assert.True(Part<Indicator>(item, NavRowParts.Indicator).Dot);
        Assert.False(Part<Badge>(item, NavRowParts.Badge).IsVisible);

        list.Compact = false;
        Assert.False(item.IsIconOnly);
        Assert.Null(ToolTip.GetTip(item));
        Assert.Equal("3", Part<Badge>(item, NavRowParts.Badge).Content);
    });
}
