using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 在代码里组合子元素、不用模板的控件：只有一个根元素（<see cref="Root"/>）。
/// 从 Control 而不是 FrameworkElement 派生：外面的隐式样式只对 Control 派生的元素进入别的组件的模板（DESIGN §7）。
/// 继承来的 Background、BorderBrush、BorderThickness、Padding 本来要靠模板才起作用，这里统一处理：
/// 背景与描边画在根元素后面，描边宽度与内边距留在根元素四周。组件自己把其中某些属性接到了根元素上
/// （带圆角的 Border）时，用 <see cref="OwnChrome"/> 说明，这里就不再重复画、重复留。
/// </summary>
public abstract class CompositeControl : Control
{
    UIElement? root;

    static CompositeControl()
    {
        // Control 上这几个属性的元数据不影响排版与绘制（交给模板）；没有模板时由这里画、这里留
        BackgroundProperty.OverrideMetadata(typeof(CompositeControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        BorderBrushProperty.OverrideMetadata(typeof(CompositeControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        BorderThicknessProperty.OverrideMetadata(typeof(CompositeControl), new FrameworkPropertyMetadata(default(Thickness), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        PaddingProperty.OverrideMetadata(typeof(CompositeControl), new FrameworkPropertyMetadata(default(Thickness), FrameworkPropertyMetadataOptions.AffectsMeasure));
    }

    /// <summary>根元素：派生类在构造时设好。</summary>
    protected UIElement? Root
    {
        get => root;
        set
        {
            if (root == value) return;
            if (root is not null)
            {
                RemoveLogicalChild(root);
                RemoveVisualChild(root);
            }
            root = value;
            if (value is not null)
            {
                AddVisualChild(value);
                AddLogicalChild(value);
            }
            InvalidateMeasure();
        }
    }

    /// <summary>组件自己接到根元素上的外框属性（这里不再画、不再留）。</summary>
    protected virtual CompositeChrome OwnChrome => CompositeChrome.None;

    /// <inheritdoc />
    protected override int VisualChildrenCount => root is null ? 0 : 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root ?? throw new ArgumentOutOfRangeException(nameof(index));

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => (root is null ? Array.Empty<object>() : [root]).GetEnumerator();

    /// <summary>根元素四周留出的：没有自己接的描边宽度与内边距。</summary>
    protected Thickness Inset
    {
        get
        {
            var border = OwnChrome.HasFlag(CompositeChrome.Border) ? default : BorderThickness;
            var padding = OwnChrome.HasFlag(CompositeChrome.Padding) ? default : Padding;
            return new Thickness(border.Left + padding.Left, border.Top + padding.Top, border.Right + padding.Right, border.Bottom + padding.Bottom);
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        var inset = Inset;
        if (root is null) return new Size(inset.Left + inset.Right, inset.Top + inset.Bottom);
        root.Measure(new Size(Math.Max(0, constraint.Width - inset.Left - inset.Right), Math.Max(0, constraint.Height - inset.Top - inset.Bottom)));
        return new Size(root.DesiredSize.Width + inset.Left + inset.Right, root.DesiredSize.Height + inset.Top + inset.Bottom);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var inset = Inset;
        root?.Arrange(new Rect(inset.Left, inset.Top, Math.Max(0, arrangeBounds.Width - inset.Left - inset.Right), Math.Max(0, arrangeBounds.Height - inset.Top - inset.Bottom)));
        return arrangeBounds;
    }

    /// <summary>背景与描边（没有自己接的）：画在根元素后面，直角。</summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        var bounds = new Rect(RenderSize);
        if (!OwnChrome.HasFlag(CompositeChrome.Background) && Background is { } background) drawingContext.DrawRectangle(background, null, bounds);
        if (OwnChrome.HasFlag(CompositeChrome.Border) || BorderBrush is not { } brush) return;
        var line = BorderThickness;
        if (line.Top > 0) drawingContext.DrawRectangle(brush, null, new Rect(0, 0, bounds.Width, line.Top));
        if (line.Bottom > 0) drawingContext.DrawRectangle(brush, null, new Rect(0, Math.Max(0, bounds.Height - line.Bottom), bounds.Width, line.Bottom));
        if (line.Left > 0) drawingContext.DrawRectangle(brush, null, new Rect(0, 0, line.Left, bounds.Height));
        if (line.Right > 0) drawingContext.DrawRectangle(brush, null, new Rect(Math.Max(0, bounds.Width - line.Right), 0, line.Right, bounds.Height));
    }
}

/// <summary><see cref="CompositeControl"/> 的组件自己接到根元素上的外框属性。</summary>
[Flags]
public enum CompositeChrome
{
    /// <summary>都由 CompositeControl 处理。</summary>
    None = 0,

    /// <summary>Background。</summary>
    Background = 1,

    /// <summary>BorderBrush 与 BorderThickness。</summary>
    Border = 2,

    /// <summary>Padding。</summary>
    Padding = 4,

    /// <summary>全部。</summary>
    All = Background | Border | Padding,
}
