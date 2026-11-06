using System.Windows;

namespace Rune.Wpf;

/// <summary>
/// 行高系数（CSS 不带单位的 line-height，vue-tsx-pro 的 lineHeights）：继承给里面的元素，文字按自己的字号乘上它，
/// 所以 12px 的 Small 放在 14px 的段落里行高是 18 而不是 21。WPF 的 TextBlock.LineHeight 是像素数、按像素继承，
/// 由 <see cref="Text"/> 换算（属性强制，基础值来自继承或默认值时才算）；正文默认 1.5（lineHeights.normal），
/// 按钮、标记这类控件设 1.25（tight）。常量与 token 由测试核对。
/// </summary>
public static class Leading
{
    /// <summary>正文的行高系数：lineHeights.normal。</summary>
    public const double Normal = 1.5;

    /// <summary>行高系数，继承给里面的元素。</summary>
    public static readonly DependencyProperty FactorProperty = DependencyProperty.RegisterAttached(
        "Factor", typeof(double), typeof(Leading), new FrameworkPropertyMetadata(Normal, FrameworkPropertyMetadataOptions.Inherits, (d, _) => (d as Text)?.CoerceValue(System.Windows.Controls.TextBlock.LineHeightProperty)));

    /// <summary>读系数。</summary>
    public static double GetFactor(DependencyObject d) => (double)d.GetValue(FactorProperty);

    /// <summary>写系数。</summary>
    public static void SetFactor(DependencyObject d, double value) => d.SetValue(FactorProperty, value);
}
