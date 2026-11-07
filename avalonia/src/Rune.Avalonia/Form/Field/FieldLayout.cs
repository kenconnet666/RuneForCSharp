using System.ComponentModel;

namespace Rune.Avalonia;

// 字段布局的档位（上游 form/field-layout.ts）。Form、FormSection 统一设置布局时也用它们

/// <summary>标签放在哪（vue-tsx-pro 的 FieldLabelPlacements）。</summary>
/// <param name="Value">取值（top、start）。</param>
[TypeConverter(typeof(VariantConverter<FieldLabelPlacement>))]
public readonly record struct FieldLabelPlacement(string Value) : IVariant<FieldLabelPlacement>
{
    /// <summary>控件上方（默认）：窄容器、标签很长时。</summary>
    public static readonly FieldLabelPlacement Top = new("top");

    /// <summary>控件的起始边：宽屏的长表单，标签一列对齐、省纵向空间。</summary>
    public static readonly FieldLabelPlacement Start = new("start");

    /// <inheritdoc />
    public static IReadOnlyList<FieldLabelPlacement> Known { get; } = [Top, Start];

    /// <inheritdoc />
    public static FieldLabelPlacement Parse(string value)
    {
        var placement = new FieldLabelPlacement(value);
        Variants.WarnIfUnknown(placement);
        return placement;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>标签在起始边时文字的对齐（vue-tsx-pro 的 FieldLabelAligns）。</summary>
/// <param name="Value">取值（start、end）。</param>
[TypeConverter(typeof(VariantConverter<FieldLabelAlign>))]
public readonly record struct FieldLabelAlign(string Value) : IVariant<FieldLabelAlign>
{
    /// <summary>靠起始边。</summary>
    public static readonly FieldLabelAlign Start = new("start");

    /// <summary>靠结束边、贴着控件（默认）：标签长短不一时，标签与控件之间的距离一致。</summary>
    public static readonly FieldLabelAlign End = new("end");

    /// <inheritdoc />
    public static IReadOnlyList<FieldLabelAlign> Known { get; } = [Start, End];

    /// <inheritdoc />
    public static FieldLabelAlign Parse(string value)
    {
        var align = new FieldLabelAlign(value);
        Variants.WarnIfUnknown(align);
        return align;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
