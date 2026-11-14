using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 按钮组（vue-tsx-pro 的 ButtonGroup）：几个相关的按钮连成一条，共用外观。拆分按钮、翻页、缩放、对齐方式这类工具栏。
/// <code>
/// &lt;rune:ButtonGroup Variant="outline" Tone="neutral" AutomationProperties.Name="文字对齐"&gt;
///   &lt;rune:IconButton Icon="{x:Static rune:Lucide.TextAlignStart}" AutomationProperties.Name="左对齐" /&gt;
///   &lt;rune:IconButton Icon="{x:Static rune:Lucide.TextAlignCenter}" AutomationProperties.Name="居中" /&gt;
/// &lt;/rune:ButtonGroup&gt;
/// </code>
/// 组里的按钮去掉相邻一侧的圆角、边框叠在一起，交叉轴拉齐（一样高，竖排时一样宽）；悬停、聚焦的那个浮到上面，边框与焦点环不被邻居盖住。
/// 组上写的 Variant、Tone、Size、Radius 是组里按钮的默认值：盖过主题，按钮自己写的（本地值、使用方的样式）优先；没写 Tone 时组里的按钮都是 accent
/// （图标按钮也是，拆分按钮的两半同色）。实底（solid）的相邻按钮之间没有分隔，连成一条时用 outline、surface 更清楚。
/// 只是排一排、不相连的按钮用 Flex；选中其中一项用 ToggleGroup。读屏里是一个组，名称写 <c>AutomationProperties.Name</c>。
/// </summary>
public class ButtonGroup : Panel
{
    /// <summary>排列方向，默认 horizontal。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(AttachedOrientation), typeof(ButtonGroup),
        new FrameworkPropertyMetadata(AttachedOrientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((ButtonGroup)d).Refresh()));

    /// <summary>组里按钮的样式；不写时按钮用自己的默认。</summary>
    public static readonly DependencyProperty VariantProperty = Register(nameof(Variant), typeof(ButtonVariant?));

    /// <summary>组里按钮的色调；不写时是 accent。</summary>
    public static readonly DependencyProperty ToneProperty = Register(nameof(Tone), typeof(StatusTone?));

    /// <summary>组里按钮的字号档位。</summary>
    public static readonly DependencyProperty SizeProperty = Register(nameof(Size), typeof(TextSize?));

    /// <summary>组里按钮的圆角（两端的按钮外侧用它）。</summary>
    public static readonly DependencyProperty RadiusProperty = Register(nameof(Radius), typeof(Radius?));

    static DependencyProperty Register(string name, Type type) =>
        DependencyProperty.Register(name, type, typeof(ButtonGroup), new PropertyMetadata(null, (d, _) => ((ButtonGroup)d).Refresh()));

    static ButtonGroup()
    {
        // 组像 CSS 的 inline-flex：不被父级拉宽
        HorizontalAlignmentProperty.OverrideMetadata(typeof(ButtonGroup), new FrameworkPropertyMetadata(HorizontalAlignment.Left));
        VerticalAlignmentProperty.OverrideMetadata(typeof(ButtonGroup), new FrameworkPropertyMetadata(VerticalAlignment.Top));
    }

    /// <summary>新建：悬停的按钮浮到上面，聚焦的再高一层（CSS 的 z-index 1、2）。</summary>
    public ButtonGroup()
    {
        AddHandler(MouseEnterEvent, new MouseEventHandler(OnChildState), true);
        AddHandler(MouseLeaveEvent, new MouseEventHandler(OnChildState), true);
        AddHandler(GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnChildState), true);
        AddHandler(LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnChildState), true);
    }

    /// <summary>排列方向。</summary>
    public AttachedOrientation Orientation { get => (AttachedOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>组里按钮的样式。</summary>
    public ButtonVariant? Variant { get => (ButtonVariant?)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>组里按钮的色调。</summary>
    public StatusTone? Tone { get => (StatusTone?)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>组里按钮的字号档位。</summary>
    public TextSize? Size { get => (TextSize?)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>组里按钮的圆角。</summary>
    public Radius? Radius { get => (Radius?)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    internal static object CoerceVariant(DependencyObject d, object baseValue) => Coerce(d, baseValue, ButtonCore.VariantProperty, group => group.Variant);

    internal static object CoerceTone(DependencyObject d, object baseValue) => Coerce(d, baseValue, ButtonCore.ToneProperty, group => group.Tone ?? StatusTone.Accent);

    internal static object CoerceSize(DependencyObject d, object baseValue) => Coerce(d, baseValue, ButtonCore.SizeProperty, group => group.Size);

    internal static object CoerceRadius(DependencyObject d, object baseValue) => Coerce(d, baseValue, ButtonCore.RadiusProperty, group => group.Radius);

    /// <summary>
    /// 组的外观盖过主题（默认值、默认样式）里的值；按钮自己的本地值、使用方的样式照用（同 TS：显式写的 prop &gt; 组 &gt; 默认）。
    /// </summary>
    static object Coerce(DependencyObject d, object baseValue, DependencyProperty property, Func<ButtonGroup, object?> value)
    {
        if (d is not FrameworkElement { Parent: ButtonGroup group } || value(group) is not { } shared) return baseValue;
        if (d.ReadLocalValue(property) != DependencyProperty.UnsetValue) return baseValue;
        if (DependencyPropertyHelper.GetValueSource(d, property).BaseValueSource
            is not (BaseValueSource.Default or BaseValueSource.DefaultStyle or BaseValueSource.DefaultStyleTrigger)) return baseValue;
        // 基础值变了时 WPF 先调强制、后记新来源：刚写的本地值、样式值这里还看不到。等这一轮记完再强制一次（那一次不再排）
        if (!Equals(baseValue, shared) && Rechecking.Add((d, property)))
            d.Dispatcher.BeginInvoke(DispatcherPriority.Send, () =>
            {
                d.CoerceValue(property);
                Rechecking.Remove((d, property));
            });
        return shared;
    }

    static readonly HashSet<(DependencyObject, DependencyProperty)> Rechecking = [];

    /// <inheritdoc />
    protected override void OnVisualChildrenChanged(DependencyObject? visualAdded, DependencyObject? visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        if (visualRemoved is UIElement removed)
        {
            removed.ClearValue(Attached.CornersProperty);
            Recoerce(removed);
            Stretch(removed, null);
        }
        if (visualAdded is UIElement added)
        {
            Recoerce(added);
            Stretch(added, Orientation);
        }
    }

    void Refresh()
    {
        foreach (UIElement child in InternalChildren)
        {
            Recoerce(child);
            Stretch(child, Orientation);
        }
        InvalidateMeasure();
    }

    /// <summary>
    /// 交叉轴拉齐（CSS 的 align-items: stretch）：按钮的主题让它在一行里居中，组里改成拉伸。用 SetCurrentValue 不占本地值，
    /// 自己写了对齐的不动；移出组（orientation 为 null）时撤掉。
    /// </summary>
    static void Stretch(UIElement child, AttachedOrientation? orientation)
    {
        if (child is not FrameworkElement element) return;
        foreach (var property in new[] { HorizontalAlignmentProperty, VerticalAlignmentProperty })
            if (element.ReadLocalValue(property) == DependencyProperty.UnsetValue && DependencyPropertyHelper.GetValueSource(element, property).IsCurrent)
                element.InvalidateProperty(property);
        if (orientation is not { } current) return;
        var target = current.IsVertical ? HorizontalAlignmentProperty : VerticalAlignmentProperty;
        if (element.ReadLocalValue(target) == DependencyProperty.UnsetValue)
            element.SetCurrentValue(target, current.IsVertical ? HorizontalAlignment.Stretch : VerticalAlignment.Stretch);
    }

    static void Recoerce(UIElement child)
    {
        if (child is not ButtonCore button) return;
        button.CoerceValue(ButtonCore.VariantProperty);
        button.CoerceValue(ButtonCore.ToneProperty);
        button.CoerceValue(ButtonCore.SizeProperty);
        button.CoerceValue(ButtonCore.RadiusProperty);
    }

    void OnChildState(object sender, RoutedEventArgs e)
    {
        foreach (UIElement child in InternalChildren) SetZIndex(child, child.IsKeyboardFocusWithin ? 2 : child.IsMouseOver ? 1 : 0);
    }

    /// <summary>沿主轴排，相邻的叠一条边框；交叉轴取最大的。顺带给每个按钮写上要去掉圆角的角（跳过隐藏的）。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var vertical = Orientation.IsVertical;
        var shown = InternalChildren.Cast<UIElement>().Where(child => child.Visibility != Visibility.Collapsed).ToList();
        for (var i = 0; i < shown.Count; i++) Attached.SetCorners(shown[i], AttachedRules.Squared(Orientation, i == 0, i == shown.Count - 1));
        var available = vertical ? new Size(availableSize.Width, double.PositiveInfinity) : new Size(double.PositiveInfinity, availableSize.Height);
        double main = 0, cross = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(available);
            if (child.Visibility == Visibility.Collapsed) continue;
            var size = child.DesiredSize;
            main += vertical ? size.Height : size.Width;
            cross = Math.Max(cross, vertical ? size.Width : size.Height);
        }
        main -= Math.Max(0, shown.Count - 1) * AttachedRules.Overlap;
        return vertical ? new Size(cross, main) : new Size(main, cross);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var vertical = Orientation.IsVertical;
        double offset = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            var size = child.DesiredSize;
            child.Arrange(vertical ? new Rect(0, offset, finalSize.Width, size.Height) : new Rect(offset, 0, size.Width, finalSize.Height));
            offset += (vertical ? size.Height : size.Width) - AttachedRules.Overlap;
        }
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonGroupAutomationPeer(this);

    sealed class ButtonGroupAutomationPeer(ButtonGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(ButtonGroup);
    }
}
