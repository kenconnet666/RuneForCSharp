using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// AppShell：品牌格与侧栏同宽对齐；全屏隐藏顶栏和侧栏（按钮、退出按钮、Esc）；导航在品牌之后、页脚在正文下面；收起侧栏成图标栏；
/// 窄时侧栏在抽屉里，点了导航项关上；外壳是窗口的内容时顶栏兼作标题栏（自绘窗口按钮）。
/// </summary>
public class AppShellTests
{
    static NavList Navigation(params string[] pages)
    {
        var list = new NavList { CurrentHref = pages[0] };
        AutomationProperties.SetName(list, "导航");
        foreach (var page in pages) list.Children.Add(new NavItem { Href = page, Icon = Lucide.House, Content = page });
        return list;
    }

    static Window Show(Control content, double width)
    {
        var window = new Window { Width = width, Height = 600, Content = content };
        window.Show();
        HeadlessUi.Flush(window);
        return window;
    }

    static T Part<T>(Control owner, string name) where T : Control => owner.GetVisualDescendants().OfType<T>().First(part => part.Name == name);

    static double Left(Control control, Visual root) => control.TranslatePoint(default, root)!.Value.X;

    static void Invoke(Control control) => ((IInvokeProvider)control.Peer()).Invoke();

    [Fact]
    [Upstream("AppShell", "brand 在顶栏的内容之前；有侧栏时品牌格与侧栏同宽对齐")]
    public Task The_brand_cell_lines_up_with_the_sidebar() => HeadlessUi.Run(() =>
    {
        var header = new TextBlock { Text = "工具" };
        var body = new TextBlock { Text = "正文" };
        var shell = new AppShell { Brand = new AppShellBrand { Content = "采购" }, Header = header, Sidebar = Navigation("/"), Content = body };
        Show(shell, 1100);

        var brand = Part<Border>(shell, AppShellParts.Brand);
        var sidebar = Part<Border>(shell, AppShellParts.Sidebar);
        Assert.Equal(0, Left(brand, shell));
        Assert.Equal(sidebar.Bounds.Width, brand.Bounds.Width);
        Assert.True(Left(header, shell) > Left(brand, shell));
        // 顶栏的内容与正文的起始边对齐
        Assert.Equal(Left(body, shell), Left(header, shell), 3);
    });

    [Fact]
    [Upstream("AppShell", "没有侧栏：品牌格照常排在行内")]
    public Task Without_a_sidebar_the_brand_sits_inline() => HeadlessUi.Run(() =>
    {
        var shell = new AppShell { Brand = new AppShellBrand { Content = "采购" }, Content = new TextBlock { Text = "正文" } };
        Show(shell, 1100);

        var brand = Part<Border>(shell, AppShellParts.Brand);
        Assert.True(Left(brand, shell) > 0);
        Assert.Equal(0, brand.BorderThickness.Right);
        Assert.False(Part<IconButton>(shell, AppShellParts.MenuButton).IsVisible);
    });

    [Fact]
    [Upstream("AppShell", "顶栏结束边有全屏按钮：进入后隐藏顶栏和左侧栏，写回 _fullscreen；退出按钮、Esc 都能退出")]
    public Task Fullscreen_hides_the_header_and_sidebar_until_exit_or_escape() => HeadlessUi.Run(() =>
    {
        var shell = new AppShell { Sidebar = Navigation("/"), Content = new TextBlock { Text = "正文" } };
        var window = Show(shell, 1100);
        var header = Part<Border>(shell, AppShellParts.Header);
        var sidebar = Part<Border>(shell, AppShellParts.Sidebar);
        var enter = Part<IconButton>(shell, AppShellParts.FullscreenButton);
        var exit = Part<IconButton>(shell, AppShellParts.FullscreenExit);

        Assert.True(enter.IsVisible);
        Assert.Equal(PatternsMessages.ZhCn.FullscreenEnter, enter.Peer().GetName());
        Invoke(enter);
        HeadlessUi.Flush(window);
        Assert.True(shell.IsFullscreen);
        Assert.False(header.IsVisible);
        Assert.False(sidebar.IsVisible);
        Assert.True(exit.IsVisible);
        Assert.Equal(WindowState.FullScreen, window.WindowState);

        Invoke(exit);
        HeadlessUi.Flush(window);
        Assert.False(shell.IsFullscreen);
        Assert.True(header.IsVisible);
        Assert.Equal(WindowState.Normal, window.WindowState);

        shell.IsFullscreen = true;
        HeadlessUi.Flush(window);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        HeadlessUi.Flush(window);
        Assert.False(shell.IsFullscreen);
    });

