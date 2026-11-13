using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace Rune.Wpf.Tests;

/// <summary>
/// AppShell：品牌格与侧栏同宽对齐；全屏隐藏顶栏和侧栏（按钮、退出按钮、Esc）；导航在品牌之后、页脚在正文下面；收起侧栏成图标栏；
/// 窄时侧栏在抽屉里，点了导航项关上；外壳是窗口的内容时顶栏兼作标题栏（WindowChrome、自绘窗口按钮）。
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

    /// <summary>放进屏幕外的窗口（同 WpfUi.Show，宽度可调），主题挂在窗口上：外壳就是窗口的内容。</summary>
    static Window Show(FrameworkElement content, double width)
    {
        var window = new Window
        {
            Content = content, Width = width, Height = 600, Left = -20000, Top = -20000,
            ShowActivated = false, ShowInTaskbar = false,
        };
        window.Resources.MergedDictionaries.Add(new RuneTheme());
        window.Show();
        WpfUi.Flush();
        return window;
    }

    static double Left(FrameworkElement element, Visual root) => element.TransformToAncestor(root).Transform(default).X;

    static void Invoke(UIElement element)
    {
        ((IInvokeProvider)element.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("AppShell", "brand 在顶栏的内容之前；有侧栏时品牌格与侧栏同宽对齐")]
    public Task The_brand_cell_lines_up_with_the_sidebar() => WpfUi.Run(() =>
    {
        var header = new TextBlock { Text = "工具" };
        var body = new TextBlock { Text = "正文" };
        var shell = new AppShell { Brand = new AppShellBrand { Content = "采购" }, Header = header, Sidebar = Navigation("/"), Content = body };
        var window = Show(shell, 1100);

        Assert.Equal(0, Left(shell.BrandPart, shell));
        Assert.Equal(shell.SidebarPart.ActualWidth, shell.BrandPart.ActualWidth);
        Assert.True(Left(header, shell) > Left(shell.BrandPart, shell));
        // 顶栏的内容与正文的起始边对齐
        Assert.Equal(Left(body, shell), Left(header, shell), 3);
        window.Close();
    });

    [Fact]
    [Upstream("AppShell", "没有侧栏：品牌格照常排在行内")]
    public Task Without_a_sidebar_the_brand_sits_inline() => WpfUi.Run(() =>
    {
        var shell = new AppShell { Brand = new AppShellBrand { Content = "采购" }, Content = new TextBlock { Text = "正文" } };
        var window = Show(shell, 1100);

        Assert.True(Left(shell.BrandPart, shell) > 0);
        Assert.Equal(0, shell.BrandPart.BorderThickness.Right);
        Assert.Equal(Visibility.Collapsed, shell.MenuButtonPart.Visibility);
        window.Close();
    });

    [Fact]
    [Upstream("AppShell", "顶栏结束边有全屏按钮：进入后隐藏顶栏和左侧栏，写回 _fullscreen；退出按钮、Esc 都能退出")]
    public Task Fullscreen_hides_the_header_and_sidebar_until_exit_or_escape() => WpfUi.Run(() =>
    {
        var shell = new AppShell { Sidebar = Navigation("/"), Content = new TextBlock { Text = "正文" } };
        var window = Show(shell, 1100);
        var enter = shell.FullscreenButtonPart;

        Assert.True(enter.IsVisible);
        Assert.Equal(PatternsMessages.ZhCn.FullscreenEnter, enter.Peer().GetName());
        Invoke(enter);
        Assert.True(shell.IsFullscreen);
        Assert.False(shell.HeaderPart.IsVisible);
        Assert.False(shell.SidebarPart.IsVisible);
        Assert.True(shell.FullscreenExitPart.IsVisible);
        Assert.Equal(WindowState.Maximized, window.WindowState);
        Assert.Equal(WindowStyle.None, window.WindowStyle);

        Invoke(shell.FullscreenExitPart);
        Assert.False(shell.IsFullscreen);
        Assert.True(shell.HeaderPart.IsVisible);
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.Equal(WindowStyle.SingleBorderWindow, window.WindowStyle);

        shell.IsFullscreen = true;
        WpfUi.Flush();
        window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
        WpfUi.Flush();
        Assert.False(shell.IsFullscreen);
        window.Close();
    });

    [Fact]
    [Upstream("AppShell", "fullscreenable 关掉：没有全屏按钮")]
    public Task Fullscreenable_off_removes_the_button() => WpfUi.Run(() =>
    {
        var shell = new AppShell { Fullscreenable = false, Content = new TextBlock { Text = "正文" } };
        var window = Show(shell, 1100);
        Assert.False(shell.FullscreenButtonPart.IsVisible);
        window.Close();
    });

    [Fact]
    [Upstream("AppShell", "部件属性：header 内容格、主体、侧栏与右栏的滚动区域")]
    public Task Parts_take_part_styles() => WpfUi.Run(() =>
    {
        var shell = new AppShell { Sidebar = Navigation("/"), Aside = new TextBlock { Text = "目录" }, Content = new TextBlock() };
        var styles = new ResourceDictionary();
        var header = new Style(typeof(Border), (Style)new RuneTheme()[AppShellParts.Header]);
        header.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
        styles[AppShellParts.Header] = header;
        Provider.SetStyles(shell, styles);
        var window = Show(shell, 1400);

        Assert.Equal(0.5, shell.HeaderPart.Opacity);
        Assert.True(shell.HeaderContentPart.IsVisible);
        Assert.True(shell.BodyPart.IsVisible);
        Assert.True(shell.SidebarScrollPart.IsVisible);
        Assert.True(shell.AsideScrollPart.IsVisible);
        window.Close();
    });

    [Fact]
    [Upstream("AppShellBrand", "链到首页：图标是装饰，名字是链接的名称")]
    public Task The_brand_is_named_by_the_product() => WpfUi.Run(() =>
    {
        var brand = new AppShellBrand { Icon = Lucide.Box, Content = "采购协同" };
        var clicked = 0;
        brand.Click += (_, _) => clicked++;
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(brand);
        WpfUi.Show(host);

        Assert.Equal("采购协同", brand.Peer().GetName());
        Assert.Equal(AutomationControlType.Button, brand.Peer().GetAutomationControlType());
        Invoke(brand);
        Assert.Equal(1, clicked);
    });

    [Fact]
    [Upstream("AppShell", "nav 在品牌之后、工具之前；footer 在正文下面")]
    public Task Nav_follows_the_brand_and_the_footer_follows_the_content() => WpfUi.Run(() =>
    {
        var nav = new TextBlock { Text = "导航" };
        var tools = new TextBlock { Text = "工具" };
        var body = new TextBlock { Text = "正文" };
        var footer = new TextBlock { Text = "页脚" };
        var shell = new AppShell { Brand = new AppShellBrand { Content = "采购" }, Nav = nav, Header = tools, Content = body, Footer = footer };
        var window = Show(shell, 1100);

        Assert.True(Left(nav, shell) > Left(shell.BrandPart, shell));
        Assert.True(Left(tools, shell) > Left(nav, shell));
        Assert.True(footer.TransformToAncestor(shell).Transform(default).Y > body.TransformToAncestor(shell).Transform(default).Y);
        // 内容少时页脚也贴着底部
        var part = shell.FooterPart;
        Assert.Equal(shell.ActualHeight, part.TransformToAncestor(shell).Transform(new Point(0, part.ActualHeight)).Y, 3);
        window.Close();
    });

    [Fact]
    [Upstream("AppShell", "sidebarCollapsible：品牌格里的“收起侧栏”把侧栏收成图标栏，品牌格只剩展开按钮，写回 sidebarCollapsed")]
    public Task Collapsing_turns_the_sidebar_into_an_icon_rail() => WpfUi.Run(() =>
    {
        var list = Navigation("/", "/orders");
        var brand = new AppShellBrand { Icon = Lucide.Box, Content = "采购" };
        var shell = new AppShell { Brand = brand, Sidebar = list, SidebarCollapsible = true, Content = new TextBlock() };
        var window = Show(shell, 1100);
        var collapse = shell.CollapsePart;
        var cell = shell.BrandPart;

        Assert.Equal(PatternsMessages.ZhCn.SidebarCollapse, collapse.Peer().GetName());
        Assert.True(collapse.IsDescendantOf(cell)); // 按钮在品牌格里、贴着结束边
        Assert.True(collapse.TranslatePoint(default, cell).X > brand.TranslatePoint(default, cell).X + brand.ActualWidth);
        Invoke(collapse);
        Assert.True(shell.IsSidebarCollapsed);
        Assert.Equal(56, shell.SidebarPart.ActualWidth);
        Assert.All(WpfUi.Descendants(list).OfType<NavRow>(), row => Assert.True(row.IsIconOnly));
        Assert.False(brand.IsVisible); // 收起后品牌格不放品牌，只放居中的展开按钮
        Assert.Equal(cell.ActualWidth / 2, collapse.TranslatePoint(new Point(collapse.ActualWidth / 2, 0), cell).X, 0);
        Assert.Equal(PatternsMessages.ZhCn.SidebarExpand, collapse.Peer().GetName());

        // 窄时侧栏在抽屉里，抽屉总是完整的
        window.Width = 800;
        WpfUi.Flush();
        Assert.DoesNotContain(WpfUi.Descendants(list).OfType<NavRow>(), row => row.IsIconOnly);
        window.Close();
    });

    [Fact]
    public Task The_drawer_closes_after_navigating() => WpfUi.Run(() =>
    {
        var list = Navigation("/", "/orders");
        var shell = new AppShell { Sidebar = list, Content = new TextBlock() };
        var window = Show(shell, 800);
        var drawer = shell.DrawerPart;

        Assert.True(shell.MenuButtonPart.IsVisible);
        Assert.False(shell.SidebarPart.IsVisible);
        Assert.Equal(PatternsMessages.ZhCn.NavigationOpen, shell.MenuButtonPart.Peer().GetName());
        Invoke(shell.MenuButtonPart);
        Assert.True(shell.IsSidebarOpen);
        Assert.True(drawer.IsVisible);
        Assert.True(drawer.IsAncestorOf(list));

        Invoke(list.Children.OfType<NavItem>().Last());
        Assert.False(shell.IsSidebarOpen);
        Assert.False(drawer.IsVisible);
        Assert.Equal("/orders", list.CurrentHref);

        // 变宽后侧栏常驻：抽屉关上，侧栏的内容回到常驻的侧栏
        shell.IsSidebarOpen = true;
        window.Width = 1100;
        WpfUi.Flush();
        Assert.False(shell.IsSidebarOpen);
        Assert.True(shell.SidebarPart.IsAncestorOf(list));
        window.Close();
    });

    [Fact]
    public Task As_window_content_the_header_becomes_the_title_bar() => WpfUi.Run(() =>
    {
        var shell = new AppShell { Content = new TextBlock() };
        var window = Show(shell, 1100);
        var buttons = shell.CaptionButtonsPart;

        Assert.True(shell.IsTitleBar);
        Assert.NotNull(WindowChrome.GetWindowChrome(window));
        Assert.True(buttons.IsVisible);
        Assert.Equal(["最小化", "最大化", "关闭"], buttons.Children.Cast<UIElement>().Select(button => button.Peer().GetName()));
        Assert.False(shell.MaximizeButtonPart.Focusable);

        Invoke(shell.MaximizeButtonPart);
        Assert.Equal(WindowState.Maximized, window.WindowState);
        Assert.Equal("向下还原", shell.MaximizeButtonPart.Peer().GetName());

        // 关掉：系统的标题栏回来，没有自绘的窗口按钮
        shell.ExtendsContentIntoTitleBar = false;
        WpfUi.Flush();
        Assert.Null(WindowChrome.GetWindowChrome(window));
        Assert.False(buttons.IsVisible);
        window.Close();
    });

    [Fact]
    public Task Nested_in_a_page_the_shell_leaves_the_window_alone() => WpfUi.Run(() =>
    {
        var shell = new AppShell { Content = new TextBlock() };
        var window = Show(new Border { Child = shell }, 1100);

        Assert.False(shell.IsTitleBar);
        Assert.Null(WindowChrome.GetWindowChrome(window));
        Assert.False(shell.CaptionButtonsPart.IsVisible);
        window.Close();
    });
}
