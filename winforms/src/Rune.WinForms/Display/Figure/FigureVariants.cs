using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>Figure 的说明放在内容的哪一边（vue-tsx-pro 的 FigureCaptionSides）。</summary>
/// <param name="Value">取值（bottom、top）。</param>
[TypeConverter(typeof(VariantConverter<FigureCaptionSide>))]
public readonly record struct FigureCaptionSide(string Value) : IVariant<FigureCaptionSide>
{
    /// <summary>下面（默认）：图片、图表。</summary>
    public static readonly FigureCaptionSide Bottom = new("bottom");

    /// <summary>上面：表格。</summary>
    public static readonly FigureCaptionSide Top = new("top");

    /// <inheritdoc />
    public static IReadOnlyList<FigureCaptionSide> Known { get; } = [Bottom, Top];

    /// <summary>说明在上面。</summary>
    public bool IsTop => Value == "top";

    /// <inheritdoc />
    public static FigureCaptionSide Parse(string value)
    {
        var side = new FigureCaptionSide(value);
        Variants.WarnIfUnknown(side);
        return side;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Figure 的内容与说明的对齐（vue-tsx-pro 的 FigureAligns）。</summary>
/// <param name="Value">取值（start、center、end）。</param>
[TypeConverter(typeof(VariantConverter<FigureAlign>))]
public readonly record struct FigureAlign(string Value) : IVariant<FigureAlign>
{
    /// <summary>靠起始边（默认），内容拉满宽度。</summary>
    public static readonly FigureAlign Start = new("start");

    /// <summary>居中。</summary>
    public static readonly FigureAlign Center = new("center");

    /// <summary>靠结束边。</summary>
    public static readonly FigureAlign End = new("end");

    /// <inheritdoc />
    public static IReadOnlyList<FigureAlign> Known { get; } = [Start, Center, End];

    /// <inheritdoc />
    public static FigureAlign Parse(string value)
    {
        var align = new FigureAlign(value);
        Variants.WarnIfUnknown(align);
        return align;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
