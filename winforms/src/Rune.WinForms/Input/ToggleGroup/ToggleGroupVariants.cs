using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>切换按钮组的排列方向（vue-tsx-pro 的 ToggleGroupOrientations）。</summary>
/// <param name="Value">取值（horizontal、vertical）。</param>
[TypeConverter(typeof(VariantConverter<ToggleGroupOrientation>))]
public readonly record struct ToggleGroupOrientation(string Value) : IVariant<ToggleGroupOrientation>
{
    /// <summary>横排（默认），左右键移动。</summary>
    public static readonly ToggleGroupOrientation Horizontal = new("horizontal");

    /// <summary>竖排，上下键移动。</summary>
    public static readonly ToggleGroupOrientation Vertical = new("vertical");

    /// <inheritdoc />
    public static IReadOnlyList<ToggleGroupOrientation> Known { get; } = [Horizontal, Vertical];

    /// <inheritdoc />
    public static ToggleGroupOrientation Parse(string value)
    {
        var orientation = new ToggleGroupOrientation(value);
        Variants.WarnIfUnknown(orientation);
        return orientation;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
