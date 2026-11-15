using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 浮动按钮（vue-tsx-pro 的 FloatButton）：窗口角落的常驻操作（新建、反馈、客服、回到顶部）。
/// <code>
/// &lt;Grid&gt;…页面…&lt;rune:FloatButton Icon="{x:Static rune:Lucide.MessageCircle}" AutomationProperties.Name="联系客服" Badge="2" /&gt;&lt;/Grid&gt;
/// </code>
/// 按钮是 <see cref="IconButton"/>（surface、中性色、圆形、lg、md 阴影），悬停或聚焦时提示名称（<see cref="Tip"/>，默认就是名称；<see cref="ShowTip"/> 为 false 不提示），
/// 提示在左边；<see cref="Badge"/> 是数字角标、<see cref="BadgeDot"/> 是小圆点（<see cref="Indicator"/>，圆形重叠）。
/// 桌面上没有“固定在视口”：单独用时靠所在面板的结束边与底部（对齐方式加 lg 的外边距，从右到左排版时在左下），放在盖住整页的 Grid 里就浮在内容上；
/// 在 <see cref="FloatButtonGroup"/> 里跟着组排。名称写在 FloatButton 上（AutomationProperties.Name），转给里面的按钮。
/// </summary>
public class FloatButton : CompositeControl
{
    /// <summary>图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(FloatButton),
        new FrameworkPropertyMetadata(null, (d, e) => ((FloatButton)d).Button.Icon = (IconData?)e.NewValue));

    /// <summary>数字角标（未读数）；0 时不显示。</summary>
    public static readonly DependencyProperty BadgeProperty = DependencyProperty.Register(nameof(Badge), typeof(int?), typeof(FloatButton),
        new FrameworkPropertyMetadata(null, (d, e) => ((FloatButton)d).indicator.Count = (int?)e.NewValue));

    /// <summary>小圆点角标（TS 的 badge: true）。</summary>
    public static readonly DependencyProperty BadgeDotProperty = DependencyProperty.Register(nameof(BadgeDot), typeof(bool), typeof(FloatButton),
        new FrameworkPropertyMetadata(false, (d, e) => ((FloatButton)d).indicator.Dot = (bool)e.NewValue));

    /// <summary>悬停、聚焦时的提示；不写时是名称。</summary>
    public static readonly DependencyProperty TipProperty = DependencyProperty.Register(nameof(Tip), typeof(object), typeof(FloatButton),
        new FrameworkPropertyMetadata(null, (d, _) => ((FloatButton)d).SyncTip()));

    /// <summary>提示，默认 true（TS 的 tooltip: false 时为 false）。</summary>
    public static readonly DependencyProperty ShowTipProperty = DependencyProperty.Register(nameof(ShowTip), typeof(bool), typeof(FloatButton),
        new FrameworkPropertyMetadata(true, (d, _) => ((FloatButton)d).SyncTip()));

    /// <summary>点击时执行的命令。</summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(FloatButton),
        new FrameworkPropertyMetadata(null, (d, e) => ((FloatButton)d).Button.Command = (ICommand?)e.NewValue));

    /// <summary>命令的参数。</summary>
    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(FloatButton),
        new FrameworkPropertyMetadata(null, (d, e) => ((FloatButton)d).Button.CommandParameter = e.NewValue));

    /// <summary><see cref="Click"/> 的路由事件。</summary>
    public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FloatButton));

    readonly Indicator indicator = new() { Overlap = IndicatorOverlap.Circular };

    static FloatButton()
    {
        FocusableProperty.OverrideMetadata(typeof(FloatButton), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(FloatButton), new FrameworkPropertyMetadata(false));
        AutomationProperties.NameProperty.OverrideMetadata(typeof(FloatButton), new FrameworkPropertyMetadata("", (d, _) => ((FloatButton)d).SyncTip()));
        FontSizeProperty.OverrideMetadata(typeof(FloatButton), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((FloatButton)d).PlaceInCorner()));
    }

    /// <summary>新建：圆形的图标按钮包在角标里。</summary>
    public FloatButton()
    {
        Button.Variant = ButtonVariant.Surface;
        Button.Tone = StatusTone.Neutral;
        Button.Radius = Radius.Full;
        Button.Size = TextSize.Lg;
        Button.SetResourceReference(EffectProperty, ThemeKeys.Shadow.Md);
        Button.Click += (_, _) => RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        Tooltip.SetPlacement(Button, FloatingPlacement.Left);
        indicator.Content = Button;
        Root = indicator;
        Loaded += (_, _) => PlaceInCorner();
    }

    /// <summary>里面的图标按钮（TS 里落到 IconButton 上的属性：样式档位、色调写在它身上）。</summary>
    public IconButton Button { get; } = new();

    /// <summary>图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>数字角标。</summary>
    public int? Badge { get => (int?)GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    /// <summary>小圆点角标。</summary>
    public bool BadgeDot { get => (bool)GetValue(BadgeDotProperty); set => SetValue(BadgeDotProperty, value); }

    /// <summary>提示。</summary>
    public object? Tip { get => GetValue(TipProperty); set => SetValue(TipProperty, value); }

    /// <summary>显示提示。</summary>
    public bool ShowTip { get => (bool)GetValue(ShowTipProperty); set => SetValue(ShowTipProperty, value); }

    /// <summary>命令。</summary>
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <summary>命令的参数。</summary>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <summary>点了按钮。</summary>
    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    /// <summary>角标（测试用）。</summary>
    internal Indicator Indicator => indicator;

    /// <summary>在组里（跟着组排，不靠角落）。</summary>
    internal bool Grouped
    {
        get
        {
            for (var node = LogicalTreeHelper.GetParent(this) ?? VisualTreeHelper.GetParent(this); node is not null; node = LogicalTreeHelper.GetParent(node) ?? VisualTreeHelper.GetParent(node))
                if (node is FloatButtonGroup) return true;
            return false;
        }
    }

    /// <summary>名称转给按钮；提示默认是名称。</summary>
    private protected virtual void SyncTip()
    {
        var name = AutomationProperties.GetName(this);
        AutomationProperties.SetName(Button, name);
        var tip = ShowTip ? Tip ?? (string.IsNullOrEmpty(name) ? null : name) : null;
        if (tip is null) Button.ClearValue(ToolTipProperty);
        else Button.ToolTip = tip;
    }

    /// <summary>
    /// 单独用时靠所在面板的结束边与底部、外边距 lg（TS 固定在视口角落）；组里的居中、没有外边距。使用方自己写了对齐、外边距时不动。
    /// </summary>
    internal void PlaceInCorner()
    {
        var grouped = Grouped;
        Set(HorizontalAlignmentProperty, grouped ? HorizontalAlignment.Center : HorizontalAlignment.Right);
        Set(VerticalAlignmentProperty, grouped ? VerticalAlignment.Center : VerticalAlignment.Bottom);
        Set(MarginProperty, grouped ? default : new Thickness(this.Resource<double>(ThemeKeys.Space.Lg) * FontSize));
    }

    void Set(DependencyProperty property, object value)
    {
        if (DependencyPropertyHelper.GetValueSource(this, property).BaseValueSource <= BaseValueSource.Style) SetCurrentValue(property, value);
    }

    /// <summary>FloatButton 自己不进读屏的树：读屏看到的是里面的按钮。</summary>
    protected override AutomationPeer? OnCreateAutomationPeer() => null;
}

