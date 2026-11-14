using System.ComponentModel;

namespace Rune.Avalonia;

/// <summary>滑块的方向（vue-tsx-pro 的 SliderOrientations）。</summary>
/// <param name="Value">取值（horizontal、vertical）。</param>
[TypeConverter(typeof(VariantConverter<SliderOrientation>))]
public readonly record struct SliderOrientation(string Value) : IVariant<SliderOrientation>
{
    /// <summary>横向（默认）：起始端是最小值。</summary>
    public static readonly SliderOrientation Horizontal = new("horizontal");

    /// <summary>纵向：底端是最小值；高度由使用方给，默认 10em。</summary>
    public static readonly SliderOrientation Vertical = new("vertical");

    /// <inheritdoc />
    public static IReadOnlyList<SliderOrientation> Known { get; } = [Horizontal, Vertical];

    /// <inheritdoc />
    public static SliderOrientation Parse(string value)
    {
        var orientation = new SliderOrientation(value);
        Variants.WarnIfUnknown(orientation);
        return orientation;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>轨道上的一个刻度（vue-tsx-pro 的 SliderMark）。</summary>
/// <param name="Value">刻度所在的值。</param>
/// <param name="Label">刻度下方（纵向时右侧）的文字；没有时只有一个点。</param>
public readonly record struct SliderMark(double Value, string? Label = null)
{
    /// <summary>只有点的刻度：<c>Marks = [0, 50, 100]</c>。</summary>
    public static implicit operator SliderMark(double value) => new(value);
}

/// <summary>范围选择的取值（vue-tsx-pro 的 [低, 高]）。</summary>
/// <param name="Low">低的一端。</param>
/// <param name="High">高的一端。</param>
public readonly record struct SliderRange(double Low, double High)
{
    /// <inheritdoc />
    public override string ToString() => $"{Low}–{High}";
}

/// <summary>滑块认的按键（平台的键先按方向与书写方向换成它）。</summary>
enum SliderKey
{
    /// <summary>+step：横向的结束边方向键、↑。</summary>
    Increase,

    /// <summary>-step：横向的起始边方向键、↓。</summary>
    Decrease,

    /// <summary>+bigStep。</summary>
    PageUp,

    /// <summary>-bigStep。</summary>
    PageDown,

    /// <summary>到能取的最小值。</summary>
    Home,

    /// <summary>到能取的最大值。</summary>
    End,
}

/// <summary>
/// 滑块的取值计算（vue-tsx-pro 的 Slider 里的 snap、bounds、nearest、键盘）：对齐步长、两个滑块互相限制、点击轨道时移动哪个。
/// 对齐用 decimal 算（同 TS 的 toFixed）：0.1 + 0.2 不会变成 0.30000000000000004。三个平台各有一份逐字相同的副本。
/// </summary>
static class SliderScale
{
    /// <summary>对齐到 min + n × step 并夹在 min–max 里。</summary>
    public static double Snap(double raw, double step, double min, double max) =>
        (double)NumberRules.Snap((decimal)Math.Clamp(raw, min, max), (decimal)step, (decimal)min, (decimal)max);

    /// <summary>值在 min–max 里的位置（0–1）；超出范围的按范围内算，min 不小于 max 时是 0。</summary>
    public static double Ratio(double value, double min, double max) => max > min ? (Math.Clamp(value, min, max) - min) / (max - min) : 0;

    /// <summary>轨道上的位置（0–1）对应的值，对齐步长。</summary>
    public static double ValueAt(double ratio, double min, double max, double step) => Snap(min + Math.Clamp(ratio, 0, 1) * (max - min), step, min, max);

    /// <summary>第 <paramref name="index"/> 个滑块能取的范围：范围选择时不能越过另一个。</summary>
    public static (double Lower, double Upper) Bounds(int index, IReadOnlyList<double> values, double min, double max) =>
        values.Count < 2 ? (min, max) : index == 0 ? (min, values[1]) : (values[0], max);

    /// <summary>离 <paramref name="target"/> 最近的滑块；两个重合时按方向选（往高处去的拿高的那个）。</summary>
    public static int Nearest(double target, IReadOnlyList<double> values)
    {
        if (values.Count < 2) return 0;
        var (low, high) = (values[0], values[1]);
        if (low == high) return target > high ? 1 : 0;
        return Math.Abs(target - low) <= Math.Abs(target - high) ? 0 : 1;
    }

    /// <summary>Page Up / Page Down 的步长：不写时为范围的十分之一（至少一个 step）。</summary>
    public static double BigStep(double? bigStep, double step, double min, double max) => bigStep ?? Math.Max(step, (max - min) / 10);

    /// <summary>按键后的值（还没夹进 <paramref name="lower"/>–<paramref name="upper"/>、没对齐）。</summary>
    public static double Target(SliderKey key, double current, double step, double big, double lower, double upper) => key switch
    {
        SliderKey.Increase => current + step,
        SliderKey.Decrease => current - step,
        SliderKey.PageUp => current + big,
        SliderKey.PageDown => current - big,
        SliderKey.Home => lower,
        _ => upper,
    };

    /// <summary>
    /// 改第 <paramref name="index"/> 个滑块：对齐步长，再夹在它能取的范围里（不越过另一个）。返回全部滑块的新值。
    /// </summary>
    public static double[] Set(IReadOnlyList<double> values, int index, double raw, double min, double max, double step)
    {
        var (lower, upper) = Bounds(index, values, min, max);
        var next = values.ToArray();
        next[index] = Math.Clamp(Snap(raw, step, min, max), lower, upper);
        return next;
    }
}
