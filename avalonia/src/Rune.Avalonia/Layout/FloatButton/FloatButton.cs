using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Metadata;

namespace Rune.Avalonia;

/// <summary>
/// 浮动按钮（vue-tsx-pro 的 FloatButton）：窗口角落的常驻操作（新建、反馈、客服、回到顶部）。
/// <code>
/// &lt;Panel&gt;…页面…&lt;rune:FloatButton Icon="{x:Static rune:Lucide.MessageCircle}" AutomationProperties.Name="联系客服" Badge="2" /&gt;&lt;/Panel&gt;
/// </code>
/// 按钮是 <see cref="IconButton"/>（surface、中性色、圆形、lg、md 阴影），悬停或聚焦时提示名称（<see cref="Tip"/>，默认就是名称；<see cref="ShowTip"/> 为 false 不提示），
/// 提示在左边；<see cref="Badge"/> 是数字角标、<see cref="BadgeDot"/> 是小圆点（<see cref="Indicator"/>，圆形重叠）。
/// 桌面上没有“固定在视口”：单独用时靠所在面板的结束边与底部（默认对齐方式加 lg 的外边距，从右到左排版时在左下），放在盖住整页的 Panel 或 Grid 里就浮在内容上；
/// 在 <see cref="FloatButtonGroup"/> 里跟着组排。名称写在 FloatButton 上（AutomationProperties.Name），转给里面的按钮。部件：<see cref="FloatButtonParts"/>。
/// </summary>
public class FloatButton : Control
{
    /// <summary>图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<FloatButton, IconData?>(nameof(Icon));

    /// <summary>数字角标（未读数）；0 时不显示。</summary>
    public static readonly StyledProperty<int?> BadgeProperty = AvaloniaProperty.Register<FloatButton, int?>(nameof(Badge));

    /// <summary>小圆点角标（TS 的 badge: true）。</summary>
    public static readonly StyledProperty<bool> BadgeDotProperty = AvaloniaProperty.Register<FloatButton, bool>(nameof(BadgeDot));

    /// <summary>悬停、聚焦时的提示；不写时是名称。</summary>
    public static readonly StyledProperty<object?> TipProperty = AvaloniaProperty.Register<FloatButton, object?>(nameof(Tip));

    /// <summary>提示，默认 true（TS 的 tooltip: false 时为 false）。</summary>
    public static readonly StyledProperty<bool> ShowTipProperty = AvaloniaProperty.Register<FloatButton, bool>(nameof(ShowTip), true);

    /// <summary>点击时执行的命令。</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty = AvaloniaProperty.Register<FloatButton, ICommand?>(nameof(Command));

    /// <summary>命令的参数。</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty = AvaloniaProperty.Register<FloatButton, object?>(nameof(CommandParameter));

    /// <summary>字号（继承）：角落的外边距按它算（em）。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<FloatButton>();

    /// <summary><see cref="Click"/> 的路由事件。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent = RoutedEvent.Register<FloatButton, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    readonly Indicator indicator = new() { Name = FloatButtonParts.Badge, Overlap = IndicatorOverlap.Circular };

    /// <summary>新建：圆形的图标按钮包在角标里。</summary>
    public FloatButton() : this(new IconButton())
    {
    }

    /// <summary>新建：按钮由派生类给（速拨的主按钮要报展开、收起）。</summary>
    private protected FloatButton(IconButton button)
    {
        Button = button;
        button.Name = FloatButtonParts.Button;
        button.Variant = ButtonVariant.Surface;
        button.Tone = StatusTone.Neutral;
        button.Radius = Radius.Full;
        button.Size = TextSize.Lg;
        button.Click += (_, _) => RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        indicator.Content = button;
        Tooltip.SetPlacement(button, FloatingPlacement.Left);
        LogicalChildren.Add(indicator);
        VisualChildren.Add(indicator);
        SyncTip();
    }

    /// <summary>里面的图标按钮（TS 里落到 IconButton 上的属性：样式档位、色调写在它身上）。</summary>
    public IconButton Button { get; }

    /// <summary>图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>数字角标。</summary>
    public int? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    /// <summary>小圆点角标。</summary>
    public bool BadgeDot { get => GetValue(BadgeDotProperty); set => SetValue(BadgeDotProperty, value); }

