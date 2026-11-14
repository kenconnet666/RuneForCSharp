using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 应用外壳（vue-tsx-pro 的 AppShell）：顶栏、左侧栏（导航）、正文、右侧栏（本页目录），桌面应用的整窗骨架。
/// <code>
/// var shell = new AppShellControl { Dock = DockStyle.Fill, Brand = new AppShellBrandControl { Text = "采购协同", Icon = Lucide.Box }, Sidebar = nav, Content = page };
/// form.Controls.Add(shell);
/// </code>
/// 按外壳自己的宽度排版（TS 按视口，断点同 sizes.lg / sizes.xl）：1024 以上显示左侧栏，更窄时收进从起始边滑出的抽屉，顶栏最前面出现打开它的按钮；
/// 抽屉里点了导航项、或变宽时自动关上。1280 以上显示右侧栏，更窄时不显示。两侧栏与正文各自滚动（ScrollAreaControl），页脚在正文下面、内容少时贴着底部。
/// 写了 <see cref="Brand"/> 且有侧栏时，宽屏下品牌这一格与侧栏同宽，侧栏的竖线一直延伸到顶栏顶端，顶栏的内容与正文的起始边对齐。
/// <see cref="Nav"/> 是顶栏里品牌后面的导航；<see cref="SidebarCollapsible"/> 在品牌格的结束边加"收起侧栏"（宽屏时），收起后只剩图标栏（侧栏里的 NavList 变紧凑），品牌格只放居中的展开按钮；没写 Brand 时也有这一格。
/// 全屏（<see cref="Fullscreenable"/>，默认开，窄于 768 时不显示按钮）：不显示顶栏和左侧栏，正文占满，外壳铺满窗体时窗体同时全屏；右上角留一个退出按钮，Esc 也退出。
/// 桌面特有（上游没有）：外壳铺满窗体（Dock = Fill）时，顶栏兼作窗体的标题栏（<see cref="ExtendsContentIntoTitleBar"/>，默认开）：去掉系统的标题栏，
/// 顶栏的空白处拖动窗体、双击最大化，结束边是自绘的最小化、最大化（还原）、关闭（悬停 danger 底）；系统的缩放边框、贴边与 Windows 11 的贴靠布局都保留。
/// 窄屏的抽屉先用外壳自己的面板，Drawer 做好后换成它；WinForms 没有半透明的子控件，遮罩画在打开时外壳的截图上。
/// </summary>
[ToolboxItem(true)]
[Description("应用外壳：顶栏、侧栏、正文、右侧栏，顶栏兼作窗体的标题栏。")]
public partial class AppShellControl : Control
{
    // 上游按 rem（16px）写死的尺寸：顶栏高、侧栏宽、图标栏宽、右侧栏宽
    const int HeaderHeight = 48;
    const int SidebarWidth = 256;
    const int RailWidth = 56;
    const int AsideWidth = 224;
    const int CaptionButtonWidth = 46;

    readonly ShellPane header = new() { Fill = colors => Blend(colors.SurfaceTranslucent, colors.Background), BorderColor = colors => colors.Border, Edges = AnchorStyles.Bottom };
    readonly IconButtonControl menuButton = new() { Icon = Lucide.Menu };
    readonly ShellPane brand = new() { BorderColor = colors => colors.Border, Align = PaneAlign.Center, FitChild = true };
    readonly ShellPane nav = new() { Align = PaneAlign.Stretch };
    readonly ShellPane headerMain = new() { Align = PaneAlign.Center };
    readonly IconButtonControl fullscreenButton = new() { Icon = Lucide.Maximize };
    readonly WindowCaptionButton minimizeButton = new(WindowCaptionKind.Minimize);
    readonly WindowCaptionButton maximizeButton = new(WindowCaptionKind.Maximize);
    readonly WindowCaptionButton closeButton = new(WindowCaptionKind.Close);
    readonly ShellPane sidebar = new() { BorderColor = colors => colors.Border, Edges = AnchorStyles.Right };
    readonly ScrollAreaControl sidebarScroll = new();
    readonly ShellPane sidebarContent = new();
    readonly IconButtonControl collapseButton = new() { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost };
    readonly ScrollAreaControl mainScroll = new();
    readonly ShellColumn column;
    readonly ShellPane main = new();
    readonly ShellPane footer = new() { BorderColor = colors => colors.BorderMuted, Edges = AnchorStyles.Top, Muted = true };
    readonly ScrollAreaControl asideScroll = new();
    readonly ShellPane asideContent = new();
    readonly IconButtonControl fullscreenExit = new() { Icon = Lucide.Minimize };
    readonly ShellDrawer drawer;
    bool wide;
    bool asideWide;
    bool fullscreenFits;

