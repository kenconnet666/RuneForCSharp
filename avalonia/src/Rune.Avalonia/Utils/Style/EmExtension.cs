using System.Globalization;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace Rune.Avalonia;

/// <summary>
/// em 尺寸：值 = 系数 × 字号，字号变了跟着变（对应 vue-tsx-pro 里组件内部一律用 em）。
/// 组件只在根元素上设字号（size 档位或继承上下文），内边距、圆角、间距等写成 em，整体等比缩放。
/// <code>
/// &lt;Setter Property="Padding" Value="{rune:Em 1, 0.25}" /&gt;                        &lt;!-- 水平 1em、垂直 0.25em --&gt;
/// &lt;Setter Property="MinHeight" Value="{rune:Em 2.25, Min=24}" /&gt;                 &lt;!-- max(2.25em, 24px) --&gt;
/// &lt;Setter Property="CornerRadius" Value="{rune:Em Resource=Rune.Radius.Md}" /&gt;   &lt;!-- 系数取主题资源（随主题变） --&gt;
/// &lt;Border CornerRadius="{rune:Em 0.4, Of=TemplatedParent}" /&gt;                   &lt;!-- 模板里取组件根的字号 --&gt;
/// </code>
/// 返回绑定到字号的绑定：写在 ControlTheme 的 Setter 里时优先级随 Setter，使用方的样式与本地值照常盖过它。
/// 比写死数值慢一些（3000 个按钮的创建约多 27%），大量重复的轻量元素（表格单元格）改在代码里按字号算。
/// 按需要选最便宜的绑定：模板里取组件根的字号用 TemplateBinding，取自己的字号用单个绑定，只有系数来自资源时才用多重绑定。
/// </summary>
public sealed class EmExtension
{
    readonly double[] factors;

    /// <summary>系数从 <see cref="Resource"/> 取。</summary>
    public EmExtension() => factors = [1];

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

    /// <summary>系数的资源键（随主题变的 em 系数，如 <c>Rune.Radius.Md</c>）；写了就不用位置参数。</summary>
    public string? Resource { get; set; }

    /// <summary>下限（设备无关像素）：CSS 的 <c>max(2.25em, 24px)</c>。</summary>
    public double Min { get; set; }

    /// <summary>XAML 调用：返回绑定到字号的绑定（Resource 写了时是字号与资源的多重绑定）。</summary>
    /// <param name="serviceProvider">XAML 服务（不使用）。</param>
    public object ProvideValue(IServiceProvider serviceProvider)
    {
        var converter = new EmConverter(Resource is null ? factors : null, Min);
        if (Resource is not null)
            return new MultiBinding { Bindings = { FontSize(), new DynamicResourceExtension(Resource) }, Converter = converter };
        return Of == RelativeSourceMode.TemplatedParent
            ? new TemplateBinding(TemplatedControl.FontSizeProperty) { Converter = converter }
            : new ReflectionBinding("FontSize") { RelativeSource = new RelativeSource(Of), Converter = converter };
    }

    ReflectionBinding FontSize() => new("FontSize") { RelativeSource = new RelativeSource(Of) };
}

/// <summary>字号（与资源里的系数）× 系数，按目标属性的类型产出 double、Thickness 或 CornerRadius，不低于下限。</summary>
/// <param name="factors">系数；为 null 时取第二个绑定值（资源里的系数）。</param>
/// <param name="min">下限（设备无关像素）。</param>
sealed class EmConverter(double[]? factors, double min) : IMultiValueConverter, IValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        values[0] is double size && (factors ?? (values.Count > 1 && values[1] is double resource ? [resource] : null)) is { } f
            ? Scale(size, f, targetType)
            : BindingOperations.DoNothing;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double size && factors is { } f ? Scale(size, f, targetType) : BindingOperations.DoNothing;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;

    object Scale(double size, double[] f, Type targetType)
    {
        double At(int i) => Math.Max(min, f[Math.Min(i, f.Length - 1)] * size);
        if (targetType == typeof(Thickness))
            return f.Length switch
            {
                1 => new Thickness(At(0)),
                2 => new Thickness(At(0), At(1)),
                _ => new Thickness(At(0), At(1), At(2), At(3)),
            };
        if (targetType == typeof(CornerRadius))
            return f.Length == 4 ? new CornerRadius(At(0), At(1), At(2), At(3)) : new CornerRadius(At(0));
        return At(0);
    }
}
