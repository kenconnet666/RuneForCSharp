using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 标签页（vue-tsx-pro 的 Tabs，WAI-ARIA 的 Tabs 模式）：Tabs（状态）里放 <see cref="TabList"/>（一排 <see cref="Tab"/>）与各标签对应的 <see cref="TabPanel"/>。
/// <code>
/// &lt;rune:Tabs Value="{Binding Tab}"&gt;
///   &lt;rune:TabList AutomationProperties.Name="订单"&gt;
///     &lt;rune:Tab Value="all"&gt;全部&lt;/rune:Tab&gt;
///     &lt;rune:Tab Value="paid"&gt;已付款&lt;/rune:Tab&gt;
///   &lt;/rune:TabList&gt;
///   &lt;rune:TabPanel Value="all"&gt;…&lt;/rune:TabPanel&gt;
///   &lt;rune:TabPanel Value="paid"&gt;…&lt;/rune:TabPanel&gt;
/// &lt;/rune:Tabs&gt;
/// </code>
/// 没有选中值时选第一个标签；只显示当前标签的面板。键盘：标签之间用方向键移动（整组一个 Tab 停靠点），Home / End 到两端；
/// <see cref="Activation"/> 为 automatic（默认）时焦点到哪个就切到哪个，manual 时按 Enter / 空格才切换。
/// 样式 <see cref="Variant"/>：line（默认，当前标签下方一条品牌色的线，切换时滑过去）、soft（浅色底，像分段控件）。
/// 排列 <see cref="Orientation"/>：horizontal（默认）、vertical（标签在左侧，上下键切换）。<see cref="Size"/> 是整组的字号档位。
/// 没做：可关闭、新建、拖动排序、"全部标签页"菜单（等 Menu）。
/// </summary>
public class Tabs : Flex
{
    /// <summary>当前标签的值；不写时是第一个标签。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<Tabs, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>方向，默认 horizontal。</summary>
    public static readonly StyledProperty<TabsOrientation> OrientationProperty = AvaloniaProperty.Register<Tabs, TabsOrientation>(nameof(Orientation), TabsOrientation.Horizontal);

    /// <summary>焦点移动时是否同时切换，默认 automatic。</summary>
    public static readonly StyledProperty<TabsActivation> ActivationProperty = AvaloniaProperty.Register<Tabs, TabsActivation>(nameof(Activation), TabsActivation.Automatic);

    /// <summary>样式，默认 line。</summary>
    public static readonly StyledProperty<TabsVariant> VariantProperty = AvaloniaProperty.Register<Tabs, TabsVariant>(nameof(Variant), TabsVariant.Line);

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Tabs, TextSize>(nameof(Size));

    /// <summary>标签按下时的波纹；不写时沿用全局开关。</summary>
    public static readonly StyledProperty<bool?> RippleProperty = AvaloniaProperty.Register<Tabs, bool?>(nameof(Ripple));

