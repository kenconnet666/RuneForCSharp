using global::Avalonia;

namespace Rune.Avalonia;

/// <summary>
/// 行高系数（CSS 不带单位的 line-height，vue-tsx-pro 的 lineHeights）：继承给里面的元素，文字按自己的字号乘上它，
/// 所以 12px 的 Small 放在 14px 的段落里行高是 18 而不是 21。Avalonia 的 TextBlock.LineHeight 是像素数、按像素继承，
/// 由 <see cref="Text"/> 换算；正文默认 1.5（lineHeights.normal），按钮、标记这类控件设 1.25（tight）。常量与 token 由测试核对。
/// </summary>
public static class Leading
{
    /// <summary>正文的行高系数：lineHeights.normal。</summary>
    public const double Normal = 1.5;

    /// <summary>行高系数，继承给里面的元素。</summary>
    public static readonly AttachedProperty<double> FactorProperty =
        AvaloniaProperty.RegisterAttached<StyledElement, double>("Factor", typeof(Leading), Normal, inherits: true);

    /// <summary>读系数。</summary>
    public static double GetFactor(StyledElement element) => element.GetValue(FactorProperty);

    /// <summary>写系数。</summary>
    public static void SetFactor(StyledElement element, double value) => element.SetValue(FactorProperty, value);
}