    [Fact]
    [Upstream("AppShell", "fullscreenable 关掉：没有全屏按钮")]
    public Task Fullscreenable_off_removes_the_button() => HeadlessUi.Run(() =>
    {
        var shell = new AppShell { Fullscreenable = false, Content = new TextBlock { Text = "正文" } };
        Show(shell, 1100);
        Assert.False(Part<IconButton>(shell, AppShellParts.FullscreenButton).IsVisible);
    });

    [Fact]
    [Upstream("AppShellBrand", "链到首页：图标是装饰，名字是链接的名称")]
    public Task The_brand_is_named_by_the_product() => HeadlessUi.Run(() =>
    {
        var brand = new AppShellBrand { Icon = Lucide.Box, Content = "采购协同" };
        var clicked = 0;
        brand.Click += (_, _) => clicked++;
        HeadlessUi.Show(brand);

        Assert.Equal("采购协同", brand.Peer().GetName());
        Assert.Equal(AutomationControlType.Button, brand.Peer().GetAutomationControlType());
        Invoke(brand);
        Assert.Equal(1, clicked);
    });

    [Fact]
    [Upstream("AppShell", "部件属性：header 内容格、主体、侧栏与右栏的滚动区域")]
    public Task Parts_are_named_for_styling() => HeadlessUi.Run(() =>
    {
        var shell = new AppShell { Sidebar = Navigation("/"), Aside = new TextBlock { Text = "目录" }, Content = new TextBlock() };
        shell.Styles.Add(new Style(selector => selector.OfType<ScrollArea>().Name(AppShellParts.AsideScroll))
        {
            Setters = { new Setter(Visual.OpacityProperty, 0.5) },
        });
        Show(shell, 1400);

        Assert.True(Part<Flex>(shell, AppShellParts.HeaderContent).IsVisible);
        Assert.True(Part<Control>(shell, AppShellParts.Body).IsVisible);
        Assert.True(Part<ScrollArea>(shell, AppShellParts.SidebarScroll).IsVisible);
        Assert.Equal(0.5, Part<ScrollArea>(shell, AppShellParts.AsideScroll).Opacity);
    });

    [Fact]
    [Upstream("AppShell", "nav 在品牌之后、工具之前；footer 在正文下面")]
    public Task Nav_follows_the_brand_and_the_footer_follows_the_content() => HeadlessUi.Run(() =>
    {
        var nav = new TextBlock { Text = "导航" };
        var tools = new TextBlock { Text = "工具" };
        var body = new TextBlock { Text = "正文" };
        var footer = new TextBlock { Text = "页脚" };
        var shell = new AppShell { Brand = new AppShellBrand { Content = "采购" }, Nav = nav, Header = tools, Content = body, Footer = footer };
        Show(shell, 1100);

        Assert.True(Left(nav, shell) > Left(Part<Border>(shell, AppShellParts.Brand), shell));
        Assert.True(Left(tools, shell) > Left(nav, shell));
        Assert.True(footer.TranslatePoint(default, shell)!.Value.Y > body.TranslatePoint(default, shell)!.Value.Y);
        // 内容少时页脚也贴着底部
        var footerPart = Part<ContentPresenter>(shell, AppShellParts.Footer);
        Assert.Equal(shell.Bounds.Height, footerPart.TranslatePoint(new Point(0, footerPart.Bounds.Height), shell)!.Value.Y, 3);
    });

    [Fact]
    [Upstream("AppShell", "sidebarCollapsible：品牌格里的“收起侧栏”把侧栏收成图标栏，品牌格只剩展开按钮，写回 sidebarCollapsed")]
    public Task Collapsing_turns_the_sidebar_into_an_icon_rail() => HeadlessUi.Run(() =>
    {
        var list = Navigation("/", "/orders");
        var brand = new AppShellBrand { Icon = Lucide.Box, Content = "采购" };
        var shell = new AppShell { Brand = brand, Sidebar = list, SidebarCollapsible = true, Content = new TextBlock() };
        var window = Show(shell, 1100);
        var collapse = Part<IconButton>(shell, AppShellParts.Collapse);
        var cell = Part<Border>(shell, AppShellParts.Brand);

        Assert.Equal(PatternsMessages.ZhCn.SidebarCollapse, collapse.Peer().GetName());
        Assert.Contains(cell, collapse.GetVisualAncestors()); // 按钮在品牌格里、贴着结束边
        Assert.True(collapse.TranslatePoint(default, cell)!.Value.X > brand.TranslatePoint(default, cell)!.Value.X + brand.Bounds.Width);
        Invoke(collapse);
        HeadlessUi.Flush(window);
        Assert.True(shell.IsSidebarCollapsed);
        Assert.Equal(56, Part<Border>(shell, AppShellParts.Sidebar).Bounds.Width);
        Assert.True(list.GetVisualDescendants().OfType<NavRow>().All(row => row.IsIconOnly));
        Assert.False(brand.IsEffectivelyVisible); // 收起后品牌格不放品牌，只放居中的展开按钮
        var center = collapse.TranslatePoint(new Point(collapse.Bounds.Width / 2, 0), cell)!.Value.X;
        Assert.Equal(cell.Bounds.Width / 2, center, 1.5);
        Assert.Equal(PatternsMessages.ZhCn.SidebarExpand, collapse.Peer().GetName());

        // 窄时侧栏在抽屉里，抽屉总是完整的
        window.Width = 800;
        HeadlessUi.Flush(window);
        Assert.DoesNotContain(list.GetVisualDescendants().OfType<NavRow>(), row => row.IsIconOnly);
    });

