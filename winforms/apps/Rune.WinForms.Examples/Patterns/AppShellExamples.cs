namespace Rune.WinForms.Examples;

/// <summary>
/// AppShell 页的示例（vue-tsx-pro 文档站 patterns/AppShell）：先有 fullscreen；admin 要等 Stat，mixed 要等 NavBar。
/// window 是桌面特有的：在新窗体里打开外壳，顶栏兼作标题栏。WinForms 里是 AppShellControl、AppShellBrandControl。
/// </summary>
static class AppShellExamples
{
    public static Example[] All =>
    [
        new("AppShell", "fullscreen", "全屏", "顶栏结束边默认有全屏按钮（Fullscreenable）：进入后不显示顶栏和左侧栏，正文占满；外壳铺满窗体时窗体同时全屏。右上角留一个退出按钮，Esc 也退出。IsFullscreenChanged 通知父级，隐藏的顶栏和侧栏还在，退出后滚动位置、展开的分组都还在。", Fullscreen, Framed: true),
        new("AppShell", "window", "顶栏兼作标题栏", "桌面特有：外壳铺满窗体时（ExtendsContentIntoTitleBar，默认开），去掉系统的标题栏，顶栏的空白处拖动窗体、双击最大化，结束边是自绘的最小化、最大化、关闭；系统的缩放边框、贴边与 Windows 11 的贴靠布局都保留。", WindowDemo),
    ];

    static NavListControl Navigation()
    {
        var list = new NavListControl { CurrentHref = "home", AccessibleName = "导航" };
        list.Controls.AddRange(
        [
            new NavItemControl { Href = "home", Icon = Lucide.House, Text = "工作台" },
            new NavItemControl { Href = "contracts", Icon = Lucide.FileText, Text = "合同" },
            new NavItemControl { Href = "suppliers", Icon = Lucide.Users, Text = "供应商" },
        ]);
        return list;
    }

    static Control Fullscreen()
    {
        var hint = new SmallControl { Tone = TextTone.Muted, Text = "点右边的按钮进入全屏" };
        var title = new H1Control { Text = "工作台" };
        var body = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, AutoSize = true };
        body.Controls.AddRange([title, Build.Card(new CardControl(), new ParagraphControl { Text = "看大屏、演示、对着一张大表工作时用全屏：顶栏和左侧栏都不占地方。右上角的按钮或 Esc 退出。" })]);
        var shell = new AppShellControl
        {
            Dock = DockStyle.Fill,
            Brand = new AppShellBrandControl { Icon = Lucide.Box, Text = "采购协同" },
            Header = hint,
            Sidebar = Navigation(),
            Content = body,
        };
        shell.IsFullscreenChanged += (_, _) =>
        {
            hint.Text = shell.IsFullscreen ? "全屏中" : "点右边的按钮进入全屏";
            title.Text = shell.IsFullscreen ? "只看正文" : "工作台";
        };
        return new FramePanel(shell, 24 * 16);
    }

    static Control WindowDemo()
    {
        var open = new ButtonControl { Text = "在新窗口打开", Icon = Lucide.AppWindow, AutoSize = true };
        open.Click += (_, _) =>
        {
            var article = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, AutoSize = true };
            article.Controls.AddRange(
            [
                new H1Control { Text = "采购总览" },
                new H2Control { Name = "overview", Text = "概览" },
                new ParagraphControl { Text = "这个窗口没有系统的标题栏：顶栏就是标题栏。窗口变窄到 1024 以下时侧栏收进抽屉，1280 以下不显示右侧栏。" },
                new H2Control { Name = "drag", Text = "拖动与贴靠" },
                new ParagraphControl { Text = "拖顶栏的空白处移动窗口，双击最大化；拖到屏幕边缘贴边，悬停最大化按钮弹出 Windows 11 的贴靠布局。" },
                new H2Control { Name = "buttons", Text = "窗口按钮" },
                new ParagraphControl { Text = "最小化、最大化（还原）、关闭都是自绘的，跟着主题与暗色模式；关闭按钮悬停是 danger 底。" },
            ]);
            var toc = new TableOfContentsControl { Items = [new("overview", "概览", 2), new("drag", "拖动与贴靠", 2), new("buttons", "窗口按钮", 2)] };
            var shell = new AppShellControl
            {
                Dock = DockStyle.Fill,
                Brand = new AppShellBrandControl { Icon = Lucide.Box, Text = "采购协同" },
                Header = new SmallControl { Tone = TextTone.Muted, Text = "拖动这里移动窗口" },
                Sidebar = Navigation(),
                SidebarCollapsible = true,
                Aside = toc,
                Footer = new SmallControl { Text = "© 2026 采购协同" },
                Content = article,
            };
            toc.ScrollRoot = shell.MainScroll;
            var form = new Form { Text = "采购协同", Font = open.FindForm()?.Font ?? open.Font, ClientSize = new Size(1320, 760), StartPosition = FormStartPosition.CenterScreen };
            form.Controls.Add(shell);
            form.Show();
        };
        return open;
    }

    /// <summary>示例的效果区（TS 的 frame）：定高（设备无关像素，按 DPI 换算），外壳铺满它。</summary>
    sealed class FramePanel : Panel
    {
        readonly int height;

        public FramePanel(Control content, int height)
        {
            this.height = height;
            Controls.Add(content);
            Height = height;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Height = LogicalToDeviceUnits(height);
        }
    }
}
