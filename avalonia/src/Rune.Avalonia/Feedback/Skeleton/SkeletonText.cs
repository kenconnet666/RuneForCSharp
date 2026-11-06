using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 多行文字的骨架（vue-tsx-pro 的 SkeletonText）：一段正文加载时的占位，几行 <see cref="Skeleton"/>，最后一行短一些（六成宽）。
/// 行高 1em、行间 space.sm，接近正文 1.5 的行高，换成内容时高度大致不变。各行的扫光依次晚 120 毫秒，像一道波从上往下扫过。
/// <code>
/// &lt;rune:SkeletonText /&gt;
/// &lt;rune:SkeletonText Lines="5" Delay="240" /&gt;   &lt;!-- 接在前面的骨架（标题、头像）后面扫 --&gt;
/// </code>
/// 整块对读屏隐藏。各行是部件：<c>rune|SkeletonText &gt; rune|Skeleton#PART_Line</c>（TS 的 propsLine）。
/// </summary>
public class SkeletonText : Panel
{
    /// <summary>相邻两行的扫光间隔（毫秒，TS 的 SHIMMER_STAGGER）：一屏七八行时整道波约一秒扫完。</summary>
    public const int Stagger = 120;

    /// <summary>行数，默认 3。</summary>
    public static readonly StyledProperty<int> LinesProperty = AvaloniaProperty.Register<SkeletonText, int>(nameof(Lines), 3);

    /// <summary>第一行扫光的延迟（毫秒），之后每行再晚 <see cref="Stagger"/>。</summary>
    public static readonly StyledProperty<int> DelayProperty = AvaloniaProperty.Register<SkeletonText, int>(nameof(Delay));

    static SkeletonText() => AffectsMeasure<SkeletonText>(TextElement.FontSizeProperty);

    /// <summary>新建：对读屏隐藏。</summary>
    public SkeletonText()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        Rebuild();
    }

    /// <summary>行数。</summary>
    public int Lines { get => GetValue(LinesProperty); set => SetValue(LinesProperty, value); }

    /// <summary>第一行扫光的延迟（毫秒）。</summary>
    public int Delay { get => GetValue(DelayProperty); set => SetValue(DelayProperty, value); }

    double Em => GetValue(TextElement.FontSizeProperty);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LinesProperty || change.Property == DelayProperty) Rebuild();
    }

    void Rebuild()
    {
        Children.Clear();
        for (var i = 0; i < Math.Max(0, Lines); i++) Children.Add(new Skeleton { Name = SkeletonTextParts.Line, Shape = SkeletonShape.Text, Delay = Delay + i * Stagger });
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        // 最后一行按六成宽量（同摆的时候）
        for (var i = 0; i < Children.Count; i++)
            Children[i].Measure(Children.Count > 1 && i == Children.Count - 1 ? availableSize.WithWidth(0.6 * availableSize.Width) : availableSize);
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new LayoutSize(width, Children.Count * Em + Math.Max(0, Children.Count - 1) * 0.5 * Em);
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        var y = 0d;
        for (var i = 0; i < Children.Count; i++)
        {
            // 最后一行六成宽（只有一行时不缩短）
            var width = Children.Count > 1 && i == Children.Count - 1 ? 0.6 * finalSize.Width : finalSize.Width;
            Children[i].Arrange(new Rect(0, y, width, Em));
            y += 1.5 * Em;
        }
        return finalSize;
    }
}

/// <summary>SkeletonText 的部件名。</summary>
public static class SkeletonTextParts
{
    /// <summary>每一行（TS 的 propsLine），各行都叫这个名字。</summary>
    public const string Line = "PART_Line";
}
