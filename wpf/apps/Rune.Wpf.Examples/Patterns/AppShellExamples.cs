using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>
/// AppShell 页的示例（vue-tsx-pro 文档站 patterns/AppShell）：先有 fullscreen；admin 要等 Stat，mixed 要等 NavBar。
/// window 是桌面特有的：在新窗口里打开外壳，顶栏兼作标题栏。
/// </summary>
static class AppShellExamples
{
    public static Example[] All =>
    [
        new("AppShell", "fullscreen", "全屏", "顶栏结束边默认有全屏按钮（Fullscreenable）：进入后不显示顶栏和左侧栏，正文占满；外壳是窗口的内容时窗口同时全屏。右上角留一个退出按钮，Esc 也退出。IsFullscreen 可以双向绑定，隐藏的顶栏和侧栏还在，退出后滚动位置、展开的分组都还在。", Fullscreen, Framed: true),
        new("AppShell", "window", "顶栏兼作标题栏", "桌面特有：外壳是窗口的内容时（ExtendsContentIntoTitleBar，默认开），去掉系统的标题栏，顶栏的空白处拖动窗口、双击最大化，结束边是自绘的最小化、最大化、关闭；系统的缩放边框、贴边与 Windows 11 的贴靠布局都保留。", WindowDemo),
    ];

    /// <summary>示例的效果区高 24rem（TS 的 frame）。</summary>
    static FrameworkElement Frame(AppShell shell) => new Border { Height = 24 * 16, ClipToBounds = true, Child = shell };

    static NavList Navigation()
    {
        var list = new NavList { CurrentHref = "home" };
        AutomationProperties.SetName(list, "导航");
        list.Children.Add(new NavItem { Href = "home", Icon = Lucide.House, Content = "工作台" });
        list.Children.Add(new NavItem { Href = "contracts", Icon = Lucide.FileText, Content = "合同" });
        list.Children.Add(new NavItem { Href = "suppliers", Icon = Lucide.Users, Content = "供应商" });
        return list;
    }

    static FrameworkElement Fullscreen()
    {
        var hint = new Small { Tone = TextTone.Muted, Text = "点右边的按钮进入全屏" };
        var title = new H1 { Text = "工作台" };
        var shell = new AppShell
        {
            Brand = new AppShellBrand { Icon = Lucide.Box, Content = "采购协同" },
            Header = hint,
            Sidebar = Navigation(),
            Content = new Flex
            {
                Direction = FlexDirection.Column,
                Gap = Space.Md,
                Children =
                {
                    title,
                    new Card { Content = new Paragraph { Text = "看大屏、演示、对着一张大表工作时用全屏：顶栏和左侧栏都不占地方。右上角的按钮或 Esc 退出。" } },
                },
            },
        };
        DependencyPropertyDescriptor.FromProperty(AppShell.IsFullscreenProperty, typeof(AppShell)).AddValueChanged(shell, (_, _) =>
        {
            hint.Text = shell.IsFullscreen ? "全屏中" : "点右边的按钮进入全屏";
            title.Text = shell.IsFullscreen ? "只看正文" : "工作台";
        });
        return Frame(shell);
    }

    static FrameworkElement WindowDemo()
    {
        var open = new Button { Content = "在新窗口打开", Icon = Lucide.AppWindow, HorizontalAlignment = HorizontalAlignment.Left };
        open.Click += (_, _) =>
        {
            var toc = new TableOfContents
            {
                Items = [new HeadingEntry("overview", "概览", 2), new HeadingEntry("drag", "拖动与贴靠", 2), new HeadingEntry("buttons", "窗口按钮", 2)],
            };
            var article = new Flex
            {
                Direction = FlexDirection.Column,
                Gap = Space.Md,
                Children =
                {
                    new H1 { Text = "采购总览" },
                    new H2 { Name = "overview", Text = "概览" },
                    new Paragraph { Text = "这个窗口没有系统的标题栏：顶栏就是标题栏。窗口变窄到 1024 以下时侧栏收进抽屉，1280 以下不显示右侧栏。" },
                    new H2 { Name = "drag", Text = "拖动与贴靠" },
                    new Paragraph { Text = "拖顶栏的空白处移动窗口，双击最大化；拖到屏幕边缘贴边，悬停最大化按钮弹出 Windows 11 的贴靠布局。" },
                    new H2 { Name = "buttons", Text = "窗口按钮" },
                    new Paragraph { Text = "最小化、最大化（还原）、关闭都是自绘的，跟着主题与暗色模式；关闭按钮悬停是 danger 底。" },
                },
            };
            var shell = new AppShell
            {
                Brand = new AppShellBrand { Icon = Lucide.Box, Content = "采购协同" },
                Header = new Small { Tone = TextTone.Muted, Text = "拖动这里移动窗口" },
                Sidebar = Navigation(),
                SidebarCollapsible = true,
                Aside = toc,
                Footer = new Small { Text = "© 2026 采购协同" },
                Content = article,
            };
            var window = new Window { Title = "采购协同", Width = 1320, Height = 760, Content = shell };
            toc.ScrollRoot = shell.MainScroll;
            window.Show();
        };
        return open;
    }
}
