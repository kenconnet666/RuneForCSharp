using System.Runtime.InteropServices;

namespace Rune.WinForms.Tests;

/// <summary>
/// AppShellControl：品牌格与侧栏同宽对齐；全屏隐藏顶栏和侧栏（按钮、退出按钮、Esc）；导航在品牌之后、页脚在正文下面；收起侧栏成图标栏；
/// 窄时侧栏在抽屉里，点了导航项关上；外壳铺满窗体时顶栏兼作标题栏（子类化窗体、自绘窗口按钮、非客户区命中）。
/// </summary>
public class AppShellTests
{
    static NavListControl Navigation(params string[] pages)
    {
        var list = new NavListControl { CurrentHref = pages[0], AccessibleName = "导航" };
        foreach (var page in pages) list.Controls.Add(new NavItemControl { Href = page, Icon = Lucide.House, Text = page });
        return list;
    }

    /// <summary>铺满屏幕外的窗体（可缩放的边框，同普通的应用窗体）。</summary>
    static Form Show(AppShellControl shell, int width)
    {
        shell.Dock = DockStyle.Fill;
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-6000, -6000), ShowInTaskbar = false };
        form.Controls.Add(shell);
        form.Show();
        form.ClientSize = new Size(form.LogicalToDeviceUnits(width), form.LogicalToDeviceUnits(600));
        WinFormsUi.Pump();
        WinFormsUi.Paint(form);
        return form;
    }

    static int Left(Control control, Control root) => root.PointToClient(control.PointToScreen(Point.Empty)).X;

    static int Top(Control control, Control root) => root.PointToClient(control.PointToScreen(Point.Empty)).Y;

    static void Invoke(Control control)
    {
        control.AccessibilityObject.DoDefaultAction();
        WinFormsUi.Pump();
    }

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [Fact]
    [Upstream("AppShell", "brand 在顶栏的内容之前；有侧栏时品牌格与侧栏同宽对齐")]
    public Task The_brand_cell_lines_up_with_the_sidebar() => WinFormsUi.Run(() =>
    {
        var header = new TextControl { Text = "工具" };
        var body = new TextControl { Text = "正文" };
        var shell = new AppShellControl { Brand = new AppShellBrandControl { Text = "采购" }, Header = header, Sidebar = Navigation("/"), Content = body };
        using var form = Show(shell, 1100);

        Assert.Equal(0, Left(shell.BrandPart, shell));
        Assert.Equal(shell.SidebarPart.Width, shell.BrandPart.Width);
        Assert.True(Left(header, shell) > Left(shell.BrandPart, shell));
        // 顶栏的内容与正文的起始边对齐
        Assert.Equal(Left(body, shell), Left(header, shell));
    });

    [Fact]
    [Upstream("AppShell", "没有侧栏：品牌格照常排在行内")]
    public Task Without_a_sidebar_the_brand_sits_inline() => WinFormsUi.Run(() =>
    {
        var shell = new AppShellControl { Brand = new AppShellBrandControl { Text = "采购" }, Content = new TextControl { Text = "正文" } };
        using var form = Show(shell, 1100);

        Assert.True(Left(shell.BrandPart, shell) > 0);
        Assert.False(shell.MenuButtonPart.Visible);
    });

    [Fact]
    [Upstream("AppShell", "顶栏结束边有全屏按钮：进入后隐藏顶栏和左侧栏，写回 _fullscreen；退出按钮、Esc 都能退出")]
    public Task Fullscreen_hides_the_header_and_sidebar_until_exit_or_escape() => WinFormsUi.Run(() =>
    {
        var shell = new AppShellControl { Sidebar = Navigation("/"), Content = new TextControl { Text = "正文" } };
        using var form = Show(shell, 1100);
        var changes = 0;
        shell.IsFullscreenChanged += (_, _) => changes++;

        Assert.True(shell.FullscreenButtonPart.Visible);
        Assert.Equal(PatternsMessages.ZhCn.FullscreenEnter, shell.FullscreenButtonPart.AccessibilityObject.Name);
        Invoke(shell.FullscreenButtonPart);
        Assert.True(shell.IsFullscreen);
        Assert.False(shell.HeaderPart.Visible);
        Assert.False(shell.SidebarPart.Visible);
        Assert.True(shell.FullscreenExitPart.Visible);
        Assert.Equal(FormBorderStyle.None, form.FormBorderStyle);
        Assert.Equal(FormWindowState.Maximized, form.WindowState);

        Invoke(shell.FullscreenExitPart);
        Assert.False(shell.IsFullscreen);
        Assert.True(shell.HeaderPart.Visible);
        Assert.Equal(FormBorderStyle.Sizable, form.FormBorderStyle);
        Assert.Equal(FormWindowState.Normal, form.WindowState);

        shell.IsFullscreen = true;
        WinFormsUi.Pump();
        SendMessage(form.Handle, 0x0100, (IntPtr)Keys.Escape, IntPtr.Zero);
        WinFormsUi.Pump();
        Assert.False(shell.IsFullscreen);
        Assert.Equal(4, changes);
    });

    [Fact]
    [Upstream("AppShell", "fullscreenable 关掉：没有全屏按钮")]
    public Task Fullscreenable_off_removes_the_button() => WinFormsUi.Run(() =>
    {
        var shell = new AppShellControl { Fullscreenable = false, Content = new TextControl { Text = "正文" } };
        using var form = Show(shell, 1100);
        Assert.False(shell.FullscreenButtonPart.Visible);
    });

    [Fact]
    [Upstream("AppShellBrand", "链到首页：图标是装饰，名字是链接的名称")]
    public Task The_brand_is_named_by_the_product() => WinFormsUi.Run(() =>
    {
        var brand = new AppShellBrandControl { Icon = Lucide.Box, Text = "采购协同" };
        var clicked = 0;
        brand.Click += (_, _) => clicked++;
        using var form = WinFormsUi.Show(brand);

        Assert.Equal("采购协同", brand.AccessibilityObject.Name);
        Assert.Equal(AccessibleRole.PushButton, brand.AccessibilityObject.Role);
        Invoke(brand);
        Assert.Equal(1, clicked);
    });

    [Fact]
    [Upstream("AppShell", "nav 在品牌之后、工具之前；footer 在正文下面")]
    public Task Nav_follows_the_brand_and_the_footer_follows_the_content() => WinFormsUi.Run(() =>
    {
        var nav = new TextControl { Text = "导航" };
        var tools = new TextControl { Text = "工具" };
        var body = new TextControl { Text = "正文" };
        var footer = new TextControl { Text = "页脚" };
        var shell = new AppShellControl { Brand = new AppShellBrandControl { Text = "采购" }, Nav = nav, Header = tools, Content = body, Footer = footer };
        using var form = Show(shell, 1100);

        Assert.True(Left(nav, shell) > Left(shell.BrandPart, shell));
        Assert.True(Left(tools, shell) > Left(nav, shell));
        Assert.True(Top(footer, shell) > Top(body, shell));
        // 内容少时页脚也贴着底部
        Assert.Equal(shell.ClientSize.Height, Top(shell.FooterPart, shell) + shell.FooterPart.Height);
    });

    [Fact]
    [Upstream("AppShell", "sidebarCollapsible：宽屏时“收起侧栏”把侧栏收成图标栏，品牌只剩标志，写回 sidebarCollapsed")]
    public Task Collapsing_turns_the_sidebar_into_an_icon_rail() => WinFormsUi.Run(() =>
    {
        var list = Navigation("/", "/orders");
        var brand = new AppShellBrandControl { Icon = Lucide.Box, Text = "采购" };
        var shell = new AppShellControl { Brand = brand, Sidebar = list, SidebarCollapsible = true, Content = new TextControl() };
        using var form = Show(shell, 1100);
        var collapse = shell.CollapsePart;

        Assert.Equal(PatternsMessages.ZhCn.SidebarCollapse, collapse.AccessibilityObject.Name);
        Invoke(collapse);
        Assert.True(shell.IsSidebarCollapsed);
        Assert.Equal(shell.LogicalToDeviceUnits(56), shell.SidebarPart.Width);
        Assert.All(list.Controls.OfType<NavItemControl>(), item => Assert.True(item.IsIconOnly));
        Assert.True(brand.IsCompact);
        Assert.Equal("采购", brand.AccessibilityObject.Name);
        Assert.Equal(PatternsMessages.ZhCn.SidebarExpand, collapse.AccessibilityObject.Name);

        // 窄时侧栏在抽屉里，抽屉总是完整的
        form.ClientSize = new Size(form.LogicalToDeviceUnits(800), form.ClientSize.Height);
        WinFormsUi.Pump();
        Assert.DoesNotContain(list.Controls.OfType<NavItemControl>(), item => item.IsIconOnly);
    });

    [Fact]
    public Task The_drawer_closes_after_navigating() => WinFormsUi.Run(() =>
    {
        var list = Navigation("/", "/orders");
        var shell = new AppShellControl { Sidebar = list, Content = new TextControl() };
        using var form = Show(shell, 800);
        var drawer = shell.DrawerPart;

        Assert.True(shell.MenuButtonPart.Visible);
        Assert.False(shell.SidebarPart.Visible);
        Assert.Equal(PatternsMessages.ZhCn.NavigationOpen, shell.MenuButtonPart.AccessibilityObject.Name);
        Invoke(shell.MenuButtonPart);
        Assert.True(shell.IsSidebarOpen);
        Assert.True(drawer.Visible);
        Assert.True(drawer.Contains(list));

        Invoke(list.Controls.OfType<NavItemControl>().Last());
        Assert.False(shell.IsSidebarOpen);
        Assert.False(drawer.Visible);
        Assert.Equal("/orders", list.CurrentHref);

        // 变宽后侧栏常驻：抽屉关上，侧栏的内容回到常驻的侧栏
        shell.IsSidebarOpen = true;
        form.ClientSize = new Size(form.LogicalToDeviceUnits(1100), form.ClientSize.Height);
        WinFormsUi.Pump();
        Assert.False(shell.IsSidebarOpen);
        Assert.True(shell.SidebarPart.Contains(list));
    });

    [Fact]
    public Task Filling_a_form_the_header_becomes_the_title_bar() => WinFormsUi.Run(() =>
    {
        var shell = new AppShellControl { Brand = new AppShellBrandControl { Text = "采购" }, Header = new TextControl { Text = "工具" }, Content = new TextControl() };
        using var form = Show(shell, 1100);
        var buttons = shell.CaptionButtons;
        var maximize = buttons[1];

        Assert.True(shell.IsTitleBar);
        // 系统的标题栏没了：客户区从窗体顶端开始
        Assert.Equal(form.Bounds.Top, form.PointToScreen(Point.Empty).Y);
        Assert.All(buttons, button => Assert.True(button.Visible));
        Assert.Equal(["最小化", "最大化", "关闭"], buttons.Select(button => button.AccessibilityObject.Name));
        Assert.False(maximize.CanSelect);

        // 非客户区的命中：顶栏空白处是标题栏，最大化按钮是 HTMAXBUTTON，顶栏里的按钮是客户区
        var header = shell.HeaderPart;
        int HitTest(Point screen) => (int)SendMessage(form.Handle, 0x0084, IntPtr.Zero, (IntPtr)((screen.Y << 16) | (screen.X & 0xFFFF)));
        Assert.Equal(2, HitTest(header.PointToScreen(new Point(header.Width / 2, header.Height / 2))));
        Assert.Equal(9, HitTest(maximize.PointToScreen(new Point(maximize.Width / 2, maximize.Height / 2))));
        Assert.Equal(1, HitTest(shell.FullscreenButtonPart.PointToScreen(new Point(4, 4))));

        Invoke(maximize);
        Assert.Equal(FormWindowState.Maximized, form.WindowState);
        Assert.Equal("向下还原", maximize.AccessibilityObject.Name);

        // 关掉：系统的标题栏回来，没有自绘的窗口按钮
        shell.ExtendsContentIntoTitleBar = false;
        form.WindowState = FormWindowState.Normal;
        WinFormsUi.Pump();
        Assert.False(shell.IsTitleBar);
        Assert.True(form.PointToScreen(Point.Empty).Y > form.Bounds.Top);
        Assert.All(buttons, button => Assert.False(button.Visible));
    });

    [Fact]
    public Task Nested_in_a_panel_the_shell_leaves_the_form_alone() => WinFormsUi.Run(() =>
    {
        var shell = new AppShellControl { Dock = DockStyle.Fill, Content = new TextControl() };
        var panel = new Panel { Dock = DockStyle.Fill };
        panel.Controls.Add(shell);
        using var form = WinFormsUi.Show(panel);

        Assert.False(shell.IsTitleBar);
        Assert.All(shell.CaptionButtons, button => Assert.False(button.Visible));
    });
}
