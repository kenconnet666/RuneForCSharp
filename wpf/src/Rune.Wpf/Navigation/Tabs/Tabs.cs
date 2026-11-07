using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 标签页（vue-tsx-pro 的 Tabs，WAI-ARIA 的 Tabs 模式）：Tabs（状态）里放 <see cref="TabList"/>（一排 <see cref="Tab"/>）与各标签对应的 <see cref="TabPanel"/>。
/// <code>
/// &lt;rune:Tabs Value="{Binding Tab}"&gt;
///   &lt;rune:TabList AutomationProperties.Name="订单"&gt;
///     &lt;rune:Tab Value="all"&gt;全部&lt;/rune:Tab&gt;
///   &lt;/rune:TabList&gt;
///   &lt;rune:TabPanel Value="all"&gt;…&lt;/rune:TabPanel&gt;
/// &lt;/rune:Tabs&gt;
/// </code>
/// 没有选中值时选第一个标签；只显示当前标签的面板。键盘：标签之间用方向键移动（整组一个 Tab 停靠点），Home / End 到两端；
/// <see cref="Activation"/> 为 automatic（默认）时焦点到哪个就切到哪个，manual 时按 Enter / 空格才切换。
/// 样式 <see cref="Variant"/>：line（默认，当前标签下方一条品牌色的线，切换时滑过去）、soft（浅色底）。<see cref="Orientation"/>：horizontal、vertical。
/// 没做：可关闭、新建、拖动排序、"全部标签页"菜单（等 Menu）。
/// </summary>
public class Tabs : Flex
{
    /// <summary>当前标签的值；不写时是第一个标签。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(Tabs),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Tabs)d).Sync()));

