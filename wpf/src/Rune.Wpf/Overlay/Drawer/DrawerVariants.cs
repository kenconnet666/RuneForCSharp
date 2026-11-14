using System.ComponentModel;

namespace Rune.Wpf;

/// <summary>抽屉从哪一侧滑入（vue-tsx-pro 的 DrawerSides）：start / end 是逻辑方向，从右到左排版下左右互换。</summary>
/// <param name="Value">取值（start、end、top、bottom）。</param>
[TypeConverter(typeof(VariantConverter<DrawerSide>))]
public readonly record struct DrawerSide(string Value) : IVariant<DrawerSide>
{
    /// <summary>起始边（从左到右排版时是左侧）：导航菜单。</summary>
    public static readonly DrawerSide Start = new("start");

    /// <summary>结束边（默认，从左到右排版时是右侧）：详情、筛选、表单。</summary>
    public static readonly DrawerSide End = new("end");

    /// <summary>顶部。</summary>
    public static readonly DrawerSide Top = new("top");

    /// <summary>底部：操作面板。</summary>
    public static readonly DrawerSide Bottom = new("bottom");

    /// <inheritdoc />
    public static IReadOnlyList<DrawerSide> Known { get; } = [Start, End, Top, Bottom];

    /// <summary>贴着左右两侧（宽度按档位、能拖动调整）；上下两侧时占满宽度、高度按内容。</summary>
    public bool Inline => Value is null or "start" or "end";

    /// <inheritdoc />
    public static DrawerSide Parse(string value)
    {
        var side = new DrawerSide(value);
        Variants.WarnIfUnknown(side);
        return side;
    }

    /// <inheritdoc />
    public override string ToString() => Value ?? "end";
}
