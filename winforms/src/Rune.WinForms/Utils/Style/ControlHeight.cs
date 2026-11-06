namespace Rune.WinForms;

/// <summary>
/// 控件高度（vue-tsx-pro 的 <c>sizes.control</c> 与 <c>fontSizeStyles</c>）：<c>max(round(系数 × 字号 ÷ 4) × 4, 24)</c>，
/// 落在 4px 网格上（按 DPI 换算），xs–xl 依次 24 / 28 / 32 / 36 / 40。字号档位 xs 的系数是 2，其余档取 token 的 2.25。
/// WinForms 的元素没有继承的作用域，系数按组件自己的档位取。
/// </summary>
public static class ControlHeight
{
    /// <summary>字号档位 xs 的系数（12px → 24px，不与 sm 的 28px 同高）。</summary>
    public const float XsFactor = 2;

    /// <summary>字号档位的系数。</summary>
    /// <param name="size">字号档位。</param>
    public static float Factor(TextSize size) => size == TextSize.Xs ? XsFactor : Tokens.Size.Control;

    /// <summary>控件高度（像素）；正好一半时往大取（同 CSS 的 round(nearest)）。</summary>
    /// <param name="context">元素的环境（DPI）。</param>
    /// <param name="em">字号（像素）。</param>
    /// <param name="factor">系数。</param>
    public static float Of(ElementContext context, float em, float factor)
    {
        var step = context.Scale(Tokens.Size.ControlRound);
        return MathF.Max(MathF.Floor(factor * em / step + 0.5f) * step, context.Scale(Tokens.Size.MinTarget));
    }
}
