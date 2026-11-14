using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 固定宽高比的框（vue-tsx-pro 的 AspectRatio）：宽度跟着容器，高度按比例算，里面的内容铺满它。图片、视频、地图、嵌入的页面
/// 加载前就占好位置，加载完不把下面的内容顶开。
/// <code>
/// &lt;rune:AspectRatio Ratio="1.7778"&gt;&lt;Image Source="{Binding Cover}" /&gt;&lt;/rune:AspectRatio&gt;
/// </code>
/// 内容被拉成和框一样大、超出的裁掉；里面的 Image 默认按 UniformToFill（CSS 的 cover）裁切，自己写了 Stretch 的照用。
/// 宽高比是数（宽 ÷ 高，默认 16 / 9）；不限宽度时按内容的宽度算。
/// </summary>
public class AspectRatio : Decorator
{
    /// <summary>宽高比（宽 ÷ 高），默认 16 / 9。</summary>
    public static readonly DependencyProperty RatioProperty = DependencyProperty.Register(
        nameof(Ratio), typeof(double), typeof(AspectRatio), new FrameworkPropertyMetadata(16d / 9, FrameworkPropertyMetadataOptions.AffectsMeasure), value => (double)value > 0);

    static AspectRatio() => ClipToBoundsProperty.OverrideMetadata(typeof(AspectRatio), new FrameworkPropertyMetadata(true));

    /// <summary>宽高比。</summary>
    public double Ratio { get => (double)GetValue(RatioProperty); set => SetValue(RatioProperty, value); }

    /// <inheritdoc />
    public override UIElement Child
    {
        get => base.Child;
        set
        {
            base.Child = value;
            // 图片按 cover 裁切：不占本地值，自己写了 Stretch 的不动
            if (value is Image image && image.ReadLocalValue(Image.StretchProperty) == DependencyProperty.UnsetValue)
                image.SetCurrentValue(Image.StretchProperty, Stretch.UniformToFill);
        }
    }

    /// <summary>宽度取给的宽度（不限时取内容的宽度），高度 = 宽度 ÷ 比例；内容按这个大小量。</summary>
    protected override Size MeasureOverride(Size constraint)
    {
        var width = constraint.Width;
        if (double.IsInfinity(width))
        {
            Child?.Measure(constraint);
            width = Child?.DesiredSize.Width ?? 0;
        }
        var size = new Size(width, width / Ratio);
        Child?.Measure(size);
        return size;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(arrangeSize));
        return arrangeSize;
    }
}
