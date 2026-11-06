using global::Avalonia;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Data;
using global::Avalonia.Data.Converters;
using global::Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using System.Globalization;

namespace Rune.Avalonia;

/// <summary>
/// 控件高度（vue-tsx-pro 的 <c>sizes.control</c> 与 <c>fontSizeStyles</c>）：<c>max(round(系数 × 字号 ÷ 4) × 4, 24)</c>，
/// 落在 4px 网格上，xs–xl 依次 24 / 28 / 32 / 36 / 40。系数是继承的：写了字号档位的组件设它（xs 用 2，其余档 2.25），
/// 里面没写档位的控件跟着外面，同 TS 里 CSS 变量的继承。
/// <code>
/// &lt;Setter Property="MinHeight" Value="{rune:ControlHeight}" /&gt;
/// </code>
/// 常量与 token（<c>Rune.Size.Control</c>、<c>Rune.Size.ControlRound</c>、<c>Rune.Size.MinTarget</c>）由测试核对。
/// </summary>
public static class ControlHeight
{
    /// <summary>默认系数：<c>sizes.control</c> 的 2.25em。</summary>
    public const double DefaultFactor = 2.25;

    /// <summary>字号档位 xs 的系数（12px → 24px，不与 sm 的 28px 同高）。</summary>
    public const double XsFactor = 2;

    /// <summary>取整的步长：4px 网格。</summary>
    public const double Step = 4;

    /// <summary>下限：可点击区域的最小尺寸（WCAG 2.2 目标尺寸 24×24）。</summary>
    public const double MinTarget = 24;

    /// <summary>控件高度的系数，继承给里面的元素。</summary>
    public static readonly AttachedProperty<double> FactorProperty =
        AvaloniaProperty.RegisterAttached<StyledElement, double>("Factor", typeof(ControlHeight), DefaultFactor, inherits: true);

    /// <summary>读系数。</summary>
    public static double GetFactor(StyledElement element) => element.GetValue(FactorProperty);

    /// <summary>写系数。</summary>
    public static void SetFactor(StyledElement element, double value) => element.SetValue(FactorProperty, value);

    /// <summary>按字号与系数算高度；正好一半时往大取（同 CSS 的 round(nearest)）。</summary>
    /// <param name="fontSize">字号。</param>
    /// <param name="factor">系数。</param>
    public static double Of(double fontSize, double factor) => Math.Max(Math.Floor(factor * fontSize / Step + 0.5) * Step, MinTarget);

    /// <summary>组件的字号档位变了：档位设系数，像素数或没写时跟着外面。</summary>
    internal static void Apply(StyledElement element, TextSize size)
    {
        if (size.ResourceKey is null) element.ClearValue(FactorProperty);
        else element.SetValue(FactorProperty, size == TextSize.Xs ? XsFactor : DefaultFactor);
    }

    /// <summary>绑定到元素自己的字号与系数，算出高度。</summary>
    internal static BindingBase Binding() => new MultiBinding
    {
        Bindings = { Self(TemplatedControl.FontSizeProperty), Self(FactorProperty) },
        Converter = HeightConverter.Instance,
    };

    static CompiledBinding Self(AvaloniaProperty property) =>
        new(new CompiledBindingPathBuilder().Self().Property(property, PropertyInfoAccessorFactory.CreateAvaloniaPropertyAccessor).Build());

    sealed class HeightConverter : IMultiValueConverter
    {
        public static readonly HeightConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
            values is [double size, double factor] ? Of(size, factor) : BindingOperations.DoNothing;
    }
}

/// <summary>XAML：<c>{rune:ControlHeight}</c>，控件高度（<see cref="ControlHeight"/>）的绑定，写在主题的 MinHeight、Width 上。</summary>
public sealed class ControlHeightExtension
{
    /// <summary>XAML 调用：返回绑定。</summary>
    /// <param name="serviceProvider">XAML 服务（不使用）。</param>
    public object ProvideValue(IServiceProvider serviceProvider) => ControlHeight.Binding();
}
