using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>NavBar 与 NavMenu：当前页、子项是当前页时菜单按钮高亮、角标、点链接先发 Navigate；放不下时收进"更多"；菜单树渲染成小节、分组、链接。</summary>
public class NavBarTests
{
    static readonly NavMenuItem[] Bar =
    [
        new("工作台") { Href = "/" },
        new("采购") { Children = [new("采购单") { Href = "/po" }, new("供应商") { Href = "/suppliers" }] },
        new("报表") { Href = "/reports", Badge = 3 },
    ];

    static void Click(Window window, Control control)
    {
        var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
    }

    // 逻辑树：藏起来的项没有套模板，不在可视树里
    static List<NavBarButton> Buttons(NavBar bar) => [.. bar.GetLogicalDescendants().OfType<NavBarButton>()];

    [Fact]
    [Upstream("NavBar", "当前页的链接带 aria-current；子项是当前页时它的下拉按钮也高亮；角标")]
    public Task Current_page_trigger_and_badge() => HeadlessUi.Run(() =>
    {
        var bar = new NavBar { Items = Bar, CurrentHref = "/po" };
        HeadlessUi.Show(new StackPanel { Width = 800, Children = { bar } });

        var buttons = Buttons(bar);
        Assert.Equal(["工作台", "采购", "报表"], buttons.Where(button => button.IsVisible && button.Content is string).Select(button => button.Content as string));
        Assert.False(buttons[0].IsCurrent);
        Assert.Equal((NavBarParts.Trigger, true, true), (buttons[1].Name, buttons[1].IsCurrent, buttons[1].ShowChevron));
        var badge = buttons[2].GetVisualDescendants().OfType<Badge>().Single(item => item.Name == NavBarParts.Badge);
        Assert.Equal((true, "3"), (badge.IsVisible, badge.Content as string));
        // 都放得下：“更多”藏起来（读屏、Tab 都跳过）
        Assert.Equal(3, bar.VisibleCount);
        Assert.False(bar.More.IsVisible);
        Assert.Equal("更多", AutomationProperties.GetName(bar.More.Trigger!));
    });

    [Fact]
    [Upstream("NavBar", "部件属性：每一项的 li、链接、菜单按钮可以按项给；链接的 onClick 先执行、可以拦下跳转")]
    public Task Links_navigate_unless_handled() => HeadlessUi.Run(() =>
    {
        var bar = new NavBar { Items = Bar, CurrentHref = "/" };
        var handled = true;
        var seen = new List<string?>();
        bar.Navigate += (_, e) =>
        {
            seen.Add(e.Href);
            e.Handled = handled;
        };
        var window = HeadlessUi.Show(new StackPanel { Width = 800, Children = { bar } });

        var reports = Buttons(bar).Single(button => button.Content as string == "报表");
        Assert.Equal(NavBarParts.Link, reports.Name);
        Click(window, reports);
        Assert.Equal(("/", false), (bar.CurrentHref, reports.IsCurrent));
        handled = false;
        Click(window, reports);
        Assert.Equal(["/reports", "/reports"], seen);
        Assert.Equal(("/reports", true), (bar.CurrentHref, reports.IsCurrent));
        // 菜单按钮弹出子项的菜单；点子项同样先发 Navigate
        var menu = bar.GetLogicalDescendants().OfType<Menu>().First(item => item.Name == NavBarParts.Item);
        Assert.Equal(["采购单", "供应商"], menu.Items.OfType<MenuItem>().Select(item => item.Content as string));
    });

    [Fact]
    public Task Narrow_bars_put_the_rest_into_more() => HeadlessUi.Run(() =>
    {
        var bar = new NavBar { Items = [.. Enumerable.Range(1, 8).Select(i => new NavMenuItem($"栏目{i}") { Href = $"/{i}" }), new("报表") { Children = [new("月报") { Href = "/monthly" }] }], CurrentHref = "/monthly" };
        var host = new Border { Width = 360, Child = bar };
        var window = HeadlessUi.Show(new StackPanel { Children = { host } });

        Assert.InRange(bar.VisibleCount, 1, 8);
        Assert.True(bar.More.IsVisible);
        Assert.True(bar.Bounds.Width <= 360);
        var more = (NavBarButton)bar.More.Trigger!;
        // 收起来的项里有当前页：“更多”也高亮
        Assert.True(more.IsCurrent);
        bar.More.IsOpen = true;
        HeadlessUi.Flush(window);
        Assert.Equal("报表", bar.More.Items.OfType<MenuGroup>().Single().Label);
        Assert.Equal(9 - bar.VisibleCount - 1, bar.More.Items.OfType<MenuItem>().Count());
        bar.More.IsOpen = false;

        host.Width = 2000;
        HeadlessUi.Flush(window);
        Assert.Equal(9, bar.VisibleCount);
        Assert.False(bar.More.IsVisible);
    });

    static readonly NavMenuItem[] Tree =
    [
        new("首页") { Href = "/", Icon = Lucide.House },
        new("降本管理")
        {
            Icon = Lucide.Boxes,
            Children = [new("降本项目") { Href = "/projects", Badge = 3 }, new("台账") { Href = "/ledger" }, new("设置") { Children = [new("字典") { Href = "/dict" }] }],
        },
        new("空目录") { Children = [new("无页面的子目录") { Children = [] }] },
    ];

    [Fact]
    [Upstream("NavMenu", "顶层的目录是小节，叶子是链接，没有页面的目录不画")]
    public Task Menu_sections_groups_and_links() => HeadlessUi.Run(() =>
    {
        var menu = new NavMenu { Items = Tree, CurrentHref = "/projects" };
        HeadlessUi.Show(new StackPanel { Width = 300, Children = { menu } });

        Assert.Equal(["首页", "降本项目", "台账", "字典"], menu.GetLogicalDescendants().OfType<NavItem>().Select(item => item.Content as string));
        Assert.Equal(["降本管理"], menu.GetLogicalDescendants().OfType<NavSection>().Select(section => section.Label));
        Assert.Equal(["设置"], menu.GetLogicalDescendants().OfType<NavGroup>().Select(group => group.Label as string));
        Assert.Equal(3, menu.GetLogicalDescendants().OfType<NavItem>().Single(item => item.Href == "/projects").Badge);
    });

    [Fact]
    [Upstream("NavMenu", "当前页的链接带 aria-current")]
    public Task Menu_marks_the_current_page() => HeadlessUi.Run(() =>
    {
        var menu = new NavMenu { Items = Tree, CurrentHref = "/ledger" };
        HeadlessUi.Show(new StackPanel { Width = 300, Children = { menu } });

        Assert.Equal(["台账"], menu.GetLogicalDescendants().OfType<NavItem>().Where(item => item.IsCurrent).Select(item => item.Content as string));
    });

    [Fact]
    [Upstream("NavMenu", "topLevel=\"group\" 时顶层目录也是可收起的分组")]
    public Task Menu_top_level_groups() => HeadlessUi.Run(() =>
    {
        var menu = new NavMenu { Items = Tree, CurrentHref = "/", TopLevel = NavMenuTopLevel.Group };
        HeadlessUi.Show(new StackPanel { Width = 300, Children = { menu } });

        Assert.Equal(["降本管理", "设置"], menu.GetLogicalDescendants().OfType<NavGroup>().Select(group => group.Label as string));
        Assert.Empty(menu.GetLogicalDescendants().OfType<NavSection>());
    });
}
