using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using LayoutGrid = Avalonia.Controls.Grid;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 应用外壳（vue-tsx-pro 的 AppShell）：顶栏、左侧栏（导航）、正文、右侧栏（本页目录），桌面应用的整窗骨架。
/// <code>
/// &lt;Window …&gt;
///   &lt;rune:AppShell Brand="{…AppShellBrand}" Header="{…}" Sidebar="{…NavList}" Aside="{…TableOfContents}"&gt;
///     …正文…
///   &lt;/rune:AppShell&gt;
/// &lt;/Window&gt;
/// </code>
/// 按外壳自己的宽度排版（TS 按视口，断点同 sizes.lg / sizes.xl）：1024 以上显示左侧栏，更窄时收进从起始边滑出的抽屉，顶栏最前面出现打开它的按钮；
/// 抽屉里点了导航项、或变宽时自动关上。1280 以上显示右侧栏，更窄时不显示。两侧栏与正文各自滚动（ScrollArea），页脚在正文下面、内容少时贴着底部。
/// 写了 <see cref="Brand"/> 且有侧栏时，宽屏下品牌这一格与侧栏同宽，侧栏的竖线一直延伸到顶栏顶端，顶栏的内容与正文的起始边对齐。
/// <see cref="Nav"/> 是顶栏里品牌后面的导航（只有它时是顶部导航布局，和侧栏一起时是混合布局）；<see cref="SidebarCollapsible"/> 在品牌格的结束边加"收起侧栏"（宽屏时），
/// 收起后只剩图标栏（侧栏里的 NavList 变紧凑），品牌格变窄、只放居中的展开按钮（不放品牌）；没写 Brand 时也有这一格放按钮。
/// 全屏（<see cref="Fullscreenable"/>，默认开，窄于 768 时不显示按钮）：不显示顶栏和左侧栏，正文占满，外壳是窗口内容时窗口同时全屏；右上角留一个退出按钮，Esc 也退出。
/// 桌面特有（上游没有）：外壳是窗口的内容时，顶栏兼作窗口的标题栏（<see cref="ExtendsContentIntoTitleBar"/>，默认开）：去掉系统的标题栏，
/// 顶栏的空白处拖动窗口、双击最大化，结束边是自绘的最小化、最大化（还原）、关闭（悬停 danger 底）；系统的缩放边框、贴边与 Windows 11 的贴靠布局都保留。
/// 窄屏的抽屉先用外壳自己的面板（遮罩 + 起始边的面板），Drawer 做好后换成它。
/// 部件是子元素，名字见 <see cref="AppShellParts"/>：<c>rune|AppShell Border#PART_Header</c>……
/// 伪类：<c>:wide</c>（侧栏常驻）、<c>:rail</c>（侧栏收成了图标栏）、<c>:fullscreen</c>、<c>:titlebar</c>（顶栏兼作标题栏）。
/// </summary>
[PseudoClasses(":wide", ":rail", ":fullscreen", ":titlebar")]
public class AppShell : Control
{
    // 上游按 rem（16px）写死的尺寸：顶栏高、侧栏宽、图标栏宽、右侧栏宽
    const double HeaderHeight = 48;
    const double SidebarWidth = 256;
    const double RailWidth = 56;
    const double AsideWidth = 224;

    /// <summary>顶栏起始边的品牌（一般是 <see cref="AppShellBrand"/>）：有侧栏时这一格与侧栏同宽。</summary>
    public static readonly StyledProperty<object?> BrandProperty = AvaloniaProperty.Register<AppShell, object?>(nameof(Brand));

    /// <summary>顶栏的内容：搜索、本页的页签、主题切换……</summary>
    public static readonly StyledProperty<object?> HeaderProperty = AvaloniaProperty.Register<AppShell, object?>(nameof(Header));

    /// <summary>顶栏里品牌后面的导航。</summary>
    public static readonly StyledProperty<object?> NavProperty = AvaloniaProperty.Register<AppShell, object?>(nameof(Nav));