/// <summary>
/// 一组浮动按钮（vue-tsx-pro 的 FloatButtonGroup）：竖排（间距 sm）靠所在面板的结束边与底部；给了 <see cref="Icon"/> 时是速拨：主按钮（solid、accent，名称默认“更多操作”）
/// 点一下展开、收起其余按钮，展开后它们从主按钮上方淡入；Esc 或点组以外的地方收起，Esc 时焦点回到主按钮。<see cref="BackTop"/> 也能放进来。
/// <code>
/// &lt;rune:FloatButtonGroup Icon="{x:Static rune:Lucide.Plus}"&gt;&lt;rune:FloatButton … /&gt;&lt;rune:BackTop /&gt;&lt;/rune:FloatButtonGroup&gt;
/// </code>
/// 读屏：不是速拨时是一组（名称写在组上）；速拨时主按钮能展开、收起。
/// </summary>
[ContentProperty(nameof(Children))]
public class FloatButtonGroup : CompositeControl
{
    /// <summary>给了就是速拨：主按钮用这个图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(FloatButtonGroup),
        new FrameworkPropertyMetadata(null, (d, _) => ((FloatButtonGroup)d).Sync()));

    /// <summary>速拨展开着（默认双向绑定）。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(FloatButtonGroup),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((FloatButtonGroup)d).Sync()));

    readonly Flex root = new() { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Flex actions = new() { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly TranslateTransform shift = new();
    Window? watching;

    static FloatButtonGroup()
    {
        FocusableProperty.OverrideMetadata(typeof(FloatButtonGroup), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(FloatButtonGroup), new FrameworkPropertyMetadata(false));
        AutomationProperties.NameProperty.OverrideMetadata(typeof(FloatButtonGroup), new FrameworkPropertyMetadata("", (d, _) => ((FloatButtonGroup)d).Sync()));
    }

    /// <summary>新建：按钮竖排；速拨的主按钮在最下面。</summary>
    public FloatButtonGroup()
    {
        Trigger.ShowTip = false;
        Trigger.Visibility = Visibility.Collapsed;
        Trigger.Button.Variant = ButtonVariant.Solid;
        Trigger.Button.Tone = StatusTone.Accent;
        Trigger.Click += (_, e) =>
        {
            SetCurrentValue(IsOpenProperty, !IsOpen);
            e.Handled = true;
        };
        actions.RenderTransform = shift;
        root.Children.Add(actions);
        root.Children.Add(Trigger);
        Root = root;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || Icon is null || !IsOpen) return;
            SetCurrentValue(IsOpenProperty, false);
            Trigger.Button.Focus();
            e.Handled = true;
        };
        Loaded += (_, _) => PlaceInCorner();
        Unloaded += (_, _) => Watch(null);
        Sync();
    }

    /// <summary>组里的按钮（FloatButton、BackTop）。</summary>
    public UIElementCollection Children => actions.Children;

    /// <summary>速拨的图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>速拨展开着。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>速拨的主按钮（TS 的 propsTrigger）。</summary>
    public FloatButton Trigger { get; } = new();

    /// <summary>速拨展开的那一列（测试用）。</summary>
    internal Flex Actions => actions;

    void PlaceInCorner()
    {
        if (DependencyPropertyHelper.GetValueSource(this, HorizontalAlignmentProperty).BaseValueSource <= BaseValueSource.Style) SetCurrentValue(HorizontalAlignmentProperty, HorizontalAlignment.Right);
        if (DependencyPropertyHelper.GetValueSource(this, VerticalAlignmentProperty).BaseValueSource <= BaseValueSource.Style) SetCurrentValue(VerticalAlignmentProperty, VerticalAlignment.Bottom);
        if (DependencyPropertyHelper.GetValueSource(this, MarginProperty).BaseValueSource <= BaseValueSource.Style)
            SetCurrentValue(MarginProperty, new Thickness(this.Resource<double>(ThemeKeys.Space.Lg) * FontSize));
        foreach (var child in actions.Children.OfType<FloatButton>()) child.PlaceInCorner();
    }

    /// <summary>
    /// 不是速拨：按钮都显示，名称在组上。速拨：主按钮显示（展开时图标换成 ×、报展开），其余按钮展开时从 0.75em 下方淡入（md），收起时藏起来；
    /// 展开期间盯着窗口里的按下，点在组外面就收起。
    /// </summary>
    void Sync()
    {
        var dial = Icon is not null;
        var open = !dial || IsOpen;
        Trigger.Visibility = dial ? Visibility.Visible : Visibility.Collapsed;
        Trigger.Icon = dial && IsOpen ? Lucide.X : Icon;
        var name = AutomationProperties.GetName(this);
        AutomationProperties.SetName(Trigger, dial ? (string.IsNullOrEmpty(name) ? Provider.GetLang(this).FloatButtonMore : name) : "");
        Trigger.Button.SetValue(Popover.TriggerExpandedProperty, dial ? IsOpen : null);
        var duration = Motion.Duration(this, ThemeKeys.Duration.Md);
        if (dial && open && actions.Visibility != Visibility.Visible && duration > TimeSpan.Zero)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            actions.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.75 * FontSize, 0, duration) { EasingFunction = ease });
        }
        actions.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        Watch(dial && IsOpen ? Window.GetWindow(this) : null);
    }

    void Watch(Window? window)
    {
        if (watching == window) return;
        if (watching is not null) watching.PreviewMouseDown -= OnWindowPressed;
        watching = window;
        if (watching is not null) watching.PreviewMouseDown += OnWindowPressed;
    }

    void OnWindowPressed(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && (source == this || IsAncestorOf(source))) return;
        SetCurrentValue(IsOpenProperty, false);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    /// <summary>读屏：一组（速拨时名称在主按钮上，组本身不另起名）。</summary>
    sealed class GroupPeer(FloatButtonGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(FloatButtonGroup);

        protected override string GetNameCore() => owner.Icon is null ? base.GetNameCore() : "";
    }
}
