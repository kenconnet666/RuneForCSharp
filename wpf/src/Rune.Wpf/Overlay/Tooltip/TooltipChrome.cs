using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 提示模板的根：面板（反色底、sm 圆角、内边距上下 0.25em 左右 0.5em、最宽 20em）与箭头，尺寸都按提示的字号（em）算。
/// 有箭头（触发器上写了 <see cref="Tooltip.ArrowProperty"/>）时四周留出箭头伸出的透明边（弹出窗口之外画不出东西），定位回调把它算进去；箭头的边与位置由 <see cref="Tooltip"/> 在显示后写。
/// </summary>
[ContentProperty(nameof(Child))]
public sealed class TooltipChrome : FrameworkElement
{
    /// <summary>底色（模板里绑定提示的 Background）。</summary>
    public static readonly DependencyProperty BackgroundProperty = Panel.BackgroundProperty.AddOwner(typeof(TooltipChrome));

    /// <summary>圆角的 em 系数（模板里引用 <c>Rune.Radius.Sm</c>，随主题变）。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(double), typeof(TooltipChrome), new FrameworkPropertyMetadata(0.25, FrameworkPropertyMetadataOptions.AffectsMeasure));

    readonly Border panel = new();
    readonly Border arrow = new() { IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5) };
    (string Side, double Center)? arrowAt;

    static TooltipChrome() => TextElement.FontSizeProperty.OverrideMetadata(typeof(TooltipChrome),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>新建：箭头在面板后面（伸进面板的一半被盖住，只露出尖角）。</summary>
    public TooltipChrome()
    {
        panel.SetResourceReference(StyleProperty, TooltipParts.Panel);
        arrow.SetResourceReference(StyleProperty, TooltipParts.Arrow);
        AddVisualChild(arrow);
        AddVisualChild(panel);
    }

    /// <summary>提示的内容。</summary>
    public UIElement? Child { get => panel.Child; set => panel.Child = value; }

    /// <summary>底色。</summary>
    public Brush? Background { get => (Brush?)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>显示箭头：触发器上写了 <see cref="Tooltip.ArrowProperty"/>（每次打开时量一次，那时触发器已经定了）。</summary>
    bool Arrow => TemplatedParent is ToolTip { PlacementTarget: { } target } && Tooltip.GetArrow(target);

    /// <summary>圆角的 em 系数。</summary>
    public double Radius { get => (double)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>箭头的边长：提示字号（sm）的 0.625 倍。</summary>
    /// <param name="scope">从这个元素往上查字号 token。</param>
    internal static double ArrowSize(FrameworkElement scope) => 0.625 * scope.Resource<double>(ThemeKeys.FontSize.Sm);

    /// <summary>箭头伸出面板的长度：旋转 45° 后对角线的一半（约 0.71 倍边长）。</summary>
    internal static double Overhang(double arrowSize) => Math.Ceiling(0.71 * arrowSize);

    double Em => TextElement.GetFontSize(this);

    double ArrowLength => Arrow ? 0.625 * Em : 0;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 2;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => index == 0 ? arrow : panel;

    /// <summary>面板在屏幕上的矩形（与设备无关的像素）。</summary>
    internal Rect PanelOnScreen() => new(Tooltip.Screen(panel, new Point()), Tooltip.Screen(panel, new Point(panel.ActualWidth, panel.ActualHeight)));

    /// <summary>箭头贴着面板的 <paramref name="side"/> 那条边（触发器在那一边），对准触发器的中心（离面板两端至少一个箭头宽）。</summary>
    /// <param name="side">top、bottom、left、right。</param>
    /// <param name="center">触发器的中心在面板这条边上的位置。</param>
    internal void PlaceArrow(string side, double center)
    {
        arrowAt = (side, center);
        InvalidateArrange();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var em = Em;
        // 行高 lineHeights.normal：继承给里面的文字
        SetValue(TextBlock.LineHeightProperty, this.Resource<double>(ThemeKeys.LineHeight.Normal) * em);
        SetValue(TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight);
        panel.Background = Background;
        panel.CornerRadius = new CornerRadius(Radius * em);
        panel.Padding = new Thickness(0.5 * em, 0.25 * em, 0.5 * em, 0.25 * em);
        panel.MaxWidth = 20 * em;
        arrow.Background = Background;
        arrow.Width = arrow.Height = ArrowLength;
        arrow.CornerRadius = new CornerRadius(0, 0, 2, 0);
        arrow.Visibility = Arrow ? Visibility.Visible : Visibility.Collapsed;
        var margin = Overhang(ArrowLength);
        panel.Measure(new Size(Math.Max(0, availableSize.Width - 2 * margin), Math.Max(0, availableSize.Height - 2 * margin)));
        arrow.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return new Size(panel.DesiredSize.Width + 2 * margin, panel.DesiredSize.Height + 2 * margin);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var margin = Overhang(ArrowLength);
        var inner = new Rect(margin, margin, Math.Max(0, finalSize.Width - 2 * margin), Math.Max(0, finalSize.Height - 2 * margin));
        panel.Arrange(inner);
        var size = ArrowLength;
        // 还没定边（刚打开）时先贴在上边中间：尖角朝下
        var (side, center) = arrowAt ?? ("top", inner.Width / 2);
        var vertical = side is "top" or "bottom";
        var length = vertical ? inner.Width : inner.Height;
        var cross = Math.Clamp(center - size / 2, size, Math.Max(size, length - 2 * size));
        var (left, top, angle) = side switch
        {
            "top" => (cross, inner.Height - size / 2, 45),
            "bottom" => (cross, -size / 2, 225),
            "left" => (inner.Width - size / 2, cross, -45),
            _ => (-size / 2, cross, 135),
        };
        arrow.Arrange(new Rect(inner.X + left, inner.Y + top, size, size));
        arrow.RenderTransform = new RotateTransform(angle);
        return finalSize;
    }

    /// <summary>箭头所在的边（测试用）；还没定时为 null。</summary>
    internal string? ArrowSide => arrowAt?.Side;

    /// <summary>箭头（测试用）。</summary>
    internal Border ArrowPart => arrow;

    /// <summary>面板（测试用）。</summary>
    internal Border PanelPart => panel;
}
