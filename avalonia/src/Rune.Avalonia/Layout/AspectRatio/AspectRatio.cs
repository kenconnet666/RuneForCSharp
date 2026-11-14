using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 固定宽高比的框（vue-tsx-pro 的 AspectRatio）：宽度跟着容器，高度按比例算，里面的内容铺满它。图片、视频、地图、嵌入的页面
/// 加载前就占好位置，加载完不把下面的内容顶开。
/// <code>
/// &lt;rune:AspectRatio Ratio="1.7778"&gt;&lt;Image Source="{Binding Cover}" /&gt;&lt;/rune:AspectRatio&gt;
/// </code>
/// 内容被拉成和框一样大、超出的裁掉；里面的 Image 默认按 UniformToFill（CSS 的 cover）裁切，要完整显示时在它上面写 Stretch。
/// 宽高比是数（宽 ÷ 高，默认 16 / 9）；不限宽度时按内容的宽度算。
/// </summary>
public class AspectRatio : Decorator
{
    /// <summary>宽高比（宽 ÷ 高），默认 16 / 9。</summary>
    public static readonly StyledProperty<double> RatioProperty = AvaloniaProperty.Register<AspectRatio, double>(nameof(Ratio), 16d / 9, validate: ratio => ratio > 0);

    IDisposable? cover;

    static AspectRatio()
    {
        AffectsMeasure<AspectRatio>(RatioProperty);
        ClipToBoundsProperty.OverrideDefaultValue<AspectRatio>(true);
    }

    /// <summary>宽高比。</summary>
    public double Ratio { get => GetValue(RatioProperty); set => SetValue(RatioProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ChildProperty) return;
        cover?.Dispose();
        // 图片按 cover 裁切：写在模板优先级上，使用方在 Image 上写的 Stretch 盖得过
        cover = Child is Image image ? image.SetValue(Image.StretchProperty, Stretch.UniformToFill, BindingPriority.Template) : null;
    }

    /// <summary>宽度取给的宽度（不限时取内容的宽度），高度 = 宽度 ÷ 比例；内容按这个大小量。</summary>
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        var width = availableSize.Width;
        if (double.IsInfinity(width))
        {
            Child?.Measure(availableSize);
            width = Child?.DesiredSize.Width ?? 0;
        }
        var size = new LayoutSize(width, width / Ratio);
        Child?.Measure(size);
        return size;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        Child?.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
