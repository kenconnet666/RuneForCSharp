using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

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
/// <see cref="Nav"/> 是顶栏里品牌后面的导航（只有它时是顶部导航布局，和侧栏一起时是混合布局）；<see cref="SidebarCollapsible"/> 在侧栏底部加"收起侧栏"，
/// 收起后只剩图标栏（宽屏时；侧栏里的 NavList 变紧凑，品牌格只剩标志）。
/// 全屏（<see cref="Fullscreenable"/>，默认开，窄于 768 时不显示按钮）：不显示顶栏和左侧栏，正文占满，外壳是窗口内容时窗口同时全屏；右上角留一个退出按钮，Esc 也退出。
/// 桌面特有（上游没有）：外壳是窗口的内容时，顶栏兼作窗口的标题栏（<see cref="ExtendsContentIntoTitleBar"/>，默认开）：去掉系统的标题栏（WindowChrome），
/// 顶栏的空白处拖动窗口、双击最大化，结束边是自绘的最小化、最大化（还原）、关闭（悬停 danger 底）；系统的缩放边框、贴边与 Windows 11 的贴靠布局都保留。
/// 窄屏的抽屉先用外壳自己的面板（遮罩 + 起始边的面板），Drawer 做好后换成它。部件样式键：<see cref="AppShellParts"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public partial class AppShell : FrameworkElement
{
    // 上游按 rem（16px）写死的尺寸：顶栏高、侧栏宽、图标栏宽、右侧栏宽
    const double HeaderHeight = 48;
    const double SidebarWidth = 256;
    const double RailWidth = 56;
    const double AsideWidth = 224;

    /// <summary>顶栏起始边的品牌（一般是 <see cref="AppShellBrand"/>）：有侧栏时这一格与侧栏同宽。</summary>
    public static readonly DependencyProperty BrandProperty = Slot(nameof(Brand));

    /// <summary>顶栏的内容：搜索、本页的页签、主题切换……</summary>
    public static readonly DependencyProperty HeaderProperty = Slot(nameof(Header));

    /// <summary>顶栏里品牌后面的导航。</summary>
    public static readonly DependencyProperty NavProperty = Slot(nameof(Nav));

    /// <summary>左侧栏：应用的导航；窄时收进抽屉。</summary>
    public static readonly DependencyProperty SidebarProperty = Slot(nameof(Sidebar));

    /// <summary>右侧栏：本页目录等辅助内容；较窄时不显示。</summary>
    public static readonly DependencyProperty AsideProperty = Slot(nameof(Aside));

    /// <summary>正文下面的页脚。</summary>
    public static readonly DependencyProperty FooterProperty = Slot(nameof(Footer));

    /// <summary>正文。</summary>
    public static readonly DependencyProperty ContentProperty = Slot(nameof(Content));

    /// <summary>侧栏底部有"收起侧栏"按钮。</summary>
    public static readonly DependencyProperty SidebarCollapsibleProperty = DependencyProperty.Register(nameof(SidebarCollapsible), typeof(bool), typeof(AppShell),
        new FrameworkPropertyMetadata(false, (d, _) => ((AppShell)d).Sync()));

    /// <summary>侧栏收起了（宽屏时成图标栏）。默认双向绑定。</summary>
    public static readonly DependencyProperty IsSidebarCollapsedProperty = DependencyProperty.Register(nameof(IsSidebarCollapsed), typeof(bool), typeof(AppShell),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((AppShell)d).Sync()));

    /// <summary>窄时的侧栏抽屉开着。默认双向绑定。</summary>
    public static readonly DependencyProperty IsSidebarOpenProperty = DependencyProperty.Register(nameof(IsSidebarOpen), typeof(bool), typeof(AppShell),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((AppShell)d).ToggleDrawer()));

    /// <summary>顶栏结束边有全屏按钮，默认开。</summary>
    public static readonly DependencyProperty FullscreenableProperty = DependencyProperty.Register(nameof(Fullscreenable), typeof(bool), typeof(AppShell),
        new FrameworkPropertyMetadata(true, (d, _) => ((AppShell)d).Sync()));

    /// <summary>全屏中。默认双向绑定。</summary>
    public static readonly DependencyProperty IsFullscreenProperty = DependencyProperty.Register(nameof(IsFullscreen), typeof(bool), typeof(AppShell),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((AppShell)d).ApplyFullscreen()));

    /// <summary>外壳是窗口的内容时，顶栏兼作标题栏（自绘窗口按钮），默认开。</summary>
    public static readonly DependencyProperty ExtendsContentIntoTitleBarProperty = DependencyProperty.Register(nameof(ExtendsContentIntoTitleBar), typeof(bool), typeof(AppShell),
        new FrameworkPropertyMetadata(true, (d, _) => ((AppShell)d).ApplyChrome()));

    /// <summary>整窗的底色（主题里是 background）。</summary>
    public static readonly DependencyProperty BackgroundProperty = Panel.BackgroundProperty.AddOwner(typeof(AppShell),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    readonly LayoutGrid root = new();
    readonly Border header = new() { Height = HeaderHeight };
    readonly LayoutGrid headerLayout = new();
    readonly Flex headerRow = new() { Align = FlexAlign.Center };
    readonly IconButton menuButton = new() { Icon = Lucide.Menu };
    readonly Border brand = new();
    readonly ContentPresenter brandContent = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly ContentPresenter nav = new();
    readonly Flex headerContent = new() { Align = FlexAlign.Center, Gap = Space.Md };
    readonly ContentPresenter headerMain = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly IconButton fullscreenButton = new() { Icon = Lucide.Maximize };
    readonly StackPanel captionButtons = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
    readonly WindowCaptionButton minimizeButton = new(WindowCaptionKind.Minimize);
    readonly WindowCaptionButton maximizeButton = new(WindowCaptionKind.Maximize);
    readonly WindowCaptionButton closeButton = new(WindowCaptionKind.Close);
    readonly LayoutGrid body = new();
    readonly Border sidebar = new();
    readonly DockPanel sidebarLayout = new();
    readonly ScrollArea sidebarScroll = new();
    readonly Border sidebarContent = new();
    readonly ContentPresenter sidebarPresenter = new();
    readonly Border collapseBar = new();
    readonly IconButton collapseButton = new() { Size = TextSize.Sm, Variant = ButtonVariant.Ghost };
    readonly ScrollArea mainScroll = new();
    readonly LayoutGrid column = new();
    readonly Border main = new();
    readonly ContentPresenter mainPresenter = new();
    readonly Border footer = new();
    readonly ContentPresenter footerPresenter = new();
    readonly Border aside = new();
    readonly ScrollArea asideScroll = new();
    readonly Border asideContent = new();
    readonly ContentPresenter asidePresenter = new();
    readonly IconButton fullscreenExit = new() { Icon = Lucide.Minimize, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    readonly LayoutGrid drawerLayer = new() { Visibility = Visibility.Collapsed };
    readonly Border scrim = new();
    readonly Border drawerShadow = new() { HorizontalAlignment = HorizontalAlignment.Left };
    readonly Border drawer = new() { HorizontalAlignment = HorizontalAlignment.Left };
    readonly DockPanel drawerLayout = new();
    readonly Flex drawerHeader = new() { Align = FlexAlign.Center, Gap = Space.Sm };
    readonly H2 drawerTitle = new() { Size = TextSize.Lg };
    readonly CloseButton drawerClose = new();
    readonly ScrollArea drawerScroll = new();
    readonly Border drawerContent = new();
    readonly ContentPresenter drawerPresenter = new();
    bool wide;
    bool asideWide;
    bool fullscreenFits;

    static AppShell()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AppShell), new FrameworkPropertyMetadata(typeof(AppShell)));
        // 字号变了重算按 em 的内边距
        TextElement.FontSizeProperty.OverrideMetadata(typeof(AppShell), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((AppShell)d).Sync()));
        Provider.LangProperty.OverrideMetadata(typeof(AppShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((AppShell)d).UpdateTexts()));
    }

    /// <summary>新建：顶栏、主体（侧栏、正文、右侧栏）、全屏的退出按钮、窄时的抽屉。</summary>
    public AppShell()
    {
        Part(header, AppShellParts.Header);
        Part(brand, AppShellParts.Brand);
        Part(sidebar, AppShellParts.Sidebar);
        Part(collapseBar, AppShellParts.CollapseBar);
        Part(main, AppShellParts.Main);
        Part(footer, AppShellParts.Footer);
        Part(aside, AppShellParts.Aside);
        Part(scrim, AppShellParts.Scrim);
        Part(drawer, AppShellParts.Drawer);
        Part(drawerShadow, AppShellParts.DrawerShadow);
        Part(fullscreenExit, AppShellParts.FullscreenExit);
        Part(collapseButton, AppShellParts.Collapse);
        Part(headerContent, AppShellParts.HeaderContent);
        Part(body, AppShellParts.Body);
        Part(sidebarScroll, AppShellParts.SidebarScroll);
        Part(asideScroll, AppShellParts.AsideScroll);
        Part(menuButton, AppShellParts.MenuButton);
        Part(fullscreenButton, AppShellParts.FullscreenButton);

        headerLayout.ColumnDefinitions.Add(new ColumnDefinition());
        headerLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        brand.Child = brandContent;
        Flex.SetGrow(headerMain, true);
        headerContent.Children.Add(headerMain);
        headerContent.Children.Add(fullscreenButton);
        foreach (UIElement child in new UIElement[] { menuButton, brand, nav, headerContent }) headerRow.Children.Add(child);
        foreach (var button in new[] { minimizeButton, maximizeButton, closeButton }) captionButtons.Children.Add(button);
        LayoutGrid.SetColumn(captionButtons, 1);
        headerLayout.Children.Add(headerRow);
        headerLayout.Children.Add(captionButtons);
        header.Child = headerLayout;

        sidebarContent.Child = sidebarPresenter;
        sidebarScroll.Content = sidebarContent;
        collapseBar.Child = collapseButton;
        DockPanel.SetDock(collapseBar, Dock.Bottom);
        sidebarLayout.Children.Add(collapseBar);
        sidebarLayout.Children.Add(sidebarScroll);
        sidebar.Child = sidebarLayout;
        column.RowDefinitions.Add(new RowDefinition());
        column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        LayoutGrid.SetRow(footer, 1);
        main.Child = mainPresenter;
        footer.Child = footerPresenter;
        asideContent.Child = asidePresenter;
        column.Children.Add(main);
        column.Children.Add(footer);
        mainScroll.Content = column;
        asideScroll.Content = asideContent;
        aside.Child = asideScroll;
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        LayoutGrid.SetColumn(mainScroll, 1);
        LayoutGrid.SetColumn(aside, 2);
        body.Children.Add(sidebar);
        body.Children.Add(mainScroll);
        body.Children.Add(aside);
        LayoutGrid.SetRow(body, 1);

        Flex.SetGrow(drawerTitle, true);
        drawerHeader.Children.Add(drawerTitle);
        drawerHeader.Children.Add(drawerClose);
        DockPanel.SetDock(drawerHeader, Dock.Top);
        drawerContent.Child = drawerPresenter;
        drawerScroll.Content = drawerContent;
        drawerLayout.Children.Add(drawerHeader);
        drawerLayout.Children.Add(drawerScroll);
        drawer.Child = drawerLayout;
        drawerLayer.Children.Add(scrim);
        drawerLayer.Children.Add(drawerShadow);
        drawerLayer.Children.Add(drawer);
        AutomationProperties.SetLabeledBy(drawer, drawerTitle);

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        LayoutGrid.SetRowSpan(fullscreenExit, 2);
        LayoutGrid.SetRowSpan(drawerLayer, 2);
        root.Children.Add(header);
        root.Children.Add(body);
        root.Children.Add(fullscreenExit);
        root.Children.Add(drawerLayer);
        AddLogicalChild(root);
        AddVisualChild(root);

        menuButton.Click += (_, _) => SetCurrentValue(IsSidebarOpenProperty, true);
        fullscreenButton.Click += (_, _) => SetCurrentValue(IsFullscreenProperty, true);
        fullscreenExit.Click += (_, _) => SetCurrentValue(IsFullscreenProperty, false);
        collapseButton.Click += (_, _) => SetCurrentValue(IsSidebarCollapsedProperty, !IsSidebarCollapsed);
        minimizeButton.Click += (_, _) => window?.WindowState = WindowState.Minimized;
        maximizeButton.Click += (_, _) => ToggleMaximized();
        closeButton.Click += (_, _) => window?.Close();
        scrim.MouseLeftButtonDown += (_, _) => SetCurrentValue(IsSidebarOpenProperty, false);
        drawerClose.Click += (_, _) => SetCurrentValue(IsSidebarOpenProperty, false);
        // 抽屉里点了导航项（通常是换页）：关上抽屉
        drawer.AddHandler(NavList.NavigateEvent, new RoutedEventHandler((_, _) => SetCurrentValue(IsSidebarOpenProperty, false)), handledEventsToo: true);
        mainScroll.ScrollChanged += (_, _) => column.MinHeight = mainScroll.ViewportHeight;
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
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
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>侧栏能收起。</summary>
    public bool SidebarCollapsible { get => (bool)GetValue(SidebarCollapsibleProperty); set => SetValue(SidebarCollapsibleProperty, value); }

    /// <summary>侧栏收起了。</summary>
    public bool IsSidebarCollapsed { get => (bool)GetValue(IsSidebarCollapsedProperty); set => SetValue(IsSidebarCollapsedProperty, value); }

    /// <summary>侧栏抽屉开着。</summary>
    public bool IsSidebarOpen { get => (bool)GetValue(IsSidebarOpenProperty); set => SetValue(IsSidebarOpenProperty, value); }

    /// <summary>有全屏按钮。</summary>
    public bool Fullscreenable { get => (bool)GetValue(FullscreenableProperty); set => SetValue(FullscreenableProperty, value); }

    /// <summary>全屏中。</summary>
    public bool IsFullscreen { get => (bool)GetValue(IsFullscreenProperty); set => SetValue(IsFullscreenProperty, value); }

    /// <summary>顶栏兼作标题栏。</summary>
    public bool ExtendsContentIntoTitleBar { get => (bool)GetValue(ExtendsContentIntoTitleBarProperty); set => SetValue(ExtendsContentIntoTitleBarProperty, value); }

    /// <summary>底色。</summary>
    public Brush? Background { get => (Brush?)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>正文那一列的滚动区域：本页目录（<see cref="TableOfContents.ScrollRoot"/>）给它。</summary>
    public ScrollViewer MainScroll => mainScroll;

    /// <summary>侧栏收成了图标栏：有侧栏、能收起、收起了，并且是宽屏（窄时侧栏在抽屉里，抽屉总是完整的）。</summary>
    internal bool IsRail => Sidebar is not null && SidebarCollapsible && IsSidebarCollapsed && wide;

    // 部件（测试用）
    internal Border HeaderPart => header;
    internal Border BrandPart => brand;
    internal Flex HeaderContentPart => headerContent;
    internal IconButton MenuButtonPart => menuButton;
    internal IconButton FullscreenButtonPart => fullscreenButton;
    internal IconButton FullscreenExitPart => fullscreenExit;
    internal StackPanel CaptionButtonsPart => captionButtons;
    internal WindowCaptionButton MaximizeButtonPart => maximizeButton;
    internal LayoutGrid BodyPart => body;
    internal Border SidebarPart => sidebar;
    internal ScrollArea SidebarScrollPart => sidebarScroll;
    internal IconButton CollapsePart => collapseButton;
    internal Border FooterPart => footer;
    internal ScrollArea AsideScrollPart => asideScroll;
    internal Border DrawerPart => drawer;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <summary>先按可用宽度定断点（TS 的媒体查询），再排版：侧栏常驻、右侧栏显示、全屏按钮显示。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var nextWide = width >= Resource("Rune.Size.Lg", 1024);
        var nextAsideWide = width >= Resource("Rune.Size.Xl", 1280);
        var nextFits = width >= Resource("Rune.Size.Md", 768);
        if (nextWide != wide || nextAsideWide != asideWide || nextFits != fullscreenFits)
        {
            // 变宽后侧栏已经常驻，抽屉没必要再开着
            if (nextWide && !wide) SetCurrentValue(IsSidebarOpenProperty, false);
            (wide, asideWide, fullscreenFits) = (nextWide, nextAsideWide, nextFits);
            Sync();
        }
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Background is { } background) drawingContext.DrawRectangle(background, null, new Rect(RenderSize));
    }

    static DependencyProperty Slot(string name) => DependencyProperty.Register(name, typeof(object), typeof(AppShell),
        new FrameworkPropertyMetadata(null, (d, _) => ((AppShell)d).UpdateSlots()));

    static void Part(FrameworkElement element, PartKey key) => element.SetResourceReference(StyleProperty, key);

    double Resource(string key, double fallback) => TryFindResource(key) as double? ?? fallback;

    /// <summary>space 档位的系数 × 字号。</summary>
    double Em(string size) => Resource($"Rune.Space.{size}", 1) * TextElement.GetFontSize(this);

    void UpdateSlots()
    {
        brandContent.Content = Brand;
        nav.Content = Nav;
        headerMain.Content = Header;
        asidePresenter.Content = Aside;
        mainPresenter.Content = Content;
        footerPresenter.Content = Footer;
        PlaceSidebar();
        Sync();
    }

    /// <summary>侧栏的内容只能放一处：宽时在常驻的侧栏里，窄时在抽屉里（TS 渲染两份）。先腾出来再放进去。</summary>
    void PlaceSidebar()
    {
        var (target, other) = wide ? (sidebarPresenter, drawerPresenter) : (drawerPresenter, sidebarPresenter);
        other.Content = null;
        target.Content = Sidebar;
    }

    /// <summary>按断点、全屏、收起排版：顶栏的各格、主体的列、侧栏的图标栏、按 em 的内边距。</summary>
    void Sync()
    {
        var hasSidebar = Sidebar is not null;
        var rail = IsRail;
        var lg = Em("Lg");
        var xl = Em("Xl");
        PlaceSidebar();

        header.Visibility = Show(!IsFullscreen);
        menuButton.Visibility = Show(hasSidebar && !wide);
        brand.Visibility = Show(Brand is not null);
        nav.Visibility = Show(Nav is not null);
        // 有导航时右边的工具按内容宽、靠结束边
        Flex.SetGrow(headerContent, Nav is null);
        Flex.SetGrow(nav, Nav is not null);
        fullscreenButton.Visibility = Show(Fullscreenable && fullscreenFits);
        // 品牌格与侧栏同宽（宽时）：贴着起始边，与内容之间隔开正文的内边距（space.xl），内容与正文的起始边对齐
        var brandRail = Brand is not null && hasSidebar && wide;
        headerRow.Gap = brandRail ? Space.Xl : Space.Md;
        headerRow.Margin = new Thickness(brandRail ? 0 : lg, 0, lg, 0);
        brand.Width = brandRail ? rail ? RailWidth : SidebarWidth : double.NaN;
        Flex.SetAlignSelf(brand, brandRail ? FlexAlign.Stretch : FlexAlign.Center);
        brand.Padding = brandRail ? new Thickness(rail ? Em("Xs") : lg, 0, rail ? Em("Xs") : lg, 0) : default;
        brand.BorderThickness = brandRail ? new Thickness(0, 0, 1, 0) : default;
        brandContent.HorizontalAlignment = rail ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        AppShellBrand.SetIsCompact(brand, rail);

        var showSidebar = hasSidebar && wide && !IsFullscreen;
        sidebar.Visibility = Show(showSidebar);
        body.ColumnDefinitions[0].Width = new GridLength(showSidebar ? rail ? RailWidth : SidebarWidth : 0);
        var showAside = Aside is not null && asideWide;
        aside.Visibility = Show(showAside);
        body.ColumnDefinitions[2].Width = new GridLength(showAside ? AsideWidth : 0);
        sidebarContent.Padding = new Thickness(rail ? Em("Xs") : Em("Md"), xl, rail ? Em("Xs") : Em("Md"), xl);
        collapseBar.Visibility = Show(SidebarCollapsible);
        collapseBar.Padding = new Thickness(Em("Sm"));
        collapseButton.HorizontalAlignment = rail ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        collapseButton.Icon = IsSidebarCollapsed ? Lucide.PanelLeftOpen : Lucide.PanelLeftClose;
        NavList.SetCompactScope(sidebarContent, rail);
        main.Padding = new Thickness(xl);
        // 页脚是 sm 字号：内边距按它自己的字号算
        var footerEm = TextElement.GetFontSize(footer);
        footer.Padding = new Thickness(Resource("Rune.Space.Xl", 1.5) * footerEm, Resource("Rune.Space.Lg", 1) * footerEm, Resource("Rune.Space.Xl", 1.5) * footerEm, Resource("Rune.Space.Lg", 1) * footerEm);
        asideContent.Padding = new Thickness(lg, xl, lg, xl);
        footer.Visibility = Show(Footer is not null);
        fullscreenExit.Visibility = Show(IsFullscreen);
        fullscreenExit.Margin = new Thickness(0, Em("Sm"), lg, 0);
        drawerHeader.Margin = new Thickness(xl, xl, xl, 0);
        drawerContent.Padding = new Thickness(xl, lg, xl, lg);
        UpdateTexts();
        ToggleDrawer();
    }

    static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>文案：外壳的按钮取页面组合的文案表，窗口按钮取桌面的文案表（都按所在的语言包）。</summary>
    void UpdateTexts()
    {
        var lang = Provider.GetLang(this);
        var messages = PatternsMessages.For(lang);
        var desktop = DesktopMessages.For(lang);
        AutomationProperties.SetName(menuButton, messages.NavigationOpen);
        AutomationProperties.SetName(fullscreenButton, messages.FullscreenEnter);
        fullscreenButton.ToolTip = messages.FullscreenEnter;
        AutomationProperties.SetName(fullscreenExit, messages.FullscreenExit);
        fullscreenExit.ToolTip = messages.FullscreenExit;
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
        if (open == (drawerLayer.Visibility == Visibility.Visible)) return;
        var focusInside = drawer.IsKeyboardFocusWithin;
        drawerLayer.Visibility = Show(open);
        drawer.Width = drawerShadow.Width = Math.Min(Resource("Rune.Size.Xs", 480), ActualWidth > 0 ? ActualWidth : double.PositiveInfinity);
        if (open) drawerClose.Focus();
        else if (focusInside) menuButton.Focus();
    }

    /// <summary>Esc：先关抽屉，再退出全屏。里面已经处理了的 Esc 不算（TS 同）。</summary>
    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled) return;
        if (drawerLayer.Visibility == Visibility.Visible) SetCurrentValue(IsSidebarOpenProperty, false);
        else if (IsFullscreen) SetCurrentValue(IsFullscreenProperty, false);
        else return;
        e.Handled = true;
    }
}

