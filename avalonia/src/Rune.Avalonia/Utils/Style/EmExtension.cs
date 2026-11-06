using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Rune.Avalonia;

/// <summary>
/// em 尺寸：值 = 系数 × 字号，字号变了跟着变（对应 vue-tsx-pro 里组件内部一律用 em）。
/// 组件只在根元素上设字号（size 档位或继承上下文），内边距、圆角、间距等写成 em，整体等比缩放。
/// <code>
/// &lt;Setter Property="Padding" Value="{rune:Em 1, 0.25}" /&gt;                        &lt;!-- 水平 1em、垂直 0.25em --&gt;
/// &lt;Border CornerRadius="{rune:Em 0.4, Of=TemplatedParent}" /&gt;                   &lt;!-- 模板里取组件根的字号 --&gt;
/// </code>
/// 返回绑定到字号的绑定：写在 ControlTheme 的 Setter 里时优先级随 Setter，使用方的样式与本地值照常盖过它。
/// 比写死数值慢一些（3000 个按钮的创建约多 27%），大量重复的轻量元素（表格单元格）改在代码里按字号算。
/// </summary>
public sealed class EmExtension
{
    readonly double[] factors;

    /// <summary>四边（或单值）相同。</summary>
    /// <param name="all">系数。</param>
    public EmExtension(double all) => factors = [all];

    /// <summary>水平、垂直两组。</summary>
    /// <param name="horizontal">左右的系数。</param>
    /// <param name="vertical">上下的系数。</param>
    public EmExtension(double horizontal, double vertical) => factors = [horizontal, vertical];

    /// <summary>四边分别给。</summary>
    /// <param name="left">左。</param>
    /// <param name="top">上。</param>
    /// <param name="right">右。</param>
    /// <param name="bottom">下。</param>
    public EmExtension(double left, double top, double right, double bottom) => factors = [left, top, right, bottom];

    /// <summary>取谁的字号：默认是元素自己（Self）；模板里的部件写 TemplatedParent，取组件根的字号。</summary>
    public RelativeSourceMode Of { get; set; } = RelativeSourceMode.Self;

    /// <summary>XAML 调用：返回绑定到字号的绑定。</summary>
    /// <param name="serviceProvider">XAML 服务（不使用）。</param>
    public object ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBinding("FontSize")
        {
            RelativeSource = new RelativeSource(Of),
            Converter = EmConverter.Instance,
            ConverterParameter = factors,
        };
}

/// <summary>字号 × 系数，按目标属性的类型产出 double、Thickness 或 CornerRadius。</summary>
sealed class EmConverter : IValueConverter
{
    public static readonly EmConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double size || parameter is not double[] f) return BindingOperations.DoNothing;
        if (targetType == typeof(Thickness))
            return f.Length switch
            {
                1 => new Thickness(f[0] * size),
                2 => new Thickness(f[0] * size, f[1] * size),
                _ => new Thickness(f[0] * size, f[1] * size, f[2] * size, f[3] * size),
            };
        if (targetType == typeof(CornerRadius))
            return f.Length == 4 ? new CornerRadius(f[0] * size, f[1] * size, f[2] * size, f[3] * size) : new CornerRadius(f[0] * size);
        return f[0] * size;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("em 尺寸是单向的。");
}
