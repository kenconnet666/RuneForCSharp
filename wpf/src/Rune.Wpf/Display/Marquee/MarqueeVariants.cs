using System.ComponentModel;

namespace Rune.Wpf;

/// <summary>跑马灯的滚动方向（vue-tsx-pro 的 MarqueeDirections）：left（默认）、right、up、down。</summary>
/// <param name="Value">取值。</param>
[TypeConverter(typeof(VariantConverter<MarqueeDirection>))]
public readonly record struct MarqueeDirection(string Value) : IVariant<MarqueeDirection>
{
    /// <summary>向左（默认）。</summary>
    public static readonly MarqueeDirection Left = new("left");

    /// <summary>向右。</summary>
    public static readonly MarqueeDirection Right = new("right");

    /// <summary>向上。</summary>
    public static readonly MarqueeDirection Up = new("up");

    /// <summary>向下。</summary>
    public static readonly MarqueeDirection Down = new("down");

    /// <inheritdoc />
    public static IReadOnlyList<MarqueeDirection> Known { get; } = [Left, Right, Up, Down];

    /// <summary>竖着滚（up、down）。</summary>
    public bool IsVertical => Value is "up" or "down";

    /// <summary>倒着走（right、down）：同上游的 animation-direction: reverse。</summary>
    public bool IsReverse => Value is "right" or "down";

    /// <inheritdoc />
    public static MarqueeDirection Parse(string value)
    {
        var direction = new MarqueeDirection(value);
        Variants.WarnIfUnknown(direction);
        return direction;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// 跑马灯的位移（同上游的 CSS 动画）：内容复制一份接在后面，第一份从 0 平移到负的一份长度（含它后面的间距）时正好和开头重合，无缝循环。
/// </summary>
static class MarqueeMotion
{
    /// <summary>默认速度：每秒 40 像素。</summary>
    public const double Speed = 40;

    /// <summary>走了 <paramref name="distance"/> 像素后第一份内容的位移（-length 到 0）：正着走越走越负，倒着走从 -length 往 0 走。</summary>
    /// <param name="distance">一共走过的距离（暂停时不累加）。</param>
    /// <param name="length">一份内容（含后面的间距）的长度；没有内容时为 0。</param>
    /// <param name="reverse">倒着走（right、down）。</param>
    public static double Offset(double distance, double length, bool reverse)
    {
        if (length <= 0) return 0;
        var phase = distance % length;
        return reverse ? phase - length : -phase;
    }
}