    /// <summary>新建：顶栏、主体（侧栏、正文、右侧栏）、全屏的退出按钮、窄时的抽屉。</summary>
    public AppShellControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        column = new ShellColumn(main, footer);
        drawer = new ShellDrawer(this);
        header.Controls.AddRange([menuButton, brand, nav, headerMain, fullscreenButton, minimizeButton, maximizeButton, closeButton]);
        // 收起、展开侧栏的按钮在顶栏的品牌格里（LayoutHeader 摆到品牌格的结束边，收起时居中）
        header.Controls.Add(collapseButton);
        collapseButton.BringToFront();
        sidebarScroll.Content = sidebarContent;
        sidebar.Controls.Add(sidebarScroll);
        mainScroll.Content = column;
        asideScroll.Content = asideContent;
        Controls.AddRange([drawer, fullscreenExit, header, sidebar, mainScroll, asideScroll]);
        footer.AccessibleRole = AccessibleRole.Grouping;

        menuButton.Click += (_, _) => IsSidebarOpen = true;
        fullscreenButton.Click += (_, _) => IsFullscreen = true;
        fullscreenExit.Click += (_, _) => IsFullscreen = false;
        collapseButton.Click += (_, _) => IsSidebarCollapsed = !IsSidebarCollapsed;
        minimizeButton.Click += (_, _) => Form?.WindowState = FormWindowState.Minimized;
        maximizeButton.Click += (_, _) => ToggleMaximized();
        closeButton.Click += (_, _) => Form?.Close();
        Sync();
    }

    /// <summary>顶栏起始边的品牌（一般是 <see cref="AppShellBrandControl"/>）：有侧栏时这一格与侧栏同宽。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Brand { get => brand.Child; set { brand.Child = value; Sync(); } }

    /// <summary>顶栏的内容：搜索、本页的页签、主题切换……</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Header { get => headerMain.Child; set { headerMain.Child = value; Sync(); } }

    /// <summary>顶栏里品牌后面的导航。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Nav { get => nav.Child; set { nav.Child = value; Sync(); } }

    /// <summary>左侧栏：应用的导航；窄时收进抽屉。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Sidebar { get; set { field = value; PlaceSidebar(); Sync(); } }

    /// <summary>右侧栏：本页目录等辅助内容；较窄时不显示。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Aside { get => asideContent.Child; set { asideContent.Child = value; Sync(); } }

    /// <summary>正文下面的页脚。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Footer { get => footer.Child; set { footer.Child = value; Sync(); } }

    /// <summary>正文。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content { get => main.Child; set { main.Child = value; Sync(); } }

    /// <summary>侧栏底部有"收起侧栏"按钮。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool SidebarCollapsible { get; set { field = value; Sync(); } }

    /// <summary>侧栏收起了（宽屏时成图标栏）。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsSidebarCollapsed
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync();
            IsSidebarCollapsedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>窄时的侧栏抽屉开着。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsSidebarOpen
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync();
            IsSidebarOpenChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>顶栏结束边有全屏按钮，默认开。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool Fullscreenable { get; set { field = value; Sync(); } } = true;

    /// <summary>全屏中。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsFullscreen
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (value) IsSidebarOpen = false;
            // WinForms 在聚焦的控件隐藏时会把焦点挪到下一个控件，所以先记下焦点是不是在全屏按钮上
            var onButton = fullscreenButton.ContainsFocus || fullscreenExit.ContainsFocus || FindForm()?.ActiveControl is null;
            ApplyFullscreen();
            Sync();
            // 进入、退出后焦点跟到对应的按钮上（上游 AppShell）；焦点本来在正文里的不动
            if (onButton) (value ? fullscreenExit : fullscreenButton).Focus();
            IsFullscreenChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>外壳铺满窗体时，顶栏兼作标题栏（自绘窗口按钮），默认开。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool ExtendsContentIntoTitleBar { get; set { field = value; ApplyChrome(); } } = true;

    /// <summary>正文那一列的滚动区域：本页目录（<see cref="TableOfContentsControl.ScrollRoot"/>）给它。</summary>
    [Browsable(false)]
    public ScrollAreaControl MainScroll => mainScroll;

    /// <summary>侧栏收起状态变了（父级记住各人的设置）。</summary>
    public event EventHandler? IsSidebarCollapsedChanged;

    /// <summary>抽屉开关变了。</summary>
    public event EventHandler? IsSidebarOpenChanged;

    /// <summary>全屏变了（按钮、退出按钮、Esc、系统退出全屏）。</summary>
    public event EventHandler? IsFullscreenChanged;

    /// <summary>侧栏收成了图标栏：有侧栏、能收起、收起了，并且是宽屏（窄时侧栏在抽屉里，抽屉总是完整的）。</summary>
    internal bool IsRail => Sidebar is not null && SidebarCollapsible && IsSidebarCollapsed && wide;

    // 部件（测试用）
    internal Control HeaderPart => header;
    internal Control BrandPart => brand;
    internal IconButtonControl MenuButtonPart => menuButton;
    internal IconButtonControl FullscreenButtonPart => fullscreenButton;
    internal IconButtonControl FullscreenExitPart => fullscreenExit;
    internal IconButtonControl CollapsePart => collapseButton;
    internal WindowCaptionButton[] CaptionButtons => [minimizeButton, maximizeButton, closeButton];
    internal Control SidebarPart => sidebar;
    internal Control FooterPart => footer;
    internal Control DrawerPart => drawer.Panel;

    float Em => this.EmPixels();

    int Dip(float value) => (int)MathF.Round(value * DeviceDpi / 96f);

    int Space(float factor) => (int)MathF.Round(factor * Em);

    /// <summary>顶栏下面的主体：宽屏时有侧栏，更宽时有右侧栏。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        UpdateBreakpoints();
        var width = ClientSize.Width;
        var headerHeight = IsFullscreen ? 0 : Dip(HeaderHeight);
        header.Bounds = new Rectangle(0, 0, width, headerHeight);
        LayoutHeader();

        var rail = IsRail;
        var sidebarWidth = sidebar.Visible ? Dip(rail ? RailWidth : SidebarWidth) : 0;
        var asideWidth = asideScroll.Visible ? Dip(AsideWidth) : 0;
        var bodyHeight = Math.Max(0, ClientSize.Height - headerHeight);
        sidebar.Bounds = new Rectangle(0, headerHeight, sidebarWidth, bodyHeight);
        sidebarScroll.Bounds = new Rectangle(0, 0, Math.Max(0, sidebarWidth - 1), bodyHeight);
        mainScroll.Bounds = new Rectangle(sidebarWidth, headerHeight, Math.Max(0, width - sidebarWidth - asideWidth), bodyHeight);
        asideScroll.Bounds = new Rectangle(width - asideWidth, headerHeight, asideWidth, bodyHeight);

        var exit = fullscreenExit.GetPreferredSize(Size.Empty);
        fullscreenExit.Bounds = new Rectangle(width - Space(Tokens.Space.Lg) - exit.Width, Space(Tokens.Space.Sm), exit.Width, exit.Height);
        drawer.Bounds = ClientRectangle;
    }

    /// <summary>
    /// 顶栏一行：菜单按钮、品牌格、导航、内容（占满剩下的宽度）、全屏按钮，竖直居中，间距 space.md；
    /// 品牌格与侧栏同宽时贴着起始边，和后面隔开正文的内边距（space.xl），内容与正文的起始边对齐。窗口按钮贴着结束边、与顶栏等高。
    /// </summary>
    void LayoutHeader()
    {
        var height = header.Height - header.Line;
        var captionWidth = minimizeButton.Visible || closeButton.Visible ? CaptionButtons.Count(button => button.Visible) * Dip(CaptionButtonWidth) : 0;
        var x = header.Width - captionWidth;
        foreach (var button in CaptionButtons.Where(button => button.Visible))
        {
            button.Bounds = new Rectangle(x, 0, Dip(CaptionButtonWidth), height);
            x += button.Width;
        }

        var brandRail = brand.Visible && Sidebar is not null && wide;
        var lg = Space(Tokens.Space.Lg);
        var start = brandRail ? 0 : lg;
        var end = header.Width - captionWidth - lg;
        var gap = Space(Tokens.Space.Md);
        var left = start;
        void Place(Control control, int width, bool stretch = false)
        {
            var size = control.GetPreferredSize(new Size(width, height));
            control.Bounds = stretch ? new Rectangle(left, 0, width, height) : new Rectangle(left, (height - size.Height) / 2, width, Math.Min(size.Height, height));
            left += width + gap;
        }
        if (menuButton.Visible) Place(menuButton, menuButton.GetPreferredSize(Size.Empty).Width);
        if (brand.Visible)
        {
            if (brandRail)
            {
                Place(brand, Dip(IsRail ? RailWidth : SidebarWidth), stretch: true);
                left += Space(Tokens.Space.Xl) - gap;
            }
            else Place(brand, brand.GetPreferredSize(Size.Empty).Width);
        }
        var fullscreenWidth = fullscreenButton.Visible ? fullscreenButton.GetPreferredSize(Size.Empty).Width : 0;
        var remaining = Math.Max(0, end - left - (fullscreenWidth > 0 ? fullscreenWidth + gap : 0));
        if (nav.Visible)
        {
            // 有导航时右边的工具按内容宽、靠结束边，导航占剩下的
            var tools = headerMain.Child is null ? 0 : headerMain.GetPreferredSize(Size.Empty).Width;
            Place(nav, Math.Max(0, remaining - (tools > 0 ? tools + gap : 0)), stretch: true);
            remaining = tools;
        }
        Place(headerMain, remaining);
        if (fullscreenButton.Visible) Place(fullscreenButton, fullscreenWidth);
        // 品牌格：内容竖直居中，左右 space.lg（图标栏时 space.xs）
        brand.Padding = brandRail ? new Padding(IsRail ? Space(Tokens.Space.Xs) : lg, 0, IsRail ? Space(Tokens.Space.Xs) : lg, 0) : Padding.Empty;
        brand.Edges = brandRail ? AnchorStyles.Right : AnchorStyles.None;
        if (!collapseButton.Visible) return;
        // 收起、展开按钮：贴着品牌格的结束边；收起后品牌格只放它，居中
        var toggle = collapseButton.GetPreferredSize(Size.Empty);
        var toggleLeft = IsRail ? brand.Left + (brand.Width - header.Line - toggle.Width) / 2 : brand.Right - header.Line - lg - toggle.Width;
        collapseButton.Bounds = new Rectangle(toggleLeft, (height - toggle.Height) / 2, toggle.Width, toggle.Height);
    }

    /// <summary>断点按外壳自己的宽度（TS 的媒体查询）：侧栏常驻、右侧栏显示、全屏按钮显示。</summary>
    void UpdateBreakpoints()
    {
        var width = ClientSize.Width;
        var nextWide = width >= Dip(Tokens.Size.Lg);
        var nextAsideWide = width >= Dip(Tokens.Size.Xl);
        var nextFits = width >= Dip(Tokens.Size.Md);
        if (nextWide == wide && nextAsideWide == asideWide && nextFits == fullscreenFits) return;
        // 变宽后侧栏已经常驻，抽屉没必要再开着
        var opened = nextWide && !wide;
        (wide, asideWide, fullscreenFits) = (nextWide, nextAsideWide, nextFits);
        if (opened) IsSidebarOpen = false;
        PlaceSidebar();
        Sync(layout: false);
    }

    /// <summary>侧栏的内容只能放一处：宽时在常驻的侧栏里，窄时在抽屉里（TS 渲染两份）。</summary>
    void PlaceSidebar()
    {
        if (wide)
        {
            drawer.Content = null;
            sidebarContent.Child = Sidebar;
        }
        else
        {
            sidebarContent.Child = null;
            drawer.Content = Sidebar;
        }
    }

    /// <summary>按断点、全屏、收起定显示与内边距，文案取语言包；之后重新布局。</summary>
    void Sync(bool layout = true)
    {
        var hasSidebar = Sidebar is not null;
        var rail = IsRail;
        var lg = Space(Tokens.Space.Lg);
        var xl = Space(Tokens.Space.Xl);
        // 侧栏能收起：品牌格里有收起、展开按钮（宽屏时；窄屏时侧栏在抽屉里），没写 Brand 时也给出这一格放它
        var collapsible = hasSidebar && SidebarCollapsible && wide;
        header.Visible = !IsFullscreen;
        menuButton.Visible = hasSidebar && !wide;
        brand.Visible = Brand is not null || collapsible;
        // 收起后品牌格不放品牌，只放展开按钮
        Brand?.Visible = !rail;
        collapseButton.Visible = collapsible;
        nav.Visible = Nav is not null;
        fullscreenButton.Visible = Fullscreenable && fullscreenFits;
        sidebar.Visible = hasSidebar && wide && !IsFullscreen;
        asideScroll.Visible = Aside is not null && asideWide;
        collapseButton.Icon = IsSidebarCollapsed ? Lucide.PanelLeftOpen : Lucide.PanelLeftClose;
        sidebarContent.Padding = new Padding(rail ? Space(Tokens.Space.Xs) : Space(Tokens.Space.Md), xl, rail ? Space(Tokens.Space.Xs) : Space(Tokens.Space.Md), xl);
        if (Sidebar is not null) foreach (var list in Descendants(Sidebar).OfType<NavListControl>()) list.ScopeCompact = rail;
        main.Padding = new Padding(xl);
        // 页脚是 sm 字号：内边距按它自己的字号算
        var footerEm = Tokens.FontSize.Sm * DeviceDpi / 96f;
        footer.Padding = new Padding((int)MathF.Round(Tokens.Space.Xl * footerEm), (int)MathF.Round(Tokens.Space.Lg * footerEm), (int)MathF.Round(Tokens.Space.Xl * footerEm), (int)MathF.Round(Tokens.Space.Lg * footerEm));
        footer.Visible = Footer is not null;
        asideContent.Padding = new Padding(lg, xl, lg, xl);
        fullscreenExit.Visible = IsFullscreen;
        fullscreenExit.BringToFront();
        UpdateTexts();
        drawer.Toggle(IsSidebarOpen && hasSidebar && !wide && !IsFullscreen);
        if (layout) PerformLayout();
    }

    static IEnumerable<Control> Descendants(Control parent)
    {
        yield return parent;
        foreach (Control child in parent.Controls)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    /// <summary>文案：外壳的按钮取页面组合的文案表，窗口按钮取桌面的文案表（都按所在的语言包）。</summary>
    void UpdateTexts()
    {
        var lang = ProviderControl.LangOf(this);
        var messages = PatternsMessages.For(lang);
        var desktop = DesktopMessages.For(lang);
        menuButton.AccessibleName = messages.NavigationOpen;
        fullscreenButton.AccessibleName = messages.FullscreenEnter;
        fullscreenExit.AccessibleName = messages.FullscreenExit;
        collapseButton.AccessibleName = IsSidebarCollapsed ? messages.SidebarExpand : messages.SidebarCollapse;
        drawer.Title = messages.Navigation;
        var maximized = Form?.WindowState == FormWindowState.Maximized;
        minimizeButton.Label = desktop.WindowMinimize;
        maximizeButton.Label = maximized ? desktop.WindowRestore : desktop.WindowMaximize;
        maximizeButton.IsMaximized = maximized;
        closeButton.Label = desktop.WindowClose;
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        AttachForm();
        UpdateTexts();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Sync();
    }

    /// <summary>整窗的底色（background）。</summary>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(ProviderControl.ThemeOf(this).Colors.Background);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    /// <summary>Esc：先关抽屉，再退出全屏（窗体的 KeyPreview 之外也收得到：外壳在窗体上挂了处理）。</summary>
    internal bool HandleEscape()
    {
        if (IsSidebarOpen && drawer.Visible) IsSidebarOpen = false;
        else if (IsFullscreen) IsFullscreen = false;
        else return false;
        return true;
    }

    /// <inheritdoc />
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && HandleEscape()) return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>半透明色叠在底色上（WinForms 的子控件不能半透明，顶栏的 surfaceTranslucent 直接合成）。</summary>
    internal static Color Blend(Color over, Color under)
    {
        var a = over.A / 255f;
        int Mix(int top, int bottom) => (int)MathF.Round(top * a + bottom * (1 - a));
        return Color.FromArgb(255, Mix(over.R, under.R), Mix(over.G, under.G), Mix(over.B, under.B));
    }
}
