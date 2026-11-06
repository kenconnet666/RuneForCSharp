using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf;

/// <summary>
/// 多行文字的骨架（vue-tsx-pro 的 SkeletonText）：一段正文加载时的占位，几行 <see cref="Skeleton"/>，最后一行短一些（六成宽）。
/// 行高 1em、行间 space.sm，接近正文 1.5 的行高，换成内容时高度大致不变。各行的扫光依次晚 120 毫秒，像一道波从上往下扫过。
/// <code>
/// &lt;rune:SkeletonText /&gt;
/// &lt;rune:SkeletonText Lines="5" Delay="240" /&gt;   &lt;!-- 接在前面的骨架（标题、头像）后面扫 --&gt;
/// </code>
/// 整块对读屏隐藏。
/// </summary>
public class SkeletonText : Panel
{
    /// <summary>相邻两行的扫光间隔（毫秒，TS 的 SHIMMER_STAGGER）：一屏七八行时整道波约一秒扫完。</summary>
    public const int Stagger = 120;

    /// <summary>行数，默认 3。</summary>
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(
        nameof(Lines), typeof(int), typeof(SkeletonText), new FrameworkPropertyMetadata(3, (d, _) => ((SkeletonText)d).Rebuild()));

    /// <summary>第一行扫光的延迟（毫秒），之后每行再晚 <see cref="Stagger"/>。</summary>
    public static readonly DependencyProperty DelayProperty = DependencyProperty.Register(
        nameof(Delay), typeof(int), typeof(SkeletonText), new FrameworkPropertyMetadata(0, (d, _) => ((SkeletonText)d).Rebuild()));

    static SkeletonText() => TextElement.FontSizeProperty.OverrideMetadata(typeof(SkeletonText), new FrameworkPropertyMetadata(
        SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>新建。</summary>
    public SkeletonText() => Rebuild();

    /// <summary>行数。</summary>
    public int Lines { get => (int)GetValue(LinesProperty); set => SetValue(LinesProperty, value); }

    /// <summary>第一行扫光的延迟（毫秒）。</summary>
    public int Delay { get => (int)GetValue(DelayProperty); set => SetValue(DelayProperty, value); }

    double Em => TextElement.GetFontSize(this);

    void Rebuild()
    {
        InternalChildren.Clear();
        for (var i = 0; i < Math.Max(0, Lines); i++) InternalChildren.Add(new Skeleton { Shape = SkeletonShape.Text, Delay = Delay + i * Stagger });
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var count = InternalChildren.Count;
        // WPF 摆的尺寸不会比量出来的小：最后一行按六成宽量
        for (var i = 0; i < count; i++)
            InternalChildren[i].Measure(count > 1 && i == count - 1 ? availableSize with { Width = 0.6 * availableSize.Width } : availableSize);
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, count * Em + Math.Max(0, count - 1) * 0.5 * Em);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = InternalChildren.Count;
        for (var i = 0; i < count; i++)
        {
            // 最后一行六成宽（只有一行时不缩短）
            var width = count > 1 && i == count - 1 ? 0.6 * finalSize.Width : finalSize.Width;
            InternalChildren[i].Arrange(new Rect(0, i * 1.5 * Em, width, Em));
        }
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SkeletonTextAutomationPeer(this);

    /// <summary>整块对读屏隐藏。</summary>
    sealed class SkeletonTextAutomationPeer(SkeletonText owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(SkeletonText);

        protected override bool IsControlElementCore() => false;

        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}
