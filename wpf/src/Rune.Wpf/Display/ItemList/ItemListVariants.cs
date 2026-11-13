using System.ComponentModel;

namespace Rune.Wpf;

/// <summary>条目列表的疏密（vue-tsx-pro 的 ItemListDensities）：条目的上下内边距。</summary>
/// <param name="Value">取值（compact、standard）。</param>
[TypeConverter(typeof(VariantConverter<ItemListDensity>))]
public readonly record struct ItemListDensity(string Value) : IVariant<ItemListDensity>
{
    /// <summary>紧凑：上下 space.xs，卡片里的短列表。</summary>
    public static readonly ItemListDensity Compact = new("compact");

    /// <summary>标准（默认）：上下 space.sm，弹出层、页面里的列表。</summary>
    public static readonly ItemListDensity Standard = new("standard");

    /// <inheritdoc />
    public static IReadOnlyList<ItemListDensity> Known { get; } = [Compact, Standard];

    /// <inheritdoc />
    public static ItemListDensity Parse(string value)
    {
        var density = new ItemListDensity(value);
        Variants.WarnIfUnknown(density);
        return density;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>条目的起始边、结束边与内容的对齐（vue-tsx-pro 的 ItemListItem align）。</summary>
/// <param name="Value">取值（center、start）。</param>
[TypeConverter(typeof(VariantConverter<ItemListAlign>))]
public readonly record struct ItemListAlign(string Value) : IVariant<ItemListAlign>
{
    /// <summary>垂直居中（默认）。</summary>
    public static readonly ItemListAlign Center = new("center");

    /// <summary>贴着第一行：多行内容、色点对齐标题。</summary>
    public static readonly ItemListAlign Start = new("start");

    /// <inheritdoc />
    public static IReadOnlyList<ItemListAlign> Known { get; } = [Center, Start];

    /// <inheritdoc />
    public static ItemListAlign Parse(string value)
    {
        var align = new ItemListAlign(value);
        Variants.WarnIfUnknown(align);
        return align;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
