using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>相连的一组（ButtonGroup、ControlGroup）的排列方向（vue-tsx-pro 的 AttachedOrientations）。</summary>
/// <param name="Value">取值（horizontal、vertical）。</param>
[TypeConverter(typeof(VariantConverter<AttachedOrientation>))]
public readonly record struct AttachedOrientation(string Value) : IVariant<AttachedOrientation>
{
    /// <summary>横排（默认）：去掉左右相邻一侧的圆角。</summary>
    public static readonly AttachedOrientation Horizontal = new("horizontal");

    /// <summary>竖排：去掉上下相邻一侧的圆角。</summary>
    public static readonly AttachedOrientation Vertical = new("vertical");

    /// <inheritdoc />
    public static IReadOnlyList<AttachedOrientation> Known { get; } = [Horizontal, Vertical];

    /// <summary>竖排。</summary>
    public bool IsVertical => Value == "vertical";

    /// <inheritdoc />
    public static AttachedOrientation Parse(string value)
    {
        var orientation = new AttachedOrientation(value);
        Variants.WarnIfUnknown(orientation);
        return orientation;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>相连的控件去掉圆角的那几个角（起始边在前：从右到左时平台整体镜像，起始边仍是 Left）。</summary>
[Flags]
public enum AttachedCorners
{
    /// <summary>不在组里，或组里只有它一个：圆角照常。</summary>
    None = 0,

    /// <summary>左上。</summary>
    TopLeft = 1,

    /// <summary>右上。</summary>
    TopRight = 2,

    /// <summary>右下。</summary>
    BottomRight = 4,

    /// <summary>左下。</summary>
    BottomLeft = 8,
}

/// <summary>相连的一组的规则（vue-tsx-pro 的 attachedStyles）。</summary>
public static class AttachedRules
{
    /// <summary>相邻的控件之间边框叠在一起的宽度（borderWidths.xs）：不显得加粗。</summary>
    public const double Overlap = 1;

    /// <summary>组里第几个（跳过隐藏的）去掉哪几个角：不是第一个时去掉靠前一侧的，不是最后一个时去掉靠后一侧的。</summary>
    /// <param name="orientation">排列方向。</param>
    /// <param name="first">是第一个。</param>
    /// <param name="last">是最后一个。</param>
    public static AttachedCorners Squared(AttachedOrientation orientation, bool first, bool last)
    {
        var vertical = orientation.IsVertical;
        var corners = AttachedCorners.None;
        if (!first) corners |= vertical ? AttachedCorners.TopLeft | AttachedCorners.TopRight : AttachedCorners.TopLeft | AttachedCorners.BottomLeft;
        if (!last) corners |= vertical ? AttachedCorners.BottomLeft | AttachedCorners.BottomRight : AttachedCorners.TopRight | AttachedCorners.BottomRight;
        return corners;
    }
}