    /// <summary>左侧栏：应用的导航；窄时收进抽屉。</summary>
    public static readonly StyledProperty<object?> SidebarProperty = AvaloniaProperty.Register<AppShell, object?>(nameof(Sidebar));

    /// <summary>右侧栏：本页目录等辅助内容；较窄时不显示。</summary>
    public static readonly StyledProperty<object?> AsideProperty = AvaloniaProperty.Register<AppShell, object?>(nameof(Aside));

    /// <summary>正文下面的页脚。</summary>
    public static readonly StyledProperty<object?> FooterProperty = AvaloniaProperty.Register<AppShell, object?>(nameof(Footer));

    /// <summary>正文。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<AppShell, object?>(nameof(Content));

    /// <summary>品牌格的结束边有"收起侧栏"按钮（宽屏时）。</summary>
    public static readonly StyledProperty<bool> SidebarCollapsibleProperty = AvaloniaProperty.Register<AppShell, bool>(nameof(SidebarCollapsible));

    /// <summary>侧栏收起了（宽屏时成图标栏）。默认双向绑定。</summary>
    public static readonly StyledProperty<bool> IsSidebarCollapsedProperty =
        AvaloniaProperty.Register<AppShell, bool>(nameof(IsSidebarCollapsed), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>窄时的侧栏抽屉开着。默认双向绑定。</summary>
    public static readonly StyledProperty<bool> IsSidebarOpenProperty =
        AvaloniaProperty.Register<AppShell, bool>(nameof(IsSidebarOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>顶栏结束边有全屏按钮，默认开。</summary>
    public static readonly StyledProperty<bool> FullscreenableProperty = AvaloniaProperty.Register<AppShell, bool>(nameof(Fullscreenable), true);

    /// <summary>全屏中。默认双向绑定。</summary>
    public static readonly StyledProperty<bool> IsFullscreenProperty =
        AvaloniaProperty.Register<AppShell, bool>(nameof(IsFullscreen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>外壳是窗口的内容时，顶栏兼作标题栏（自绘窗口按钮），默认开。</summary>
    public static readonly StyledProperty<bool> ExtendsContentIntoTitleBarProperty =
        AvaloniaProperty.Register<AppShell, bool>(nameof(ExtendsContentIntoTitleBar), true);

    /// <summary>整窗的底色（主题里是 background）。</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<AppShell>();

    readonly LayoutGrid root = new() { RowDefinitions = new RowDefinitions("Auto,*") };
    readonly Border header = new() { Name = AppShellParts.Header, Height = HeaderHeight };
    readonly LayoutGrid headerLayout = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
    readonly Flex headerRow = new() { Align = FlexAlign.Center };
    readonly IconButton menuButton = new() { Name = AppShellParts.MenuButton, Icon = Lucide.Menu };
    readonly Border brand = new() { Name = AppShellParts.Brand };
    readonly Flex brandRow = new() { Align = FlexAlign.Center, Gap = Space.Sm };
    readonly ContentPresenter brandContent = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly ContentPresenter nav = new() { Name = AppShellParts.Nav, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center };
    readonly Flex headerContent = new() { Name = AppShellParts.HeaderContent, Align = FlexAlign.Center, Gap = Space.Md };
    readonly ContentPresenter headerMain = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly IconButton fullscreenButton = new() { Name = AppShellParts.FullscreenButton, Icon = Lucide.Maximize };
    readonly StackPanel captionButtons = new() { Name = AppShellParts.CaptionButtons, Orientation = Orientation.Horizontal, IsVisible = false };
    readonly WindowCaptionButton minimizeButton = new(WindowCaptionKind.Minimize) { Name = AppShellParts.MinimizeButton };
    readonly WindowCaptionButton maximizeButton = new(WindowCaptionKind.Maximize) { Name = AppShellParts.MaximizeButton };
    readonly WindowCaptionButton closeButton = new(WindowCaptionKind.Close) { Name = AppShellParts.CloseButton };
    readonly LayoutGrid body = new() { Name = AppShellParts.Body, ColumnDefinitions = new ColumnDefinitions("0,*,0") };
    readonly Border sidebar = new() { Name = AppShellParts.Sidebar };
    readonly DockPanel sidebarLayout = new();
    readonly ScrollArea sidebarScroll = new() { Name = AppShellParts.SidebarScroll };
    readonly ContentPresenter sidebarContent = new() { Name = AppShellParts.SidebarContent };
    readonly IconButton collapseButton = new() { Name = AppShellParts.Collapse, Size = TextSize.Sm };
    readonly ScrollArea mainScroll = new() { Name = AppShellParts.MainScroll };
    readonly LayoutGrid column = new() { RowDefinitions = new RowDefinitions("*,Auto") };
    readonly ContentPresenter main = new() { Name = AppShellParts.Main };
    readonly ContentPresenter footer = new() { Name = AppShellParts.Footer };
    readonly Border aside = new() { Name = AppShellParts.Aside };
    readonly ScrollArea asideScroll = new() { Name = AppShellParts.AsideScroll };
    readonly ContentPresenter asideContent = new() { Name = AppShellParts.AsideContent };
    readonly IconButton fullscreenExit = new() { Name = AppShellParts.FullscreenExit, Icon = Lucide.Minimize };
    readonly Panel drawerLayer = new() { IsVisible = false };
    readonly Border scrim = new() { Name = AppShellParts.Scrim };
    readonly Border drawer = new() { Name = AppShellParts.Drawer, HorizontalAlignment = HorizontalAlignment.Left };
    readonly DockPanel drawerLayout = new();
    readonly Flex drawerHeader = new() { Name = AppShellParts.DrawerHeader, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly H2 drawerTitle = new() { Size = TextSize.Lg };
    readonly CloseButton drawerClose = new() { Name = AppShellParts.DrawerClose };
    readonly ScrollArea drawerScroll = new() { Name = AppShellParts.DrawerScroll };
    readonly ContentPresenter drawerContent = new() { Name = AppShellParts.DrawerContent };
    TopLevel? top;
    Window? window;
    WindowDecorations restoreDecorations;
    WindowState restoreState;
    bool wide;
    bool asideWide;
    bool fullscreenFits;

    static AppShell() => AffectsRender<AppShell>(BackgroundProperty);

    /// <summary>新建：顶栏、主体（侧栏、正文、右侧栏）、全屏的退出按钮、窄时的抽屉。</summary>
    public AppShell()
    {
        brandRow.Children.AddRange([brandContent, collapseButton]);
        brand.Child = brandRow;
        Flex.SetGrow(headerMain, true);
        headerContent.Children.AddRange([headerMain, fullscreenButton]);
        headerRow.Children.AddRange([menuButton, brand, nav, headerContent]);
        captionButtons.Children.AddRange([minimizeButton, maximizeButton, closeButton]);
        LayoutGrid.SetColumn(captionButtons, 1);
        headerLayout.Children.AddRange([headerRow, captionButtons]);
        header.Child = headerLayout;
        AutomationProperties.SetLandmarkType(header, AutomationLandmarkType.Banner);

        sidebarScroll.Content = sidebarContent;
        sidebarLayout.Children.Add(sidebarScroll);
        sidebar.Child = sidebarLayout;
        LayoutGrid.SetRow(footer, 1);
        column.Children.AddRange([main, footer]);
        mainScroll.Content = column;
        AutomationProperties.SetLandmarkType(main, AutomationLandmarkType.Main);
        AutomationProperties.SetLandmarkType(footer, AutomationLandmarkType.ContentInfo);
        asideScroll.Content = asideContent;
        aside.Child = asideScroll;
        AutomationProperties.SetLandmarkType(aside, AutomationLandmarkType.Complementary);
        LayoutGrid.SetColumn(mainScroll, 1);
        LayoutGrid.SetColumn(aside, 2);
        body.Children.AddRange([sidebar, mainScroll, aside]);
        LayoutGrid.SetRow(body, 1);

        Flex.SetGrow(drawerTitle, true);
        drawerHeader.Children.AddRange([drawerTitle, drawerClose]);
        DockPanel.SetDock(drawerHeader, Dock.Top);
        drawerScroll.Content = drawerContent;
        drawerLayout.Children.AddRange([drawerHeader, drawerScroll]);
        drawer.Child = drawerLayout;
        drawerLayer.Children.AddRange([scrim, drawer]);
        AutomationProperties.SetLabeledBy(drawer, drawerTitle);
        LayoutGrid.SetRowSpan(drawerLayer, 2);
        LayoutGrid.SetRowSpan(fullscreenExit, 2);

        root.Children.AddRange([header, body, fullscreenExit, drawerLayer]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);

        menuButton.Click += (_, _) => SetCurrentValue(IsSidebarOpenProperty, true);
        fullscreenButton.Click += (_, _) => SetCurrentValue(IsFullscreenProperty, true);
        fullscreenExit.Click += (_, _) => SetCurrentValue(IsFullscreenProperty, false);
        collapseButton.Click += (_, _) => SetCurrentValue(IsSidebarCollapsedProperty, !IsSidebarCollapsed);
        minimizeButton.Click += (_, _) => window?.WindowState = WindowState.Minimized;
        maximizeButton.Click += (_, _) => window?.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        closeButton.Click += (_, _) => window?.Close();
        scrim.PointerPressed += (_, _) => SetCurrentValue(IsSidebarOpenProperty, false);
        drawerClose.Click += (_, _) => SetCurrentValue(IsSidebarOpenProperty, false);
        // 抽屉里点了导航项（通常是换页）：关上抽屉
        drawer.AddHandler(NavList.NavigateEvent, (_, _) => SetCurrentValue(IsSidebarOpenProperty, false), handledEventsToo: true);
        mainScroll.ScrollChanged += (_, _) => column.MinHeight = mainScroll.Viewport.Height;
        UpdateSlots();
    }

    /// <summary>品牌。</summary>
    public object? Brand { get => GetValue(BrandProperty); set => SetValue(BrandProperty, value); }

    /// <summary>顶栏的内容。</summary>
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>顶栏的导航。</summary>
    public object? Nav { get => GetValue(NavProperty); set => SetValue(NavProperty, value); }

    /// <summary>左侧栏。</summary>
    public object? Sidebar { get => GetValue(SidebarProperty); set => SetValue(SidebarProperty, value); }

    /// <summary>右侧栏。</summary>
    public object? Aside { get => GetValue(AsideProperty); set => SetValue(AsideProperty, value); }

    /// <summary>页脚。</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>正文。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>侧栏能收起。</summary>
    public bool SidebarCollapsible { get => GetValue(SidebarCollapsibleProperty); set => SetValue(SidebarCollapsibleProperty, value); }

    /// <summary>侧栏收起了。</summary>
    public bool IsSidebarCollapsed { get => GetValue(IsSidebarCollapsedProperty); set => SetValue(IsSidebarCollapsedProperty, value); }

    /// <summary>侧栏抽屉开着。</summary>
    public bool IsSidebarOpen { get => GetValue(IsSidebarOpenProperty); set => SetValue(IsSidebarOpenProperty, value); }

    /// <summary>有全屏按钮。</summary>
    public bool Fullscreenable { get => GetValue(FullscreenableProperty); set => SetValue(FullscreenableProperty, value); }

    /// <summary>全屏中。</summary>
    public bool IsFullscreen { get => GetValue(IsFullscreenProperty); set => SetValue(IsFullscreenProperty, value); }

    /// <summary>顶栏兼作标题栏。</summary>
    public bool ExtendsContentIntoTitleBar { get => GetValue(ExtendsContentIntoTitleBarProperty); set => SetValue(ExtendsContentIntoTitleBarProperty, value); }

    /// <summary>底色。</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>正文那一列的滚动区域：本页目录（<see cref="TableOfContents.ScrollRoot"/>）给它。</summary>
    public ScrollViewer MainScroll => mainScroll;

    /// <summary>侧栏收成了图标栏：有侧栏、能收起、收起了，并且是宽屏（窄时侧栏在抽屉里，抽屉总是完整的）。</summary>
    bool Rail => Sidebar is not null && SidebarCollapsible && IsSidebarCollapsed && wide;

    /// <summary>顶栏兼作窗口的标题栏（测试用）。</summary>
    internal bool IsTitleBar => window is not null && ExtendsContentIntoTitleBar;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BrandProperty || change.Property == HeaderProperty || change.Property == NavProperty || change.Property == SidebarProperty
            || change.Property == AsideProperty || change.Property == FooterProperty || change.Property == ContentProperty) UpdateSlots();
        else if (change.Property == IsSidebarOpenProperty) ToggleDrawer();
        else if (change.Property == IsFullscreenProperty) ApplyFullscreen();
        else if (change.Property == ExtendsContentIntoTitleBarProperty) ApplyChrome();
        else if (change.Property == SidebarCollapsibleProperty || change.Property == IsSidebarCollapsedProperty || change.Property == FullscreenableProperty
                 || change.Property == TextElement.FontSizeProperty) Sync();
        else if (change.Property == Provider.LangProperty) UpdateTexts();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        UpdateTexts();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Esc 在整个窗口里都算（TS 监听 document）：焦点不在外壳里时也能退出全屏
        top = TopLevel.GetTopLevel(this);
        top?.AddHandler(KeyDownEvent, OnKeyDown);
        // 只有外壳就是窗口的内容时才接管窗口（嵌在页面里的外壳不动窗口）
        if (TopLevel.GetTopLevel(this) is Window host && host.Content == this)
        {
            window = host;
            restoreDecorations = host.WindowDecorations;
            host.PropertyChanged += OnWindowPropertyChanged;
            ApplyChrome();
            ApplyFullscreen();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        top?.RemoveHandler(KeyDownEvent, OnKeyDown);
        top = null;
        if (window is null) return;
        window.PropertyChanged -= OnWindowPropertyChanged;
        RestoreChrome(window);
        window = null;
        PseudoClasses.Set(":titlebar", false);
        captionButtons.IsVisible = false;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Background is { } background) context.FillRectangle(background, new Rect(Bounds.Size));
    }

    /// <summary>先按可用宽度定断点（TS 的媒体查询），再排版：侧栏常驻、右侧栏显示、全屏按钮显示。</summary>
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        var width = availableSize.Width;
        var nextWide = width >= this.Resource<double>(ThemeKeys.Size.Lg);
        var nextAsideWide = width >= this.Resource<double>(ThemeKeys.Size.Xl);
        var nextFits = width >= this.Resource<double>(ThemeKeys.Size.Md);
        if (nextWide != wide || nextAsideWide != asideWide || nextFits != fullscreenFits)
        {
            // 变宽后侧栏已经常驻，抽屉没必要再开着
            if (nextWide && !wide) SetCurrentValue(IsSidebarOpenProperty, false);
            (wide, asideWide, fullscreenFits) = (nextWide, nextAsideWide, nextFits);
            Sync();
        }
        return base.MeasureOverride(availableSize);
    }

    void UpdateSlots()
    {
        brandContent.Content = Brand;
        nav.Content = Nav;
        headerMain.Content = Header;
        asideContent.Content = Aside;
        main.Content = Content;
        footer.Content = Footer;
        PlaceSidebar();
        Sync();
    }

    /// <summary>侧栏的内容只能放一处：宽时在常驻的侧栏里，窄时在抽屉里（TS 渲染两份）。先腾出来再放进去。</summary>
    void PlaceSidebar()
    {
        var target = wide ? sidebarContent : drawerContent;
        var other = wide ? drawerContent : sidebarContent;
        other.Content = null;
        target.Content = Sidebar;
    }

    /// <summary>按断点、全屏、收起排版：顶栏的各格、主体的列、侧栏的图标栏。</summary>
    void Sync()
    {
        var hasSidebar = Sidebar is not null;
        var hasBrand = Brand is not null;
        var rail = Rail;
        // 侧栏能收起：品牌格里有收起、展开按钮（宽屏时；窄屏时侧栏在抽屉里），没写 Brand 时也给出这一格放它
        var collapsible = hasSidebar && SidebarCollapsible && wide;
        var brandCell = hasBrand || collapsible;
        var em = TextElement.GetFontSize(this);
        var lg = this.Resource<double>(ThemeKeys.Space.Lg) * em;
        PseudoClasses.Set(":wide", wide);
        PseudoClasses.Set(":rail", rail);
        PseudoClasses.Set(":fullscreen", IsFullscreen);
        PlaceSidebar();

        header.IsVisible = !IsFullscreen;
        menuButton.IsVisible = hasSidebar && !wide;
        brand.IsVisible = brandCell;
        nav.IsVisible = Nav is not null;
        // 有导航时右边的工具按内容宽、靠结束边
        Flex.SetGrow(headerContent, Nav is null);
        Flex.SetGrow(nav, Nav is not null);
        fullscreenButton.IsVisible = Fullscreenable && fullscreenFits;
        // 品牌格与侧栏同宽（宽时）：贴着起始边，与内容之间隔开正文的内边距（space.xl），内容与正文的起始边对齐
        var brandRail = brandCell && hasSidebar && wide;
        headerRow.Gap = brandRail ? Space.Xl : Space.Md;
        headerRow.Margin = new Thickness(brandRail ? 0 : lg, 0, lg, 0);
        brand.Width = brandRail ? rail ? RailWidth : SidebarWidth : double.NaN;
        Flex.SetAlignSelf(brand, brandRail ? FlexAlign.Stretch : FlexAlign.Center);
        brand.Padding = brandRail ? new Thickness(rail ? this.Resource<double>(ThemeKeys.Space.Xs) * em : lg, 0) : default;
        brand.BorderThickness = brandRail ? new Thickness(0, 0, 1, 0) : default;
        // 收起后品牌格只放展开按钮（居中）；展开时按钮贴着结束边
        brandContent.IsVisible = hasBrand && !rail;
        Flex.SetGrow(brandContent, collapsible);
        brandRow.Justify = rail ? FlexJustify.Center : FlexJustify.Start;

        var showSidebar = hasSidebar && wide && !IsFullscreen;
        sidebar.IsVisible = showSidebar;
        body.ColumnDefinitions[0].Width = new GridLength(showSidebar ? rail ? RailWidth : SidebarWidth : 0);
        aside.IsVisible = Aside is not null && asideWide;
        body.ColumnDefinitions[2].Width = new GridLength(aside.IsVisible ? AsideWidth : 0);
        collapseButton.IsVisible = collapsible;
        collapseButton.Icon = IsSidebarCollapsed ? Lucide.PanelLeftOpen : Lucide.PanelLeftClose;
        NavList.SetCompactScope(sidebarContent, rail);
        footer.IsVisible = Footer is not null;
        fullscreenExit.IsVisible = IsFullscreen;
        UpdateTexts();
        ToggleDrawer();
    }

    /// <summary>文案：外壳的按钮取页面组合的文案表，窗口按钮取桌面的文案表（都按所在的语言包）。</summary>
    void UpdateTexts()
    {
        var lang = Provider.GetLang(this);
        var messages = PatternsMessages.For(lang);
        var desktop = DesktopMessages.For(lang);
        AutomationProperties.SetName(menuButton, messages.NavigationOpen);
        AutomationProperties.SetName(fullscreenButton, messages.FullscreenEnter);
        ToolTip.SetTip(fullscreenButton, messages.FullscreenEnter);
        AutomationProperties.SetName(fullscreenExit, messages.FullscreenExit);
        ToolTip.SetTip(fullscreenExit, messages.FullscreenExit);
        AutomationProperties.SetName(collapseButton, IsSidebarCollapsed ? messages.SidebarExpand : messages.SidebarCollapse);
        drawerTitle.Text = messages.Navigation;
        var maximized = window?.WindowState == WindowState.Maximized;
        minimizeButton.Label = desktop.WindowMinimize;
        maximizeButton.Label = maximized ? desktop.WindowRestore : desktop.WindowMaximize;
        maximizeButton.IsMaximized = maximized;
        closeButton.Label = desktop.WindowClose;
    }

    /// <summary>窄时的抽屉：打开时焦点进到抽屉，关上时回到打开它的按钮。</summary>
    void ToggleDrawer()
    {
        var open = IsSidebarOpen && Sidebar is not null && !wide && !IsFullscreen;
        if (open == drawerLayer.IsVisible) return;
        var focusInside = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused && drawer.IsVisualAncestorOf(focused);
        drawerLayer.IsVisible = open;
        drawer.Width = Math.Min(this.Resource<double>(ThemeKeys.Size.Xs), Bounds.Width);
        if (open) drawerClose.Focus();
        else if (focusInside) menuButton.Focus();
    }

    /// <summary>全屏：不显示顶栏和侧栏；外壳是窗口的内容时窗口也全屏，退出时回到原来的状态。</summary>
    void ApplyFullscreen()
    {
        if (IsFullscreen) SetCurrentValue(IsSidebarOpenProperty, false);
        if (window is { } host)
        {
            if (IsFullscreen && host.WindowState != WindowState.FullScreen)
            {
                restoreState = host.WindowState;
                host.WindowState = WindowState.FullScreen;
            }
            else if (!IsFullscreen && host.WindowState == WindowState.FullScreen) host.WindowState = restoreState;
        }
        Sync();
    }

    /// <summary>顶栏兼作标题栏：去掉系统的标题栏、保留缩放边框，内容延伸到原来标题栏的位置；窗口按钮自绘。</summary>
    void ApplyChrome()
    {
        if (window is not { } host) return;
        if (ExtendsContentIntoTitleBar)
        {
            host.WindowDecorations = WindowDecorations.BorderOnly;
            host.ExtendClientAreaToDecorationsHint = true;
        }
        else RestoreChrome(host);
        PseudoClasses.Set(":titlebar", ExtendsContentIntoTitleBar);
        captionButtons.IsVisible = ExtendsContentIntoTitleBar;
        UpdateCaptionButtons();
    }

    void RestoreChrome(Window host)
    {
        host.ExtendClientAreaToDecorationsHint = false;
        host.WindowDecorations = restoreDecorations;
    }

    /// <summary>窗口不能缩放时没有最大化，不能最小化时没有最小化（同系统标题栏）。</summary>
    void UpdateCaptionButtons()
    {
        if (window is not { } host) return;
        minimizeButton.IsVisible = host.CanMinimize;
        maximizeButton.IsVisible = host.CanMaximize;
        UpdateTexts();
    }

    void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            // 从别处退出了窗口全屏（系统快捷键）：外壳也退出
            if (IsFullscreen && e.GetOldValue<WindowState>() == WindowState.FullScreen && e.GetNewValue<WindowState>() != WindowState.FullScreen)
                SetCurrentValue(IsFullscreenProperty, false);
            UpdateTexts();
        }
        else if (e.Property == Window.CanMinimizeProperty || e.Property == Window.CanMaximizeProperty) UpdateCaptionButtons();
    }

    /// <summary>Esc：先关抽屉，再退出全屏。里面已经处理了的 Esc 不算（TS 同）。</summary>
    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled) return;
        if (drawerLayer.IsVisible) SetCurrentValue(IsSidebarOpenProperty, false);
        else if (IsFullscreen) SetCurrentValue(IsFullscreenProperty, false);
        else return;
        e.Handled = true;
    }

}

