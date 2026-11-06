using System.Windows;

namespace Rune.Wpf;

/// <summary>
/// 控件高度（vue-tsx-pro 的 <c>sizes.control</c> 与 <c>fontSizeStyles</c>）：<c>max(round(系数 × 字号 ÷ 4) × 4, 24)</c>，
/// 落在 4px 网格上，xs–xl 依次 24 / 28 / 32 / 36 / 40。系数是继承的：写了字号档位的组件设它（xs 用 2，其余档 2.25），
/// 里面没写档位的控件跟着外面，同 TS 里 CSS 变量的继承。主题样式里打开（控件要先 <see cref="EmSize.Register"/>）：
/// <code>
/// &lt;Setter Property="rune:ControlHeight.MinHeight" Value="True" /&gt;   &lt;!-- 图标按钮这类方形控件另设 Width --&gt;
/// </code>
/// 同 EmSize，只在基础值来自库时才算，使用方的样式与本地值照常生效。常量与 token 由测试核对。
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
    public static readonly DependencyProperty FactorProperty = DependencyProperty.RegisterAttached(
        "Factor", typeof(double), typeof(ControlHeight), new FrameworkPropertyMetadata(DefaultFactor, FrameworkPropertyMetadataOptions.Inherits, (d, _) => EmSize.Refresh(d)));

    /// <summary>最小高度取控件高度。</summary>
    public static readonly DependencyProperty MinHeightProperty = DependencyProperty.RegisterAttached(
        "MinHeight", typeof(bool), typeof(ControlHeight), new PropertyMetadata(false, (d, _) => d.CoerceValue(FrameworkElement.MinHeightProperty)));

    /// <summary>宽度取控件高度（图标按钮这类方形控件）。</summary>
    public static readonly DependencyProperty WidthProperty = DependencyProperty.RegisterAttached(
        "Width", typeof(bool), typeof(ControlHeight), new PropertyMetadata(false, (d, _) => d.CoerceValue(FrameworkElement.WidthProperty)));

    /// <summary>读系数。</summary>
    public static double GetFactor(DependencyObject d) => (double)d.GetValue(FactorProperty);

    /// <summary>写系数。</summary>
    public static void SetFactor(DependencyObject d, double value) => d.SetValue(FactorProperty, value);

    /// <summary>读"最小高度取控件高度"。</summary>
    public static bool GetMinHeight(DependencyObject d) => (bool)d.GetValue(MinHeightProperty);

    /// <summary>写"最小高度取控件高度"。</summary>
    public static void SetMinHeight(DependencyObject d, bool value) => d.SetValue(MinHeightProperty, value);

    /// <summary>读"宽度取控件高度"。</summary>
    public static bool GetWidth(DependencyObject d) => (bool)d.GetValue(WidthProperty);

    /// <summary>写"宽度取控件高度"。</summary>
    public static void SetWidth(DependencyObject d, bool value) => d.SetValue(WidthProperty, value);

    /// <summary>按字号与系数算高度；正好一半时往大取（同 CSS 的 round(nearest)）。</summary>
    /// <param name="fontSize">字号。</param>
    /// <param name="factor">系数。</param>
    public static double Of(double fontSize, double factor) => Math.Max(Math.Floor(factor * fontSize / Step + 0.5) * Step, MinTarget);

    /// <summary>组件的字号档位变了：档位设系数，像素数或没写时跟着外面。</summary>
    internal static void Apply(DependencyObject d, TextSize size)
    {
        if (size.ResourceKey is null) d.ClearValue(FactorProperty);
        else d.SetValue(FactorProperty, size == TextSize.Xs ? XsFactor : DefaultFactor);
    }
}
