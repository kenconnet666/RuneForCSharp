using System.ComponentModel;

namespace Rune.Wpf;

/// <summary>滚动区域可以滚动的方向（vue-tsx-pro 的 ScrollAreaAxes）。</summary>
/// <param name="Value">取值（y、x、both）。</param>
[TypeConverter(typeof(VariantConverter<ScrollAreaAxis>))]
public readonly record struct ScrollAreaAxis(string Value) : IVariant<ScrollAreaAxis>
{
    /// <summary>纵向（默认），横向超出的部分裁掉。</summary>
    public static readonly ScrollAreaAxis Y = new("y");

    /// <summary>横向，纵向超出的部分裁掉。</summary>
    public static readonly ScrollAreaAxis X = new("x");

    /// <summary>两个方向。</summary>
    public static readonly ScrollAreaAxis Both = new("both");

    /// <inheritdoc />
    public static IReadOnlyList<ScrollAreaAxis> Known { get; } = [Y, X, Both];

    /// <inheritdoc />
    public static ScrollAreaAxis Parse(string value)
    {
        var axis = new ScrollAreaAxis(value);
        Variants.WarnIfUnknown(axis);
        return axis;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>滚动条什么时候显示（vue-tsx-pro 的 ScrollbarTypes）。</summary>
/// <param name="Value">取值（hover、scroll、always）。</param>
[TypeConverter(typeof(VariantConverter<ScrollbarType>))]
public readonly record struct ScrollbarType(string Value) : IVariant<ScrollbarType>
{
    /// <summary>指针在区域上方或正在滚动时显示（默认）。</summary>
    public static readonly ScrollbarType Hover = new("hover");

    /// <summary>只在滚动时显示，停下后淡出。</summary>
    public static readonly ScrollbarType Scroll = new("scroll");

    /// <summary>内容超出时一直显示。</summary>
    public static readonly ScrollbarType Always = new("always");

    /// <inheritdoc />
    public static IReadOnlyList<ScrollbarType> Known { get; } = [Hover, Scroll, Always];

    /// <inheritdoc />
    public static ScrollbarType Parse(string value)
    {
        var type = new ScrollbarType(value);
        Variants.WarnIfUnknown(type);
        return type;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
