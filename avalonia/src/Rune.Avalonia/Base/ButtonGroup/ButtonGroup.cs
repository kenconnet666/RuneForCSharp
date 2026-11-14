using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 按钮组（vue-tsx-pro 的 ButtonGroup）：几个相关的按钮连成一条，共用外观。拆分按钮、翻页、缩放、对齐方式这类工具栏。
/// <code>
/// &lt;rune:ButtonGroup Variant="outline" Tone="neutral" AutomationProperties.Name="文字对齐"&gt;
///   &lt;rune:IconButton Icon="{x:Static rune:Lucide.AlignLeft}" AutomationProperties.Name="左对齐" /&gt;
///   &lt;rune:IconButton Icon="{x:Static rune:Lucide.AlignCenter}" AutomationProperties.Name="居中" /&gt;
/// &lt;/rune:ButtonGroup&gt;
/// </code>
/// 组里的按钮去掉相邻一侧的圆角、边框叠在一起，交叉轴拉齐（一样高，竖排时一样宽）；悬停、聚焦的那个浮到上面，边框与焦点环不被邻居盖住。
/// 组上写的 Variant、Tone、Size、Radius 是组里按钮的默认值，按钮自己写的优先；没写 Tone 时组里的按钮都是 accent（图标按钮也是，拆分按钮的两半同色）。实底（solid）的相邻按钮之间没有分隔，连成一条时用 outline、surface 更清楚。
/// 只是排一排、不相连的按钮用 Flex；选中其中一项用 ToggleGroup。读屏里是一个组，名称写 <c>AutomationProperties.Name</c>。
/// </summary>
public class ButtonGroup : Panel
{
    /// <summary>排列方向，默认 horizontal。</summary>
    public static readonly StyledProperty<AttachedOrientation> OrientationProperty =
        AvaloniaProperty.Register<ButtonGroup, AttachedOrientation>(nameof(Orientation), AttachedOrientation.Horizontal);

    /// <summary>组里按钮的样式；不写时按钮用自己的默认。</summary>
    public static readonly StyledProperty<ButtonVariant?> VariantProperty = AvaloniaProperty.Register<ButtonGroup, ButtonVariant?>(nameof(Variant));

    /// <summary>组里按钮的色调；不写时是 accent。</summary>
    public static readonly StyledProperty<StatusTone?> ToneProperty = AvaloniaProperty.Register<ButtonGroup, StatusTone?>(nameof(Tone));

    /// <summary>组里按钮的字号档位。</summary>
    public static readonly StyledProperty<TextSize?> SizeProperty = AvaloniaProperty.Register<ButtonGroup, TextSize?>(nameof(Size));

    /// <summary>组里按钮的圆角（两端的按钮外侧用它）。</summary>
    public static readonly StyledProperty<Radius?> RadiusProperty = AvaloniaProperty.Register<ButtonGroup, Radius?>(nameof(Radius));

    static ButtonGroup()
    {
        AffectsMeasure<ButtonGroup>(OrientationProperty);
        // 组像 CSS 的 inline-flex：不被父级拉宽
        HorizontalAlignmentProperty.OverrideDefaultValue<ButtonGroup>(HorizontalAlignment.Left);
        VerticalAlignmentProperty.OverrideDefaultValue<ButtonGroup>(VerticalAlignment.Top);
    }

    /// <summary>排列方向。</summary>
    public AttachedOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>组里按钮的样式。</summary>
    public ButtonVariant? Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>组里按钮的色调。</summary>
    public StatusTone? Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>组里按钮的字号档位。</summary>
    public TextSize? Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>组里按钮的圆角。</summary>
    public Radius? Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>组写给每个按钮的默认值（模板优先级）：换外观、移出组时撤掉，不碰按钮自己写的值。</summary>
    readonly Dictionary<Control, List<IDisposable>> applied = [];

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
            ApplyAppearance(child);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty || change.Property == ToneProperty || change.Property == SizeProperty || change.Property == RadiusProperty || change.Property == OrientationProperty)
            foreach (var child in Children) ApplyAppearance(child);
    }

    /// <summary>
    /// 组的外观是按钮的默认值：写在模板优先级上（主题之上、按钮自己写的本地值之下）。交叉轴拉齐（CSS 的 align-items: stretch）。
    /// </summary>
    void ApplyAppearance(Control child)
    {
        Revert(child);
        var vertical = Orientation.IsVertical;
        List<IDisposable?> values =
        [
            vertical
                ? child.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch, BindingPriority.Template)
                : child.SetValue(VerticalAlignmentProperty, VerticalAlignment.Stretch, BindingPriority.Template),
        ];
        if (child is ButtonCore button)
        {
            if (Variant is { } variant) values.Add(button.SetValue(ButtonCore.VariantProperty, variant, BindingPriority.Template));
            // 组里的按钮外观要一致：没写色调时都按 Button 的默认（accent），图标按钮也是（拆分按钮的两半同色）
            values.Add(button.SetValue(ButtonCore.ToneProperty, Tone ?? StatusTone.Accent, BindingPriority.Template));
            if (Size is { } size) values.Add(button.SetValue(ButtonCore.SizeProperty, size, BindingPriority.Template));
            if (Radius is { } radius) values.Add(button.SetValue(ButtonCore.RadiusProperty, radius, BindingPriority.Template));
        }
        applied[child] = values.OfType<IDisposable>().ToList();
    }

    void Revert(Control child)
    {
        if (!applied.Remove(child, out var values)) return;
        foreach (var value in values) value.Dispose();
    }

    /// <summary>悬停的浮到上面，聚焦的再高一层（CSS 的 z-index 1、2）。</summary>
    static void OnChildPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not Control child || (e.Property != IsPointerOverProperty && e.Property != IsKeyboardFocusWithinProperty)) return;
        child.ZIndex = child.IsKeyboardFocusWithin ? 2 : child.IsPointerOver ? 1 : 0;
    }

    /// <summary>沿主轴排，相邻的叠一条边框；交叉轴取最大的。顺带给每个按钮写上要去掉圆角的角（跳过隐藏的）。</summary>
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        var vertical = Orientation.IsVertical;
        var shown = Children.Where(child => child.IsVisible).ToList();
        for (var i = 0; i < shown.Count; i++) Attached.SetCorners(shown[i], AttachedRules.Squared(Orientation, i == 0, i == shown.Count - 1));
        var available = vertical ? availableSize.WithHeight(double.PositiveInfinity) : availableSize.WithWidth(double.PositiveInfinity);
        double main = 0, cross = 0;
        foreach (var child in Children)
        {
            child.Measure(available);
            if (!child.IsVisible) continue;
            var size = child.DesiredSize;
            main += vertical ? size.Height : size.Width;
            cross = Math.Max(cross, vertical ? size.Width : size.Height);
        }
        main -= Math.Max(0, shown.Count - 1) * AttachedRules.Overlap;
        return vertical ? new LayoutSize(cross, main) : new LayoutSize(main, cross);
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        var vertical = Orientation.IsVertical;
        double offset = 0;
        foreach (var child in Children.Where(child => child.IsVisible))
        {
            var size = child.DesiredSize;
            if (vertical)
            {
                child.Arrange(new Rect(0, offset, finalSize.Width, size.Height));
                offset += size.Height - AttachedRules.Overlap;
            }
            else
            {
                child.Arrange(new Rect(offset, 0, size.Width, finalSize.Height));
                offset += size.Width - AttachedRules.Overlap;
            }
        }
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonGroupAutomationPeer(this);

    sealed class ButtonGroupAutomationPeer(ButtonGroup owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;
    }
}
