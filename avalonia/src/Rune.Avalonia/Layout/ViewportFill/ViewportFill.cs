using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 占满到窗口底边（vue-tsx-pro 的 ViewportFill）：从自己的顶边一直到所在滚动区域的可视区底边（留出 <see cref="Bottom"/>），里面竖排。
/// 列表页让表格正好占满一屏、在表格里滚动，分页贴着底边；收件箱的左右分栏占满剩下的高度。
/// <code>
/// &lt;rune:ViewportFill Gap="md"&gt;
///   &lt;local:Toolbar /&gt;
///   &lt;rune:Table rune:Flex.Grow="True" … /&gt;   &lt;!-- 撑满剩下的 --&gt;
///   &lt;rune:Pagination … /&gt;
/// &lt;/rune:ViewportFill&gt;
/// </code>
/// 顶边是自己在滚动内容里的位置（上面的页头、页签、查询栏换行变高都跟着变），不随滚动变；外面没有滚动区域时按窗口算。
/// 可视区太矮时不小于 <see cref="MinFillHeight"/>，整页滚动。
/// </summary>
public class ViewportFill : Flex
{
    /// <summary>离可视区底边留多少，默认 xl（与 AppShell 正文的内边距一致：留得比它少，整页会多出一截滚动）。</summary>
    public static readonly StyledProperty<Space> BottomProperty = AvaloniaProperty.Register<ViewportFill, Space>(nameof(Bottom), Space.Xl);

    /// <summary>最小高度，默认 24rem：可视区太矮时整页滚动。</summary>
    public static readonly StyledProperty<Length> MinFillHeightProperty = AvaloniaProperty.Register<ViewportFill, Length>(nameof(MinFillHeight), new Length("24rem"));

    ScrollViewer? scroller;
    TopLevel? root;
    double top = double.NaN;

    static ViewportFill() => AffectsMeasure<ViewportFill>(BottomProperty, MinFillHeightProperty);

    /// <summary>新建：竖排。</summary>
    public ViewportFill()
    {
        Direction = FlexDirection.Column;
        // 上面的内容变高、窗口变大小之后，重新量顶边与可视区
        LayoutUpdated += (_, _) => Remeasure();
    }

    /// <summary>离可视区底边留多少。</summary>
    public Space Bottom { get => GetValue(BottomProperty); set => SetValue(BottomProperty, value); }

    /// <summary>最小高度。</summary>
    public Length MinFillHeight { get => GetValue(MinFillHeightProperty); set => SetValue(MinFillHeightProperty, value); }

    double Em => global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);

    /// <summary>该有的高度：可视区高减去顶边与底部留白，不小于最小高度；还没进界面时 null。</summary>
    internal double? FillHeight
    {
        get
        {
            if (double.IsNaN(top)) return null;
            var viewport = scroller is not null ? scroller.Viewport.Height : root?.ClientSize.Height ?? 0;
            var bottom = Bottom.ResourceKey is { } key ? this.Resource<double>(key) * Em : 0;
            return Math.Max(MinFillHeight.Pixels(Em), viewport - top - bottom);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        scroller = this.FindAncestorOfType<ScrollViewer>();
        root = TopLevel.GetTopLevel(this);
        top = double.NaN;
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        scroller = null;
        root = null;
    }

    /// <summary>顶边：在滚动内容里的位置（加回已经滚过的距离，不随滚动变）；没有滚动区域时是在窗口里的位置。</summary>
    void Remeasure()
    {
        Visual? reference = scroller is not null ? scroller : root;
        if (reference is null || this.TranslatePoint(default, reference) is not { } point) return;
        var next = Math.Round(point.Y + (scroller?.Offset.Y ?? 0));
        var height = FillHeight;
        if (next == top && height is { } known && Math.Abs(known - Bounds.Height) < 0.5) return;
        top = next;
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (FillHeight is not { } height) return base.MeasureOverride(availableSize);
        var size = base.MeasureOverride(availableSize.WithHeight(height));
        return size.WithHeight(height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize) =>
        base.ArrangeOverride(FillHeight is { } height ? finalSize.WithHeight(height) : finalSize);
}
