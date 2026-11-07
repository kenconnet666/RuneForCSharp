using System.ComponentModel;

namespace Rune.Avalonia;

/// <summary>输入框外壳的样式（vue-tsx-pro 的 ControlVariants）：TextField、Textarea、Select 等输入框共用。</summary>
/// <param name="Value">取值（surface、soft）。</param>
[TypeConverter(typeof(VariantConverter<ControlVariant>))]
public readonly record struct ControlVariant(string Value) : IVariant<ControlVariant>
{
    /// <summary>表面色加描边（默认）。</summary>
    public static readonly ControlVariant Surface = new("surface");

    /// <summary>浅色底，没有描边：表单很密、或放在有底色的区域里。</summary>
    public static readonly ControlVariant Soft = new("soft");

    /// <inheritdoc />
    public static IReadOnlyList<ControlVariant> Known { get; } = [Surface, Soft];

    /// <inheritdoc />
    public static ControlVariant Parse(string value)
    {
        var variant = new ControlVariant(value);
        Variants.WarnIfUnknown(variant);
        return variant;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