/// <summary>应用外壳的部件样式键（DESIGN §7）。</summary>
public static class AppShellParts
{
    /// <summary>顶栏（TS 的 propsHeader）。</summary>
    public static readonly PartKey Header = new(typeof(AppShell), nameof(Header), typeof(Border));

    /// <summary>窄时打开侧栏的按钮（TS 的 propsMenuButton）。</summary>
    public static readonly PartKey MenuButton = new(typeof(AppShell), nameof(MenuButton), typeof(IconButton));

    /// <summary>品牌格（TS 的 propsBrand）。</summary>
    public static readonly PartKey Brand = new(typeof(AppShell), nameof(Brand), typeof(Border));

    /// <summary>顶栏里 Header 内容那一格（TS 的 propsHeaderContent）。</summary>
    public static readonly PartKey HeaderContent = new(typeof(AppShell), nameof(HeaderContent), typeof(Flex));

    /// <summary>全屏按钮（TS 的 propsFullscreenButton）。</summary>
    public static readonly PartKey FullscreenButton = new(typeof(AppShell), nameof(FullscreenButton), typeof(IconButton));

    /// <summary>顶栏下面的主体（TS 的 propsBody）。</summary>
    public static readonly PartKey Body = new(typeof(AppShell), nameof(Body), typeof(System.Windows.Controls.Grid));

