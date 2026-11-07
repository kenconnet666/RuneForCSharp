using System.ComponentModel;

namespace Rune.Wpf;

/// <summary>
/// 浮层贴着触发器的位置（vue-tsx-pro 的 FloatingPlacement）：边加对齐，共 12 种。start / end 对齐是逻辑方向（从右到左排版时镜像）；
/// 边是物理方向。空间不够时翻到另一边。
/// </summary>
/// <param name="Value">取值（top、top-start、top-end、bottom……、left、left-start……、right……）。</param>
[TypeConverter(typeof(VariantConverter<FloatingPlacement>))]
public readonly record struct FloatingPlacement(string Value) : IVariant<FloatingPlacement>
{
    /// <summary>上方居中。</summary>
    public static readonly FloatingPlacement Top = new("top");

    /// <summary>上方，与触发器的起始边对齐。</summary>
    public static readonly FloatingPlacement TopStart = new("top-start");

    /// <summary>上方，与触发器的结束边对齐。</summary>
    public static readonly FloatingPlacement TopEnd = new("top-end");

    /// <summary>下方居中。</summary>
    public static readonly FloatingPlacement Bottom = new("bottom");

    /// <summary>下方，与触发器的起始边对齐。</summary>
    public static readonly FloatingPlacement BottomStart = new("bottom-start");

    /// <summary>下方，与触发器的结束边对齐。</summary>
    public static readonly FloatingPlacement BottomEnd = new("bottom-end");

    /// <summary>左侧居中。</summary>
    public static readonly FloatingPlacement Left = new("left");

    /// <summary>左侧，与触发器的顶边对齐。</summary>
    public static readonly FloatingPlacement LeftStart = new("left-start");

    /// <summary>左侧，与触发器的底边对齐。</summary>
    public static readonly FloatingPlacement LeftEnd = new("left-end");

    /// <summary>右侧居中。</summary>
    public static readonly FloatingPlacement Right = new("right");

    /// <summary>右侧，与触发器的顶边对齐。</summary>
    public static readonly FloatingPlacement RightStart = new("right-start");

    /// <summary>右侧，与触发器的底边对齐。</summary>
    public static readonly FloatingPlacement RightEnd = new("right-end");

    /// <inheritdoc />
    public static IReadOnlyList<FloatingPlacement> Known { get; } =
        [Top, TopStart, TopEnd, Bottom, BottomStart, BottomEnd, Left, LeftStart, LeftEnd, Right, RightStart, RightEnd];

    /// <summary>边：top、bottom、left、right（default 时按 top）。</summary>
    public string Side => Value?.Split('-')[0] ?? "top";

    /// <summary>对齐：start、end，居中时为 null。</summary>
    public string? Align => Value?.Split('-') is [_, var align] ? align : null;

    /// <summary>浮层在触发器上方或下方（主轴是竖直方向）。</summary>
    public bool IsVertical => Side is "top" or "bottom";

    /// <inheritdoc />
    public static FloatingPlacement Parse(string value)
    {
        var placement = new FloatingPlacement(value);
        Variants.WarnIfUnknown(placement);
        return placement;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