    /// <summary>方向，默认 horizontal。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(TabsOrientation), typeof(Tabs),
        new FrameworkPropertyMetadata(TabsOrientation.Horizontal, (d, _) => ((Tabs)d).ApplyOrientation()));

    /// <summary>焦点移动时是否同时切换，默认 automatic。</summary>
    public static readonly DependencyProperty ActivationProperty = DependencyProperty.Register(nameof(Activation), typeof(TabsActivation), typeof(Tabs),
        new FrameworkPropertyMetadata(TabsActivation.Automatic));

    /// <summary>样式，默认 line。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(TabsVariant), typeof(Tabs),
        new FrameworkPropertyMetadata(TabsVariant.Line, (d, _) => ((Tabs)d).Sync()));

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Tabs),
        new FrameworkPropertyMetadata(default(TextSize), (d, _) => ((Tabs)d).ApplySize()));

    /// <summary>标签按下时的波纹；不写时沿用全局开关。</summary>
    public static readonly DependencyProperty RippleProperty = DependencyProperty.Register(nameof(Ripple), typeof(bool?), typeof(Tabs),
        new FrameworkPropertyMetadata(null, (d, _) => ((Tabs)d).Sync()));

    /// <summary>新建：竖排列表、间距 space.md。</summary>
    public Tabs()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Md;
        Loaded += (_, _) => Sync();
    }

    /// <summary>当前标签的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>方向。</summary>
    public TabsOrientation Orientation { get => (TabsOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>焦点移动时是否同时切换。</summary>
    public TabsActivation Activation { get => (TabsActivation)GetValue(ActivationProperty); set => SetValue(ActivationProperty, value); }

    /// <summary>样式。</summary>
    public TabsVariant Variant { get => (TabsVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>波纹。</summary>
    public bool? Ripple { get => (bool?)GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    /// <summary>实际选中的值：写了的，没写时第一个标签的。</summary>
    public string? SelectedValue => Value ?? Members<Tab>().FirstOrDefault()?.Value;

    /// <summary>属于这组的成员（不进到里面另一组 Tabs）。</summary>
    internal IEnumerable<T> Members<T>() where T : DependencyObject => Descendants(this).OfType<T>().Where(member => Owner(member) == this);

    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    /// <summary>元素属于哪组 Tabs（逻辑树上最近的一个）。</summary>
    internal static Tabs? Owner(DependencyObject element)
    {
        for (var parent = LogicalTreeHelper.GetParent(element); parent is not null; parent = LogicalTreeHelper.GetParent(parent))
            if (parent is Tabs tabs) return tabs;
        return null;
    }

    /// <summary>切到某个标签（标签的点击、自动切换时的聚焦）。</summary>
    internal void Select(string value) => SetCurrentValue(ValueProperty, value);

    void ApplyOrientation()
    {
        var vertical = Orientation == TabsOrientation.Vertical;
        Direction = vertical ? FlexDirection.Row : FlexDirection.Column;
        Align = vertical ? FlexAlign.Start : default;
        Sync();
    }

    void ApplySize()
    {
        if (Size.Pixels is { } pixels) TextElement.SetFontSize(this, pixels);
        else if (Size.ResourceKey is { } key) SetResourceReference(TextElement.FontSizeProperty, key);
        else ClearValue(TextElement.FontSizeProperty);
    }

    /// <summary>选中状态、外观交给标签、列表与面板（标签、面板挂上来时也会调）。</summary>
    internal void Sync()
    {
        var selected = SelectedValue;
        foreach (var tab in Members<Tab>()) tab.Apply(this, tab.Value == selected);
        foreach (var list in Members<TabList>()) list.Apply(this);
        foreach (var panel in Members<TabPanel>())
        {
            panel.Visibility = panel.Value == selected ? Visibility.Visible : Visibility.Collapsed;
            // 竖排时面板占满标签列表右边的宽度；横排时高度按内容
            SetGrow(panel, Orientation == TabsOrientation.Vertical);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabsPeer(this);

    sealed class TabsPeer(Tabs owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Tabs);
    }
}

/// <summary>
/// 一排标签（role="tablist"）：line 时底边（竖排时结束边）一道 borderMuted 的线、当前标签那一段 2 像素的品牌色指示条（切换时滑过去，durations.md、standard）；
/// soft 时 fill 底、xs 内边距、md 圆角，宽度按标签。方向键在标签之间移动（漫游焦点），到两端后从另一端继续（<see cref="Loop"/>）。
/// 名称写 <c>AutomationProperties.Name</c>。
/// </summary>
public class TabList : Flex
{
    /// <summary>到两端后是否从另一端继续，默认 true。</summary>
    public static readonly DependencyProperty LoopProperty = DependencyProperty.Register(nameof(Loop), typeof(bool), typeof(TabList),
        new FrameworkPropertyMetadata(true, (d, _) => ((TabList)d).focus.Loop = (bool)d.GetValue(LoopProperty)));

    static readonly DependencyProperty IndicatorStartProperty = DependencyProperty.Register("IndicatorStart", typeof(double), typeof(TabList),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    static readonly DependencyProperty IndicatorLengthProperty = DependencyProperty.Register("IndicatorLength", typeof(double), typeof(TabList),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    readonly RovingFocus focus;
    Tabs? owner;
    bool placed;

    /// <summary>新建：横排、拉伸对齐。</summary>
    public TabList()
    {
        Align = FlexAlign.Stretch;
        focus = new RovingFocus(this, element => element is Tab);
        // 方向键移动了焦点之后（漫游焦点先处理）：自动切换时切到新聚焦的标签
        AddHandler(KeyDownEvent, new KeyEventHandler((_, _) =>
        {
            if (owner?.Activation == TabsActivation.Automatic && Keyboard.FocusedElement is Tab { IsDisabled: false, Value: { } value } tab && Tabs.Owner(tab) == owner)
                owner.Select(value);
        }), handledEventsToo: true);
        LayoutUpdated += (_, _) => PlaceIndicator();
        Loaded += (_, _) => Tabs.Owner(this)?.Sync();
    }

    /// <summary>到两端后是否从另一端继续。</summary>
    public bool Loop { get => (bool)GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>跟着 Tabs 的样式与方向：间距、soft 的宽度、方向键的方向。</summary>
    internal void Apply(Tabs tabs)
    {
        owner = tabs;
        var vertical = tabs.Orientation == TabsOrientation.Vertical;
        var line = tabs.Variant != TabsVariant.Soft;
        Direction = vertical ? FlexDirection.Column : FlexDirection.Row;
        Gap = line && !vertical ? Space.Md : Space.Xs;
        // line 的分隔线横贯整行；soft 的底色只在标签后面（列表按内容宽）
        HorizontalAlignment = line || vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        focus.Orientation = vertical ? "vertical" : "horizontal";
        focus.Sync();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>soft 时四周 space.xs 的内边距（Flex 没有 Padding：量之前收进去、排完再挪过去）。</summary>
    Thickness Inset => owner?.Variant == TabsVariant.Soft ? new Thickness(0.25 * TextElement.GetFontSize(this)) : default;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var inset = Inset;
        var inner = base.MeasureOverride(new Size(Math.Max(0, availableSize.Width - inset.Left - inset.Right), Math.Max(0, availableSize.Height - inset.Top - inset.Bottom)));
        return new Size(inner.Width + inset.Left + inset.Right, inner.Height + inset.Top + inset.Bottom);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var inset = Inset;
        base.ArrangeOverride(new Size(Math.Max(0, finalSize.Width - inset.Left - inset.Right), Math.Max(0, finalSize.Height - inset.Top - inset.Bottom)));
        if (inset != default)
            foreach (UIElement child in InternalChildren)
            {
                var slot = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot((FrameworkElement)child);
                child.Arrange(new Rect(slot.X + inset.Left, slot.Y + inset.Top, slot.Width, slot.Height));
            }
        return finalSize;
    }

    /// <summary>指示条对准当前标签：第一次直接到位，之后滑过去（系统关闭动画时直接到位）。</summary>
    void PlaceIndicator()
    {
        if (owner is null || owner.Variant == TabsVariant.Soft) return;
        if (InternalChildren.OfType<Tab>().FirstOrDefault(tab => tab.IsSelected) is not { IsArrangeValid: true } selected) return;
        var vertical = owner.Orientation == TabsOrientation.Vertical;
        var offset = selected.TranslatePoint(new Point(), this);
        var (start, length) = vertical ? (offset.Y, selected.ActualHeight) : (offset.X, selected.ActualWidth);
        if (Math.Abs(start - (double)GetValue(IndicatorStartProperty)) < 0.01 && Math.Abs(length - (double)GetValue(IndicatorLengthProperty)) < 0.01) return;
        var duration = placed ? Motion.Duration(this, "Rune.Duration.Md", TimeSpan.FromMilliseconds(200)) : TimeSpan.Zero;
        placed = true;
        Animate(IndicatorStartProperty, start, duration);
        Animate(IndicatorLengthProperty, length, duration);
    }

    void Animate(DependencyProperty property, double to, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            BeginAnimation(property, null);
            SetValue(property, to);
            return;
        }
        var from = (double)GetValue(property);
        SetValue(property, to);
        BeginAnimation(property, new DoubleAnimation(from, to, duration) { EasingFunction = TryFindResource("Rune.Easing.Standard") as IEasingFunction, FillBehavior = FillBehavior.Stop });
    }

    /// <summary>soft：fill 底、md 圆角。line：分隔线与当前标签那一段 2 像素的品牌色指示条（sm 圆角）。</summary>
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (owner is null) return;
        var em = TextElement.GetFontSize(this);
        if (owner.Variant == TabsVariant.Soft)
        {
            if (TryFindResource("Rune.Brush.Fill") is Brush fill)
            {
                var radius = (TryFindResource("Rune.Radius.Md") as double? ?? 0.375) * em;
                context.DrawRoundedRectangle(fill, null, new Rect(RenderSize), radius, radius);
            }
            return;
        }
        var vertical = owner.Orientation == TabsOrientation.Vertical;
        var size = RenderSize;
        if (TryFindResource("Rune.Brush.BorderMuted") is Brush divider)
            context.DrawRectangle(divider, null, vertical ? new Rect(size.Width - 1, 0, 1, size.Height) : new Rect(0, size.Height - 1, size.Width, 1));
        var start = (double)GetValue(IndicatorStartProperty);
        var length = (double)GetValue(IndicatorLengthProperty);
        if (length <= 0 || TryFindResource("Rune.Brush.Accent") is not Brush accent) return;
        var bar = vertical ? new Rect(size.Width - 2, start, 2, length) : new Rect(start, size.Height - 2, length, 2);
        context.DrawRoundedRectangle(accent, null, bar, 1, 1);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabListPeer(this);

    sealed class TabListPeer(TabList owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

        protected override string GetClassNameCore() => nameof(TabList);
    }
}

/// <summary>
/// 标签（role="tab"）：中等字重，平时 textMuted、悬停与选中时 text；控件高度、左右 space.md、sm 圆角。点击切换（使用方的 Click 先执行，标记 Handled 可以拦下）；
/// 自动切换时键盘聚焦就切换。读屏里是选项卡项，选中状态是选择项的 IsSelected。
/// </summary>
public class Tab : ButtonBase
{
    /// <summary>这个标签的值（同对应面板的 Value）。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(Tab));

    static readonly DependencyPropertyKey IsSelectedKey = DependencyProperty.RegisterReadOnly(nameof(IsSelected), typeof(bool), typeof(Tab), new PropertyMetadata(false));

    /// <summary>选中着。</summary>
    public static readonly DependencyProperty IsSelectedProperty = IsSelectedKey.DependencyProperty;

    static readonly DependencyPropertyKey VariantKey = DependencyProperty.RegisterReadOnly(nameof(Variant), typeof(TabsVariant), typeof(Tab), new PropertyMetadata(TabsVariant.Line));

    /// <summary>所在 Tabs 的样式（主题的触发器按它切换外观）。</summary>
    public static readonly DependencyProperty VariantProperty = VariantKey.DependencyProperty;

    static readonly DependencyPropertyKey OrientationKey = DependencyProperty.RegisterReadOnly(nameof(Orientation), typeof(TabsOrientation), typeof(Tab), new PropertyMetadata(TabsOrientation.Horizontal));

    /// <summary>所在 Tabs 的方向。</summary>
    public static readonly DependencyProperty OrientationProperty = OrientationKey.DependencyProperty;

    Tabs? owner;

    static Tab() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Tab), new FrameworkPropertyMetadata(typeof(Tab)));

    /// <summary>新建：挂上来时告诉所在的 Tabs。</summary>
    public Tab() => Loaded += (_, _) => Tabs.Owner(this)?.Sync();

    /// <summary>这个标签的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>选中着。</summary>
    public bool IsSelected => (bool)GetValue(IsSelectedProperty);

    /// <summary>所在 Tabs 的样式。</summary>
    public TabsVariant Variant => (TabsVariant)GetValue(VariantProperty);

    /// <summary>所在 Tabs 的方向。</summary>
    public TabsOrientation Orientation => (TabsOrientation)GetValue(OrientationProperty);

    /// <summary>跟着 Tabs 的选中值、样式、方向与波纹。</summary>
    internal void Apply(Tabs tabs, bool selected)
    {
        owner = tabs;
        SetValue(IsSelectedKey, selected);
        SetValue(VariantKey, tabs.Variant);
        SetValue(OrientationKey, tabs.Orientation);
        if (tabs.Ripple is { } ripple) Ripple = ripple;
        else ClearValue(RippleProperty);
    }

    /// <summary>点击：先发 Click（使用方的处理先执行，标记 Handled 可以拦下切换），再切到这个标签。</summary>
    protected override void OnClick()
    {
        if (IsLoading || IsDisabled) return;
        var click = new RoutedEventArgs(ClickEvent, this);
        RaiseEvent(click);
        if (click.Handled) return;
        if (Command is { } command && command.CanExecute(CommandParameter)) command.Execute(CommandParameter);
        if (Value is { } value) owner?.Select(value);
    }

    /// <summary>自动切换：键盘把焦点移到哪个标签就切到哪个。</summary>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (owner?.Activation == TabsActivation.Automatic && InputManager.Current.MostRecentInputDevice is KeyboardDevice && !IsDisabled && Value is { } value)
            owner.Select(value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabPeer(this);

    sealed class TabPeer(Tab owner) : ButtonBaseAutomationPeer(owner), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override string GetClassNameCore() => nameof(Tab);

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);

        public bool IsSelected => owner.IsSelected;

        public IRawElementProviderSimple? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Select()
        {
            if (!owner.IsDisabled && owner.Value is { } value) owner.owner?.Select(value);
        }
    }
}

/// <summary>标签面板（role="tabpanel"）：只显示当前标签的那一个；可以聚焦（键盘从标签 Tab 进来），sm 圆角的焦点环。读屏名称是对应标签的文字。</summary>
public class TabPanel : ContentControl
{
    /// <summary>对应标签的值。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(TabPanel));

    static TabPanel() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TabPanel), new FrameworkPropertyMetadata(typeof(TabPanel)));

    /// <summary>新建：挂上来时告诉所在的 Tabs。</summary>
    public TabPanel() => Loaded += (_, _) => Tabs.Owner(this)?.Sync();

    /// <summary>对应标签的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabPanelPeer(this);

    sealed class TabPanelPeer(TabPanel owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override string GetClassNameCore() => nameof(TabPanel);

        /// <summary>写了名称时用它；否则是对应标签的文字（TS 的 aria-labelledby），不用面板里的内容。</summary>
        protected override string GetNameCore() => System.Windows.Automation.AutomationProperties.GetName(owner) is { Length: > 0 } name ? name
            : Tabs.Owner(owner)?.Members<Tab>().FirstOrDefault(tab => tab.Value == owner.Value)?.Content?.ToString() ?? base.GetNameCore();
    }
}