    /// <summary>左侧栏（TS 的 propsSidebar）。</summary>
    public static readonly PartKey Sidebar = new(typeof(AppShell), nameof(Sidebar), typeof(Border));

    /// <summary>左侧栏里的滚动区域（TS 的 propsSidebarScroll）。</summary>
    public static readonly PartKey SidebarScroll = new(typeof(AppShell), nameof(SidebarScroll), typeof(ScrollArea));

    /// <summary>侧栏底部放"收起侧栏"的一条。</summary>
    public static readonly PartKey CollapseBar = new(typeof(AppShell), nameof(CollapseBar), typeof(Border));

    /// <summary>"收起侧栏"按钮（TS 的 propsCollapse）。</summary>
    public static readonly PartKey Collapse = new(typeof(AppShell), nameof(Collapse), typeof(IconButton));

    /// <summary>正文（TS 的 propsMain）。</summary>
    public static readonly PartKey Main = new(typeof(AppShell), nameof(Main), typeof(Border));

    /// <summary>页脚（TS 的 propsFooter）。</summary>
    public static readonly PartKey Footer = new(typeof(AppShell), nameof(Footer), typeof(Border));

    /// <summary>右侧栏（TS 的 propsAside）。</summary>
    public static readonly PartKey Aside = new(typeof(AppShell), nameof(Aside), typeof(Border));

    /// <summary>右侧栏里的滚动区域（TS 的 propsAsideScroll）。</summary>
    public static readonly PartKey AsideScroll = new(typeof(AppShell), nameof(AsideScroll), typeof(ScrollArea));

    /// <summary>全屏时右上角的退出按钮（TS 的 propsFullscreenExit）。</summary>
    public static readonly PartKey FullscreenExit = new(typeof(AppShell), nameof(FullscreenExit), typeof(IconButton));

    /// <summary>抽屉后面的遮罩。</summary>
    public static readonly PartKey Scrim = new(typeof(AppShell), nameof(Scrim), typeof(Border));

    /// <summary>抽屉面板后面画阴影的一层。</summary>
    public static readonly PartKey DrawerShadow = new(typeof(AppShell), nameof(DrawerShadow), typeof(Border));

    /// <summary>窄时的侧栏抽屉（TS 的 propsDrawer）。</summary>
    public static readonly PartKey Drawer = new(typeof(AppShell), nameof(Drawer), typeof(Border));
}