    /// <summary>提示。</summary>
    public object? Tip { get => GetValue(TipProperty); set => SetValue(TipProperty, value); }

    /// <summary>显示提示。</summary>
    public bool ShowTip { get => GetValue(ShowTipProperty); set => SetValue(ShowTipProperty, value); }

    /// <summary>命令。</summary>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <summary>命令的参数。</summary>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>点了按钮。</summary>
    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    /// <summary>角标（测试用）。</summary>
    internal Indicator Indicator => indicator;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty) Button.Icon = Icon;
        else if (change.Property == BadgeProperty) indicator.Count = Badge;
        else if (change.Property == BadgeDotProperty) indicator.Dot = BadgeDot;
        else if (change.Property == CommandProperty) Button.Command = Command;
        else if (change.Property == CommandParameterProperty) Button.CommandParameter = CommandParameter;
        else if (change.Property == AutomationProperties.NameProperty || change.Property == TipProperty || change.Property == ShowTipProperty) SyncTip();
    }

    /// <summary>名称转给按钮；提示默认是名称。</summary>
    private protected virtual void SyncTip()
    {
        var name = AutomationProperties.GetName(this);
        AutomationProperties.SetName(Button, name);
        ToolTip.SetTip(Button, ShowTip ? Tip ?? name : null);
    }

    /// <summary>FloatButton 自己不进读屏的树：读屏看到的是里面的按钮。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new FloatButtonPeer(this);

    sealed class FloatButtonPeer(FloatButton owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>
/// 一组浮动按钮（vue-tsx-pro 的 FloatButtonGroup）：竖排（间距 sm）靠所在面板的结束边与底部；给了 <see cref="Icon"/> 时是速拨：主按钮（solid、accent，名称默认“更多操作”）
/// 点一下展开、收起其余按钮，展开后它们从主按钮上方淡入；Esc 或点组以外的地方收起，Esc 时焦点回到主按钮。<see cref="BackTop"/> 也能放进来。
/// <code>
/// &lt;rune:FloatButtonGroup Icon="{x:Static rune:Lucide.Plus}"&gt;&lt;rune:FloatButton … /&gt;&lt;rune:BackTop /&gt;&lt;/rune:FloatButtonGroup&gt;
/// </code>
/// 读屏：不是速拨时是一组（名称写在组上）；速拨时主按钮能展开、收起。部件：<see cref="FloatButtonParts"/>。
/// </summary>
public class FloatButtonGroup : Control
{
    /// <summary>给了就是速拨：主按钮用这个图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<FloatButtonGroup, IconData?>(nameof(Icon));

    /// <summary>速拨展开着（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<FloatButtonGroup, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>字号（继承）：角落的外边距按它算（em）。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<FloatButtonGroup>();

    /// <summary>展开的进度：0 在 0.75em 下方、透明，1 到位（动画改它，那一列的透明度与位移跟着）。</summary>
    static readonly StyledProperty<double> RevealProperty = AvaloniaProperty.Register<FloatButtonGroup, double>("Reveal", 1);

    readonly TranslateTransform shift = new();
    readonly Flex root = new() { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Flex actions = new() { Name = FloatButtonParts.Actions, Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly SpeedDialTrigger trigger;
    TopLevel? watching;

    /// <summary>新建：按钮竖排；速拨的主按钮在最下面。</summary>
    public FloatButtonGroup()
    {
        trigger = new SpeedDialTrigger(this) { Name = FloatButtonParts.Trigger, IsVisible = false, ShowTip = false };
        trigger.Button.Variant = ButtonVariant.Solid;
        trigger.Button.Tone = StatusTone.Accent;
        trigger.Click += (_, _) => SetCurrentValue(IsOpenProperty, !IsOpen);
        actions.RenderTransform = shift;
        root.Children.AddRange([actions, trigger]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.Escape || Icon is null || !IsOpen) return;
            SetCurrentValue(IsOpenProperty, false);
            trigger.Button.Focus(NavigationMethod.Tab);
            e.Handled = true;
        });
        Sync();
    }

    /// <summary>组里的按钮（FloatButton、BackTop）。</summary>
    [Content]
    public global::Avalonia.Controls.Controls Children => actions.Children;

    /// <summary>速拨的图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>速拨展开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>速拨的主按钮（TS 的 propsTrigger）。</summary>
    public FloatButton Trigger => trigger;

    /// <summary>速拨展开的那一列（测试用）。</summary>
    internal Flex Actions => actions;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty || change.Property == IsOpenProperty || change.Property == AutomationProperties.NameProperty) Sync();
        else if (change.Property == RevealProperty)
        {
            var progress = change.GetNewValue<double>();
            actions.Opacity = progress;
            shift.Y = (1 - progress) * 0.75 * FontSize;
        }
    }

    /// <summary>
    /// 不是速拨：按钮都显示，名称在组上。速拨：主按钮显示（展开时图标换成 ×），其余按钮展开时从下往上淡入（md），收起时藏起来（不占位、不能聚焦）；
    /// 展开期间盯着窗口里的按下，点在组外面就收起。
    /// </summary>
    void Sync()
    {
        var dial = Icon is not null;
        var open = !dial || IsOpen;
        trigger.IsVisible = dial;
        trigger.Icon = dial && IsOpen ? Lucide.X : Icon;
        var name = AutomationProperties.GetName(this);
        AutomationProperties.SetName(trigger, dial ? name ?? Provider.GetLang(this).FloatButtonMore : null);
        PseudoClasses.Set(":open", dial && IsOpen);
        var duration = Motion.Duration(this, ThemeKeys.Duration.Md);
        if (dial && open && !actions.IsVisible && duration > TimeSpan.Zero) Reveal(duration);
        actions.IsVisible = open;
        Watch(dial && IsOpen ? TopLevel.GetTopLevel(this) : null);
    }

    /// <summary>展开：从 0.75em 下方淡入。</summary>
    void Reveal(TimeSpan duration) => _ = new Animation
    {
        Duration = duration,
        Easing = this.Resource<global::Avalonia.Animation.Easings.Easing>(ThemeKeys.Easing.Standard),
        Children =
        {
            new KeyFrame { Cue = new Cue(0), Setters = { new Setter(RevealProperty, 0d) } },
            new KeyFrame { Cue = new Cue(1), Setters = { new Setter(RevealProperty, 1d) } },
        },
    }.RunAsync(this);

    void Watch(TopLevel? top)
    {
        if (watching == top) return;
        watching?.RemoveHandler(PointerPressedEvent, OnWindowPressed);
        watching = top;
        watching?.AddHandler(PointerPressedEvent, OnWindowPressed, RoutingStrategies.Tunnel);
    }

    void OnWindowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && (source == this || this.IsVisualAncestorOf(source))) return;
        SetCurrentValue(IsOpenProperty, false);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(null);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    /// <summary>读屏：一组（速拨时名称在主按钮上，组本身不另起名）。</summary>
    sealed class GroupPeer(FloatButtonGroup owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => owner.Icon is null ? base.GetNameCore() : null;
    }

    /// <summary>速拨的主按钮：读屏能展开、收起（TS 的 aria-expanded）。</summary>
    sealed class SpeedDialTrigger(FloatButtonGroup group) : FloatButton(new ExpandButton(group));

    sealed class ExpandButton(FloatButtonGroup group) : IconButton
    {
        // 主题按类型找：沿用 IconButton 的
        protected override Type StyleKeyOverride => typeof(IconButton);

        protected override AutomationPeer OnCreateAutomationPeer() => new ExpandPeer(this, group);
    }

    sealed class ExpandPeer(ExpandButton owner, FloatButtonGroup group) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState => group.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand() => group.SetCurrentValue(IsOpenProperty, true);

        public void Collapse() => group.SetCurrentValue(IsOpenProperty, false);
    }
}

/// <summary>FloatButton、FloatButtonGroup 的部件名。</summary>
public static class FloatButtonParts
{
    /// <summary>里面的图标按钮。</summary>
    public const string Button = "PART_Button";

    /// <summary>角标。</summary>
    public const string Badge = "PART_Badge";

    /// <summary>速拨展开的那一列（TS 的 propsActions）。</summary>
    public const string Actions = "PART_Actions";

    /// <summary>速拨的主按钮（TS 的 propsTrigger）。</summary>
    public const string Trigger = "PART_Trigger";
}