    [Fact]
    public Task The_drawer_closes_after_navigating() => HeadlessUi.Run(() =>
    {
        var list = Navigation("/", "/orders");
        var shell = new AppShell { Sidebar = list, Content = new TextBlock() };
        var window = Show(shell, 800);
        var menu = Part<IconButton>(shell, AppShellParts.MenuButton);
        var drawer = Part<Border>(shell, AppShellParts.Drawer);

        Assert.True(menu.IsVisible);
        Assert.False(Part<Border>(shell, AppShellParts.Sidebar).IsVisible);
        Assert.Equal(PatternsMessages.ZhCn.NavigationOpen, menu.Peer().GetName());
        Invoke(menu);
        HeadlessUi.Flush(window);
        Assert.True(shell.IsSidebarOpen);
        Assert.True(drawer.IsEffectivelyVisible);
        Assert.True(drawer.IsVisualAncestorOf(list));

        Invoke(list.Children.OfType<NavItem>().Last());
        HeadlessUi.Flush(window);
        Assert.False(shell.IsSidebarOpen);
        Assert.False(drawer.IsEffectivelyVisible);
        Assert.Equal("/orders", list.CurrentHref);

        // 变宽后侧栏常驻：抽屉关上，侧栏的内容回到常驻的侧栏
        shell.IsSidebarOpen = true;
        window.Width = 1100;
        HeadlessUi.Flush(window);
        Assert.False(shell.IsSidebarOpen);
        Assert.True(Part<Border>(shell, AppShellParts.Sidebar).IsVisualAncestorOf(list));
    });

    [Fact]
    public Task As_window_content_the_header_becomes_the_title_bar() => HeadlessUi.Run(() =>
    {
        var shell = new AppShell { Content = new TextBlock() };
        var window = Show(shell, 1100);
        var buttons = Part<StackPanel>(shell, AppShellParts.CaptionButtons);
        var maximize = Part<WindowCaptionButton>(shell, AppShellParts.MaximizeButton);

        Assert.True(shell.IsTitleBar);
        Assert.Equal(WindowDecorations.BorderOnly, window.WindowDecorations);
        Assert.True(window.ExtendClientAreaToDecorationsHint);
        Assert.True(buttons.IsVisible);
        Assert.Equal(["最小化", "最大化", "关闭"], buttons.Children.Select(button => button.Peer().GetName()));
        Assert.False(maximize.Focusable);

        Invoke(maximize);
        HeadlessUi.Flush(window);
        Assert.Equal(WindowState.Maximized, window.WindowState);
        Assert.Equal("向下还原", maximize.Peer().GetName());

        // 关掉：系统的标题栏回来，没有自绘的窗口按钮
        shell.ExtendsContentIntoTitleBar = false;
        HeadlessUi.Flush(window);
        Assert.False(window.ExtendClientAreaToDecorationsHint);
        Assert.Equal(WindowDecorations.Full, window.WindowDecorations);
        Assert.False(buttons.IsVisible);
    });

    [Fact]
    public Task Nested_in_a_page_the_shell_leaves_the_window_alone() => HeadlessUi.Run(() =>
    {
        var shell = new AppShell { Content = new TextBlock() };
        var window = Show(new Border { Child = shell }, 1100);

        Assert.False(shell.IsTitleBar);
        Assert.False(window.ExtendClientAreaToDecorationsHint);
        Assert.False(Part<StackPanel>(shell, AppShellParts.CaptionButtons).IsVisible);
    });
}
