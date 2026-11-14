using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Rune.Wpf.Tests;

/// <summary>Breadcrumbs：最后一项是当前页、其余有地址的是链接；超过 MaxItems 时中间几级收进菜单；订阅了 Navigate 时没有地址的各级是按钮。</summary>
public class BreadcrumbsTests
{
    static BreadcrumbItem[] Deep => [new("首页", "#home"), new("项目", "#projects"), new("官网改版"), new("设计稿", "#designs"), new("2026 秋季", "#autumn"), new("首页 v3.fig")];

    static UIElement Entry(Breadcrumbs breadcrumbs, int position) => ((Flex)breadcrumbs.List.Children[position]).Children[0];

    static Breadcrumbs Show(Breadcrumbs breadcrumbs)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(breadcrumbs);
        WpfUi.Show(host);
        return breadcrumbs;
    }

    [Fact]
    public Task The_last_item_is_the_current_page_and_the_rest_are_links() => WpfUi.Run(() =>
    {
        var breadcrumbs = Show(new Breadcrumbs { Items = [new("首页", "#home"), new("订单", "#orders"), new("#10234")] });

        Assert.Equal(Provider.GetLang(breadcrumbs).Breadcrumbs, breadcrumbs.Peer().GetName());
        var link = Assert.IsType<Link>(Assert.IsType<Text>(Entry(breadcrumbs, 1)).Inlines.FirstInline);
        Assert.Equal(new Uri("#orders", UriKind.Relative), link.Href);
        var current = Assert.IsType<Border>(Entry(breadcrumbs, 2));
        Assert.Equal("当前页", AutomationProperties.GetItemStatus(current));
        // 分隔符在各项之间，最后一项后面没有
        Assert.IsType<Icon>(((Flex)breadcrumbs.List.Children[0]).Children[1]);
        Assert.Single(((Flex)breadcrumbs.List.Children[2]).Children);
    });

    [Fact]
    public Task MaxItems_keeps_the_first_and_last_two_and_folds_the_rest_into_a_menu() => WpfUi.Run(() =>
    {
        var breadcrumbs = Show(new Breadcrumbs { Items = Deep, MaxItems = 4 });

        Assert.Equal(4, breadcrumbs.List.Children.Count);
        var menu = Assert.IsType<Menu>(Entry(breadcrumbs, 1));
        Assert.Equal(Provider.GetLang(breadcrumbs).BreadcrumbsExpand, AutomationProperties.GetName((DependencyObject)menu.Trigger!));
        Assert.Equal(["项目", "官网改版", "设计稿"], menu.Items.OfType<MenuItemBase>().Select(item => item.Content as string));
        Assert.IsType<MenuLinkItem>(menu.Items[0]);
        Assert.False(((UIElement)menu.Items[1]).IsEnabled); // 没有地址、也没有人处理导航的那一级点不了

        breadcrumbs.MaxItems = 1; // 少于 3 项可折的不折
        Assert.Equal(4, breadcrumbs.List.Children.Count);
        breadcrumbs.Items = [new("首页", "#home"), new("订单", "#orders"), new("#10234")];
        Assert.Equal(3, breadcrumbs.List.Children.Count);
    });

    [Fact]
    public Task Levels_without_an_address_navigate_when_someone_handles_it() => WpfUi.Run(() =>
    {
        BreadcrumbItem[] path = [new("全公司"), new("研发中心"), new("前端组")];
        var breadcrumbs = Show(new Breadcrumbs { Items = path });
        Assert.IsType<Flex>(Entry(breadcrumbs, 0)); // 没人处理时是纯文字

        BreadcrumbNavigateEventArgs? navigated = null;
        breadcrumbs.Navigate += (_, e) => navigated = e;
        var button = Assert.IsType<Button>(Entry(breadcrumbs, 1));
        Assert.Equal(ButtonVariant.Link, button.Variant);
        Assert.Equal("当前位置", AutomationProperties.GetItemStatus(Entry(breadcrumbs, 2)));

        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal(path[1], navigated?.Item);
        Assert.Equal(1, navigated?.Index);
    });
}
