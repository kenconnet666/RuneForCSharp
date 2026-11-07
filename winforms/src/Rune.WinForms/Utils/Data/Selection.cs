using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>选择方式（vue-tsx-pro 的 SelectionModes）：ToggleGroup、Listbox、Select 共用。</summary>
/// <param name="Value">取值（single、multiple）。</param>
[TypeConverter(typeof(VariantConverter<SelectionMode>))]
public readonly record struct SelectionMode(string Value) : IVariant<SelectionMode>
{
    /// <summary>单选（默认）：选中一项会取消其他项；再点已选的项取消选中（必选时不取消）。</summary>
    public static readonly SelectionMode Single = new("single");

    /// <summary>多选：每项独立切换。</summary>
    public static readonly SelectionMode Multiple = new("multiple");

    /// <inheritdoc />
    public static IReadOnlyList<SelectionMode> Known { get; } = [Single, Multiple];

    /// <inheritdoc />
    public static SelectionMode Parse(string value)
    {
        var mode = new SelectionMode(value);
        Variants.WarnIfUnknown(mode);
        return mode;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>选择模型（vue-tsx-pro 的 utils/data/selection）：单选、多选怎样响应"点了一项"。</summary>
public static class Selection
{
    /// <summary>点了 <paramref name="key"/> 之后的选择；没有变化时返回原列表。</summary>
    /// <param name="selected">已选的值。</param>
    /// <param name="key">点的那一项。</param>
    /// <param name="mode">选择方式。</param>
    /// <param name="required">至少保留一项：最后一个已选项不能被取消。</param>
    public static IReadOnlyList<string> Toggle(IReadOnlyList<string> selected, string key, SelectionMode mode, bool required)
    {
        var isSelected = selected.Contains(key);
        if (mode != SelectionMode.Multiple)
        {
            if (!isSelected) return [key];
            return required ? selected : [];
        }
        if (!isSelected) return [.. selected, key];
        if (required && selected.Count == 1) return selected;
        return [.. selected.Where(item => item != key)];
    }
}