/// <summary>应用外壳的部件名：样式里用 <c>rune|AppShell Border#PART_Header</c> 选中。</summary>
public static class AppShellParts
{
    /// <summary>顶栏（TS 的 propsHeader）。</summary>
    public const string Header = "PART_Header";

    /// <summary>窄时打开侧栏的按钮（TS 的 propsMenuButton）。</summary>
    public const string MenuButton = "PART_MenuButton";

    /// <summary>品牌格（TS 的 propsBrand）。</summary>
    public const string Brand = "PART_Brand";

    /// <summary>顶栏里导航那一格（TS 的 propsNav）。</summary>
    public const string Nav = "PART_Nav";

    /// <summary>顶栏里 Header 内容那一格（TS 的 propsHeaderContent）。</summary>
    public const string HeaderContent = "PART_HeaderContent";

    /// <summary>全屏按钮（TS 的 propsFullscreenButton）。</summary>
    public const string FullscreenButton = "PART_FullscreenButton";

    /// <summary>全屏时右上角的退出按钮（TS 的 propsFullscreenExit）。</summary>
    public const string FullscreenExit = "PART_FullscreenExit";

    /// <summary>窗口按钮那一组（桌面特有）。</summary>
    public const string CaptionButtons = "PART_CaptionButtons";

    /// <summary>最小化。</summary>
    public const string MinimizeButton = "PART_MinimizeButton";

