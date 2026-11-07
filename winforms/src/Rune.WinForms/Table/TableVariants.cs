using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>表格的密度（vue-tsx-pro 的 DataTableDensities）：单元格的上下内边距；紧凑时字号也小一档。</summary>
/// <param name="Value">取值（compact、standard、comfortable）。</param>
[TypeConverter(typeof(VariantConverter<TableDensity>))]
public readonly record struct TableDensity(string Value) : IVariant<TableDensity>
{
    /// <summary>紧凑：上下 space.xs，字号 sm。</summary>
    public static readonly TableDensity Compact = new("compact");

    /// <summary>标准（默认）：上下 space.sm。</summary>
    public static readonly TableDensity Standard = new("standard");

    /// <summary>宽松：上下 space.md。</summary>
    public static readonly TableDensity Comfortable = new("comfortable");

    /// <inheritdoc />
    public static IReadOnlyList<TableDensity> Known { get; } = [Compact, Standard, Comfortable];

    /// <inheritdoc />
    public static TableDensity Parse(string value)
    {
        var density = new TableDensity(value);
        Variants.WarnIfUnknown(density);
        return density;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>表格的样式（vue-tsx-pro 的 DataTableVariants）。</summary>
/// <param name="Value">取值（surface、ghost）。</param>
[TypeConverter(typeof(VariantConverter<TableVariant>))]
public readonly record struct TableVariant(string Value) : IVariant<TableVariant>
{
    /// <summary>表面（默认）：border 边框、lg 圆角、surface 底。</summary>
    public static readonly TableVariant Surface = new("surface");

    /// <summary>无外框：页面底色。</summary>
    public static readonly TableVariant Ghost = new("ghost");

    /// <inheritdoc />
    public static IReadOnlyList<TableVariant> Known { get; } = [Surface, Ghost];

    /// <inheritdoc />
    public static TableVariant Parse(string value)
    {
        var variant = new TableVariant(value);
        Variants.WarnIfUnknown(variant);
        return variant;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>单元格的对齐（vue-tsx-pro 的 TableAligns）。</summary>
/// <param name="Value">取值（start、center、end）。</param>
[TypeConverter(typeof(VariantConverter<TableAlign>))]
public readonly record struct TableAlign(string Value) : IVariant<TableAlign>
{
    /// <summary>起始边（默认）。</summary>
    public static readonly TableAlign Start = new("start");

    /// <summary>居中。</summary>
    public static readonly TableAlign Center = new("center");

    /// <summary>结束边：数字、金额。</summary>
    public static readonly TableAlign End = new("end");

    /// <inheritdoc />
    public static IReadOnlyList<TableAlign> Known { get; } = [Start, Center, End];

    /// <inheritdoc />
    public static TableAlign Parse(string value)
    {
        var align = new TableAlign(value);
        Variants.WarnIfUnknown(align);
        return align;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
