using System.ComponentModel;

namespace Rune.Wpf;

/// <summary>菜单项的色调（vue-tsx-pro 的 MenuItemTone）：菜单里只区分普通操作与破坏性操作，其余状态色会让一列选项显得像警报。</summary>
/// <param name="Value">取值（neutral、danger）。</param>
[TypeConverter(typeof(VariantConverter<MenuItemTone>))]
public readonly record struct MenuItemTone(string Value) : IVariant<MenuItemTone>
{
    /// <summary>普通操作（默认）：正文色，悬停 fillHover。</summary>
    public static readonly MenuItemTone Neutral = new("neutral");

    /// <summary>破坏性操作（删除）：dangerText，悬停 dangerSoft。</summary>
    public static readonly MenuItemTone Danger = new("danger");

    /// <inheritdoc />
    public static IReadOnlyList<MenuItemTone> Known { get; } = [Neutral, Danger];

    /// <inheritdoc />
    public static MenuItemTone Parse(string value)
    {
        var tone = new MenuItemTone(value);
        Variants.WarnIfUnknown(tone);
        return tone;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