    /// <summary>最大化（还原）。</summary>
    public const string MaximizeButton = "PART_MaximizeButton";

    /// <summary>关闭窗口。</summary>
    public const string CloseButton = "PART_CloseButton";

    /// <summary>顶栏下面的主体（TS 的 propsBody）。</summary>
    public const string Body = "PART_Body";

    /// <summary>左侧栏（TS 的 propsSidebar）。</summary>
    public const string Sidebar = "PART_Sidebar";

    /// <summary>左侧栏里的滚动区域（TS 的 propsSidebarScroll）。</summary>
    public const string SidebarScroll = "PART_SidebarScroll";

    /// <summary>左侧栏的内容层（内边距）。</summary>
    public const string SidebarContent = "PART_SidebarContent";

    /// <summary>品牌格里"收起侧栏"、"展开侧栏"的按钮（TS 的 propsCollapse，部件 collapseButton）。</summary>
    public const string Collapse = "PART_Collapse";

    /// <summary>正文那一列的滚动区域（TS 是整页滚动）。</summary>
    public const string MainScroll = "PART_MainScroll";

    /// <summary>正文（TS 的 propsMain）。</summary>
    public const string Main = "PART_Main";

    /// <summary>页脚（TS 的 propsFooter）。</summary>
    public const string Footer = "PART_Footer";

    /// <summary>右侧栏（TS 的 propsAside）。</summary>
    public const string Aside = "PART_Aside";

    /// <summary>右侧栏里的滚动区域（TS 的 propsAsideScroll）。</summary>
    public const string AsideScroll = "PART_AsideScroll";

    /// <summary>右侧栏的内容层（内边距）。</summary>
    public const string AsideContent = "PART_AsideContent";

    /// <summary>抽屉后面的遮罩。</summary>
    public const string Scrim = "PART_Scrim";

    /// <summary>窄时的侧栏抽屉（TS 的 propsDrawer）。</summary>
    public const string Drawer = "PART_Drawer";

    /// <summary>抽屉的标题行。</summary>
    public const string DrawerHeader = "PART_DrawerHeader";

    /// <summary>抽屉的关闭按钮。</summary>
    public const string DrawerClose = "PART_DrawerClose";

    /// <summary>抽屉里的滚动区域。</summary>
    public const string DrawerScroll = "PART_DrawerScroll";

    /// <summary>抽屉的内容层（内边距）。</summary>
    public const string DrawerContent = "PART_DrawerContent";
}
