using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf;

/// <summary>
/// 相连的一组（ButtonGroup、ControlGroup 共用）：沿主轴排，相邻的叠一条边框，交叉轴拉齐；给每个控件写上要去掉圆角的角（跳过隐藏的），
/// 悬停的浮到上面、聚焦的再高一层（CSS 的 z-index 1、2）。主轴上要分剩余空间的控件由派生类指定（ControlGroup 的输入框）。读屏里是一个组。
/// </summary>
public abstract class AttachedGroup : Panel
{
    /// <summary>排列方向，默认 horizontal。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(AttachedOrientation), typeof(AttachedGroup),
        new FrameworkPropertyMetadata(AttachedOrientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((AttachedGroup)d).Refresh()));

    /// <summary>每个显示着的控件的主轴长度（Measure 算好，Arrange 照用）。</summary>
    readonly Dictionary<UIElement, double> lengths = [];

    /// <summary>新建：悬停的控件浮到上面，聚焦的再高一层。</summary>
    protected AttachedGroup()
    {
        AddHandler(MouseEnterEvent, new MouseEventHandler(OnChildState), true);
        AddHandler(MouseLeaveEvent, new MouseEventHandler(OnChildState), true);
        AddHandler(GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnChildState), true);
        AddHandler(LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnChildState), true);
    }

    /// <summary>排列方向。</summary>
    public AttachedOrientation Orientation { get => (AttachedOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>主轴上分剩余空间的控件（CSS 的 flex-grow: 1）；只在横排、宽度有限时生效。</summary>
    protected virtual bool Grows(UIElement child) => false;

    /// <summary>控件放进、移出组（移出时 joined 为 false），或组的方向、外观变了：派生类在这里写、撤自己给的默认值。</summary>
    protected virtual void OnJoined(UIElement child, bool joined)
    {
    }

    /// <inheritdoc />
    protected override void OnVisualChildrenChanged(DependencyObject? visualAdded, DependencyObject? visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        if (visualRemoved is UIElement removed)
        {
            removed.ClearValue(Attached.CornersProperty);
            Stretch(removed, null);
            OnJoined(removed, false);
        }
        if (visualAdded is UIElement added)
        {
            Stretch(added, Orientation);
            OnJoined(added, true);
        }
    }

    /// <summary>方向、外观变了：每个控件重新来一遍。</summary>
    protected void Refresh()
    {
        foreach (UIElement child in InternalChildren)
        {
            Stretch(child, Orientation);
            OnJoined(child, true);
        }
        InvalidateMeasure();
    }

    /// <summary>
    /// 交叉轴拉齐（CSS 的 align-items: stretch）：控件的主题让它在一行里居中，组里改成拉伸。用 SetCurrentValue 不占本地值，
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

    void OnChildState(object sender, RoutedEventArgs e)
    {
        foreach (UIElement child in InternalChildren) SetZIndex(child, child.IsKeyboardFocusWithin ? 2 : child.IsMouseOver ? 1 : 0);
    }

    /// <summary>
    /// 先按内容量；横排且宽度有限时，多出来的宽度平分给要分剩余空间的控件（CSS 的 flex-grow，基准是内容宽），再按分到的宽度量一次。
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var vertical = Orientation.IsVertical;
        var shown = InternalChildren.Cast<UIElement>().Where(child => child.Visibility != Visibility.Collapsed).ToList();
        for (var i = 0; i < shown.Count; i++) Attached.SetCorners(shown[i], AttachedRules.Squared(Orientation, i == 0, i == shown.Count - 1));
        var available = vertical ? new Size(availableSize.Width, double.PositiveInfinity) : new Size(double.PositiveInfinity, availableSize.Height);
        foreach (UIElement child in InternalChildren) child.Measure(available);
        lengths.Clear();
        foreach (var child in shown) lengths[child] = vertical ? child.DesiredSize.Height : child.DesiredSize.Width;
        var overlaps = Math.Max(0, shown.Count - 1) * AttachedRules.Overlap;
        List<UIElement> growing = vertical ? [] : shown.Where(Grows).ToList();
        if (growing.Count > 0 && !double.IsInfinity(availableSize.Width))
        {
            var extra = availableSize.Width - (lengths.Values.Sum() - overlaps);
            if (extra > 0)
                foreach (var child in growing)
                {
                    lengths[child] += extra / growing.Count;
                    child.Measure(new Size(lengths[child], availableSize.Height));
                }
        }
        var main = lengths.Values.Sum() - overlaps;
        var cross = shown.Select(child => vertical ? child.DesiredSize.Width : child.DesiredSize.Height).DefaultIfEmpty().Max();
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
            var length = lengths.TryGetValue(child, out var measured) ? measured : vertical ? child.DesiredSize.Height : child.DesiredSize.Width;
            child.Arrange(vertical ? new Rect(0, offset, finalSize.Width, length) : new Rect(offset, 0, length, finalSize.Height));
            offset += length - AttachedRules.Overlap;
        }
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AttachedGroupAutomationPeer(this);

    /// <summary>读屏：一个组，类名是组件名。</summary>
    /// <param name="owner">组。</param>
    protected class AttachedGroupAutomationPeer(AttachedGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        /// <inheritdoc />
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        /// <inheritdoc />
        protected override string GetClassNameCore() => owner.GetType().Name;
    }
}
