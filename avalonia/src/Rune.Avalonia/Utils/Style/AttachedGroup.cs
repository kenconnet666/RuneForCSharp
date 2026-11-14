using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 相连的一组（ButtonGroup、ControlGroup 共用）：沿主轴排，相邻的叠一条边框，交叉轴拉齐；给每个控件写上要去掉圆角的角（跳过隐藏的），
/// 悬停的浮到上面、聚焦的再高一层（CSS 的 z-index 1、2）。主轴上要分剩余空间的控件由派生类指定（ControlGroup 的输入框）。读屏里是一个组。
/// </summary>
public abstract class AttachedGroup : Panel
{
    /// <summary>排列方向，默认 horizontal。</summary>
    public static readonly StyledProperty<AttachedOrientation> OrientationProperty =
        AvaloniaProperty.Register<AttachedGroup, AttachedOrientation>(nameof(Orientation), AttachedOrientation.Horizontal);

    /// <summary>组写给每个控件的值（模板优先级）：换方向、外观，移出组时撤掉，不碰控件自己写的值。</summary>
    readonly Dictionary<Control, List<IDisposable>> applied = [];

    static AttachedGroup() => AffectsMeasure<AttachedGroup>(OrientationProperty);

    /// <summary>排列方向。</summary>
    public AttachedOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>主轴上分剩余空间的控件（CSS 的 flex-grow: 1）；只在不限内容宽、横排时生效。</summary>
    protected virtual bool Grows(Control child) => false;

    /// <summary>组写给控件的默认值（派生类加上自己的，如按钮的外观）：写在模板优先级上，返回的句柄由组撤掉。</summary>
    protected virtual IEnumerable<IDisposable?> Defaults(Control child)
    {
        yield return Orientation.IsVertical
            ? child.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch, BindingPriority.Template)
            : child.SetValue(VerticalAlignmentProperty, VerticalAlignment.Stretch, BindingPriority.Template);
    }

    /// <summary>重新写一遍每个控件的默认值（方向、外观变了）。</summary>
    protected void Reapply()
    {
        foreach (var child in Children) Apply(child);
    }

    void Apply(Control child)
    {
        Revert(child);
        applied[child] = Defaults(child).OfType<IDisposable>().ToList();
    }

    void Revert(Control child)
    {
        if (!applied.Remove(child, out var values)) return;
        foreach (var value in values) value.Dispose();
    }

    /// <inheritdoc />
    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        foreach (var child in e.OldItems?.OfType<Control>() ?? [])
        {
            child.PropertyChanged -= OnChildPropertyChanged;
            child.ClearValue(Attached.CornersProperty);
            Revert(child);
        }
        foreach (var child in e.NewItems?.OfType<Control>() ?? [])
        {
            child.PropertyChanged += OnChildPropertyChanged;
            Apply(child);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty) Reapply();
    }

    static void OnChildPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not Control child || (e.Property != IsPointerOverProperty && e.Property != IsKeyboardFocusWithinProperty)) return;
        child.ZIndex = child.IsKeyboardFocusWithin ? 2 : child.IsPointerOver ? 1 : 0;
    }

    /// <summary>每个显示着的控件的主轴长度（Measure 算好，Arrange 照用）。</summary>
    readonly Dictionary<Control, double> lengths = [];

    /// <summary>
    /// 先按内容量；横排且宽度有限时，多出来的宽度平分给要分剩余空间的控件（CSS 的 flex-grow，基准是内容宽），再按分到的宽度量一次。
    /// </summary>
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        var vertical = Orientation.IsVertical;
        var shown = Children.Where(child => child.IsVisible).ToList();
        for (var i = 0; i < shown.Count; i++) Attached.SetCorners(shown[i], AttachedRules.Squared(Orientation, i == 0, i == shown.Count - 1));
        var available = vertical ? availableSize.WithHeight(double.PositiveInfinity) : availableSize.WithWidth(double.PositiveInfinity);
        foreach (var child in Children) child.Measure(available);
        lengths.Clear();
        foreach (var child in shown) lengths[child] = vertical ? child.DesiredSize.Height : child.DesiredSize.Width;
        var overlaps = Math.Max(0, shown.Count - 1) * AttachedRules.Overlap;
        var growing = vertical ? [] : shown.Where(Grows).ToList();
        if (growing.Count > 0 && !double.IsInfinity(availableSize.Width))
        {
            var extra = availableSize.Width - (lengths.Values.Sum() - overlaps);
            if (extra > 0)
                foreach (var child in growing)
                {
                    lengths[child] += extra / growing.Count;
                    child.Measure(availableSize.WithWidth(lengths[child]));
                }
        }
        var main = lengths.Values.Sum() - overlaps;
        var cross = shown.Select(child => vertical ? child.DesiredSize.Width : child.DesiredSize.Height).DefaultIfEmpty().Max();
        return vertical ? new LayoutSize(cross, main) : new LayoutSize(main, cross);
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        var vertical = Orientation.IsVertical;
        double offset = 0;
        foreach (var child in Children.Where(child => child.IsVisible))
        {
            var length = lengths.TryGetValue(child, out var measured) ? measured : vertical ? child.DesiredSize.Height : child.DesiredSize.Width;
            child.Arrange(vertical ? new Rect(0, offset, finalSize.Width, length) : new Rect(offset, 0, length, finalSize.Height));
            offset += length - AttachedRules.Overlap;
        }
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AttachedGroupAutomationPeer(this);

    /// <summary>读屏：一个组。</summary>
    /// <param name="owner">组。</param>
    protected class AttachedGroupAutomationPeer(AttachedGroup owner) : ControlAutomationPeer(owner)
    {
        /// <inheritdoc />
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        /// <inheritdoc />
        protected override bool IsControlElementCore() => true;
    }
}
