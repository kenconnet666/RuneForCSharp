namespace Rune.WinForms.Tests;

/// <summary>BreadcrumbsControl：最后一项是当前页、其余有地址的是链接；超过 MaxItems 时中间几级收进菜单；订阅了 Navigate 时没有地址的各级是按钮；整条 sm 字号。</summary>
public class BreadcrumbsTests
{
    static BreadcrumbItem[] Deep => [new("首页", "#home"), new("项目", "#projects"), new("官网改版"), new("设计稿", "#designs"), new("2026 秋季", "#autumn"), new("首页 v3.fig")];

    static Control Entry(BreadcrumbsControl breadcrumbs, int position) => breadcrumbs.Controls[position].Controls[0];

    [Fact]
    public Task The_last_item_is_the_current_page_and_the_rest_are_links() => WinFormsUi.Run(() =>
    {
        var breadcrumbs = new BreadcrumbsControl { Items = [new("首页", "#home"), new("订单", "#orders"), new("#10234")] };
        using var form = WinFormsUi.Show(breadcrumbs);

        Assert.Equal(ProviderControl.LangOf(breadcrumbs).Breadcrumbs, breadcrumbs.AccessibilityObject.Name);
        Assert.Equal(Tokens.FontSize.Sm * 72 / 96, breadcrumbs.Font.SizeInPoints, 2);
        Assert.Equal(new Uri("#orders", UriKind.Relative), Assert.IsType<LinkControl>(Entry(breadcrumbs, 1)).Href);
        Assert.Equal("当前页", Assert.IsType<TextControl>(Entry(breadcrumbs, 2)).AccessibleDescription);
        // 分隔符在各项之间，最后一项后面没有
        Assert.IsType<IconControl>(breadcrumbs.Controls[0].Controls[1]);
        Assert.Single(breadcrumbs.Controls[2].Controls.Cast<Control>());
    });

    [Fact]
    public Task MaxItems_keeps_the_first_and_last_two_and_folds_the_rest_into_a_menu() => WinFormsUi.Run(() =>
    {
        var breadcrumbs = new BreadcrumbsControl { Items = Deep, MaxItems = 4 };
        using var form = WinFormsUi.Show(breadcrumbs);

        Assert.Equal(4, breadcrumbs.Controls.Count);
        var menu = Assert.IsType<MenuControl>(Entry(breadcrumbs, 1));
        Assert.Equal(ProviderControl.LangOf(breadcrumbs).BreadcrumbsExpand, menu.Trigger!.AccessibleName);
        Assert.Equal(["项目", "官网改版", "设计稿"], menu.Items.Cast<Control>().Select(item => item.Text));
        Assert.IsType<MenuLinkItemControl>(menu.Items[0]);
        Assert.False(menu.Items[1].Enabled); // 没有地址、也没有人处理导航的那一级点不了

        breadcrumbs.MaxItems = 1; // 少于 3 项可折的不折
        Assert.Equal(4, breadcrumbs.Controls.Count);
        breadcrumbs.Items = [new("首页", "#home"), new("订单", "#orders"), new("#10234")];
        Assert.Equal(3, breadcrumbs.Controls.Count);
    });

    [Fact]
    public Task Levels_without_an_address_navigate_when_someone_handles_it() => WinFormsUi.Run(() =>
    {
        BreadcrumbItem[] path = [new("全公司"), new("研发中心"), new("前端组")];
        var breadcrumbs = new BreadcrumbsControl { Items = path };
        using var form = WinFormsUi.Show(breadcrumbs);
        Assert.IsType<TextControl>(Entry(breadcrumbs, 0)); // 没人处理时是纯文字

        BreadcrumbNavigateEventArgs? navigated = null;
        breadcrumbs.Navigate += (_, e) => navigated = e;
        var button = Assert.IsType<ButtonControl>(Entry(breadcrumbs, 1));
        Assert.Equal(ButtonVariant.Link, button.Variant);
        Assert.Equal("当前位置", Entry(breadcrumbs, 2).AccessibleDescription);

        button.PerformClick();
        Assert.Equal(path[1], navigated?.Item);
        Assert.Equal(1, navigated?.Index);
    });
}