    /// <summary>新建：竖排列表、间距 space.md。</summary>
    public Tabs()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Md;
    }

    /// <summary>当前标签的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>方向。</summary>
    public TabsOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>焦点移动时是否同时切换。</summary>
    public TabsActivation Activation { get => GetValue(ActivationProperty); set => SetValue(ActivationProperty, value); }

    /// <summary>样式。</summary>
    public TabsVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>波纹。</summary>
    public bool? Ripple { get => GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    /// <summary>实际选中的值：写了的，没写时第一个标签的。</summary>
    public string? SelectedValue => Value ?? Tabs_().FirstOrDefault()?.Value;

    /// <summary>属于这组的标签（不进到里面另一组 Tabs）。</summary>
    internal IEnumerable<Tab> Tabs_() => Members<Tab>();

    /// <summary>属于这组的面板。</summary>
    internal IEnumerable<TabPanel> Panels() => Members<TabPanel>();

    IEnumerable<T> Members<T>() where T : Control => this.GetLogicalDescendants().OfType<T>().Where(member => member.FindLogicalAncestorOfType<Tabs>() == this);

    /// <summary>切到某个标签（标签的点击、自动切换时的聚焦）。</summary>
    internal void Select(string value) => SetCurrentValue(ValueProperty, value);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty)
        {
            var vertical = Orientation == TabsOrientation.Vertical;
            Direction = vertical ? FlexDirection.Row : FlexDirection.Column;
            Align = vertical ? FlexAlign.Start : default;
            Sync();
        }
        else if (change.Property == SizeProperty)
        {
            ControlHeight.Apply(this, Size);
            if (Size.Pixels is { } pixels) SetValue(TextElement.FontSizeProperty, pixels);
            else if (Size.ResourceKey is { } key) Bind(TextElement.FontSizeProperty, this.GetResourceObservable(key));
            else ClearValue(TextElement.FontSizeProperty);
        }
        else if (change.Property == ValueProperty || change.Property == VariantProperty) Sync();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Sync();
    }

    /// <summary>选中状态、外观交给标签、列表与面板（标签、面板挂上来时也会调）。</summary>
    internal void Sync()
    {
        var selected = SelectedValue;
        foreach (var tab in Tabs_()) tab.Apply(this, tab.Value == selected);
        foreach (var list in Members<TabList>()) list.Apply(this);
        foreach (var panel in Panels())
        {
            panel.Apply(panel.Value == selected);
            // 竖排时面板占满标签列表右边的宽度；横排时高度按内容（在不限高的容器里不能往下撑）
            Flex.SetGrow(panel, Orientation == TabsOrientation.Vertical);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabsPeer(this);

    sealed class TabsPeer(Tabs owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

/// <summary>
/// 一排标签（role="tablist"）：line 时底边一道 borderMuted 的线、当前标签下方 2 像素的品牌色指示条（切换时滑过去，durations.md、standard）；
/// soft 时 fill 底、xs 内边距、md 圆角。方向键在标签之间移动（漫游焦点），到两端后从另一端继续（<see cref="Loop"/>）。
/// 名称写 <c>AutomationProperties.Name</c>（TS 的 aria-label）。
/// </summary>
[PseudoClasses(":line", ":soft", ":vertical")]
public class TabList : Flex
{
    /// <summary>到两端后是否从另一端继续，默认 true。</summary>
    public static readonly StyledProperty<bool> LoopProperty = AvaloniaProperty.Register<TabList, bool>(nameof(Loop), true);

    /// <summary>指示条的起点（主轴上，相对列表）。</summary>
    static readonly StyledProperty<double> IndicatorStartProperty = AvaloniaProperty.Register<TabList, double>("IndicatorStart");

    /// <summary>指示条的长度。</summary>
    static readonly StyledProperty<double> IndicatorLengthProperty = AvaloniaProperty.Register<TabList, double>("IndicatorLength");

    readonly RovingFocus focus;
    Tabs? owner;
    bool placed;

    readonly Decoration decoration;

    /// <summary>新建：横排、居中对齐；分隔线、指示条、soft 底色画在最底下一层（不参加 Flex 的排版）。</summary>
    public TabList()
    {
        Align = FlexAlign.Stretch;
        // ZIndex -1：画在所有标签下面（Panel 按 Children 的下标插入可视子元素，下标会错开一位）
        decoration = new Decoration(this) { ZIndex = -1, IsHitTestVisible = false };
        VisualChildren.Insert(0, decoration);
        focus = new RovingFocus(this, control => control is Tab) { Loop = true };
        LayoutUpdated += (_, _) => PlaceIndicator();
    }

    /// <summary>到两端后是否从另一端继续。</summary>
    public bool Loop { get => GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LoopProperty) focus.Loop = Loop;
        else if (change.Property == IndicatorStartProperty || change.Property == IndicatorLengthProperty) decoration.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        this.FindLogicalAncestorOfType<Tabs>()?.Sync();
    }

    /// <summary>跟着 Tabs 的样式与方向：间距、内边距、底色在主题里按伪类；方向键的方向。</summary>
    internal void Apply(Tabs tabs)
    {
        owner = tabs;
        var vertical = tabs.Orientation == TabsOrientation.Vertical;
        var line = tabs.Variant != TabsVariant.Soft;
        PseudoClasses.Set(":line", line);
        PseudoClasses.Set(":soft", !line);
        PseudoClasses.Set(":vertical", vertical);
        Direction = vertical ? FlexDirection.Column : FlexDirection.Row;
        Gap = line && !vertical ? Space.Md : Space.Xs;
        // line 的分隔线横贯整行；soft 的底色只在标签后面（列表按内容宽）
        HorizontalAlignment = line || vertical ? global::Avalonia.Layout.HorizontalAlignment.Stretch : global::Avalonia.Layout.HorizontalAlignment.Left;
        focus.Orientation = vertical ? "vertical" : "horizontal";
        focus.Sync();
        InvalidateMeasure();
        decoration.InvalidateVisual();
    }

    /// <summary>指示条对准当前标签：第一次直接到位，之后滑过去（系统关闭动画时直接到位）。</summary>
    void PlaceIndicator()
    {
        if (owner is null || owner.Variant == TabsVariant.Soft) return;
        if (Children.OfType<Tab>().FirstOrDefault(tab => tab.IsSelected) is not { } selected || !selected.IsArrangeValid) return;
        var vertical = owner.Orientation == TabsOrientation.Vertical;
        var bounds = selected.Bounds;
        var (start, length) = vertical ? (bounds.Y, bounds.Height) : (bounds.X, bounds.Width);
        if (start == IndicatorStart && length == IndicatorLength) return;
        if (!placed)
        {
            placed = true;
            Transitions = null;
        }
        else if (Transitions is null)
        {
            var duration = Motion.Duration(this, "Rune.Duration.Md", TimeSpan.FromMilliseconds(200));
            var easing = this.Resource<Easing>("Rune.Easing.Standard", new CubicEaseInOut());
            if (duration > TimeSpan.Zero)
                Transitions =
                [
                    new DoubleTransition { Property = IndicatorStartProperty, Duration = duration, Easing = easing },
                    new DoubleTransition { Property = IndicatorLengthProperty, Duration = duration, Easing = easing },
                ];
        }
        IndicatorStart = start;
        IndicatorLength = length;
    }

    double IndicatorStart { get => GetValue(IndicatorStartProperty); set => SetValue(IndicatorStartProperty, value); }

    double IndicatorLength { get => GetValue(IndicatorLengthProperty); set => SetValue(IndicatorLengthProperty, value); }

    /// <summary>soft 时四周 space.xs 的内边距（Flex 没有 Padding：量之前收进去、排完再挪过去）。</summary>
    Thickness Inset => owner?.Variant == TabsVariant.Soft ? new Thickness(0.25 * TextElement.GetFontSize(this)) : default;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var inset = Inset;
        decoration.Measure(availableSize);
        return base.MeasureOverride(availableSize.Deflate(inset)).Inflate(inset);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var inset = Inset;
        decoration.Arrange(new Rect(finalSize));
        base.ArrangeOverride(finalSize.Deflate(inset));
        if (inset != default)
            foreach (var child in Children) child.Arrange(child.Bounds.Translate(new Vector(inset.Left, inset.Top)));
        return finalSize;
    }

    /// <summary>
    /// soft：fill 底、md 圆角。line：底边（竖排时结束边）一道 borderMuted 的分隔线，当前标签那一段压上 2 像素的品牌色指示条（sm 圆角）。
    /// </summary>
    void Paint(DrawingContext context)
    {
        if (owner is null) return;
        if (owner.Variant == TabsVariant.Soft)
        {
            // soft：fill 底、md 圆角
            if (this.Resource<IBrush?>("Rune.Brush.Fill", null) is { } fill)
                context.DrawRectangle(fill, null, new RoundedRect(new Rect(Bounds.Size), this.Resource("Rune.Radius.Md", 0.375) * TextElement.GetFontSize(this)));
            return;
        }
        var vertical = owner.Orientation == TabsOrientation.Vertical;
        var size = Bounds.Size;
        var divider = this.Resource<IBrush?>("Rune.Brush.BorderMuted", null);
        if (divider is not null)
            context.FillRectangle(divider, vertical ? new Rect(size.Width - 1, 0, 1, size.Height) : new Rect(0, size.Height - 1, size.Width, 1));
        if (IndicatorLength <= 0 || this.Resource<IBrush?>("Rune.Brush.Accent", null) is not { } accent) return;
        var bar = vertical ? new Rect(size.Width - 2, IndicatorStart, 2, IndicatorLength) : new Rect(IndicatorStart, size.Height - 2, IndicatorLength, 2);
        var radius = this.Resource("Rune.Radius.Sm", 0.25) * TextElement.GetFontSize(this);
        context.DrawRectangle(accent, null, new RoundedRect(bar, Math.Min(radius, 1)));
    }

    /// <summary>最底下一层：只画 TabList 的分隔线、指示条与 soft 底色（Panel 的 Render 是密封的）。</summary>
    sealed class Decoration(TabList owner) : Control
    {
        public override void Render(DrawingContext context) => owner.Paint(context);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabListPeer(this);

    sealed class TabListPeer(TabList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    }
}

/// <summary>
/// 标签（role="tab"）：中等字重，平时 textMuted、悬停与选中时 text；控件高度、左右 space.md、sm 圆角。点击切换；自动切换时键盘聚焦就切换。
/// 读屏里是选项卡项，选中状态是选择项的 IsSelected。伪类：<c>:selected</c>、<c>:line</c>、<c>:soft</c>、<c>:vertical</c>。
/// </summary>
[PseudoClasses(":selected", ":line", ":soft", ":vertical")]
public class Tab : ButtonBase
{
    /// <summary>这个标签的值（同对应面板的 Value）。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<Tab, string?>(nameof(Value));

    /// <summary>选中着。</summary>
    public static readonly DirectProperty<Tab, bool> IsSelectedProperty = AvaloniaProperty.RegisterDirect<Tab, bool>(nameof(IsSelected), tab => tab.IsSelected);

    Tabs? owner;

    /// <summary>这个标签的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>选中着。</summary>
    public bool IsSelected { get; private set => SetAndRaise(IsSelectedProperty, ref field, value); }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Tab);

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        this.FindLogicalAncestorOfType<Tabs>()?.Sync();
    }

    /// <summary>跟着 Tabs 的选中值、样式、方向与波纹。</summary>
    internal void Apply(Tabs tabs, bool selected)
    {
        owner = tabs;
        IsSelected = selected;
        PseudoClasses.Set(":selected", selected);
        PseudoClasses.Set(":line", tabs.Variant != TabsVariant.Soft);
        PseudoClasses.Set(":soft", tabs.Variant == TabsVariant.Soft);
        PseudoClasses.Set(":vertical", tabs.Orientation == TabsOrientation.Vertical);
        if (tabs.Ripple is { } ripple) Ripple = ripple;
        else ClearValue(RippleProperty);
    }

    /// <summary>点击：先发 Click（使用方的处理先执行，标记 Handled 可以拦下切换），再切到这个标签。</summary>
    protected override void OnClick()
    {
        if (IsDisabled || IsLoading) return;
        var click = new RoutedEventArgs(ClickEvent);
        RaiseEvent(click);
        if (click.Handled) return;
        if (Command is { } command && command.CanExecute(CommandParameter)) command.Execute(CommandParameter);
        if (Value is { } value) owner?.Select(value);
    }

    /// <summary>自动切换：键盘把焦点移到哪个标签就切到哪个（指针按下得到的焦点由点击切换）。</summary>
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (owner?.Activation == TabsActivation.Automatic && e.NavigationMethod != NavigationMethod.Pointer && !IsDisabled && Value is { } value) owner.Select(value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabPeer(this);

    sealed class TabPeer(Tab owner) : ButtonBaseAutomationPeer(owner), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        public bool IsSelected => owner.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

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
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<TabPanel, string?>(nameof(Value));

    /// <summary>对应标签的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TabPanel);

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        this.FindLogicalAncestorOfType<Tabs>()?.Sync();
    }

    /// <summary>当前标签的面板才显示（看不见的面板不排版、不绘制）。</summary>
    internal void Apply(bool selected) => IsVisible = selected;

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabPanelPeer(this);

    sealed class TabPanelPeer(TabPanel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name
            : owner.FindLogicalAncestorOfType<Tabs>()?.Tabs_().FirstOrDefault(tab => tab.Value == owner.Value)?.Content?.ToString();
    }
}
