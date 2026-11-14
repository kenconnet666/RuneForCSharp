using System.ComponentModel;
using System.Globalization;

namespace Rune.Avalonia;

/// <summary>比例条横条的粗细（vue-tsx-pro 的 size：sm、md）。</summary>
/// <param name="Value">取值（sm、md）。</param>
[TypeConverter(typeof(VariantConverter<ProportionBarSize>))]
public readonly record struct ProportionBarSize(string Value) : IVariant<ProportionBarSize>
{
    /// <summary>细：0.5rem（8 像素）。</summary>
    public static readonly ProportionBarSize Sm = new("sm");

    /// <summary>默认：0.75rem（12 像素）。</summary>
    public static readonly ProportionBarSize Md = new("md");

    /// <inheritdoc />
    public static IReadOnlyList<ProportionBarSize> Known { get; } = [Sm, Md];

    /// <summary>横条的高度（像素；rem 按浏览器的根字号 16 算）。</summary>
    internal double Height => Value == "sm" ? 8 : 12;

    /// <inheritdoc />
    public static ProportionBarSize Parse(string value)
    {
        var size = new ProportionBarSize(value);
        Variants.WarnIfUnknown(size);
        return size;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>比例条的计算（三个库各有一份，除命名空间外逐字相同，tools/twins）。</summary>
static class Proportions
{
    /// <summary>没写色调的段按顺序取的色调（TS 的 ORDER）。</summary>
    static readonly StatusTone[] Order = [StatusTone.Accent, StatusTone.Info, StatusTone.Success, StatusTone.Warning, StatusTone.Danger, StatusTone.Neutral];

    /// <summary>第 <paramref name="index"/> 段的色调：写了的，否则按顺序取。</summary>
    public static StatusTone ToneAt(StatusTone? tone, int index) => tone is { Value: not null } own ? own : Order[index % Order.Length];

    /// <summary>横条给读屏的名称：名称后面带上各段的数量（"工单池：已确认 25，暂缓 0，待定 35"）。</summary>
    public static string Label(string? label, IEnumerable<(string Label, double Value)> segments) =>
        $"{label}：{string.Join("，", segments.Select(segment => $"{segment.Label} {segment.Value.ToString(CultureInfo.InvariantCulture)}"))}";
}
